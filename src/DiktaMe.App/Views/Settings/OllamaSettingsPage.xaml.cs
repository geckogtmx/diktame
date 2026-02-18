
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class OllamaSettingsPage : Page
{
    public OllamaSettingsViewModel ViewModel { get; }

    public OllamaSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<OllamaSettingsViewModel>();
        this.InitializeComponent();
    }
}
