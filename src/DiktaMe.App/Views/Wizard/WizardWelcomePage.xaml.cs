namespace DiktaMe.App.Views.Wizard;

using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

public sealed partial class WizardWelcomePage : Page, IWizardStepPage
{
    public WizardWelcomePage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel) { }
}
