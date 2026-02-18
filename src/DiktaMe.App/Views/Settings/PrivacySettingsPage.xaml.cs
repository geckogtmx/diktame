namespace DiktaMe.App.Views.Settings;

using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class PrivacySettingsPage : Page
{
    public PrivacySettingsViewModel ViewModel { get; }

    public PrivacySettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<PrivacySettingsViewModel>();
        this.InitializeComponent();
    }
}
