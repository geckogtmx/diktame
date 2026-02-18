
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private bool _isLoading;

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

    public GeneralSettingsViewModel(SettingsManager settings)
    {
        _settings = settings;
        LoadFromSettings();
    }

    public string[] Languages { get; } = ["English", "Spanish"];
    public string[] LanguageCodes { get; } = ["en", "es"];
    public string[] AdditionalKeyOptions { get; } = ["None", "Enter", "Tab", "Space"];
    public string[] AdditionalKeyCodes { get; } = ["", "Enter", "Tab", "Space"];

    private void LoadFromSettings()
    {
        _isLoading = true;
        var g = _settings.Current.General;
        SelectedLanguageIndex = Array.IndexOf(LanguageCodes, g.Language) is var i and >= 0 ? i : 0;
        AutoStart = g.AutoStart;
        SoundFeedback = g.SoundFeedback;
        SelectedAdditionalKeyIndex = Array.IndexOf(AdditionalKeyCodes, g.AdditionalKey) is var j and >= 0 ? j : 0;
        TrailingSpace = g.TrailingSpace;
        _isLoading = false;
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
            General = new GeneralSettings
            {
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
}
