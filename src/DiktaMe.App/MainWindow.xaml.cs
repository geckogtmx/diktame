using Microsoft.UI.Xaml;

namespace DiktaMe.App;

/// <summary>
/// The main application window. Will host the Control Panel view.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Set minimum window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(480, 320));
        appWindow.Title = "dIKta.me";
    }
}
