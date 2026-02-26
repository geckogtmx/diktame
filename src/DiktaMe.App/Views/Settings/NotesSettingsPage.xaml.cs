
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Notes settings page.
/// </summary>
public sealed partial class NotesSettingsPage : Page
{
    public NotesSettingsViewModel ViewModel { get; }

    public NotesSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<NotesSettingsViewModel>();
        this.InitializeComponent();
    }
}
