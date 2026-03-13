
using System.Collections.ObjectModel;
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

    // ── Inner list ───────────────────────────────────────────────────────

    public ObservableCollection<ModeListItem> SubItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isApplicationSelected;

    [ObservableProperty]
    private bool _isBehaviorSelected;

    // ── Application fields ───────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedUiLanguageIndex;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _showRestartWarning;

    // ── Control Panel fields (absorbed from ControlPanelConfigViewModel) ─

    [ObservableProperty]
    private bool _showModesRow = true;

    [ObservableProperty]
    private bool _showActionsRow = true;

    [ObservableProperty]
    private bool _showSessionStats = true;

    [ObservableProperty]
    private bool _showPerformanceStats = true;

    // ── Behavior fields ──────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedLanguageIndex;

    public GeneralSettingsViewModel(SettingsManager settings, LocalizationService loc)
    {
        _settings = settings;
        _loc = loc;

        LoadSubItems();
        LoadFromSettings();

        if (SubItems.Count > 0)
        {
            SelectedIndex = 0;
        }
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

    // ── Sub-item list ───────────────────────────────────────────────────

    private void LoadSubItems()
    {
        SubItems.Clear();
        SubItems.Add(new ModeListItem { Id = "application", Title = _loc.GetString("Settings_General_Sub_Application"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "behavior", Title = _loc.GetString("Settings_General_Sub_Behavior"), IsDictationMode = false, IsSeparator = false });
    }

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsApplicationSelected = false;
            IsBehaviorSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsApplicationSelected = id == "application";
        IsBehaviorSelected = id == "behavior";
    }

    // ── Load / Save ─────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        _isLoading = true;
        var g = _settings.Current.General;
        SelectedUiLanguageIndex = Array.IndexOf(UiLanguageCodes, g.UiLanguage) is var ui and >= 0 ? ui : 0;
        SelectedLanguageIndex = Array.IndexOf(LanguageCodes, g.Language) is var i and >= 0 ? i : 0;
        AutoStart = g.AutoStart;
        ShowRestartWarning = false;

        var cp = _settings.Current.ControlPanel;
        ShowModesRow = cp.ShowModesRow;
        ShowActionsRow = cp.ShowActionsRow;
        ShowSessionStats = cp.ShowSessionStats;
        ShowPerformanceStats = cp.ShowPerformanceStats;

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
    partial void OnShowModesRowChanged(bool value) => Save();
    partial void OnShowActionsRowChanged(bool value) => Save();
    partial void OnShowSessionStatsChanged(bool value) => Save();
    partial void OnShowPerformanceStatsChanged(bool value) => Save();

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
            },
            ControlPanel = _settings.Current.ControlPanel with
            {
                ShowModesRow = ShowModesRow,
                ShowActionsRow = ShowActionsRow,
                ShowSessionStats = ShowSessionStats,
                ShowPerformanceStats = ShowPerformanceStats,
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
