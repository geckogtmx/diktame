
using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardLanguagePage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardLanguagePage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;

        // Restore selection from ViewModel
        if (string.Equals(_viewModel.LanguageChoice, "es-MX", StringComparison.Ordinal))
        {
            SpanishRadio.IsChecked = true;
        }
        else
        {
            EnglishRadio.IsChecked = true;
        }
    }

    private void LanguageRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var selected = LanguageRadio.SelectedItem as RadioButton;
        _viewModel.LanguageChoice = selected?.Tag as string ?? "en";
    }
}
