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
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
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

        // Find the header bar in the ControlPanelPage and set it as the title bar drag region
        var page = FindDescendant<Views.ControlPanelPage>(Content as DependencyObject);
        var headerBar = page?.FindName("HeaderBar") as UIElement;
        if (headerBar is not null)
        {
            SetTitleBar(headerBar);
        }
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
