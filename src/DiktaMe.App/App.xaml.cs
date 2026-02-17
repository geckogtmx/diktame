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
    /// Gets the system tray icon view.
    /// </summary>
    public TrayIconView? TrayIcon => _trayIcon;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Configure DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Configure logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DiktaMe", "logs", "diktame_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("dIKta.me V2 starting up...");

        // Create tray icon (kept alive for the duration of the app)
        _trayIcon = new TrayIconView();

        // Create and activate main window
        _window = new MainWindow();
        _window.Activate();
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
        }
        _window.Activate();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Core engine (singletons) ─────────────────────────────────────────
        // NOTE: AudioDeviceManager and ClipboardManager are static utility classes;
        // they do not need DI registration.
        services.AddSingleton<AudioRecorder>();
        services.AddSingleton<AudioDucker>(sp =>
        {
            var ducker = new AudioDucker();
            ducker.AttachTo(sp.GetRequiredService<AudioRecorder>());
            return ducker;
        });
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
        // Cloud LLM providers are created by PipelineFactory (E.1) using API keys
        // from SecureStorage.
        services.AddSingleton<ILLMProvider>(sp => new LLMRouter(
            primary: sp.GetRequiredService<OllamaProvider>()));

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

        // ── Config & Security (E.1 / E.3) ───────────────────────────────────
        services.AddSingleton<SecureStorage>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<PromptRepository>();
        services.AddSingleton<SnippetManager>();
        services.AddSingleton<ISTTProviderFactory, STTProviderFactory>();
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
        services.AddSingleton<PipelineFactory>();

        // ── Data (E.2) ───────────────────────────────────────────────────────
        services.AddSingleton<HistoryManager>();
        services.AddSingleton<MetricsCollector>();

        // ── System (I.5) ──────────────────────────────────────────────────────
        services.AddSingleton<OllamaManager>();
    }
}
