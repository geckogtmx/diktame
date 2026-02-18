namespace DiktaMe.App.Views.Wizard;

using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

public sealed partial class WizardLlmPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardLlmPage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
        // Restore selection from VM
        switch (viewModel.LlmChoice)
        {
            case "local": LlmLocal.IsChecked = true; break;
            case "skip": LlmSkip.IsChecked = true; break;
            default: LlmCloud.IsChecked = true; break;
        }
    }

    private void LlmRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        if (LlmLocal.IsChecked == true)
            _viewModel.LlmChoice = "local";
        else if (LlmSkip.IsChecked == true)
            _viewModel.LlmChoice = "skip";
        else
            _viewModel.LlmChoice = "cloud";
    }
}
