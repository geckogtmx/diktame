
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AccountSettingsViewModel : ObservableObject
{
    private readonly ITrialAccountService _trialService;
    private readonly SettingsManager _settings;

    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private int _wordsUsed;
    [ObservableProperty] private int _wordsQuota = 15_000;
    [ObservableProperty] private int _daysRemaining;
    [ObservableProperty] private bool _trialActive;
    [ObservableProperty] private double _usagePercent;
    [ObservableProperty] private string _usageText = "0 / 15,000 words";
    [ObservableProperty] private string _statusText = "Not signed in";

    public AccountSettingsViewModel(ITrialAccountService trialService, SettingsManager settings)
    {
        _trialService = trialService;
        _settings = settings;
        _trialService.StatusChanged += OnStatusChanged;
        RefreshFromSettings();
    }

    [RelayCommand]
    private void SignIn()
    {
        _trialService.Login();
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _trialService.LogoutAsync();
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://dikta.me/dashboard")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountSettingsViewModel: failed to open dashboard");
        }
    }

    private void OnStatusChanged(TrialStatus? status)
    {
        RefreshFromSettings();
    }

    private void RefreshFromSettings()
    {
        var trial = _settings.Current.Trial;
        IsSignedIn = _trialService.HasValidToken;
        Email = trial.TrialEmail;
        WordsUsed = trial.TrialWordsUsed;
        WordsQuota = trial.TrialWordsQuota > 0 ? trial.TrialWordsQuota : 15_000;
        DaysRemaining = trial.TrialDaysRemaining;
        TrialActive = trial.TrialActive;
        UsagePercent = WordsQuota > 0 ? Math.Min(100.0, (double)WordsUsed / WordsQuota * 100.0) : 0;
        UsageText = $"{WordsUsed:N0} / {WordsQuota:N0} words";
        StatusText = IsSignedIn
            ? (TrialActive ? $"{DaysRemaining} days remaining" : "Trial expired")
            : "Not signed in";
    }
}
