namespace DiktaMe.App.Views.Settings;

using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class AIEngineSettingsPage : Page
{
    public AIEngineSettingsViewModel ViewModel { get; }

    public AIEngineSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<AIEngineSettingsViewModel>();
        this.InitializeComponent();
    }
}
