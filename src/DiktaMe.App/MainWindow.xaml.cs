using Microsoft.UI.Xaml;

namespace DiktaMe.App;

/// <summary>
/// The main application window. Hosts the Control Panel dashboard.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 480));
        appWindow.Title = "dIKta.me";
    }
}
