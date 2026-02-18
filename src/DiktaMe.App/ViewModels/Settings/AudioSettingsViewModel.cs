
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AudioSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
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

    public AudioSettingsViewModel(SettingsManager settings)
    {
        _settings = settings;
        LoadDevices();
        LoadFromSettings();
    }

    public string[] DurationLabels { get; } = ["30 seconds", "60 seconds", "120 seconds", "Unlimited"];
    public int[] DurationValues { get; } = [30, 60, 120, 0];

    private void LoadDevices()
    {
        Devices.Clear();
        Devices.Add("(Default device)");
        foreach (var device in AudioDeviceManager.GetInputDevices())
        {
            Devices.Add(device.Name);
        }
    }

    private void LoadFromSettings()
    {
        _isLoading = true;
        var a = _settings.Current.Audio;
        var d = _settings.Current.AudioDucking;

        SelectedDeviceIndex = string.IsNullOrEmpty(a.DeviceName) ? 0
            : Devices.IndexOf(a.DeviceName) is var i and >= 0 ? i : 0;

        SelectedDurationIndex = Array.IndexOf(DurationValues, a.MaxDurationSeconds) is var j and >= 0 ? j : 1;
        DuckingEnabled = d.Enabled;
        DuckLevelPercent = d.DuckLevelPercent;
        _isLoading = false;
    }

    partial void OnSelectedDeviceIndexChanged(int value) => Save();
    partial void OnSelectedDurationIndexChanged(int value) => Save();
    partial void OnDuckingEnabledChanged(bool value) => Save();
    partial void OnDuckLevelPercentChanged(double value) => Save();

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
            Audio = new AudioSettings
            {
                DeviceName = deviceName,
                MaxDurationSeconds = duration,
            },
            AudioDucking = new AudioDuckingSettings
            {
                Enabled = DuckingEnabled,
                DuckLevelPercent = (int)DuckLevelPercent,
            }
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
