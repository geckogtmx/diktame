
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Workflows &amp; Modes settings page — hosts Dictation Behaviors and utility pipeline sub-items.
/// </summary>
public sealed partial class WorkflowsSettingsPage : Page
{
    public WorkflowsSettingsViewModel ViewModel { get; }

    public WorkflowsSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<WorkflowsSettingsViewModel>();
        this.InitializeComponent();
    }
}
