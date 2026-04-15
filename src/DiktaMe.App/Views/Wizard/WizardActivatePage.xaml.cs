
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardActivatePage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;
    private readonly LicenseManager _licenseManager;

    public WizardActivatePage()
    {
        _licenseManager = App.Current.Services.GetRequiredService<LicenseManager>();
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        string key = LicenseKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ActivateButton.IsEnabled = false;
        ActivateProgress.IsActive = true;
        ActivateProgress.Visibility = Visibility.Visible;
        ActivateStatus.Visibility = Visibility.Collapsed;

        try
        {
            bool success = await _licenseManager.ActivateAsync(key);

            if (success)
            {
                ActivateStatus.Text = "License activated! Choose your setup path...";
                ActivateStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.LimeGreen);
                ActivateStatus.Visibility = Visibility.Visible;

                Log.Information("Wizard: license activated, returning to Get Started");

                await System.Threading.Tasks.Task.Delay(1500);
                _viewModel?.ReturnToGetStarted();
            }
            else
            {
                ActivateStatus.Text = _licenseManager.LastError ?? "Invalid license key.";
                ActivateStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Tomato);
                ActivateStatus.Visibility = Visibility.Visible;
            }
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "Wizard: license activation failed");
            ActivateStatus.Text = "Connection error. Please try again.";
            ActivateStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Tomato);
            ActivateStatus.Visibility = Visibility.Visible;
        }
        finally
        {
            ActivateButton.IsEnabled = true;
            ActivateProgress.IsActive = false;
            ActivateProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BuyLink_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(new System.Uri("https://dikta.me/pricing"));
    }
}
