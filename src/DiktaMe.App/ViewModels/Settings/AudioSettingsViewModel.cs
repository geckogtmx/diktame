
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AudioSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly NotificationService _notifications;
    private readonly LocalizationService _loc;
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<string> _devices = new();

    [ObservableProperty]
    private int _selectedDeviceIndex = -1;

    [ObservableProperty]
    private int _selectedDurationIndex = 1; // 0=30s, 1=60s, 2=120s, 3=unlimited

    [ObservableProperty]
    private bool _duckingEnabled = true;

    [ObservableProperty]
    private double _duckLevelPercent = 20;

    [ObservableProperty]
    private double _rampDownMs = 500;

    // ── Sound Feedback ────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _soundEnabled = true;

    [ObservableProperty]
    private ObservableCollection<string> _availableSounds = new();

    [ObservableProperty]
    private int _selectedStartSoundIndex = -1;

    [ObservableProperty]
    private int _selectedStopSoundIndex = -1;

    [ObservableProperty]
    private int _selectedUtilitySoundIndex = -1;

    public AudioSettingsViewModel(SettingsManager settings, NotificationService notifications, LocalizationService loc)
    {
        _settings = settings;
        _notifications = notifications;
        _loc = loc;
        LoadDevices();
        LoadSounds();
        LoadFromSettings();
    }

    public string[] DurationLabels => [
        _loc.GetString("Settings_Audio_Duration_30s"),
        _loc.GetString("Settings_Audio_Duration_60s"),
        _loc.GetString("Settings_Audio_Duration_120s"),
        _loc.GetString("Settings_Audio_Duration_Unlimited"),
    ];
    public int[] DurationValues { get; } = [30, 60, 120, 0];

    private void LoadDevices()
    {
        Devices.Clear();
        Devices.Add(_loc.GetString("Settings_Audio_DefaultDevice"));
        foreach (var device in AudioDeviceManager.GetInputDevices())
        {
            Devices.Add(device.Name);
        }
    }

    private void LoadSounds()
    {
        AvailableSounds.Clear();
        foreach (var stem in NotificationService.GetAvailableSounds())
        {
            AvailableSounds.Add(stem);
        }
    }

    private int SoundStemToIndex(string stem)
    {
        if (AvailableSounds.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < AvailableSounds.Count; i++)
        {
            if (string.Equals(AvailableSounds[i], stem, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0; // fallback to first sound
    }

    private string IndexToSoundStem(int index)
    {
        return index >= 0 && index < AvailableSounds.Count ? AvailableSounds[index] : "a";
    }

    private void LoadFromSettings()
    {
        _isLoading = true;
        var a = _settings.Current.Audio ?? new();
        var d = _settings.Current.AudioDucking ?? new();
        var s = _settings.Current.Sound ?? new();

        SelectedDeviceIndex = string.IsNullOrEmpty(a.DeviceName) ? 0
            : Devices.IndexOf(a.DeviceName) is var i and >= 0 ? i : 0;

        SelectedDurationIndex = Array.IndexOf(DurationValues, a.MaxDurationSeconds) is var j and >= 0 ? j : 1;
        DuckingEnabled = d.Enabled;
        DuckLevelPercent = d.DuckLevelPercent;
        RampDownMs = d.RampDownMs;

        SoundEnabled = _settings.Current.General.SoundFeedback;
        SelectedStartSoundIndex = SoundStemToIndex(s.StartSound);
        SelectedStopSoundIndex = SoundStemToIndex(s.StopSound);
        SelectedUtilitySoundIndex = SoundStemToIndex(s.UtilitySound);

        _isLoading = false;
    }

    partial void OnSelectedDeviceIndexChanged(int value) => Save();
    partial void OnSelectedDurationIndexChanged(int value) => Save();
    partial void OnDuckingEnabledChanged(bool value) => Save();
    partial void OnDuckLevelPercentChanged(double value) => Save();
    partial void OnRampDownMsChanged(double value) => Save();
    partial void OnSoundEnabledChanged(bool value) => Save();
    partial void OnSelectedStartSoundIndexChanged(int value) => Save();
    partial void OnSelectedStopSoundIndexChanged(int value) => Save();
    partial void OnSelectedUtilitySoundIndexChanged(int value) => Save();

    [RelayCommand]
    private void PreviewStartSound() => _notifications.PlayCustomSound(IndexToSoundStem(SelectedStartSoundIndex));

    [RelayCommand]
    private void PreviewStopSound() => _notifications.PlayCustomSound(IndexToSoundStem(SelectedStopSoundIndex));

    [RelayCommand]
    private void PreviewUtilitySound() => _notifications.PlayCustomSound(IndexToSoundStem(SelectedUtilitySoundIndex));

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }

        string deviceName = SelectedDeviceIndex > 0 && SelectedDeviceIndex < Devices.Count
            ? Devices[SelectedDeviceIndex] : "";
        int duration = SelectedDurationIndex >= 0 && SelectedDurationIndex < DurationValues.Length
            ? DurationValues[SelectedDurationIndex] : 60;

        var updated = _settings.Current with
        {
            General = (_settings.Current.General ?? new()) with
            {
                SoundFeedback = SoundEnabled,
            },
            Audio = new AudioSettings
            {
                DeviceName = deviceName,
                MaxDurationSeconds = duration,
            },
            AudioDucking = new AudioDuckingSettings
            {
                Enabled = DuckingEnabled,
                DuckLevelPercent = (int)DuckLevelPercent,
                RampDownMs = (int)RampDownMs,
            },
            Sound = new SoundSettings
            {
                StartSound = IndexToSoundStem(SelectedStartSoundIndex),
                StopSound = IndexToSoundStem(SelectedStopSoundIndex),
                UtilitySound = IndexToSoundStem(SelectedUtilitySoundIndex),
            },
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save audio settings");
            }
        }, TaskScheduler.Default);
    }
}
