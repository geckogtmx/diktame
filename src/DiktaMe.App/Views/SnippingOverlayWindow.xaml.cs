using System.Runtime.InteropServices.WindowsRuntime;
using DiktaMe.Core.Vision;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Serilog;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Streams;

namespace DiktaMe.App.Views;

/// <summary>
/// Canvas subclass that exposes ProtectedCursor for crosshair cursor support.
/// WinUI 3's ProtectedCursor is protected, requiring a subclass to set it.
/// </summary>
public sealed class CrosshairCanvas : Canvas
{
    public CrosshairCanvas()
    {
        Loaded += (_, _) =>
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
        };
    }
}

/// <summary>
/// Fullscreen overlay for Vision screenshot region selection.
/// Captures a screenshot first, displays it as background with a dim overlay,
/// then lets the user drag to select a region. This avoids WinUI 3's lack
/// of true window transparency.
/// </summary>
public sealed partial class SnippingOverlayWindow : Window
{
    private readonly TaskCompletionSource<SnippingResult?> _tcs = new();
    private Point _dragStart;
    private bool _isDragging;
    private bool _isFreeform;
    private bool _isDimOnly; // When true, shows dim without selection capability
    private readonly List<Point> _freeformPoints = [];
    private Polyline? _freeformLine;
    private Rectangle? _selectionRect;

    // Dark overlay rectangles (4 around the selection cutout)
    private readonly Rectangle _overlayTop = CreateDimRect();
    private readonly Rectangle _overlayBottom = CreateDimRect();
    private readonly Rectangle _overlayLeft = CreateDimRect();
    private readonly Rectangle _overlayRight = CreateDimRect();
    private Rectangle? _overlayFull;

    public SnippingOverlayWindow()
    {
        InitializeComponent();

        // Make window fullscreen, always on top, no title bar
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        }

        // Default: cover primary display. Call SetBounds() before Activate() to override.
        var display = DisplayArea.Primary;
        AppWindow.MoveAndResize(new RectInt32(
            display.OuterBounds.X, display.OuterBounds.Y,
            display.OuterBounds.Width, display.OuterBounds.Height));

        // Full semi-transparent dim overlay (shown initially, replaced by 4-rect cutout on drag)
        _overlayFull = CreateDimRect();
        OverlayCanvas.Children.Add(_overlayFull);

        // Dashed selection border
        _selectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            StrokeDashArray = [5, 3],
            Fill = new SolidColorBrush(Colors.Transparent),
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Add(_selectionRect);

        // Freeform selection polyline
        _freeformLine = new Polyline
        {
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            StrokeDashArray = [5, 3],
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Add(_freeformLine);

        // 4 partial dim rects for cutout effect (hidden until drag)
        OverlayCanvas.Children.Add(_overlayTop);
        OverlayCanvas.Children.Add(_overlayBottom);
        OverlayCanvas.Children.Add(_overlayLeft);
        OverlayCanvas.Children.Add(_overlayRight);
        _overlayTop.Visibility = Visibility.Collapsed;
        _overlayBottom.Visibility = Visibility.Collapsed;
        _overlayLeft.Visibility = Visibility.Collapsed;
        _overlayRight.Visibility = Visibility.Collapsed;

        OverlayCanvas.PointerPressed += OnPointerPressed;
        OverlayCanvas.PointerMoved += OnPointerMoved;
        OverlayCanvas.PointerReleased += OnPointerReleased;

        // Crosshair cursor set via CrosshairCanvas.ProtectedCursor (WinUI 3 native)

        // ESC handling: hook KeyDown on both Content and Canvas, and force focus on activation.
        // Without focus, KeyDown never fires — which makes ESC feel unresponsive.
        Content.KeyDown += OnKeyDown;
        Content.IsTabStop = true;
        OverlayCanvas.IsTabStop = true;
        OverlayCanvas.KeyDown += OnKeyDown;

        // Force keyboard focus when the window activates
        Activated += (_, _) =>
        {
            OverlayCanvas.Focus(FocusState.Programmatic);
        };

        OverlayCanvas.SizeChanged += (_, _) => ResizeOverlayFull();
    }

