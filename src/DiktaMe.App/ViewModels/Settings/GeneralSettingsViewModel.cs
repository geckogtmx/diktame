
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
    private readonly ThemeService _themeService;
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
    private bool _isLanguageSelected;

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

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private int _expandDirectionIndex; // 0 = Down, 1 = Up

    // ── Visual Effects fields ───────────────────────────────────────────

    [ObservableProperty]
    private bool _visualEffectsEnabled = true;

    [ObservableProperty]
    private int _visualEffectsScopeIndex; // 0 = WholeApp, 1 = TopBarOnly

    [ObservableProperty]
    private double _visualEffectsIntensityPercent = 50; // 0-100

    // ── Auto-Hide fields ──────────────────────────────────────────────

    [ObservableProperty]
    private bool _autoHideEnabled;

    [ObservableProperty]
    private int _autoHideDelayIndex; // 0=1s, 1=3s, 2=5s, 3=10s, 4=Never

    public int[] AutoHideDelayValues { get; } = [10, 30, 60, 300, 0]; // 0 = Never

    // ── Auto-Collapse fields ──────────────────────────────────────────

    [ObservableProperty]
    private bool _autoCollapseEnabled;

    [ObservableProperty]
    private int _autoCollapseDelayIndex; // 0=5s, 1=10s, 2=30s, 3=60s

    public int[] AutoCollapseDelayValues { get; } = [5, 10, 30, 60];

    // ── Waveform style fields ─────────────────────────────────────────

    [ObservableProperty]
    private int _waveformStyleIndex; // 0=Wave, 1=Bars, 2=Off

    public string[] WaveformStyleValues { get; } = ["Wave", "Bars", "Off"];

    // ── Bar Position field ──────────────────────────────────────────

    [ObservableProperty]
    private string _barPosition = "TopRight";

    // ── Idle roll animation ─────────────────────────────────────────

    [ObservableProperty]
    private bool _idleRollEnabled = true;

    // ── Language fields ──────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedLanguageIndex;

    // ── Theme field ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedThemeIndex; // 0=Midnight, 1=Ember, 2=Frost

    public string[] ThemeNames => ThemeService.AvailableThemes;

    public GeneralSettingsViewModel(SettingsManager settings, LocalizationService loc, ThemeService themeService)
    {
        _settings = settings;
        _loc = loc;
        _themeService = themeService;

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

    public string[] AutoHideDelayLabels => [
        _loc.GetString("Settings_AutoHide_Delay_10s"),
        _loc.GetString("Settings_AutoHide_Delay_30s"),
        _loc.GetString("Settings_AutoHide_Delay_1m"),
        _loc.GetString("Settings_AutoHide_Delay_5m"),
        _loc.GetString("Settings_AutoHide_Delay_Never"),
    ];

    public string[] AutoCollapseDelayLabels => [
        _loc.GetString("Settings_AutoCollapse_Delay_5s"),
        _loc.GetString("Settings_AutoCollapse_Delay_10s"),
        _loc.GetString("Settings_AutoCollapse_Delay_30s"),
        _loc.GetString("Settings_AutoCollapse_Delay_1m"),
    ];

    public string[] WaveformStyleLabels => [
        _loc.GetString("Settings_Waveform_Style_Wave"),
        _loc.GetString("Settings_Waveform_Style_Bars"),
        _loc.GetString("Settings_Waveform_Style_Off"),
    ];

    // ── Sub-item list ───────────────────────────────────────────────────

    private void LoadSubItems()
    {
        SubItems.Clear();
        SubItems.Add(new ModeListItem { Id = "application", Title = _loc.GetString("Settings_General_Sub_Application"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "language", Title = _loc.GetString("Settings_General_Sub_Language"), IsDictationMode = false, IsSeparator = false });
    }

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsApplicationSelected = false;
            IsLanguageSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsApplicationSelected = id == "application";
        IsLanguageSelected = id == "language";
    }

    // ── Load / Save ─────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        _isLoading = true;
        var g = _settings.Current.General;
        SelectedUiLanguageIndex = Array.IndexOf(UiLanguageCodes, g.UiLanguage) is var ui and >= 0 ? ui : 0;
        SelectedLanguageIndex = Array.IndexOf(LanguageCodes, g.Language) is var i and >= 0 ? i : 0;
        AutoStart = g.AutoStart;
        SelectedThemeIndex = Array.IndexOf(ThemeNames, g.ThemeName) is var ti and >= 0 ? ti : 0;
        ShowRestartWarning = false;

        var cp = _settings.Current.ControlPanel;
        ShowModesRow = cp.ShowModesRow;
        ShowActionsRow = cp.ShowActionsRow;
        ShowSessionStats = cp.ShowSessionStats;
        ShowPerformanceStats = cp.ShowPerformanceStats;
        AlwaysOnTop = cp.AlwaysOnTop;
        ExpandDirectionIndex = string.Equals(cp.ExpandDirection, "Up", StringComparison.Ordinal) ? 1 : 0;
        VisualEffectsEnabled = cp.VisualEffectsEnabled;
        VisualEffectsScopeIndex = string.Equals(cp.VisualEffectsScope, "TopBarOnly", StringComparison.Ordinal) ? 1 : 0;
        VisualEffectsIntensityPercent = cp.VisualEffectsIntensity * 100;
        AutoHideEnabled = cp.AutoHideEnabled;
        AutoHideDelayIndex = Array.IndexOf(AutoHideDelayValues, cp.AutoHideDelaySeconds) is var ah and >= 0 ? ah : 1; // default 30s
        AutoCollapseEnabled = cp.AutoCollapseEnabled;
        AutoCollapseDelayIndex = Array.IndexOf(AutoCollapseDelayValues, cp.AutoCollapseDelaySeconds) is var ac and >= 0 ? ac : 1; // default 10s
        WaveformStyleIndex = Array.IndexOf(WaveformStyleValues, cp.WaveformStyle) is var ws and >= 0 ? ws : 0; // default Wave
        BarPosition = cp.BarPosition ?? "TopRight";
        IdleRollEnabled = cp.IdleRollEnabled;

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
    partial void OnAlwaysOnTopChanged(bool value) => Save();
    partial void OnExpandDirectionIndexChanged(int value) => Save();
    partial void OnVisualEffectsEnabledChanged(bool value) => Save();
    partial void OnVisualEffectsScopeIndexChanged(int value) => Save();
    partial void OnVisualEffectsIntensityPercentChanged(double value) => Save();
    partial void OnAutoHideEnabledChanged(bool value) => Save();
    partial void OnAutoHideDelayIndexChanged(int value) => Save();
    partial void OnAutoCollapseEnabledChanged(bool value) => Save();
    partial void OnAutoCollapseDelayIndexChanged(int value) => Save();
    partial void OnWaveformStyleIndexChanged(int value) => Save();
    partial void OnBarPositionChanged(string value) => Save();
    partial void OnIdleRollEnabledChanged(bool value) => Save();

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (!_isLoading && value >= 0 && value < ThemeNames.Length)
        {
            _ = _themeService.ApplyAndSaveAsync(ThemeNames[value]);
        }
    }

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
                AlwaysOnTop = AlwaysOnTop,
                ExpandDirection = ExpandDirectionIndex == 1 ? "Up" : "Down",
                VisualEffectsEnabled = VisualEffectsEnabled,
                VisualEffectsScope = VisualEffectsScopeIndex == 1 ? "TopBarOnly" : "WholeApp",
                VisualEffectsIntensity = VisualEffectsIntensityPercent / 100.0,
                AutoCollapseEnabled = AutoCollapseEnabled,
                AutoCollapseDelaySeconds = AutoCollapseDelayIndex >= 0 && AutoCollapseDelayIndex < AutoCollapseDelayValues.Length
                    ? AutoCollapseDelayValues[AutoCollapseDelayIndex] : 10,
                WaveformStyle = WaveformStyleIndex >= 0 && WaveformStyleIndex < WaveformStyleValues.Length
                    ? WaveformStyleValues[WaveformStyleIndex] : "Wave",
                AutoHideEnabled = AutoHideEnabled,
                // Enforce constraint: hide delay ≥ collapse delay when both enabled
                AutoHideDelaySeconds = EnforceHideDelay(),
                BarPosition = BarPosition,
                IdleRollEnabled = IdleRollEnabled,
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

    private int EnforceHideDelay()
    {
        int hideDelay = AutoHideDelayIndex >= 0 && AutoHideDelayIndex < AutoHideDelayValues.Length
            ? AutoHideDelayValues[AutoHideDelayIndex] : 30;
        // "Never" (0) is exempt from constraint
        if (hideDelay == 0)
        {
            return 0;
        }
        int collapseDelay = AutoCollapseDelayIndex >= 0 && AutoCollapseDelayIndex < AutoCollapseDelayValues.Length
            ? AutoCollapseDelayValues[AutoCollapseDelayIndex] : 10;
        // When both enabled, hide delay must be ≥ collapse delay
        if (AutoCollapseEnabled && AutoHideEnabled && hideDelay < collapseDelay)
        {
            return collapseDelay;
        }
        return hideDelay;
    }

    [RelayCommand]
    private void SetBarPosition(string position)
    {
        BarPosition = position;
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
