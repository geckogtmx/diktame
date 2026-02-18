
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class ApiKeysSettingsPage : Page
{
    public ApiKeysSettingsViewModel ViewModel { get; }

    public ApiKeysSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ApiKeysSettingsViewModel>();
        this.InitializeComponent();
    }
}
