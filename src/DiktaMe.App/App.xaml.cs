using System.Runtime.InteropServices;
using System.Text.Json;
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
using DiktaMe.Core.TTS;
using DiktaMe.Core.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;
using WinUI3Localizer;

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
    /// Tracks all active windows for theme switching (RequestedTheme per-window).
    /// </summary>
    private readonly List<Window> _activeWindows = [];

    /// <summary>
    /// Gets all currently active windows. Used by ThemeService to set RequestedTheme.
    /// </summary>
    public IReadOnlyList<Window> ActiveWindows => _activeWindows;

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

        // Catch unhandled exceptions early — WinUI native crashes often bypass managed try/catch
        this.UnhandledException += (s, e) =>
        {
            Log.Fatal(e.Exception, "UNHANDLED EXCEPTION: {Message}", e.Message);
            Log.CloseAndFlush();
            e.Handled = false;
        };

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

        // ── Initialize localizer before any UI is created ──────────────────────
        InitializeLocalizer();

        // ── Configure DI ─────────────────────────────────────────────────────
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // ── Apply theme from settings (before any UI is created) ─────────────
        Services.GetRequiredService<Services.ThemeService>().ApplyFromSettings();

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
        TrackWindow(loading);
        loading.Closed += (_, _) => UntrackWindow(loading);
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
            // Rate limiting: ignore deeplinks within 2 seconds of the last one
            var now = DateTime.UtcNow;
            if ((now - _lastDeepLinkTime).TotalMilliseconds < DeepLinkCooldownMs)
            {
                Log.Warning("App: ignoring deeplink — rate limited");
                return;
            }
            _lastDeepLinkTime = now;

            var parsed = new Uri(uri);
            if (!string.Equals(parsed.Host, "auth", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("App: ignoring unknown deeplink host: {Host}", parsed.Host);
                return;
            }

            var query = HttpUtility.ParseQueryString(parsed.Query);
            string? token = query["token"];
            string? refreshToken = query["refresh_token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Warning("App: deeplink missing token parameter");
                return;
            }

            if (!IsValidJwtFormat(token))
            {
                Log.Warning("App: deeplink token has invalid JWT format");
                return;
            }

            Log.Information("App: processing auth deeplink");
            var accountService = Services.GetRequiredService<IAccountService>();
            await accountService.HandleAuthCallbackAsync(token, refreshToken).ConfigureAwait(false);

            // Sync wallet balance + refresh HUD after sign-in
            var loadingVm = Services.GetRequiredService<ViewModels.LoadingViewModel>();
            _ = Task.Run(async () =>
            {
                try
                {
                    await loadingVm.SyncWalletAfterSignInAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "App: post-sign-in wallet sync failed");
                }
            });
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

    private DateTime _lastDeepLinkTime = DateTime.MinValue;
    private const int DeepLinkCooldownMs = 2000;

    /// <summary>
    /// Initializes WinUI3Localizer from .resw files on disk and sets the
    /// UI language from settings.json. Must be called before any UI is created.
    /// </summary>
    private static void InitializeLocalizer()
    {
        try
        {
            // Build localizer from on-disk .resw files (bypasses broken PrimaryLanguageOverride)
            string stringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");
            new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                .SetOptions(options => options.DefaultLanguage = "en")
                .Build()
                .GetAwaiter()
                .GetResult();

            // Read UiLanguage from settings.json (before DI exists)
            string settingsPath = SettingsManager.DefaultSettingsFilePath;
            if (!File.Exists(settingsPath))
            {
                return; // First run — English defaults
            }

            string json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("General", out var general) &&
                general.TryGetProperty("UiLanguage", out var langEl) &&
                langEl.ValueKind == JsonValueKind.String)
            {
                string? lang = langEl.GetString();
                if (!string.IsNullOrWhiteSpace(lang) &&
                    !string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase))
                {
                    Localizer.Get().SetLanguage(lang).GetAwaiter().GetResult();
                    Log.Information("App: UI language set to '{Lang}' via WinUI3Localizer", lang);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "App: failed to initialize localizer — using default language");
        }
    }

    /// <summary>
    /// Validates that a token has valid JWT structure (3 base64url-encoded segments).
    /// Does NOT verify the cryptographic signature — that's the server's responsibility.
    /// </summary>
    internal static bool IsValidJwtFormat(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        // JWTs must be within a reasonable length range
        if (token.Length < 50 || token.Length > 4096)
        {
            return false;
        }

        // JWTs have exactly 3 segments separated by dots
        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        // Each segment must be non-empty and contain only base64url characters
        foreach (string part in parts)
        {
            if (part.Length == 0)
            {
                return false;
            }

            foreach (char c in part)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '=')
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Registers a window for theme tracking. Called when windows are created.
    /// </summary>
    internal void TrackWindow(Window window)
    {
        if (!_activeWindows.Contains(window))
        {
            _activeWindows.Add(window);
        }
    }

    /// <summary>
    /// Unregisters a window from theme tracking. Called when windows are destroyed.
    /// </summary>
    internal void UntrackWindow(Window window) => _activeWindows.Remove(window);

    /// <summary>
    /// Shows and brings the main window to the foreground.
    /// Creates a new window if it was previously closed.
    /// </summary>
    public void ShowMainWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            TrackWindow(_window);
            // Intercept close: hide instead of destroying, so the tray icon keeps the app alive.
            _window.AppWindow.Closing += (s, e) =>
            {
                e.Cancel = true;
                _window.AppWindow.Hide();
            };
        }

        _window.AppWindow.Show();
        _window.Activate();
        // Restore full opacity and reset auto-hide state in case the window was faded
        (_window as MainWindow)?.RestoreFromTray();
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
            TrackWindow(_settingsWindow);
            _settingsWindow.Closed += (_, _) =>
            {
                UntrackWindow(_settingsWindow!);
                _settingsWindow = null;
            };
        }
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Toggles the Quick Chat overlay window (show/hide).
    /// Window is preserved across toggle to maintain conversation state.
    /// </summary>
    public void ToggleQuickChat()
    {
        if (_quickChatWindow is not null)
        {
            // Hide instead of destroy to preserve conversation state
            _quickChatWindow.AppWindow.Hide();
            _quickChatWindow = null;
            return;
        }

        _quickChatWindow = new Views.QuickChatWindow();
        TrackWindow(_quickChatWindow);
        _quickChatWindow.AppWindow.Closing += (s, e) =>
        {
            e.Cancel = true;
            _quickChatWindow.AppWindow.Hide();
            UntrackWindow(_quickChatWindow!);
            _quickChatWindow = null;
        };
        _quickChatWindow.Activate();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Core engine ──────────────────────────────────────────────────────
        services.AddTransient<AudioRecorder>(); // Transient: IDisposable, cannot reuse after disposal
        services.AddSingleton<AudioLevelMonitor>();
        services.AddSingleton<AudioDucker>();
        services.AddSingleton<MuteDetector>();
        services.AddSingleton<TextInjector>();
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<WeatherService>();

        // ── STT providers ────────────────────────────────────────────────────
        // Local Whisper (no API key needed — requires model download)
        services.AddSingleton<WhisperProvider>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsManager>();
            return new WhisperProvider(settings.Current.WhisperModel);
        });

        // Cloud providers registered as concrete types so they can be resolved
        // by PipelineFactory (E.1) once SecureStorage provides real API keys.
        // NOTE: Cloud providers require keys; they are deliberately NOT registered
        // against ISTTProvider here — the router below uses WhisperProvider as
        // the default until settings/keys are configured.
        services.AddSingleton<WalletDeepgramProxy>(sp => new WalletDeepgramProxy(
            sp.GetRequiredService<SecureStorage>(),
            sp.GetRequiredService<SettingsManager>(),
            sp.GetRequiredService<WalletManager>()));
        services.AddSingleton<ISTTProvider>(sp => new STTRouter(
            primary: sp.GetRequiredService<WhisperProvider>(),
            settings: sp.GetRequiredService<SettingsManager>(),
            walletStt: sp.GetRequiredService<WalletDeepgramProxy>()));

        // ── LLM providers ────────────────────────────────────────────────────
        // Ollama (local, no API key — works out of the box when Ollama is running)
        services.AddSingleton<OllamaProvider>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsManager>();
            var http = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60),
            };
            http.DefaultRequestHeaders.ConnectionClose = false; // HTTP keep-alive
            return new OllamaProvider(
                settings.Current.OllamaModel,
                baseUrl: settings.Current.OllamaBaseUrl,
                httpClient: http,
                keepAlive: settings.Current.OllamaKeepAlive,
                numCtx: settings.Current.OllamaNumCtx);
        });

        // Register router against interface — Ollama is the default offline provider.
        // Cloud LLM providers are created dynamically by LLMRouter (J.5) for per-mode model selection.
        services.AddSingleton<WalletGeminiProxy>(sp => new WalletGeminiProxy(
            sp.GetRequiredService<SecureStorage>(),
            sp.GetRequiredService<SettingsManager>(),
            sp.GetRequiredService<WalletManager>()));
        services.AddSingleton<ILLMProvider>(sp => new LLMRouter(
            primary: sp.GetRequiredService<OllamaProvider>(),
            factory: sp.GetRequiredService<ILLMProviderFactory>(),
            settings: sp.GetRequiredService<SettingsManager>(),
            walletProvider: sp.GetRequiredService<WalletGeminiProxy>()));

        // ── Pipelines (transient — new instance per invocation) ──────────────
        services.AddTransient<DictationPipeline>(sp => new DictationPipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>(),
            injector: sp.GetRequiredService<TextInjector>()));

        services.AddTransient<RefinePipeline>(sp => new RefinePipeline(
            llm: sp.GetRequiredService<ILLMProvider>(),
            injector: sp.GetRequiredService<TextInjector>(),
            settings: sp.GetRequiredService<SettingsManager>(),
            stt: sp.GetRequiredService<ISTTProvider>()));

        services.AddTransient<AskPipeline>(sp => new AskPipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>(),
            settings: sp.GetRequiredService<SettingsManager>()));

        services.AddTransient<TranslatePipeline>(sp => new TranslatePipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>(),
            injector: sp.GetRequiredService<TextInjector>()));

        services.AddTransient<NotePipeline>(sp => new NotePipeline(
            stt: sp.GetRequiredService<ISTTProvider>(),
            llm: sp.GetRequiredService<ILLMProvider>()));

        services.AddTransient<ChatPipeline>(sp => new ChatPipeline(
            llm: sp.GetRequiredService<ILLMProvider>(),
            settings: sp.GetRequiredService<SettingsManager>(),
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
        services.AddSingleton<ITTSProviderFactory, TTSProviderFactory>();
        services.AddSingleton<ITtsPlayerService, TtsPlayerService>();
        services.AddSingleton<TtsSpeaker>();
        services.AddSingleton(sp => new ModelListService( // J.5: Multi-provider model discovery
            sp.GetRequiredService<SecureStorage>(),
            sp.GetRequiredService<SettingsManager>()));
        services.AddSingleton<PipelineFactory>(sp => new PipelineFactory(
            sp.GetRequiredService<ProfileManager>(),
            sp.GetRequiredService<ISTTProviderFactory>(),
            sp.GetRequiredService<ILLMProviderFactory>(),
            sp.GetRequiredService<ITTSProviderFactory>(),
            sp.GetRequiredService<ITtsPlayerService>(),
            sp.GetRequiredService<TextInjector>(),
            sp.GetRequiredService<SettingsManager>(),
            sp.GetRequiredService<SnippetManager>(),
            walletStt: sp.GetRequiredService<WalletDeepgramProxy>(),
            walletLlm: sp.GetRequiredService<WalletGeminiProxy>()));

        // ── Account (K.2 / K.8) ──────────────────────────────────────────────
        services.AddSingleton<AccountService>();
        services.AddSingleton<IAccountService>(sp => sp.GetRequiredService<AccountService>());
        services.AddSingleton<TokenRefreshService>();

        // ── Data (E.2 + SPEC_007 + K.9) ─────────────────────────────────────
        services.AddSingleton<HistoryManager>();
        services.AddSingleton<MetricsCollector>();
        services.AddSingleton<ConversationManager>();
        services.AddSingleton<WalletManager>();

        // ── System (I.5) ──────────────────────────────────────────────────────
        services.AddSingleton<OllamaManager>(sp =>
            new OllamaManager(baseUrl: sp.GetRequiredService<SettingsManager>().Current.OllamaBaseUrl));
        services.AddSingleton<OllamaSearchService>();
        services.AddSingleton<HardwareInfoService>();

        // ── UI Services (F.5) ──────────────────────────────────────────────────
        services.AddSingleton<Services.LocalizationService>();
        services.AddSingleton<Services.NotificationService>();
        services.AddSingleton<Services.ThemeService>();

        ConfigureViewModels(services);
    }

    private static void ConfigureViewModels(IServiceCollection services)
    {
        // ── UI ViewModels (F.2+) ───────────────────────────────────────────────
        services.AddSingleton<ViewModels.ControlPanelViewModel>();
        services.AddTransient<ViewModels.Settings.AccountSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.GeneralSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.AIEngineSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.AudioSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.TtsSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.HotkeysSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ModesSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.DictationModesSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.PrivacySettingsViewModel>();
        services.AddTransient<ViewModels.Settings.ApiKeysSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.OllamaSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.CloudLlmSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.SnippetsSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.HardwareSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.WorkflowsSettingsViewModel>();
        services.AddTransient<ViewModels.WizardViewModel>();
        services.AddSingleton<ViewModels.LoadingViewModel>();
        services.AddSingleton<ViewModels.QuickChatViewModel>();
    }
}
