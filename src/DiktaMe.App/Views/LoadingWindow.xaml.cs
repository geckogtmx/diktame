
using System.Reflection;
using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DiktaMe.App.Views;
public sealed partial class LoadingWindow : Window
{
    public LoadingViewModel ViewModel { get; }

    public LoadingWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<LoadingViewModel>();
        this.InitializeComponent();

        // Small centered window
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(400, 340));

        // Frameless: hide title bar and caption buttons
        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        // Set window icon
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }

        // Version number from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is not null
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : "";

        ViewModel.LoadingComplete += OnLoadingComplete;
    }

    public async void StartLoading()
    {
        await ViewModel.InitializeAsync();
    }

    private void OnLoadingComplete()
    {
        // Transition to wizard or main window
        var settings = App.Current.Services.GetRequiredService<DiktaMe.Core.Config.SettingsManager>();
        if (!settings.Current.WizardCompleted)
        {
            var wizard = new WizardWindow();
            wizard.Activate();
        }
        else
        {
            App.Current.ShowMainWindow();
        }

        this.Close();
    }
}
