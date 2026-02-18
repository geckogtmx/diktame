namespace DiktaMe.App.Views.Settings;

using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class ApiKeysSettingsPage : Page
{
    public ApiKeysSettingsViewModel ViewModel { get; }

    public ApiKeysSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ApiKeysSettingsViewModel>();
        this.InitializeComponent();
    }
}
