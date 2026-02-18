
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class GeneralSettingsPage : Page
{
    public GeneralSettingsViewModel ViewModel { get; }

    public GeneralSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<GeneralSettingsViewModel>();
        this.InitializeComponent();
    }
}
