using DiktaMe.App.Views;
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
        // Core services will be registered here as they are implemented
        // Example:
        // services.AddSingleton<ISTTProvider, DeepgramProvider>();
        // services.AddSingleton<ILLMProvider, GeminiProvider>();
        // services.AddSingleton<SettingsManager>();
        // services.AddSingleton<AudioRecorder>();
    }
}
