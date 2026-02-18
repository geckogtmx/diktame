
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class SnippetsSettingsPage : Page
{
    public SnippetsSettingsViewModel ViewModel { get; }

    public SnippetsSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SnippetsSettingsViewModel>();
        this.InitializeComponent();
    }
}
