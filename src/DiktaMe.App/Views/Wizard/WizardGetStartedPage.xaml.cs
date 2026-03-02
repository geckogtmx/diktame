
using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardGetStartedPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardGetStartedPage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;

        // Restore selection from ViewModel
        if (string.Equals(_viewModel.OnboardingChoice, "apikeys", StringComparison.Ordinal))
        {
            ApiKeysRadio.IsChecked = true;
        }
        else
        {
            TrialRadio.IsChecked = true;
        }
    }

    private void OnboardingRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var selected = OnboardingRadio.SelectedItem as RadioButton;
        _viewModel.OnboardingChoice = selected?.Tag as string ?? "trial";
    }
}
