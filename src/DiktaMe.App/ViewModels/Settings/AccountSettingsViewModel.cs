
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AccountSettingsViewModel : ObservableObject
{
    private readonly IAccountService _accountService;
    private readonly ITrialService _trialService;
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;

    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _hasTrialData;
    [ObservableProperty] private int _wordsUsed;
    [ObservableProperty] private int _wordsQuota = 15_000;
    [ObservableProperty] private int _daysRemaining;
    [ObservableProperty] private bool _trialActive;
    [ObservableProperty] private double _usagePercent;
    [ObservableProperty] private string _usageText = "";
    [ObservableProperty] private string _statusText = "";

    public AccountSettingsViewModel(IAccountService accountService, ITrialService trialService, SettingsManager settings, LocalizationService loc)
    {
        _accountService = accountService;
        _trialService = trialService;
        _settings = settings;
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
    private async Task RefreshAsync()
    {
        await _trialService.RefreshStatusAsync();
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

    internal void Refresh()
    {
        var trial = _settings.Current.Trial;
        IsSignedIn = _accountService.HasValidToken;
        Email = _accountService.Email ?? string.Empty;

        // Trial section is conditional — only show when trial data exists
        HasTrialData = trial.TrialWordsQuota > 0
            && !string.IsNullOrEmpty(trial.TrialExpiresAt);

        WordsUsed = trial.TrialWordsUsed;
        WordsQuota = trial.TrialWordsQuota > 0 ? trial.TrialWordsQuota : 15_000;
        DaysRemaining = trial.TrialDaysRemaining;
        TrialActive = trial.TrialActive;
        UsagePercent = WordsQuota > 0 ? Math.Min(100.0, (double)WordsUsed / WordsQuota * 100.0) : 0;
        UsageText = _loc.GetFormatted("Settings_Account_UsageText", WordsUsed, WordsQuota);
        StatusText = IsSignedIn
            ? (HasTrialData
                ? (TrialActive ? _loc.GetFormatted("Settings_Account_Status_DaysRemaining", DaysRemaining) : _loc.GetString("Settings_Account_Status_Expired"))
                : _loc.GetString("Settings_Account_Status_Active"))
            : _loc.GetString("Settings_Account_Status_NotSignedIn");
    }
}
