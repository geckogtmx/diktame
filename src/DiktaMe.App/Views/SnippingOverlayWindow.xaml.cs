using DiktaMe.Core.Vision;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Serilog;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace DiktaMe.App.Views;

/// <summary>
/// Transparent fullscreen overlay for Vision screenshot region selection.
/// Click = capture active window, Drag = capture region, Esc = cancel.
/// </summary>
public sealed partial class SnippingOverlayWindow : Window
{
    private readonly TaskCompletionSource<SnippingResult?> _tcs = new();
    private Point _dragStart;
    private bool _isDragging;
    private Rectangle? _selectionRect;

    // Dark overlay rectangles (4 around the selection cutout)
    private readonly Rectangle _overlayTop = CreateOverlayRect();
    private readonly Rectangle _overlayBottom = CreateOverlayRect();
    private readonly Rectangle _overlayLeft = CreateOverlayRect();
    private readonly Rectangle _overlayRight = CreateOverlayRect();
    private readonly Rectangle _overlayFull;

    public SnippingOverlayWindow()
    {
        InitializeComponent();

        // Make window fullscreen, always on top, no title bar
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        }

        // Cover entire primary display
        var display = DisplayArea.Primary;
        AppWindow.MoveAndResize(new RectInt32(
            display.WorkArea.X, display.WorkArea.Y,
            display.OuterBounds.Width, display.OuterBounds.Height));

        // Full dark overlay (shown initially, hidden during drag)
        _overlayFull = CreateOverlayRect();
        OverlayCanvas.Children.Add(_overlayFull);

        // Selection rectangle border
        _selectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            StrokeDashArray = [5, 3],
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Add(_selectionRect);

        // Add the 4 partial overlay rects (hidden until drag starts)
        OverlayCanvas.Children.Add(_overlayTop);
        OverlayCanvas.Children.Add(_overlayBottom);
        OverlayCanvas.Children.Add(_overlayLeft);
        OverlayCanvas.Children.Add(_overlayRight);
        _overlayTop.Visibility = Visibility.Collapsed;
        _overlayBottom.Visibility = Visibility.Collapsed;
        _overlayLeft.Visibility = Visibility.Collapsed;
        _overlayRight.Visibility = Visibility.Collapsed;

        // Set crosshair cursor
        OverlayCanvas.PointerPressed += OnPointerPressed;
        OverlayCanvas.PointerMoved += OnPointerMoved;
        OverlayCanvas.PointerReleased += OnPointerReleased;

        // Esc to cancel
        Content.KeyDown += OnKeyDown;
        Content.IsTabStop = true;

        // Size overlay to window on load
        OverlayCanvas.SizeChanged += (_, _) => ResizeOverlayFull();
    }

    public Task<SnippingResult?> GetResultAsync() => _tcs.Task;

    private void ResizeOverlayFull()
    {
        _overlayFull.Width = OverlayCanvas.ActualWidth;
        _overlayFull.Height = OverlayCanvas.ActualHeight;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Log.Debug("SnippingOverlay: cancelled by Esc");
            _tcs.TrySetResult(null);
            Close();
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(OverlayCanvas);
        _dragStart = point.Position;
        _isDragging = true;
        OverlayCanvas.CapturePointer(e.Pointer);

        // Switch from full overlay to partial overlays (with cutout)
        _overlayFull.Visibility = Visibility.Collapsed;
        _overlayTop.Visibility = Visibility.Visible;
        _overlayBottom.Visibility = Visibility.Visible;
        _overlayLeft.Visibility = Visibility.Visible;
        _overlayRight.Visibility = Visibility.Visible;

        if (_selectionRect is not null)
        {
            _selectionRect.Visibility = Visibility.Visible;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _selectionRect is null) return;

        var point = e.GetCurrentPoint(OverlayCanvas);
        var current = point.Position;

        double x = Math.Min(_dragStart.X, current.X);
        double y = Math.Min(_dragStart.Y, current.Y);
        double w = Math.Abs(current.X - _dragStart.X);
        double h = Math.Abs(current.Y - _dragStart.Y);

        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;

        // Update the 4 overlay rects to create the cutout effect
        UpdateOverlayRects(x, y, w, h);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        OverlayCanvas.ReleasePointerCapture(e.Pointer);

        var point = e.GetCurrentPoint(OverlayCanvas);
        var current = point.Position;

        double w = Math.Abs(current.X - _dragStart.X);
        double h = Math.Abs(current.Y - _dragStart.Y);

        if (w < 10 && h < 10)
        {
            // Click (no significant drag) = capture active window
            Log.Debug("SnippingOverlay: click — capturing active window");
            _tcs.TrySetResult(new SnippingResult(CaptureMode.ActiveWindow, null));
        }
        else
        {
            // Drag = capture region
            int x = (int)Math.Min(_dragStart.X, current.X);
            int y = (int)Math.Min(_dragStart.Y, current.Y);
            Log.Debug("SnippingOverlay: region selected ({X},{Y} {W}x{H})", x, y, (int)w, (int)h);
            _tcs.TrySetResult(new SnippingResult(
                CaptureMode.Region,
                new Windows.Graphics.RectInt32(x, y, (int)w, (int)h)));
        }

        Close();
    }

    private void UpdateOverlayRects(double selX, double selY, double selW, double selH)
    {
        double canvasW = OverlayCanvas.ActualWidth;
        double canvasH = OverlayCanvas.ActualHeight;

        // Top: full width, from 0 to selection top
        Canvas.SetLeft(_overlayTop, 0);
        Canvas.SetTop(_overlayTop, 0);
        _overlayTop.Width = canvasW;
        _overlayTop.Height = Math.Max(0, selY);

        // Bottom: full width, from selection bottom to canvas bottom
        Canvas.SetLeft(_overlayBottom, 0);
        Canvas.SetTop(_overlayBottom, selY + selH);
        _overlayBottom.Width = canvasW;
        _overlayBottom.Height = Math.Max(0, canvasH - selY - selH);

        // Left: selection height, from 0 to selection left
        Canvas.SetLeft(_overlayLeft, 0);
        Canvas.SetTop(_overlayLeft, selY);
        _overlayLeft.Width = Math.Max(0, selX);
        _overlayLeft.Height = selH;

        // Right: selection height, from selection right to canvas right
        Canvas.SetLeft(_overlayRight, selX + selW);
        Canvas.SetTop(_overlayRight, selY);
        _overlayRight.Width = Math.Max(0, canvasW - selX - selW);
        _overlayRight.Height = selH;
    }

    private static Rectangle CreateOverlayRect() => new()
    {
        Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x99, 0x00, 0x00, 0x00)),
    };
}

/// <summary>
/// Result from the snipping overlay: capture mode + optional region bounds.
/// </summary>
public sealed record SnippingResult(CaptureMode Mode, RectInt32? Region);
