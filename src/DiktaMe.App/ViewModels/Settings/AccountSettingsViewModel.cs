
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AccountSettingsViewModel : ObservableObject
{
    private readonly IAccountService _accountService;
    private readonly SettingsManager _settings;
    private readonly WalletManager _wallet;
    private readonly LocalizationService _loc;
    private readonly LicenseManager _licenseManager;

    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _walletBalanceFormatted = "0 credits";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _balanceColorHex = "#FFFFFF";
    [ObservableProperty] private bool _useWalletProxy;

    // License state
    [ObservableProperty] private bool _isLicensed;
    [ObservableProperty] private string _licenseStatusText = "";
    [ObservableProperty] private string _licenseKeyInput = "";

    /// <summary>Recent wallet transactions for the history list.</summary>
    public ObservableCollection<WalletTransactionItem> RecentTransactions { get; } = [];

    public AccountSettingsViewModel(
        IAccountService accountService,
        SettingsManager settings,
        WalletManager wallet,
        LocalizationService loc,
        LicenseManager licenseManager)
    {
        _accountService = accountService;
        _settings = settings;
        _wallet = wallet;
        _loc = loc;
        _licenseManager = licenseManager;
        Refresh();
    }

    [RelayCommand]
    private async Task ActivateLicenseAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKeyInput))
        {
            LicenseStatusText = _loc.GetString("License_InvalidKey");
            return;
        }

        LicenseStatusText = "Activating...";

        bool success = await _licenseManager.ActivateAsync(LicenseKeyInput.Trim());
        if (success)
        {
            IsLicensed = true;
            LicenseStatusText = _loc.GetString("License_Activated");
            LicenseKeyInput = "";
            Log.Information("AccountSettings: Power License activated");
        }
        else
        {
            LicenseStatusText = _licenseManager.LastError ?? _loc.GetString("License_InvalidKey");
        }
    }

    [RelayCommand]
    private void BuyLicense()
    {
        try
        {
            var locale = GetWebsiteLocale(_settings.Current.General.UiLanguage);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"https://www.dikta.me/{locale}/pricing")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountSettingsViewModel: failed to open pricing page");
        }
    }

    [RelayCommand]
    private async Task DeactivateLicenseAsync()
    {
        await _licenseManager.DeactivateAsync();
        IsLicensed = false;
        LicenseStatusText = "";
        Log.Information("AccountSettings: Power License deactivated");
    }

    [RelayCommand]
    private void SignIn()
    {
        _accountService.Login();
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _accountService.LogoutAsync();
    }

    [RelayCommand]
    private void ManageAccount()
    {
        try
        {
            var locale = GetWebsiteLocale(_settings.Current.General.UiLanguage);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"https://www.dikta.me/{locale}/dashboard")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountSettingsViewModel: failed to open dashboard");
        }
    }

    [RelayCommand]
    private void BuyCredits()
    {
        try
        {
            // LemonSqueezy checkout for 4,000-credit pack ($5) — prefill email if signed in
            string url = "https://diktame.lemonsqueezy.com/buy/964385";
            string? email = _accountService.Email;
            if (!string.IsNullOrEmpty(email))
            {
                url += "?checkout[email]=" + Uri.EscapeDataString(email);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountSettingsViewModel: failed to open credits purchase page");
        }
    }

    partial void OnUseWalletProxyChanged(bool value)
    {
        AuthMode newMode = value ? AuthMode.Wallet : AuthMode.Account;
        if (_settings.Current.AuthMode != newMode)
        {
            _ = _settings.UpdateAsync(_settings.Current with { AuthMode = newMode });
            Log.Information("AccountSettings: Wallet proxy toggled to {Mode}", newMode);
        }
    }

    internal void Refresh()
    {
        IsSignedIn = _accountService.HasValidToken;
        Email = _accountService.Email ?? string.Empty;
        UseWalletProxy = _settings.Current.AuthMode == AuthMode.Wallet;

        // License state
        IsLicensed = _licenseManager.IsLicensed;
        LicenseStatusText = IsLicensed
            ? _loc.GetString("License_UnlockedStatus")
            : "";

        // Format wallet balance as credits (1 credit = 1,000 microdollars = $0.001)
        long balanceMicro = _settings.Current.Account.WalletBalanceMicro;
        long credits = balanceMicro / 1_000;
        WalletBalanceFormatted = credits.ToString("N0", CultureInfo.InvariantCulture) + " credits";

        // Color-code balance: green > 1000, yellow 500-1000, red < 500
        BalanceColorHex = credits >= 1_000 ? "#7AFF9E"  // green
            : credits >= 500 ? "#FFD54F"                // yellow
            : "#FF4444";                                 // red

        StatusText = IsSignedIn
            ? _loc.GetString("Settings_Account_Status_Active")
            : _loc.GetString("Settings_Account_Status_NotSignedIn");

        // Load recent transactions (fire-and-forget — UI update via dispatcher)
        _ = LoadTransactionsAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            var summaries = await _wallet.GetDailySummariesAsync(20);
            RecentTransactions.Clear();

            foreach (var summary in summaries)
            {
                string typeLabel;
                string icon;

                if (summary.IsAggregate)
                {
                    typeLabel = "Usage";
                    icon = "\uE700"; // Down arrow
                }
                else
                {
                    typeLabel = summary.Type;
                    icon = GetTypeIcon(summary.Type);
                }

                RecentTransactions.Add(new WalletTransactionItem
                {
                    TypeIcon = icon,
                    TypeLabel = typeLabel,
                    AmountFormatted = FormatAmount(summary.AmountMicro),
                    AmountColorHex = summary.AmountMicro >= 0 ? "#7AFF9E" : "#FF4444",
                    DateFormatted = summary.DateLabel,
                    BalanceAfterFormatted = (summary.BalanceAfterMicro / 1_000).ToString("N0", CultureInfo.InvariantCulture) + " cr",
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountSettingsViewModel: failed to load transactions");
        }
    }

    private static string GetTypeIcon(string type) => type switch
    {
        WalletTransactionType.Grant => "\uE8A1",    // Gift
        WalletTransactionType.Purchase => "\uE8A1",  // Gift/Money
        WalletTransactionType.Refund => "\uE72C",    // Undo
        WalletTransactionType.Expiry => "\uE823",    // Clock
        _ => "\uE7BA",                               // Info
    };

    private static string FormatAmount(long amountMicro)
    {
        long credits = amountMicro / 1_000;
        string sign = credits >= 0 ? "+" : "";
        return sign + credits.ToString("N0", CultureInfo.InvariantCulture) + " cr";
    }

    private static string GetWebsiteLocale(string uiLanguage) =>
        uiLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
}

/// <summary>
/// Display-friendly transaction item for the UI ListView.
/// </summary>
public sealed class WalletTransactionItem
{
    public required string TypeIcon { get; init; }
    public required string TypeLabel { get; init; }
    public required string AmountFormatted { get; init; }
    public required string AmountColorHex { get; init; }
    public required string DateFormatted { get; init; }
    public required string BalanceAfterFormatted { get; init; }
}