    /// <summary>
    /// Positions the overlay at the given screen coordinates. Call before Activate().
    /// </summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>
    /// Sets the background screenshot image. Call before Activate().
    /// </summary>
    public async Task SetBackgroundScreenshotAsync(byte[] pngData)
    {
        // Prepare the stream on the current thread
        var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngData.AsBuffer()).AsTask().ConfigureAwait(false);
        stream.Seek(0);

        // Load the bitmap on the UI thread (BitmapImage requires UI thread)
        var tcs = new TaskCompletionSource();
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.SetSource(stream);
                ScreenshotImage.Source = bitmap;
                stream.Dispose();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                stream.Dispose();
                Log.Error(ex, "SnippingOverlay: failed to set background screenshot ({Size} bytes)", pngData.Length);
                tcs.SetException(ex);
            }
        });
        await tcs.Task.ConfigureAwait(false);
    }

    public Task<SnippingResult?> GetResultAsync() => _tcs.Task;

    /// <summary>Put overlay in dim-only mode (no selection, just frozen dimmed screenshot). Keeps keyboard (ESC) active.</summary>
    public void SetDimOnlyMode()
    {
        _isDimOnly = true;
        // Pointer handlers already guard with `if (_isDimOnly) return;`
        // Keep IsHitTestVisible=true so keyboard (ESC) still works
    }

    /// <summary>Switch from dim-only to selection mode (re-enable pointer events).</summary>
    public void EnableSelection()
    {
        _isDimOnly = false;
    }

    /// <summary>Close the overlay without producing a result.</summary>
    public void DismissDim()
    {
        _tcs.TrySetResult(null);
        Close();
    }

    private void ResizeOverlayFull()
    {
        if (_overlayFull is not null)
        {
            _overlayFull.Width = OverlayCanvas.ActualWidth;
            _overlayFull.Height = OverlayCanvas.ActualHeight;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Log.Debug("SnippingOverlay: cancelled by Esc");
            _tcs.TrySetResult(null);
            Close();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.F)
        {
            // F = capture active monitor (full screen of current display)
            Log.Debug("SnippingOverlay: Full Screen (F key)");
            _tcs.TrySetResult(new SnippingResult(CaptureMode.FullScreen, null));
            Close();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.A)
        {
            // A = capture all monitors (virtual screen)
            Log.Debug("SnippingOverlay: All Monitors (A key)");
            _tcs.TrySetResult(new SnippingResult(CaptureMode.AllMonitors, null));
            Close();
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isDimOnly) return;
        var point = e.GetCurrentPoint(OverlayCanvas);
        _dragStart = point.Position;
        _isDragging = true;
        _isFreeform = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        OverlayCanvas.CapturePointer(e.Pointer);

        // Switch from full dim to 4-rect cutout (or freeform line)
        if (_overlayFull is not null)
        {
            _overlayFull.Visibility = Visibility.Collapsed;
        }

        if (_isFreeform)
        {
            // Freeform: start collecting points
            _freeformPoints.Clear();
            _freeformPoints.Add(point.Position);
            if (_freeformLine is not null)
            {
                _freeformLine.Points.Clear();
                _freeformLine.Points.Add(point.Position);
                _freeformLine.Visibility = Visibility.Visible;
            }

            // Show full dim with freeform line on top
            _overlayTop.Visibility = Visibility.Visible;
            _overlayBottom.Visibility = Visibility.Collapsed;
            _overlayLeft.Visibility = Visibility.Collapsed;
            _overlayRight.Visibility = Visibility.Collapsed;
            _overlayTop.Width = OverlayCanvas.ActualWidth;
            _overlayTop.Height = OverlayCanvas.ActualHeight;
            Canvas.SetLeft(_overlayTop, 0);
            Canvas.SetTop(_overlayTop, 0);
        }
        else
        {
            // Rectangle: show 4-rect cutout
            _overlayTop.Visibility = Visibility.Visible;
            _overlayBottom.Visibility = Visibility.Visible;
            _overlayLeft.Visibility = Visibility.Visible;
            _overlayRight.Visibility = Visibility.Visible;
            if (_selectionRect is not null)
            {
                _selectionRect.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var current = e.GetCurrentPoint(OverlayCanvas).Position;

        if (_isFreeform)
        {
            // Freeform: accumulate points, draw polyline
            _freeformPoints.Add(current);
            _freeformLine?.Points.Add(current);
        }
        else
        {
            if (_selectionRect is null)
            {
                return;
            }

            double x = Math.Min(_dragStart.X, current.X);
            double y = Math.Min(_dragStart.Y, current.Y);
            double w = Math.Abs(current.X - _dragStart.X);
            double h = Math.Abs(current.Y - _dragStart.Y);

            Canvas.SetLeft(_selectionRect, x);
            Canvas.SetTop(_selectionRect, y);
            _selectionRect.Width = w;
            _selectionRect.Height = h;

            UpdateCutout(x, y, w, h);
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        OverlayCanvas.ReleasePointerCapture(e.Pointer);

        if (_isFreeform && _freeformPoints.Count > 5)
        {
            // Freeform: compute bounding box of all drawn points
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var pt in _freeformPoints)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            int x = (int)minX;
            int y = (int)minY;
            int w = (int)(maxX - minX);
            int h = (int)(maxY - minY);

            if (w > 10 && h > 10)
            {
                Log.Debug("SnippingOverlay: freeform region ({X},{Y} {W}x{H}) from {Points} points",
                    x, y, w, h, _freeformPoints.Count);
                _tcs.TrySetResult(new SnippingResult(CaptureMode.Region, new RectInt32(x, y, w, h)));
            }
            else
            {
                _tcs.TrySetResult(null); // Too small
            }
        }
        else
        {
            var current = e.GetCurrentPoint(OverlayCanvas).Position;
            double w = Math.Abs(current.X - _dragStart.X);
            double h = Math.Abs(current.Y - _dragStart.Y);

            if (w < 10 && h < 10)
            {
                Log.Debug("SnippingOverlay: click — capturing active window");
                _tcs.TrySetResult(new SnippingResult(CaptureMode.ActiveWindow, null));
            }
            else
            {
                int x = (int)Math.Min(_dragStart.X, current.X);
                int y = (int)Math.Min(_dragStart.Y, current.Y);
                Log.Debug("SnippingOverlay: region selected ({X},{Y} {W}x{H})", x, y, (int)w, (int)h);
                _tcs.TrySetResult(new SnippingResult(CaptureMode.Region, new RectInt32(x, y, (int)w, (int)h)));
            }
        }

        Close();
    }

    private void UpdateCutout(double selX, double selY, double selW, double selH)
    {
        double cw = OverlayCanvas.ActualWidth;
        double ch = OverlayCanvas.ActualHeight;

        Canvas.SetLeft(_overlayTop, 0); Canvas.SetTop(_overlayTop, 0);
        _overlayTop.Width = cw; _overlayTop.Height = Math.Max(0, selY);

        Canvas.SetLeft(_overlayBottom, 0); Canvas.SetTop(_overlayBottom, selY + selH);
        _overlayBottom.Width = cw; _overlayBottom.Height = Math.Max(0, ch - selY - selH);

        Canvas.SetLeft(_overlayLeft, 0); Canvas.SetTop(_overlayLeft, selY);
        _overlayLeft.Width = Math.Max(0, selX); _overlayLeft.Height = selH;

        Canvas.SetLeft(_overlayRight, selX + selW); Canvas.SetTop(_overlayRight, selY);
        _overlayRight.Width = Math.Max(0, cw - selX - selW); _overlayRight.Height = selH;
    }

    private static Rectangle CreateDimRect() => new()
    {
        Fill = new SolidColorBrush(ColorHelper.FromArgb(0x80, 0x00, 0x00, 0x00)),
    };

}

/// <summary>Result from the snipping overlay.</summary>
public sealed record SnippingResult(CaptureMode Mode, RectInt32? Region);
