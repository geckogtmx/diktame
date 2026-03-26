using System.Runtime.InteropServices.WindowsRuntime;
using DiktaMe.Core.Vision.Annotations;
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
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace DiktaMe.App.Views;

/// <summary>
/// Screenshot annotation editor. Displays captured image with a drawing canvas overlay.
/// Supports arrow, rectangle, ellipse, freehand, text, step, highlight, and blur tools.
/// Returns flattened annotated image bytes + annotation context for AI prompts.
/// </summary>
public sealed partial class AnnotationWindow : Window
{
    private readonly TaskCompletionSource<AnnotationResult?> _tcs = new();
    private readonly List<IAnnotation> _annotations = [];
    private readonly List<UIElement> _canvasShapes = [];
    private readonly Stack<(IAnnotation Annotation, UIElement Shape)> _redoStack = new();
    private byte[]? _originalPngData;

    // Drawing state
    private AnnotationType _currentTool = AnnotationType.Arrow;
    private Color _currentColor = Colors.Red;
    private bool _isDrawing;
    private Point _dragStart;

    // Preview shape (shown during drag)
    private UIElement? _previewShape;

    // Freehand accumulator
    private readonly List<Point> _freehandPoints = [];

    // Step counter
    private int _nextStepNumber = 1;

    public AnnotationWindow()
    {
        InitializeComponent();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = true;
            presenter.IsResizable = true;
        }

        // Default size — will be adjusted to image aspect ratio
        AppWindow.Resize(new SizeInt32(1200, 800));

