
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Full-width CRUD page for dictation mode presets.
/// </summary>
public sealed partial class DictationPresetsSettingsPage : Page
{
    public DictationModesSettingsViewModel ViewModel { get; }

    public DictationPresetsSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<DictationModesSettingsViewModel>();
        this.InitializeComponent();
    }
}
