namespace DiktaMe.App.Views.Wizard;

using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

public sealed partial class WizardSttPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardSttPage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
        // Restore selection from VM
        if (viewModel.SttChoice == "local")
            SttLocal.IsChecked = true;
        else
            SttCloud.IsChecked = true;
    }

    private void SttRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        if (SttLocal.IsChecked == true)
            _viewModel.SttChoice = "local";
        else
            _viewModel.SttChoice = "cloud";
    }
}
