using System.Net.Http;
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardApiKeysPage : Page, IWizardStepPage
{
    private readonly LocalizationService _loc;
    private WizardViewModel? _viewModel;

    public WizardApiKeysPage()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
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

        // Enable Test button only when key is entered
        TestSttKeyButton.IsEnabled = !string.IsNullOrWhiteSpace(SttKeyBox.Password);
        SttKeyStatus.Visibility = Visibility.Collapsed; // Hide previous test result
    }

    private void LlmKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.GeminiApiKey = LlmKeyBox.Password;
        }

        // Enable Test button only when key is entered
        TestLlmKeyButton.IsEnabled = !string.IsNullOrWhiteSpace(LlmKeyBox.Password);
        LlmKeyStatus.Visibility = Visibility.Collapsed; // Hide previous test result
    }

    private async void TestSttKeyButton_Click(object sender, RoutedEventArgs e)
    {
        string apiKey = SttKeyBox.Password;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        TestSttKeyButton.IsEnabled = false;
        SttKeyStatus.Visibility = Visibility.Visible;
        SttKeyProgress.IsActive = true;
        SttKeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Testing");

        try
        {
            // Test Deepgram API key by making a simple request
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Token", apiKey);

            var response = await client.GetAsync("https://api.deepgram.com/v1/projects");

            if (response.IsSuccessStatusCode)
            {
                SttKeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Valid");
                SttKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Green);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                SttKeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Invalid");
                SttKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Red);
            }
            else
            {
                SttKeyStatusText.Text = _loc.GetFormatted("Wizard_ApiKeys_TestFailed", response.StatusCode);
                SttKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Orange);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Deepgram API key test failed");
            SttKeyStatusText.Text = _loc.GetFormatted("Wizard_ApiKeys_ConnectionFailed", ex.Message);
            SttKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Red);
        }
        finally
        {
            SttKeyProgress.IsActive = false;
            TestSttKeyButton.IsEnabled = true;
        }
    }

    private async void TestLlmKeyButton_Click(object sender, RoutedEventArgs e)
    {
        string apiKey = LlmKeyBox.Password;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        TestLlmKeyButton.IsEnabled = false;
        LlmKeyStatus.Visibility = Visibility.Visible;
        LlmKeyProgress.IsActive = true;
        LlmKeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Testing");

        try
        {
            // Test Gemini API key by making a simple list-models request
            using var client = new HttpClient();
            var response = await client.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}");

            if (response.IsSuccessStatusCode)
            {
                LlmKeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Valid");
                LlmKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Green);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                     response.StatusCode == (System.Net.HttpStatusCode)400)
            {
                LlmKeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Invalid");
                LlmKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Red);
            }
            else
            {
                LlmKeyStatusText.Text = _loc.GetFormatted("Wizard_ApiKeys_TestFailed", response.StatusCode);
                LlmKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Orange);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Gemini API key test failed");
            LlmKeyStatusText.Text = _loc.GetFormatted("Wizard_ApiKeys_ConnectionFailed", ex.Message);
            LlmKeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Red);
        }
        finally
        {
            LlmKeyProgress.IsActive = false;
            TestLlmKeyButton.IsEnabled = true;
        }
    }
}
