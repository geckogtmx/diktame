
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class PrivacySettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;
    private readonly SecureStorage _secureStorage;
    private readonly LocalizationService _loc;
    private bool _isLoading;

    [ObservableProperty]
    private double _privacyLevel = 2; // Slider value 0-3

    [ObservableProperty]
    private string _privacyLevelLabel = "Balanced";

    [ObservableProperty]
    private bool _piiScrubEnabled = true;

    [ObservableProperty]
    private double _historyRetentionDays = 90;

    [ObservableProperty]
    private bool _isWiping;

    public PrivacySettingsViewModel(SettingsManager settings, HistoryManager history, SecureStorage secureStorage, LocalizationService loc)
    {
        _settings = settings;
        _history = history;
        _secureStorage = secureStorage;
        _loc = loc;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _isLoading = true;
        var p = _settings.Current.Privacy;
        PrivacyLevel = (int)p.Level;
        PiiScrubEnabled = p.PiiScrubEnabled;
        HistoryRetentionDays = p.HistoryRetentionDays;
        UpdateLabel();
        _isLoading = false;
    }

    partial void OnPrivacyLevelChanged(double value)
    {
        UpdateLabel();
        Save();
    }

    partial void OnPiiScrubEnabledChanged(bool value) => Save();
    partial void OnHistoryRetentionDaysChanged(double value) => Save();

    [RelayCommand]
    private async Task WipeAllDataAsync()
    {
        IsWiping = true;
        try
        {
            await _history.WipeAllAsync();
            Log.Information("All history data wiped by user");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to wipe data");
        }
        finally
        {
            IsWiping = false;
        }
    }

    private void UpdateLabel()
    {
        PrivacyLevelLabel = (int)PrivacyLevel switch
        {
            0 => _loc.GetString("Settings_Privacy_Level_Ghost"),
            1 => _loc.GetString("Settings_Privacy_Level_Stats"),
            2 => _loc.GetString("Settings_Privacy_Level_Balanced"),
            3 => _loc.GetString("Settings_Privacy_Level_Full"),
            _ => _loc.GetString("Settings_Privacy_Level_Balanced"),
        };
    }

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }

        var updated = _settings.Current with
        {
            Privacy = new PrivacySettings
            {
                Level = (PrivacyLevel)(int)PrivacyLevel,
                PiiScrubEnabled = PiiScrubEnabled,
                HistoryRetentionDays = (int)HistoryRetentionDays,
            }
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save privacy settings");
            }
        }, TaskScheduler.Default);
    }
}
