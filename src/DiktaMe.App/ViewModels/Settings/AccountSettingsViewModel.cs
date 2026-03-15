
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AccountSettingsViewModel : ObservableObject
{
    private readonly IAccountService _accountService;
    private readonly SettingsManager _settings;
    private readonly WalletManager _wallet;
    private readonly LocalizationService _loc;

    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _walletBalanceFormatted = "$0.00";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _balanceColorHex = "#FFFFFF";
    [ObservableProperty] private bool _useWalletProxy;

    /// <summary>Recent wallet transactions for the history list.</summary>
    public ObservableCollection<WalletTransactionItem> RecentTransactions { get; } = [];

    public AccountSettingsViewModel(
        IAccountService accountService,
        SettingsManager settings,
        WalletManager wallet,
        LocalizationService loc)
    {
        _accountService = accountService;
        _settings = settings;
        _wallet = wallet;
        _loc = loc;
        Refresh();
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
    private static void ManageAccount()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.dikta.me/dashboard")
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
    private static void TopUp()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.dikta.me/wallet")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountSettingsViewModel: failed to open top-up page");
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

        // Format wallet balance from cached microdollars
        long balanceMicro = _settings.Current.Account.WalletBalanceMicro;
        decimal balanceDollars = balanceMicro / 1_000_000m;
        WalletBalanceFormatted = balanceDollars.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

        // Color-code balance: green > $1, yellow $0.50-$1, red < $0.50
        BalanceColorHex = balanceDollars >= 1.00m ? "#7AFF9E"  // green
            : balanceDollars >= 0.50m ? "#FFD54F"              // yellow
            : "#FF4444";                                        // red

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
            var transactions = await _wallet.GetTransactionsAsync(20);
            RecentTransactions.Clear();
            foreach (var txn in transactions)
            {
                RecentTransactions.Add(new WalletTransactionItem
                {
                    TypeIcon = GetTypeIcon(txn.Type),
                    TypeLabel = txn.Type,
                    AmountFormatted = FormatAmount(txn.AmountMicro),
                    AmountColorHex = txn.AmountMicro >= 0 ? "#7AFF9E" : "#FF4444",
                    DateFormatted = txn.CreatedAt.LocalDateTime.ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture),
                    BalanceAfterFormatted = (txn.BalanceAfterMicro / 1_000_000m).ToString("C2", CultureInfo.GetCultureInfo("en-US")),
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
        WalletTransactionType.Usage => "\uE700",     // Down arrow
        WalletTransactionType.Refund => "\uE72C",    // Undo
        WalletTransactionType.Sync => "\uE895",      // Sync
        WalletTransactionType.Expiry => "\uE823",    // Clock
        _ => "\uE7BA",                               // Info
    };

    private static string FormatAmount(long amountMicro)
    {
        decimal dollars = amountMicro / 1_000_000m;
        string sign = dollars >= 0 ? "+" : "";
        return sign + dollars.ToString("C4", CultureInfo.GetCultureInfo("en-US"));
    }
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
