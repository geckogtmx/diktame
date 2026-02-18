
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class ModesSettingsPage : Page
{
    public ModesSettingsViewModel ViewModel { get; }

    public ModesSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ModesSettingsViewModel>();
        this.InitializeComponent();
    }
}
