using System.Runtime.InteropServices;
using DiktaMe.App.Views;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.Security;
using DiktaMe.Core.STT;
using DiktaMe.Core.SystemManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;

namespace DiktaMe.App;

/// <summary>
/// The main application entry point. Configures DI container and manages app lifecycle.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private TrayIconView? _trayIcon;
    private Views.SettingsWindow? _settingsWindow;
    private Views.QuickChatWindow? _quickChatWindow;

    /// <summary>
    /// Gets the current App instance.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the DI service provider.
    /// </summary>
    public IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public Window? MainWindow => _window;

    /// <summary>
    /// Gets the system tray icon view (standalone, created at app startup).
    /// </summary>
    public TrayIconView? TrayIcon => _trayIcon;

    public App()
    {
        this.InitializeComponent();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Allocate a console window for debugging (only in Debug builds or when debugger attached)
#if DEBUG
        AllocConsole();
#else
        if (System.Diagnostics.Debugger.IsAttached)
        {
            AllocConsole();
        }
#endif

        // Configure DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Configure logging (file + console for debugging)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DiktaMe", "logs", "diktame_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("dIKta.me V2 starting up...");

        // Create tray icon standalone — not inside any window's visual tree.
        // H.NotifyIcon's TaskbarIcon creates its own hidden Win32 message window
        // internally; it does not need a WinUI visual tree parent.
        _trayIcon = new TrayIconView();

        // Show loading screen and run async initialization
        var loading = new Views.LoadingWindow();
        loading.Activate();
        loading.StartLoading();
    }

    /// <summary>
    /// Shows and brings the main window to the foreground.
    /// Creates a new window if it was previously closed.
    /// </summary>
    public void ShowMainWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            // Intercept close: hide instead of destroying, so the tray icon keeps the app alive.
            _window.AppWindow.Closing += (s, e) =>
            {
                e.Cancel = true;
                _window.AppWindow.Hide();
            };
        }

        _window.AppWindow.Show();
        _window.Activate();
    }

    /// <summary>
    /// Shows the Settings window. Creates a singleton instance if needed.
    /// </summary>
    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new Views.SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Toggles the Quick Chat overlay window (show/hide).
    /// </summary>
    public void ToggleQuickChat()
    {
        if (_quickChatWindow is not null)
        {
            _quickChatWindow.Close();
            _quickChatWindow = null;
            return;
        }

        _quickChatWindow = new Views.QuickChatWindow();
        _quickChatWindow.Closed += (_, _) => _quickChatWindow = null;
        _quickChatWindow.Activate();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Core engine ──────────────────────────────────────────────────────
        // NOTE: AudioDeviceManager and ClipboardManager are static utility classes;
        // they do not need DI registration.

        // AudioRecorder is Transient because it implements IDisposable and cannot be reused
        // after disposal. Each usage (wizard test, pipeline run) needs a fresh instance.
        services.AddTransient<AudioRecorder>();

        // AudioDucker is Singleton but no longer auto-attached to AudioRecorder.
        // Pipelines will attach it manually when recording starts.
        services.AddSingleton<AudioDucker>();
        services.AddSingleton<MuteDetector>();
        services.AddSingleton<TextInjector>();
        services.AddSingleton<HotkeyManager>();

        // ── STT providers ────────────────────────────────────────────────────
        // Local Whisper (no API key needed — requires model download)
        services.AddSingleton<WhisperProvider>();

        // Cloud providers registered as concrete types so they can be resolved
        // by PipelineFactory (E.1) once SecureStorage provides real API keys.
        // NOTE: Cloud providers require keys; they are deliberately NOT registered
        // against ISTTProvider here — the router below uses WhisperProvider as
        // the default until settings/keys are configured.
        services.AddSingleton<ISTTProvider>(sp => new STTRouter(
            primary: sp.GetRequiredService<WhisperProvider>()));

        // ── LLM providers ────────────────────────────────────────────────────
        // Ollama (local, no API key — works out of the box when Ollama is running)
        services.AddSingleton<OllamaProvider>(sp => new OllamaProvider("llama3.2"));

        // Register router against interface — Ollama is the default offline provider.
        // Cloud LLM providers are created dynamically by LLMRouter (J.5) for per-mode model selection.
        services.AddSingleton<ILLMProvider>(sp => new LLMRouter(
            primary: sp.GetRequiredService<OllamaProvider>(),
            factory: sp.GetRequiredService<ILLMProviderFactory>()));

        // ── Pipelines (transient — new instance per invocation) ──────────────
        services.AddTransient<DictationPipeline>(sp => new DictationPipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>(),
            injector: sp.GetRequiredService<TextInjector>()));

        services.AddTransient<RefinePipeline>(sp => new RefinePipeline(
            llm: sp.GetRequiredService<ILLMProvider>(),
            injector: sp.GetRequiredService<TextInjector>(),
            stt: sp.GetRequiredService<ISTTProvider>()));

        services.AddTransient<AskPipeline>(sp => new AskPipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>()));

        services.AddTransient<TranslatePipeline>(sp => new TranslatePipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>(),
            injector: sp.GetRequiredService<TextInjector>()));

        services.AddTransient<NotePipeline>(sp => new NotePipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>()));

        services.AddTransient<ChatPipeline>(sp => new ChatPipeline(
            llm: sp.GetRequiredService<ILLMProvider>(),
            stt: sp.GetRequiredService<ISTTProvider>()));

        // ── Config & Security (E.1 / E.3 / J.2) ─────────────────────────────
        services.AddSingleton<SecureStorage>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<ProfileManager>(); // DEPRECATED: Use DictationModeManager instead
        services.AddSingleton<PromptRepository>(); // DEPRECATED: Prompts now in DictationProfile.SystemPrompt
        services.AddSingleton<DictationModeManager>(); // J.2: CRUD for dictation modes
        services.AddSingleton<PipelineConfigManager>(); // J.2: CRUD for utility pipelines
        services.AddSingleton<SnippetManager>();
        services.AddSingleton<ISTTProviderFactory, STTProviderFactory>();
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
        services.AddSingleton(sp => new ModelListService( // J.5: Multi-provider model discovery
            sp.GetRequiredService<SecureStorage>(),
            sp.GetRequiredService<SettingsManager>()));
        services.AddSingleton<PipelineFactory>();

        // ── Data (E.2) ───────────────────────────────────────────────────────
        services.AddSingleton<HistoryManager>();
        services.AddSingleton<MetricsCollector>();

        // ── System (I.5) ──────────────────────────────────────────────────────
        services.AddSingleton<OllamaManager>();

        // ── UI Services (F.5) ──────────────────────────────────────────────────
        services.AddSingleton<Services.NotificationService>();

        // ── UI ViewModels (F.2+) ───────────────────────────────────────────────
        services.AddSingleton<ViewModels.ControlPanelViewModel>();
        services.AddTransient<ViewModels.Settings.GeneralSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.AIEngineSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.AudioSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.HotkeysSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ModesSettingsViewModel>(); // DEPRECATED: legacy modes page
        services.AddTransient<ViewModels.Settings.DictationModesSettingsViewModel>(); // J.6: CRUD modes page
        services.AddTransient<ViewModels.Settings.NotesSettingsViewModel>(); // Note pipeline config
        services.AddTransient<ViewModels.Settings.ChatSettingsViewModel>(); // Chat overlay config
        services.AddTransient<ViewModels.Settings.PrivacySettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ApiKeysSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.OllamaSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.SnippetsSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ControlPanelConfigViewModel>();
        services.AddTransient<ViewModels.WizardViewModel>();
        services.AddTransient<ViewModels.LoadingViewModel>();
        services.AddSingleton<ViewModels.QuickChatViewModel>();
    }
}
