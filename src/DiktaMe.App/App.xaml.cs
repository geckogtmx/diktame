using System.Runtime.InteropServices;
using System.Web;
using DiktaMe.App.Services;
using DiktaMe.App.Views;
using DiktaMe.Core.Account;
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
using Microsoft.UI.Dispatching;
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
    private ViewModels.LoadingViewModel? _loadingViewModel;
    private SingleInstanceManager? _singleInstance;

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

        // Configure logging early (needed before single-instance check)
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

        // ── Single-instance + deeplink forwarding ────────────────────────────
        string? deepLinkArg = FindDeepLinkArg();

        _singleInstance = new SingleInstanceManager();
        if (!_singleInstance.TryAcquire())
        {
            // Secondary instance — forward deeplink to primary and exit
            if (deepLinkArg is not null)
            {
                Log.Information("Secondary instance — forwarding deeplink to primary");
                SingleInstanceManager.SendDeepLinkAsync(deepLinkArg).GetAwaiter().GetResult();
            }
            else
            {
                Log.Information("Secondary instance — no deeplink, exiting");
            }

            Exit();
            return;
        }

        // Primary instance — start pipe listener for deeplinks from secondary instances
        _singleInstance.StartListening();

        // Register diktame:// protocol handler (HKCU, no admin needed)
        ProtocolRegistrar.Register();

        // ── Configure DI ─────────────────────────────────────────────────────
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // ── Wire deeplink handler ────────────────────────────────────────────
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _singleInstance.DeepLinkReceived += uri => dispatcher.TryEnqueue(() => HandleDeepLink(uri));

        // Handle deeplink from this launch (e.g. diktame://auth?token=...)
        if (deepLinkArg is not null)
        {
            dispatcher.TryEnqueue(() => HandleDeepLink(deepLinkArg));
        }

        // Create tray icon standalone — not inside any window's visual tree.
        // H.NotifyIcon's TaskbarIcon creates its own hidden Win32 message window
        // internally; it does not need a WinUI visual tree parent.
        _trayIcon = new TrayIconView();

        // Show loading screen and run async initialization
        var loading = new Views.LoadingWindow();
        _loadingViewModel = loading.ViewModel; // Keep alive — owns hotkey event subscriptions
        loading.Activate();
        loading.StartLoading();
    }

    /// <summary>
    /// Extracts a <c>diktame://</c> URI from command-line arguments, if present.
    /// </summary>
    private static string? FindDeepLinkArg()
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        foreach (string arg in cmdArgs)
        {
            if (arg.StartsWith("diktame://", StringComparison.OrdinalIgnoreCase))
            {
                return arg;
            }
        }

        return null;
    }

    /// <summary>
    /// Processes a <c>diktame://auth?token=JWT</c> deeplink URI.
    /// Must be called on the UI thread.
    /// </summary>
    private async void HandleDeepLink(string uri)
    {
        try
        {
            var parsed = new Uri(uri);
            if (!string.Equals(parsed.Host, "auth", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("App: ignoring unknown deeplink host: {Host}", parsed.Host);
                return;
            }

            var query = HttpUtility.ParseQueryString(parsed.Query);
            string? token = query["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Warning("App: deeplink missing token parameter");
                return;
            }

            Log.Information("App: processing auth deeplink");
            var accountService = Services.GetRequiredService<IAccountService>();
            await accountService.HandleAuthCallbackAsync(token).ConfigureAwait(false);
        }
        catch (UriFormatException ex)
        {
            Log.Warning(ex, "App: failed to parse deeplink URI: {Uri}", uri);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "App: error handling deeplink");
        }
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
    /// Hides the main window (to system tray).
    /// </summary>
    public void HideMainWindow()
    {
        _window?.AppWindow.Hide();
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
        // Trial mode routes through TrialGeminiAudioProvider when AuthMode == Trial.
        services.AddSingleton<TrialGeminiAudioProvider>();
        services.AddSingleton<ISTTProvider>(sp => new STTRouter(
            primary: sp.GetRequiredService<WhisperProvider>(),
            settings: sp.GetRequiredService<SettingsManager>(),
            trialStt: sp.GetRequiredService<TrialGeminiAudioProvider>()));

        // ── LLM providers ────────────────────────────────────────────────────
        // Ollama (local, no API key — works out of the box when Ollama is running)
        services.AddSingleton<OllamaProvider>(sp => new OllamaProvider("llama3.2"));

        // Trial Gemini provider (managed proxy, Bearer JWT auth)
        services.AddSingleton<TrialGeminiProvider>();

        // Register router against interface — Ollama is the default offline provider.
        // Cloud LLM providers are created dynamically by LLMRouter (J.5) for per-mode model selection.
        // Trial mode routes through TrialGeminiProvider when AuthMode == Trial.
        services.AddSingleton<ILLMProvider>(sp => new LLMRouter(
            primary: sp.GetRequiredService<OllamaProvider>(),
            factory: sp.GetRequiredService<ILLMProviderFactory>(),
            settings: sp.GetRequiredService<SettingsManager>(),
            trialProvider: sp.GetRequiredService<TrialGeminiProvider>()));

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

        // ── Account (K.2 / L.3h) ─────────────────────────────────────────────
        // Single instance behind three interfaces for incremental migration.
        services.AddSingleton<TrialAccountService>();
#pragma warning disable CS0618 // ITrialAccountService is obsolete — kept for backward compat during migration
        services.AddSingleton<ITrialAccountService>(sp => sp.GetRequiredService<TrialAccountService>());
#pragma warning restore CS0618
        services.AddSingleton<IAccountService>(sp => sp.GetRequiredService<TrialAccountService>());
        services.AddSingleton<ITrialService>(sp => sp.GetRequiredService<TrialAccountService>());

        // ── Data (E.2) ───────────────────────────────────────────────────────
        services.AddSingleton<HistoryManager>();
        services.AddSingleton<MetricsCollector>();

        // ── System (I.5) ──────────────────────────────────────────────────────
        services.AddSingleton<OllamaManager>();

        // ── UI Services (F.5) ──────────────────────────────────────────────────
        services.AddSingleton<Services.NotificationService>();

        // ── UI ViewModels (F.2+) ───────────────────────────────────────────────
        services.AddSingleton<ViewModels.ControlPanelViewModel>();
        services.AddTransient<ViewModels.Settings.AccountSettingsViewModel>(); // K.5c: Trial account page
        services.AddTransient<ViewModels.Settings.GeneralSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.AIEngineSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.AudioSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.HotkeysSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ModesSettingsViewModel>(); // Unified modes page (Ask, Refine x2, Translate, Notes, Chat)
        services.AddTransient<ViewModels.Settings.DictationModesSettingsViewModel>(); // J.6: CRUD dictation modes page
        services.AddTransient<ViewModels.Settings.PrivacySettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ApiKeysSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.OllamaSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.SnippetsSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ControlPanelConfigViewModel>();
        services.AddTransient<ViewModels.WizardViewModel>();
        services.AddSingleton<ViewModels.LoadingViewModel>();
        services.AddSingleton<ViewModels.QuickChatViewModel>();
    }
}
