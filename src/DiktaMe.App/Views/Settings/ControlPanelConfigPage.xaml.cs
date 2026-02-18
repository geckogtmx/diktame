namespace DiktaMe.App.Views.Settings;

using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class ControlPanelConfigPage : Page
{
    public ControlPanelConfigViewModel ViewModel { get; }

    public ControlPanelConfigPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ControlPanelConfigViewModel>();
        this.InitializeComponent();
    }
}
