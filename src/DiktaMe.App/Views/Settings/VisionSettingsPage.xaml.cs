
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Vision settings page.
/// </summary>
public sealed partial class VisionSettingsPage : Page
{
    public VisionSettingsViewModel ViewModel { get; }

    public VisionSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<VisionSettingsViewModel>();
        this.InitializeComponent();
    }
}
