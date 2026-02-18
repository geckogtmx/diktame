namespace DiktaMe.App.ViewModels.Settings;

using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.Core.Config;
using Serilog;

public sealed partial class ControlPanelConfigViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private bool _isLoading;

    [ObservableProperty] private bool _showModesRow = true;
    [ObservableProperty] private bool _showActionsRow = true;
    [ObservableProperty] private bool _showSessionStats = true;
    [ObservableProperty] private bool _showPerformanceStats = true;

    public ControlPanelConfigViewModel(SettingsManager settings)
    {
        _settings = settings;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _isLoading = true;
        var cp = _settings.Current.ControlPanel;
        ShowModesRow = cp.ShowModesRow;
        ShowActionsRow = cp.ShowActionsRow;
        ShowSessionStats = cp.ShowSessionStats;
        ShowPerformanceStats = cp.ShowPerformanceStats;
        _isLoading = false;
    }

    partial void OnShowModesRowChanged(bool value) => Save();
    partial void OnShowActionsRowChanged(bool value) => Save();
    partial void OnShowSessionStatsChanged(bool value) => Save();
    partial void OnShowPerformanceStatsChanged(bool value) => Save();

    private void Save()
    {
        if (_isLoading) return;
        var updated = _settings.Current with
        {
            ControlPanel = new ControlPanelSettings
            {
                ShowModesRow = ShowModesRow,
                ShowActionsRow = ShowActionsRow,
                ShowSessionStats = ShowSessionStats,
                ShowPerformanceStats = ShowPerformanceStats,
            }
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted) Log.Error(t.Exception, "Failed to save control panel config");
        }, TaskScheduler.Default);
    }
}
