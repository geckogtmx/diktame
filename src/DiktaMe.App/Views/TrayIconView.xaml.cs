namespace DiktaMe.App.Views;

using Microsoft.UI.Xaml.Controls;

/// <summary>
/// Hosts the H.NotifyIcon TaskbarIcon system tray control.
/// Must be instantiated and kept alive for the duration of the app.
/// Port of src/services/trayManager.ts from V1.
/// </summary>
public sealed partial class TrayIconView : UserControl
{
    /// <summary>Gets the ViewModel backing this tray icon.</summary>
    public TrayIconViewModel ViewModel { get; } = new TrayIconViewModel();

    public TrayIconView()
    {
        this.InitializeComponent();
        InitContextMenu();
    }

    private void InitContextMenu()
    {
        // Build the right-click context menu as a WinUI MenuFlyout and attach
        // it to the TaskbarIcon via ContextFlyout (standard WinUI mechanism).
        var flyout = new MenuFlyout();

        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = "Open Control Panel",
            Command = ViewModel.OpenControlPanelCommand,
        });
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = "Settings",
            Command = ViewModel.OpenSettingsCommand,
        });
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = "Quit dIKta.me",
            Command = ViewModel.QuitCommand,
        });

        Icon.ContextFlyout = flyout;
    }

    /// <summary>
    /// Updates the displayed icon state and tooltip.
    /// Safe to call from any thread — dispatches to the UI thread.
    /// </summary>
    public void SetState(TrayIconState state, string? statusSuffix = null)
    {
        ViewModel.SetState(state, statusSuffix);
    }

    /// <summary>
    /// Updates the tooltip to reflect the active STT + LLM provider combination,
    /// e.g. "dIKta.me — Cloud STT + Gemini LLM".
    /// </summary>
    public void SetCapabilityTooltip(string capabilitySummary)
    {
        ViewModel.TooltipText = $"dIKta.me — {capabilitySummary}";
    }
}
