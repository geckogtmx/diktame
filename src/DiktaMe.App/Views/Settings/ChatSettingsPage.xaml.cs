
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Chat settings page.
/// </summary>
public sealed partial class ChatSettingsPage : Page
{
    public ChatSettingsViewModel ViewModel { get; }

    public ChatSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ChatSettingsViewModel>();
        this.InitializeComponent();
    }
}
