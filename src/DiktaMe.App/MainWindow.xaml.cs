using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DiktaMe.App;

/// <summary>
/// The main application window. Hosts the Control Panel dashboard.
/// Custom title bar: content extends into the title bar area for a frameless look.
/// The header row (Row 0) acts as the drag region; interactive controls (buttons) are auto-excluded.
/// </summary>
public sealed partial class MainWindow : Window
{
    // Win32 subclassing to intercept title bar double-click
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int GWLP_WNDPROC = -4;
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _newWndProc; // prevent GC collection
    private IntPtr _oldWndProc;

    // Win32 layered window for opacity (auto-hide fade)
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int LWA_ALPHA = 0x2;
    private IntPtr _hWnd;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    public MainWindow()
    {
        this.InitializeComponent();

        var appWindow = this.AppWindow;
        appWindow.Resize(new SizeInt32(420, 274));
        appWindow.Title = "dIKta.me";

        // Set window icon
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }

        // Remove the default Windows title bar — our Row 0 header replaces it.
        // ExtendsContentIntoTitleBar hides the default chrome text/icon.
        ExtendsContentIntoTitleBar = true;

        // Hide the system caption buttons (min/max/close) — we provide our own [X] in the header.
        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);

            // Apply Always On Top from saved settings
            var settings = App.Current.Services.GetRequiredService<DiktaMe.Core.Config.SettingsManager>();
            presenter.IsAlwaysOnTop = settings.Current.ControlPanel.AlwaysOnTop;
        }

        // Defer SetTitleBar until the visual tree is loaded
        if (Content is FrameworkElement root)
        {
            root.Loaded += OnRootLoaded;
        }
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement root)
        {
            root.Loaded -= OnRootLoaded;
        }

        // Find the drag region inside the header bar and set it as the title bar drag surface.
        // We target DragRegion (a transparent Border) instead of the whole HeaderBar grid
        // so that buttons inside the header keep full pointer control (hold-click, etc.).
        var page = FindDescendant<Views.ControlPanelPage>(Content as DependencyObject);
        var dragRegion = page?.FindName("DragRegion") as UIElement;
        if (dragRegion is not null)
        {
            SetTitleBar(dragRegion);
        }

        // Subclass the window to intercept WM_NCLBUTTONDBLCLK (title bar double-click).
        // With IsMaximizable=false the OS still tries to snap on double-click, which
        // jumps the window to a corner. We eat the message and toggle expand instead.
        try
        {
            InstallDoubleClickHook(page);
        }
        catch (EntryPointNotFoundException ex)
        {
            // SetWindowLongPtr may not exist on 32-bit user32.dll — non-critical, skip gracefully
            Serilog.Log.Warning(ex, "MainWindow: double-click hook unavailable — skipped");
        }

        // Enable WS_EX_LAYERED for window-level opacity (auto-hide fade)
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var exStyle = (int)GetWindowLongPtr(_hWnd, GWL_EXSTYLE);
        SetWindowLongPtr(_hWnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_LAYERED));
        SetLayeredWindowAttributes(_hWnd, 0, 255, LWA_ALPHA);
    }

    /// <summary>
    /// Set window opacity (0 = invisible, 255 = fully opaque).
    /// Called by ControlPanelPage auto-hide logic.
    /// </summary>
    public void SetOpacity(byte alpha)
    {
        if (_hWnd != IntPtr.Zero)
        {
            SetLayeredWindowAttributes(_hWnd, 0, alpha, LWA_ALPHA);
        }
    }

    /// <summary>
    /// Restores full visibility after the window is shown from tray.
    /// Resets both Win32 opacity and ControlPanelPage auto-hide/collapse state.
    /// </summary>
    public void RestoreFromTray()
    {
        SetOpacity(255);
        var page = FindDescendant<Views.ControlPanelPage>(Content as DependencyObject);
        page?.ResetAutoHideState();
    }

    private void InstallDoubleClickHook(Views.ControlPanelPage? page)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _newWndProc = (IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam) =>
        {
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                // Swallow the OS double-click and toggle expand/collapse instead
                page?.ViewModel?.ToggleExpandedCommand.Execute(null);
                return IntPtr.Zero;
            }
            return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
        };
        _oldWndProc = SetWindowLongPtr(hWnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private static T? FindDescendant<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
        {
            return null;
        }

        if (parent is T match)
        {
            return match;
        }

        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            var result = FindDescendant<T>(child);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }
}
