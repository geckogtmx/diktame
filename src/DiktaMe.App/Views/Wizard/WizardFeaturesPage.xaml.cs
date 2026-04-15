
using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardFeaturesPage : Page, IWizardStepPage
{
    public WizardFeaturesPage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        // No state needed — purely informational page
    }

    private void GetNowLink_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(new System.Uri("https://dikta.me/pricing"));
    }
}
