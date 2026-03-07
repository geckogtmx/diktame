
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using Microsoft.UI.Xaml;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedUiLanguageIndex;

    [ObservableProperty]
    private int _selectedLanguageIndex;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _soundFeedback = true;

    [ObservableProperty]
    private int _selectedAdditionalKeyIndex;

    [ObservableProperty]
    private bool _trailingSpace = true;

    [ObservableProperty]
    private bool _showRestartWarning;

    public GeneralSettingsViewModel(SettingsManager settings, LocalizationService loc)
    {
        _settings = settings;
        _loc = loc;
        LoadFromSettings();
    }

    public string[] UiLanguages => [
        _loc.GetString("Settings_General_UiLang_English"),
        _loc.GetString("Settings_General_UiLang_Spanish"),
    ];
    public string[] UiLanguageCodes { get; } = ["en", "es-MX"];

    public string[] Languages => [
        _loc.GetString("Settings_General_Lang_English"),
        _loc.GetString("Settings_General_Lang_Spanish"),
    ];
    public string[] LanguageCodes { get; } = ["en", "es"];

    public string[] AdditionalKeyOptions => [
        _loc.GetString("Settings_General_AdditionalKey_None"),
        _loc.GetString("Settings_General_AdditionalKey_Enter"),
        _loc.GetString("Settings_General_AdditionalKey_Tab"),
        _loc.GetString("Settings_General_AdditionalKey_Space"),
    ];
    public string[] AdditionalKeyCodes { get; } = ["", "Enter", "Tab", "Space"];

    private void LoadFromSettings()
    {
        _isLoading = true;
        var g = _settings.Current.General;
        SelectedUiLanguageIndex = Array.IndexOf(UiLanguageCodes, g.UiLanguage) is var ui and >= 0 ? ui : 0;
        SelectedLanguageIndex = Array.IndexOf(LanguageCodes, g.Language) is var i and >= 0 ? i : 0;
        AutoStart = g.AutoStart;
        SoundFeedback = g.SoundFeedback;
        SelectedAdditionalKeyIndex = Array.IndexOf(AdditionalKeyCodes, g.AdditionalKey) is var j and >= 0 ? j : 0;
        TrailingSpace = g.TrailingSpace;
        ShowRestartWarning = false;
        _isLoading = false;
    }

    partial void OnSelectedUiLanguageIndexChanged(int value)
    {
        if (!_isLoading)
        {
            ShowRestartWarning = true;
        }
        Save();
    }

    partial void OnSelectedLanguageIndexChanged(int value) => Save();
    partial void OnAutoStartChanged(bool value) => Save();
    partial void OnSoundFeedbackChanged(bool value) => Save();
    partial void OnSelectedAdditionalKeyIndexChanged(int value) => Save();
    partial void OnTrailingSpaceChanged(bool value) => Save();

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }

        var updated = _settings.Current with
        {
            General = _settings.Current.General with
            {
                UiLanguage = SelectedUiLanguageIndex >= 0 && SelectedUiLanguageIndex < UiLanguageCodes.Length
                    ? UiLanguageCodes[SelectedUiLanguageIndex] : "en",
                Language = SelectedLanguageIndex >= 0 && SelectedLanguageIndex < LanguageCodes.Length
                    ? LanguageCodes[SelectedLanguageIndex] : "en",
                AutoStart = AutoStart,
                SoundFeedback = SoundFeedback,
                AdditionalKey = SelectedAdditionalKeyIndex >= 0 && SelectedAdditionalKeyIndex < AdditionalKeyCodes.Length
                    ? AdditionalKeyCodes[SelectedAdditionalKeyIndex] : "",
                TrailingSpace = TrailingSpace,
            }
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save general settings");
            }
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    private void RestartApp()
    {
        string? exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            Log.Warning("RestartApp: could not determine process path");
            return;
        }

        // Launch a new instance after a brief delay so the current one can release the mutex
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
        });

        Application.Current.Exit();
    }
}
