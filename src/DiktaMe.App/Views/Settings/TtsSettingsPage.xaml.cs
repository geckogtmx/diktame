
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class TtsSettingsPage : Page
{
    public TtsSettingsViewModel ViewModel { get; }

    public TtsSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<TtsSettingsViewModel>();
        this.InitializeComponent();
    }
}