        Content.KeyDown += OnKeyDown;
        UpdateToolHighlight();
    }

    /// <summary>Set the background screenshot. Call before Activate().</summary>
    public async Task SetImageAsync(byte[] pngData)
    {
        _originalPngData = pngData;

        var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngData.AsBuffer()).AsTask().ConfigureAwait(false);
        stream.Seek(0);

        var tcs = new TaskCompletionSource();
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.SetSource(stream);
                BackgroundImage.Source = bitmap;
                stream.Dispose();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                stream.Dispose();
                Log.Error(ex, "AnnotationWindow: failed to set background");
                tcs.SetException(ex);
            }
        });
        await tcs.Task.ConfigureAwait(false);
    }

    public Task<AnnotationResult?> GetResultAsync() => _tcs.Task;

    // ── Pointer handlers ─────────────────────────────────────────────

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(DrawingCanvas);
        _dragStart = point.Position;
        _isDrawing = true;
        DrawingCanvas.CapturePointer(e.Pointer);

        if (_currentTool == AnnotationType.Freehand)
        {
            _freehandPoints.Clear();
            _freehandPoints.Add(_dragStart);
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(_currentColor),
                StrokeThickness = WidthSlider.Value,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            polyline.Points.Add(_dragStart);
            _previewShape = polyline;
            DrawingCanvas.Children.Add(polyline);
        }
        else if (_currentTool == AnnotationType.Step)
        {
            // Steps are single-click — place immediately
            PlaceStep(_dragStart);
            _isDrawing = false;
            e.Handled = true;
        }
        else if (_currentTool == AnnotationType.Text)
        {
            PlaceTextInput(_dragStart);
            _isDrawing = false;
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing) { return; }

        var current = e.GetCurrentPoint(DrawingCanvas).Position;

        if (_currentTool == AnnotationType.Freehand && _previewShape is Polyline polyline)
        {
            _freehandPoints.Add(current);
            polyline.Points.Add(current);
        }
        else if (_currentTool is AnnotationType.Arrow)
        {
            UpdateArrowPreview(_dragStart, current);
        }
        else
        {
            UpdateShapePreview(_dragStart, current);
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing) { return; }
        _isDrawing = false;
        DrawingCanvas.ReleasePointerCapture(e.Pointer);

        var end = e.GetCurrentPoint(DrawingCanvas).Position;

        // Remove preview
        if (_previewShape != null)
        {
            DrawingCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }

        // Minimum drag distance to avoid accidental clicks
        double dx = Math.Abs(end.X - _dragStart.X);
        double dy = Math.Abs(end.Y - _dragStart.Y);
        if (_currentTool != AnnotationType.Freehand && dx < 5 && dy < 5) { return; }

        // Create finalized annotation
        IAnnotation? annotation = _currentTool switch
        {
            AnnotationType.Arrow => CreateArrowAnnotation(_dragStart, end),
            AnnotationType.Rectangle => CreateRectAnnotation(_dragStart, end, filled: false),
            AnnotationType.Ellipse => CreateEllipseAnnotation(_dragStart, end),
            AnnotationType.Freehand => CreateFreehandAnnotation(),
            AnnotationType.Highlight => CreateRectAnnotation(_dragStart, end, filled: true),
            AnnotationType.Blur => CreateBlurAnnotation(_dragStart, end),
            _ => null,
        };

        if (annotation != null)
        {
            var shape = RenderAnnotation(annotation);
            AddAnnotation(annotation, shape);
        }
    }

    // ── Annotation creation ──────────────────────────────────────────

    private ArrowAnnotation CreateArrowAnnotation(Point start, Point end)
        => new(start.X, start.Y, end.X, end.Y,
            _currentColor.R, _currentColor.G, _currentColor.B, WidthSlider.Value);

    private IAnnotation CreateRectAnnotation(Point start, Point end, bool filled)
    {
        var (x, y, w, h) = NormalizeBounds(start, end);
        if (filled)
        {
            return new HighlightAnnotation(x, y, w, h,
                _currentColor.R, _currentColor.G, _currentColor.B);
        }

        return new RectAnnotation(x, y, w, h,
            _currentColor.R, _currentColor.G, _currentColor.B, WidthSlider.Value, false);
    }

    private EllipseAnnotation CreateEllipseAnnotation(Point start, Point end)
    {
        var (x, y, w, h) = NormalizeBounds(start, end);
        return new EllipseAnnotation(x + w / 2, y + h / 2, w / 2, h / 2,
            _currentColor.R, _currentColor.G, _currentColor.B, WidthSlider.Value);
    }

    private FreehandAnnotation? CreateFreehandAnnotation()
    {
        if (_freehandPoints.Count < 2) { return null; }
        var points = _freehandPoints.Select(p => (p.X, p.Y)).ToList();
        return new FreehandAnnotation(points,
            _currentColor.R, _currentColor.G, _currentColor.B, WidthSlider.Value);
    }

    private BlurAnnotation CreateBlurAnnotation(Point start, Point end)
    {
        var (x, y, w, h) = NormalizeBounds(start, end);
        return new BlurAnnotation(x, y, w, h);
    }

    // ── Render annotation to XAML shape ──────────────────────────────

    private UIElement RenderAnnotation(IAnnotation annotation)
    {
        var color = Color.FromArgb(255, annotation.ColorR, annotation.ColorG, annotation.ColorB);
        var brush = new SolidColorBrush(color);

        return annotation switch
        {
            ArrowAnnotation a => RenderArrow(a, brush),
            RectAnnotation r => RenderRect(r, brush),
            EllipseAnnotation el => RenderEllipse(el, brush),
            FreehandAnnotation f => RenderFreehand(f, brush),
            TextAnnotation t => RenderText(t, brush),
            StepAnnotation s => RenderStep(s, brush),
            HighlightAnnotation h => RenderHighlight(h, color),
            BlurAnnotation b => RenderBlur(b),
            _ => new Border(),
        };
    }

    private UIElement RenderArrow(ArrowAnnotation a, SolidColorBrush brush)
    {
        var canvas = new Canvas();

        // Shaft
        var line = new Line
        {
            X1 = a.StartX, Y1 = a.StartY,
            X2 = a.EndX, Y2 = a.EndY,
            Stroke = brush, StrokeThickness = a.StrokeWidth,
            StrokeStartLineCap = PenLineCap.Round,
        };
        canvas.Children.Add(line);

        // Arrowhead
        double angle = Math.Atan2(a.EndY - a.StartY, a.EndX - a.StartX);
        double headLen = Math.Max(12, a.StrokeWidth * 4);
        double spread = Math.PI / 7;

        var p1 = new Point(
            a.EndX - headLen * Math.Cos(angle - spread),
            a.EndY - headLen * Math.Sin(angle - spread));
        var p2 = new Point(
            a.EndX - headLen * Math.Cos(angle + spread),
            a.EndY - headLen * Math.Sin(angle + spread));

        var head = new Polygon
        {
            Fill = brush,
            Points = { new Point(a.EndX, a.EndY), p1, p2 },
        };
        canvas.Children.Add(head);

        return canvas;
    }

    private UIElement RenderRect(RectAnnotation r, SolidColorBrush brush)
    {
        var rect = new Rectangle
        {
            Width = r.Width, Height = r.Height,
            Stroke = brush, StrokeThickness = r.StrokeWidth,
        };
        Canvas.SetLeft(rect, r.X);
        Canvas.SetTop(rect, r.Y);
        return rect;
    }

    private UIElement RenderEllipse(EllipseAnnotation el, SolidColorBrush brush)
    {
        var ellipse = new Ellipse
        {
            Width = el.RadiusX * 2, Height = el.RadiusY * 2,
            Stroke = brush, StrokeThickness = el.StrokeWidth,
        };
        Canvas.SetLeft(ellipse, el.CenterX - el.RadiusX);
        Canvas.SetTop(ellipse, el.CenterY - el.RadiusY);
        return ellipse;
    }

    private static UIElement RenderFreehand(FreehandAnnotation f, SolidColorBrush brush)
    {
        var polyline = new Polyline
        {
            Stroke = brush, StrokeThickness = f.StrokeWidth,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        foreach (var (x, y) in f.Points)
        {
            polyline.Points.Add(new Point(x, y));
        }

        return polyline;
    }

    private static UIElement RenderText(TextAnnotation t, SolidColorBrush brush)
    {
        var text = new TextBlock
        {
            Text = t.Content,
            FontSize = t.FontSize,
            Foreground = brush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        Canvas.SetLeft(text, t.X);
        Canvas.SetTop(text, t.Y);
        return text;
    }

    private static UIElement RenderStep(StepAnnotation s, SolidColorBrush brush)
    {
        var grid = new Grid { Width = 28, Height = 28 };
        grid.Children.Add(new Ellipse
        {
            Fill = brush,
            Width = 28, Height = 28,
        });
        grid.Children.Add(new TextBlock
        {
            Text = s.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Canvas.SetLeft(grid, s.X - 14);
        Canvas.SetTop(grid, s.Y - 14);
        return grid;
    }

    private static UIElement RenderHighlight(HighlightAnnotation h, Color color)
    {
        var rect = new Rectangle
        {
            Width = h.Width, Height = h.Height,
            Fill = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)),
        };
        Canvas.SetLeft(rect, h.X);
        Canvas.SetTop(rect, h.Y);
        return rect;
    }

    private static UIElement RenderBlur(BlurAnnotation b)
    {
        // Visual placeholder — checkerboard pattern suggests redaction
        var rect = new Rectangle
        {
            Width = b.Width, Height = b.Height,
            Fill = new SolidColorBrush(Color.FromArgb(200, 60, 60, 60)),
            Stroke = new SolidColorBrush(Colors.Gray),
            StrokeThickness = 1,
            StrokeDashArray = { 4, 2 },
        };
        Canvas.SetLeft(rect, b.X);
        Canvas.SetTop(rect, b.Y);
        return rect;
    }

    // ── Preview shapes (during drag) ─────────────────────────────────

    private void UpdateArrowPreview(Point start, Point end)
    {
        if (_previewShape != null) { DrawingCanvas.Children.Remove(_previewShape); }

        var line = new Line
        {
            X1 = start.X, Y1 = start.Y,
            X2 = end.X, Y2 = end.Y,
            Stroke = new SolidColorBrush(_currentColor),
            StrokeThickness = WidthSlider.Value,
            Opacity = 0.6,
            StrokeDashArray = { 4, 2 },
        };
        _previewShape = line;
        DrawingCanvas.Children.Add(line);
    }

    private void UpdateShapePreview(Point start, Point end)
    {
        if (_previewShape != null) { DrawingCanvas.Children.Remove(_previewShape); }

        var (x, y, w, h) = NormalizeBounds(start, end);
        var brush = new SolidColorBrush(_currentColor);

        UIElement shape;
        if (_currentTool == AnnotationType.Ellipse)
        {
            shape = new Ellipse
            {
                Width = w, Height = h,
                Stroke = brush, StrokeThickness = WidthSlider.Value,
                Opacity = 0.6, StrokeDashArray = { 4, 2 },
            };
        }
        else if (_currentTool == AnnotationType.Highlight)
        {
            shape = new Rectangle
            {
                Width = w, Height = h,
                Fill = new SolidColorBrush(Color.FromArgb(40, _currentColor.R, _currentColor.G, _currentColor.B)),
            };
        }
        else if (_currentTool == AnnotationType.Blur)
        {
            shape = new Rectangle
            {
                Width = w, Height = h,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1, StrokeDashArray = { 4, 2 },
                Opacity = 0.6,
            };
        }
        else
        {
            shape = new Rectangle
            {
                Width = w, Height = h,
                Stroke = brush, StrokeThickness = WidthSlider.Value,
                Opacity = 0.6, StrokeDashArray = { 4, 2 },
            };
        }

        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        _previewShape = shape;
        DrawingCanvas.Children.Add(shape);
    }

    // ── Text + Step placement ────────────────────────────────────────

    private void PlaceTextInput(Point position)
    {
        var textBox = new TextBox
        {
            Width = 200,
            FontSize = 16,
            PlaceholderText = "Type text...",
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 46)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(_currentColor),
        };
        Canvas.SetLeft(textBox, position.X);
        Canvas.SetTop(textBox, position.Y);
        DrawingCanvas.Children.Add(textBox);

        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                DrawingCanvas.Children.Remove(textBox);
                var annotation = new TextAnnotation(position.X, position.Y, textBox.Text,
                    _currentColor.R, _currentColor.G, _currentColor.B, 16);
                var shape = RenderAnnotation(annotation);
                AddAnnotation(annotation, shape);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                DrawingCanvas.Children.Remove(textBox);
                e.Handled = true;
            }
        };

        textBox.Focus(FocusState.Programmatic);
    }

    private void PlaceStep(Point position)
    {
        var annotation = new StepAnnotation(position.X, position.Y, _nextStepNumber++,
            _currentColor.R, _currentColor.G, _currentColor.B);
        var shape = RenderAnnotation(annotation);
        AddAnnotation(annotation, shape);
    }

    // ── Annotation management ────────────────────────────────────────

    private void AddAnnotation(IAnnotation annotation, UIElement shape)
    {
        _annotations.Add(annotation);
        _canvasShapes.Add(shape);
        DrawingCanvas.Children.Add(shape);
        _redoStack.Clear();
        UpdateCountText();
        Log.Debug("Annotation: added {Type}, total={Count}", annotation.Type, _annotations.Count);
    }

    private void UpdateCountText()
    {
        CountText.Text = _annotations.Count > 0
            ? $"{_annotations.Count} annotation{(_annotations.Count == 1 ? "" : "s")}"
            : "";
    }

    // ── Toolbar handlers ─────────────────────────────────────────────

    private void OnToolSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag &&
            Enum.TryParse<AnnotationType>(tag, out var tool))
        {
            _currentTool = tool;
            UpdateToolHighlight();
        }
    }

    private void UpdateToolHighlight()
    {
        var buttons = new (Button Btn, AnnotationType Tool)[]
        {
            (ArrowBtn, AnnotationType.Arrow), (RectBtn, AnnotationType.Rectangle),
            (EllipseBtn, AnnotationType.Ellipse), (FreehandBtn, AnnotationType.Freehand),
            (TextBtn, AnnotationType.Text), (StepBtn, AnnotationType.Step),
            (HighlightBtn, AnnotationType.Highlight), (BlurBtn, AnnotationType.Blur),
        };

        foreach (var (btn, tool) in buttons)
        {
            btn.Opacity = tool == _currentTool ? 1.0 : 0.5;
        }
    }

    private void OnColorSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            byte r = Convert.ToByte(hex.Substring(1, 2), 16);
            byte g = Convert.ToByte(hex.Substring(3, 2), 16);
            byte b = Convert.ToByte(hex.Substring(5, 2), 16);
            _currentColor = Color.FromArgb(255, r, g, b);
        }
    }

    private void OnUndo(object sender, RoutedEventArgs e) => Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => Redo();

    private void Undo()
    {
        if (_annotations.Count == 0) { return; }
        int last = _annotations.Count - 1;
        var annotation = _annotations[last];
        var shape = _canvasShapes[last];
        _annotations.RemoveAt(last);
        _canvasShapes.RemoveAt(last);
        DrawingCanvas.Children.Remove(shape);
        _redoStack.Push((annotation, shape));
        UpdateCountText();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) { return; }
        var (annotation, shape) = _redoStack.Pop();
        _annotations.Add(annotation);
        _canvasShapes.Add(shape);
        DrawingCanvas.Children.Add(shape);
        UpdateCountText();
    }

    // ── Keyboard shortcuts ───────────────────────────────────────────

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrl && e.Key == Windows.System.VirtualKey.Z) { Undo(); e.Handled = true; }
        else if (ctrl && e.Key == Windows.System.VirtualKey.Y) { Redo(); e.Handled = true; }
        else if (e.Key == Windows.System.VirtualKey.Escape) { OnCancel(null!, null!); e.Handled = true; }
        else if (e.Key == Windows.System.VirtualKey.Number1) { _currentTool = AnnotationType.Arrow; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number2) { _currentTool = AnnotationType.Rectangle; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number3) { _currentTool = AnnotationType.Ellipse; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number4) { _currentTool = AnnotationType.Freehand; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number5) { _currentTool = AnnotationType.Text; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number6) { _currentTool = AnnotationType.Step; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number7) { _currentTool = AnnotationType.Highlight; UpdateToolHighlight(); }
        else if (e.Key == Windows.System.VirtualKey.Number8) { _currentTool = AnnotationType.Blur; UpdateToolHighlight(); }
    }

    // ── Done / Cancel ────────────────────────────────────────────────

    private async void OnDone(object sender, RoutedEventArgs e)
    {
        try
        {
            byte[]? flattenedImage = null;
            if (_annotations.Count > 0 && _originalPngData != null)
            {
                flattenedImage = await FlattenAsync().ConfigureAwait(false);
            }

            string context = AnnotationContext.BuildPromptContext(_annotations);

            DispatcherQueue.TryEnqueue(() =>
            {
                _tcs.TrySetResult(new AnnotationResult(
                    flattenedImage ?? _originalPngData!,
                    context,
                    _annotations.ToList()));
                Close();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AnnotationWindow: flatten failed");
            DispatcherQueue.TryEnqueue(() =>
            {
                _tcs.TrySetResult(new AnnotationResult(_originalPngData!, "", []));
                Close();
            });
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(null);
        Close();
    }

    // ── Flatten: render canvas to bitmap ─────────────────────────────

    private async Task<byte[]> FlattenAsync()
    {
        var tcs = new TaskCompletionSource<byte[]>();
        DispatcherQueue.TryEnqueue(() => _ = FlattenCoreAsync(tcs));
        return await tcs.Task.ConfigureAwait(false);
    }

    private async Task FlattenCoreAsync(TaskCompletionSource<byte[]> tcs)
    {
            try
            {
                // Capture the drawing canvas as a bitmap
                var rtb = new RenderTargetBitmap();
                await rtb.RenderAsync(DrawingCanvas);
                var overlayPixels = (await rtb.GetPixelsAsync()).ToArray();
                int overlayW = rtb.PixelWidth;
                int overlayH = rtb.PixelHeight;

                // Decode original image
                using var origStream = new InMemoryRandomAccessStream();
                await origStream.WriteAsync(_originalPngData.AsBuffer());
                origStream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(origStream);
                var origProvider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                byte[] origPixels = origProvider.DetachPixelData();
                int origW = (int)decoder.PixelWidth;
                int origH = (int)decoder.PixelHeight;

                // Scale overlay to original image dimensions and composite
                int outW = origW;
                int outH = origH;
                byte[] result = new byte[outW * outH * 4];
                Array.Copy(origPixels, result, Math.Min(origPixels.Length, result.Length));

                // Alpha-blend overlay onto original
                double scaleX = (double)outW / overlayW;
                double scaleY = (double)outH / overlayH;

                for (int oy = 0; oy < overlayH; oy++)
                {
                    int destY = (int)(oy * scaleY);
                    if (destY >= outH) { break; }

                    for (int ox = 0; ox < overlayW; ox++)
                    {
                        int destX = (int)(ox * scaleX);
                        if (destX >= outW) { break; }

                        int srcIdx = (oy * overlayW + ox) * 4;
                        int dstIdx = (destY * outW + destX) * 4;

                        if (srcIdx + 3 >= overlayPixels.Length || dstIdx + 3 >= result.Length) { continue; }

                        byte sB = overlayPixels[srcIdx];
                        byte sG = overlayPixels[srcIdx + 1];
                        byte sR = overlayPixels[srcIdx + 2];
                        byte sA = overlayPixels[srcIdx + 3];

                        if (sA == 0) { continue; }

                        if (sA == 255)
                        {
                            result[dstIdx] = sB;
                            result[dstIdx + 1] = sG;
                            result[dstIdx + 2] = sR;
                            result[dstIdx + 3] = 255;
                        }
                        else
                        {
                            // Alpha blend
                            float a = sA / 255f;
                            result[dstIdx] = (byte)(sB * a + result[dstIdx] * (1 - a));
                            result[dstIdx + 1] = (byte)(sG * a + result[dstIdx + 1] * (1 - a));
                            result[dstIdx + 2] = (byte)(sR * a + result[dstIdx + 2] * (1 - a));
                            result[dstIdx + 3] = 255;
                        }
                    }
                }

                // Encode to PNG
                using var outStream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    (uint)outW, (uint)outH, 96, 96, result);
                await encoder.FlushAsync();

                outStream.Seek(0);
                byte[] pngBytes = new byte[outStream.Size];
                await outStream.ReadAsync(pngBytes.AsBuffer(), (uint)pngBytes.Length, InputStreamOptions.None);

                Log.Information("AnnotationWindow: flattened {Count} annotations onto {W}x{H} image ({Size}KB)",
                    _annotations.Count, outW, outH, pngBytes.Length / 1024);

                tcs.SetResult(pngBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AnnotationWindow: flatten error");
                tcs.SetException(ex);
            }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static (double X, double Y, double Width, double Height) NormalizeBounds(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(b.X - a.X);
        double h = Math.Abs(b.Y - a.Y);
        return (x, y, w, h);
    }
}

/// <summary>Result from annotation editor.</summary>
public sealed record AnnotationResult(
    byte[] ImageData,
    string AnnotationContext,
    IReadOnlyList<IAnnotation> Annotations);
