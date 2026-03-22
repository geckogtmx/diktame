
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Hardware settings page — hosts Microphone and Sound Feedback sub-items.
/// </summary>
public sealed partial class HardwareSettingsPage : Page
{
    public HardwareSettingsViewModel ViewModel { get; }

    public HardwareSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<HardwareSettingsViewModel>();
        this.InitializeComponent();
    }
}
