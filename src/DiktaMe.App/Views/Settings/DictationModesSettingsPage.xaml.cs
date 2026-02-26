
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class DictationModesSettingsPage : Page
{
    public DictationModesSettingsViewModel ViewModel { get; }

    public DictationModesSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<DictationModesSettingsViewModel>();
        this.InitializeComponent();
    }
}
