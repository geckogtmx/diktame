
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class AudioSettingsPage : Page
{
    public AudioSettingsViewModel ViewModel { get; }

    public AudioSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<AudioSettingsViewModel>();
        this.InitializeComponent();
    }
}
