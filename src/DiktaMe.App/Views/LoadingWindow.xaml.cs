namespace DiktaMe.App.Views;

using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

public sealed partial class LoadingWindow : Window
{
    public LoadingViewModel ViewModel { get; }

    public LoadingWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<LoadingViewModel>();
        this.InitializeComponent();

        // Small centered window
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(400, 300));

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
