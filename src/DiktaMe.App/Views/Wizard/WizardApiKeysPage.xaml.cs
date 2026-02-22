
using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardApiKeysPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardApiKeysPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        // Show/hide panels based on selected providers
        bool needsSttKey = string.Equals(_viewModel.SttChoice, "cloud", StringComparison.Ordinal);
        bool needsLlmKey = string.Equals(_viewModel.LlmChoice, "cloud", StringComparison.Ordinal);

        SttKeyPanel.Visibility = needsSttKey ? Visibility.Visible : Visibility.Collapsed;
        LlmKeyPanel.Visibility = needsLlmKey ? Visibility.Visible : Visibility.Collapsed;

        // Load any existing keys from ViewModel
        if (!string.IsNullOrWhiteSpace(_viewModel.DeepgramApiKey))
        {
            SttKeyBox.Password = _viewModel.DeepgramApiKey;
        }
        if (!string.IsNullOrWhiteSpace(_viewModel.GeminiApiKey))
        {
            LlmKeyBox.Password = _viewModel.GeminiApiKey;
        }
    }

    private void SttKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.DeepgramApiKey = SttKeyBox.Password;
        }
    }

    private void LlmKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.GeminiApiKey = LlmKeyBox.Password;
        }
    }
}
