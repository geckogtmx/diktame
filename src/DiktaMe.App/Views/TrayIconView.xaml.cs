
using System.Drawing;
using DiktaMe.App.Services;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views;
/// <summary>
/// Manages the system tray icon via H.NotifyIcon.
/// The TaskbarIcon is created entirely in code (no XAML) so it works
/// without a WinUI visual tree — critical for standalone creation in App.OnLaunched.
/// Port of src/services/trayManager.ts from V1.
/// </summary>
public sealed partial class TrayIconView : UserControl, IDisposable
{
    private readonly TrayIcon _trayIcon;
    private readonly LocalizationService _loc;
    private Icon? _icon; // Must stay alive — disposing it invalidates the HICON handle.

    /// <summary>Gets the ViewModel backing this tray icon.</summary>
    public TrayIconViewModel ViewModel { get; } = new TrayIconViewModel();

    public TrayIconView()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        this.InitializeComponent();

        // Create the native tray icon directly via H.NotifyIcon.Core.TrayIcon
        // (the low-level Win32 wrapper — no XAML/rendering needed).
        _trayIcon = new TrayIcon();

        // Load .ico from disk next to the exe.
        // The Icon object must NOT be disposed — it owns the HICON handle.
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (File.Exists(icoPath))
        {
            _icon = new Icon(icoPath);
            _trayIcon.Icon = _icon.Handle;
            Log.Information("TrayIcon: loaded icon from {Path}", icoPath);
        }
        else
        {
            Log.Warning("TrayIcon: icon file not found at {Path}", icoPath);
        }

        _trayIcon.ToolTip = $"dIKta.me — {_loc.GetString("Tray_State_Idle")}";

        // Wire up mouse events
        _trayIcon.MessageWindow.MouseEventReceived += OnMouseEvent;

        // Show the icon in the system tray
        _trayIcon.Create();

        Log.Information("TrayIcon: created and visible");
    }

    private void OnMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs e)
    {
        switch (e.MouseEvent)
        {
            case MouseEvent.IconLeftMouseUp:
                Log.Debug("TrayIcon: left click");
                ViewModel.OpenControlPanelCommand.Execute(null);
                break;

            case MouseEvent.IconLeftDoubleClick:
                Log.Debug("TrayIcon: double click — restoring window");
                ViewModel.OpenControlPanelCommand.Execute(null);
                break;

            case MouseEvent.IconRightMouseUp:
                Log.Debug("TrayIcon: right click — showing context menu");
                ShowContextMenu(e.Point);
                break;
        }
    }

    private void ShowContextMenu(System.Drawing.Point cursorPosition)
    {
        var menu = new PopupMenu();

        menu.Items.Add(new PopupMenuItem(_loc.GetString("Tray_OpenControlPanel"), (_, _) =>
        {
            ViewModel.OpenControlPanelCommand.Execute(null);
        }));
        menu.Items.Add(new PopupMenuItem(_loc.GetString("Tray_QuickChat"), (_, _) =>
        {
            ViewModel.OpenQuickChatCommand.Execute(null);
        }));
        menu.Items.Add(new PopupMenuItem(_loc.GetString("Tray_Settings"), (_, _) =>
        {
            ViewModel.OpenSettingsCommand.Execute(null);
        }));
        // Plugin-contributed tray menu items
        var pluginUI = App.Current.Services.GetService<DiktaMe.Plugin.PluginUIRegistry>();
        if (pluginUI is not null)
        {
            var pluginItems = pluginUI.TrayMenuItems;
            if (pluginItems.Count > 0)
            {
                menu.Items.Add(new PopupMenuSeparator());
                foreach (var item in pluginItems)
                {
                    if (item.IsSeparatorBefore)
                    {
                        menu.Items.Add(new PopupMenuSeparator());
                    }
                    menu.Items.Add(new PopupMenuItem(item.Label, (_, _) => item.OnClick()));
                }
            }
        }

        menu.Items.Add(new PopupMenuSeparator());
        menu.Items.Add(new PopupMenuItem(_loc.GetString("Tray_Quit"), (_, _) =>
        {
            ViewModel.QuitCommand.Execute(null);
        }));

        menu.Show(
            ownerHandle: _trayIcon.MessageWindow.Handle,
            x: cursorPosition.X,
            y: cursorPosition.Y);
    }

    /// <summary>
    /// Updates the displayed icon state and tooltip.
    /// </summary>
    public void SetState(TrayIconState state, string? statusSuffix = null)
    {
        ViewModel.SetState(state, statusSuffix);
        _trayIcon.ToolTip = ViewModel.TooltipText;
    }

    /// <summary>
    /// Updates the tooltip to reflect the active STT + LLM provider combination.
    /// </summary>
    public void SetCapabilityTooltip(string capabilitySummary)
    {
        ViewModel.TooltipText = $"dIKta.me — {capabilitySummary}";
        _trayIcon.ToolTip = ViewModel.TooltipText;
    }

    public void Dispose()
    {
        _trayIcon.MessageWindow.MouseEventReceived -= OnMouseEvent;
        _trayIcon.Remove();
        _trayIcon.Dispose();
        _icon?.Dispose();
        _icon = null;
    }
}
