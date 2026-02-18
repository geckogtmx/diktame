namespace DiktaMe.App.Views.Settings;

using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class SnippetsSettingsPage : Page
{
    public SnippetsSettingsViewModel ViewModel { get; }

    public SnippetsSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SnippetsSettingsViewModel>();
        this.InitializeComponent();
    }
}
