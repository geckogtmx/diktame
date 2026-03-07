
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;

namespace DiktaMe.App.Views;
/// <summary>
/// The tray icon states, matching V1's icon state names.
/// </summary>
public enum TrayIconState
{
    Idle,
    Recording,
    Processing,
    Error,
}

/// <summary>
/// ViewModel for the system tray icon.
/// Manages icon state, tooltip text, and context menu commands.
/// Port of src/services/trayManager.ts from V1.
/// </summary>
public sealed partial class TrayIconViewModel : ObservableObject
{
    private readonly LocalizationService _loc;

    public TrayIconViewModel()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        _tooltipText = $"dIKta.me — {_loc.GetString("Tray_State_Idle")}";
    }

    // ── Observable properties ─────────────────────────────────────────────────

    [ObservableProperty]
    private TrayIconState _iconState = TrayIconState.Idle;

    [ObservableProperty]
    private string _tooltipText = "";

    [ObservableProperty]
    private bool _isRecording;

    // ── State label for GeneratedIconSource background colour ─────────────────

    /// <summary>
    /// Returns the state name string for binding to the tray icon source.
    /// </summary>
    public string IconStateName => IconState.ToString();

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenControlPanel()
    {
        Log.Information("TrayIcon: OpenControlPanel");
        App.Current.ShowMainWindow();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        Log.Information("TrayIcon: OpenSettings");
        App.Current.ShowSettings();
    }

    [RelayCommand]
    private void OpenQuickChat()
    {
        Log.Information("TrayIcon: OpenQuickChat");
        App.Current.ToggleQuickChat();
    }

    [RelayCommand]
    private void Quit()
    {
        Log.Information("TrayIcon: Quit requested");
        Application.Current.Exit();
    }

    // ── State update helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Updates the tray icon state and tooltip to reflect current app activity.
    /// </summary>
    public void SetState(TrayIconState state, string? statusSuffix = null)
    {
        IconState = state;
        IsRecording = state == TrayIconState.Recording;

        string status = statusSuffix ?? state switch
        {
            TrayIconState.Idle => _loc.GetString("Tray_State_Idle"),
            TrayIconState.Recording => _loc.GetString("Tray_State_Recording"),
            TrayIconState.Processing => _loc.GetString("Tray_State_Processing"),
            TrayIconState.Error => _loc.GetString("Tray_State_Error"),
            _ => state.ToString(),
        };

        TooltipText = $"dIKta.me — {status}";
        OnPropertyChanged(nameof(IconStateName));
        Log.Debug("TrayIcon: state → {State}, tooltip → '{Tooltip}'", state, TooltipText);
    }
}
