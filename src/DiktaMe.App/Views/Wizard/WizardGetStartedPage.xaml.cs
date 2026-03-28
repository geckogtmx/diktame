
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardGetStartedPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;
    private readonly LicenseManager _licenseManager;

    public WizardGetStartedPage()
    {
        _licenseManager = App.Current.Services.GetRequiredService<LicenseManager>();
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;

        BuyLicenseLink.Click += (_, _) =>
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new System.Uri("https://dikta.me/pricing"));
        };
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;

        // Restore selection from ViewModel
        if (string.Equals(_viewModel.OnboardingChoice, "apikeys", StringComparison.Ordinal))
        {
            ApiKeysRadio.IsChecked = true;
        }
        else if (string.Equals(_viewModel.OnboardingChoice, "local", StringComparison.Ordinal))
        {
            LocalRadio.IsChecked = true;
        }
        else
        {
            WalletRadio.IsChecked = true;
        }

        _licenseManager.LicenseStateChanged += OnLicenseStateChanged;
        UpdateLicenseGate();
    }

    private void OnboardingRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var selected = OnboardingRadio.SelectedItem as RadioButton;
        _viewModel.OnboardingChoice = selected?.Tag as string ?? "wallet";
        UpdateLicenseGate();
    }

    private void UpdateLicenseGate()
    {
        bool needsLicense = _viewModel?.OnboardingChoice is "apikeys" or "local";
        bool show = needsLicense && !_licenseManager.IsLicensed;
        LicenseGatePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnLicenseStateChanged(bool licensed)
    {
        DispatcherQueue.TryEnqueue(UpdateLicenseGate);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _licenseManager.LicenseStateChanged -= OnLicenseStateChanged;
    }
}
