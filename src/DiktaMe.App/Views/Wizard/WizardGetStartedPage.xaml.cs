
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

        _licenseManager.LicenseStateChanged += OnLicenseStateChanged;
        UpdateOptionAvailability();

        // Restore selection from ViewModel (only if licensed — unlicensed always gets Wallet)
        if (_licenseManager.IsLicensed)
        {
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
        }
        else
        {
            WalletRadio.IsChecked = true;
            _viewModel.OnboardingChoice = "wallet";
        }
    }

    private void OnboardingRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var selected = OnboardingRadio.SelectedItem as RadioButton;
        _viewModel.OnboardingChoice = selected?.Tag as string ?? "wallet";
    }

    private void UpdateOptionAvailability()
    {
        bool licensed = _licenseManager.IsLicensed;
        ApiKeysRadio.IsEnabled = licensed;
        LocalRadio.IsEnabled = licensed;
        LicenseInfoPanel.Visibility = licensed ? Visibility.Collapsed : Visibility.Visible;

        // If license was just activated, keep current selection; if deactivated, reset to Wallet
        if (!licensed && _viewModel is not null)
        {
            WalletRadio.IsChecked = true;
            _viewModel.OnboardingChoice = "wallet";
        }
    }

    private void OnLicenseStateChanged(bool licensed)
    {
        DispatcherQueue.TryEnqueue(UpdateOptionAvailability);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _licenseManager.LicenseStateChanged -= OnLicenseStateChanged;
    }
}
