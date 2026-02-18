namespace DiktaMe.App.Views;

using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

public sealed partial class ControlPanelPage : Page
{
    public ControlPanelViewModel ViewModel { get; }

    public ControlPanelPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ControlPanelViewModel>();
        this.InitializeComponent();
    }
}
