using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace DiktaMe.App.Views;

/// <summary>
/// Compact always-on-top floating bar shown during video recording.
/// Shows a red recording indicator, elapsed timer, pause and stop buttons.
/// </summary>
public sealed partial class VideoRecordingBarWindow : Window
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _blinkTimer;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private bool _isPaused;
    private bool _dotVisible = true;

    public VideoRecordingBarWindow()
    {
        InitializeComponent();

        try
        {
            // Configure window: small, always on top, no title bar
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            }

            AppWindow.Resize(new SizeInt32(280, 56));

            // Position at top-center of primary monitor
            var area = DisplayArea.Primary;
            if (area is not null)
            {
                int x = (area.WorkArea.Width - 280) / 2;
                AppWindow.Move(new PointInt32(x, 16));
            }
        }
        catch
        {
            // Non-fatal — window will still work with default position/size
        }

        // Timer for elapsed display
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) =>
        {
            TimerText.Text = _stopwatch.Elapsed.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
        };

        // Blink timer for recording dot (simpler than Storyboard, no native crash risk)
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _blinkTimer.Tick += (_, _) =>
        {
            _dotVisible = !_dotVisible;
            RecDot.Opacity = _dotVisible ? 1.0 : 0.2;
        };

        // Handle window close as stop
        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            DoStop();
        };
    }

    /// <summary>
    /// Starts the recording timer. Returns when the user clicks Stop.
    /// </summary>
    public Task<bool> WaitForStopAsync()
    {
        _stopwatch.Start();
        _timer.Start();
        _blinkTimer.Start();
        return _tcs.Task;
    }

    /// <summary>Total elapsed recording time (excluding paused intervals).</summary>
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _stopwatch.Stop();
            _timer.Stop();
            _blinkTimer.Stop();
            RecDot.Opacity = 0.4;
            PauseButton.Content = new FontIcon { Glyph = "\uE768", FontSize = 14 }; // Play
            ToolTipService.SetToolTip(PauseButton, "Resume");
        }
        else
        {
            _stopwatch.Start();
            _timer.Start();
            _blinkTimer.Start();
            PauseButton.Content = new FontIcon { Glyph = "\uE769", FontSize = 14 }; // Pause
            ToolTipService.SetToolTip(PauseButton, "Pause");
        }
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        DoStop();
    }

    private void DoStop()
    {
        _stopwatch.Stop();
        _timer.Stop();
        _blinkTimer.Stop();
        _tcs.TrySetResult(true);
        Close();
    }
}
