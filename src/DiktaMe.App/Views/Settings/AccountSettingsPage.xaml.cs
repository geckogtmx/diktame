
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class AccountSettingsPage : Page
{
    public AccountSettingsViewModel ViewModel { get; }

    public AccountSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<AccountSettingsViewModel>();
        this.InitializeComponent();
    }
}
