using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Streams;

namespace DiktaMe.App.Views;

/// <summary>
/// Fullscreen overlay for picking a color from the screen.
/// Displays the frozen screenshot, tracks cursor position, shows live hex/RGB preview,
/// and copies the picked color to clipboard on click.
/// </summary>
public sealed partial class ColorPickerOverlayWindow : Window
{
    private readonly TaskCompletionSource<List<ColorPickResult>?> _tcs = new();
    private readonly List<ColorPickResult> _palette = [];
    private byte[]? _pixelData;
    private int _imageWidth;
    private int _imageHeight;

    public ColorPickerOverlayWindow()
    {
        InitializeComponent();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        }

        var display = DisplayArea.Primary;
        AppWindow.MoveAndResize(new RectInt32(
            display.OuterBounds.X, display.OuterBounds.Y,
            display.OuterBounds.Width, display.OuterBounds.Height));

        OverlayCanvas.PointerMoved += OnPointerMoved;
        OverlayCanvas.PointerPressed += OnPointerPressed;

        OverlayCanvas.IsTabStop = true;
        OverlayCanvas.KeyDown += OnKeyDown;
        Content.KeyDown += OnKeyDown;

        Activated += (_, _) => OverlayCanvas.Focus(FocusState.Programmatic);
    }

    public void SetBounds(int x, int y, int width, int height)
    {
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>
    /// Sets the background screenshot and decodes pixel data for color sampling.
    /// Call before Activate().
    /// </summary>
    public async Task SetBackgroundScreenshotAsync(byte[] pngData)
    {
        // Decode PNG to raw BGRA pixel data for sampling
        await DecodePngPixelsAsync(pngData).ConfigureAwait(false);

        // Display as background
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
                ScreenshotImage.Source = bitmap;
                stream.Dispose();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                stream.Dispose();
                Log.Error(ex, "ColorPicker: failed to set background screenshot");
                tcs.SetException(ex);
            }
        });
        await tcs.Task.ConfigureAwait(false);
    }

    public Task<List<ColorPickResult>?> GetResultAsync() => _tcs.Task;

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_palette.Count > 0)
            {
                // Esc with picks = finish with current palette
                Log.Information("ColorPicker: finishing palette with {Count} colors (Esc)", _palette.Count);
                _tcs.TrySetResult(_palette);
            }
            else
            {
                Log.Debug("ColorPicker: cancelled by Esc");
                _tcs.TrySetResult(null);
            }

            Close();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // Enter = finish with current palette
            Log.Information("ColorPicker: finishing palette with {Count} colors (Enter)", _palette.Count);
            _tcs.TrySetResult(_palette.Count > 0 ? _palette : null);
            Close();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Back && _palette.Count > 0)
        {
            // Backspace = undo last pick
            var removed = _palette[^1];
            _palette.RemoveAt(_palette.Count - 1);
            Log.Debug("ColorPicker: undid {Hex}, {Count} remaining", removed.Hex, _palette.Count);
            UpdatePaletteUI();
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(OverlayCanvas).Position;
        UpdateColorPreview(pos);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(OverlayCanvas).Position;
        var (r, g, b) = GetPixelColor(pos);
        string hex = $"#{r:X2}{g:X2}{b:X2}";

        var pick = new ColorPickResult(hex, r, g, b);
        _palette.Add(pick);
        Log.Information("ColorPicker: picked {Hex} at ({X},{Y}) — palette #{N}", hex, (int)pos.X, (int)pos.Y, _palette.Count);
        UpdatePaletteUI();
    }

    private void UpdatePaletteUI()
    {
        PalettePanel.Children.Clear();
        foreach (var pick in _palette)
        {
            var swatch = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(128, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, pick.R, pick.G, pick.B)),
            };

            var tooltip = new ToolTip { Content = pick.Hex };
            ToolTipService.SetToolTip(swatch, tooltip);

            PalettePanel.Children.Add(swatch);
        }

        PaletteContainer.Visibility = _palette.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PaletteCountText.Text = $"{_palette.Count} color{(_palette.Count == 1 ? "" : "s")}  |  Click more  |  Enter = Done  |  Backspace = Undo";
    }

    private void UpdateColorPreview(Point pos)
    {
        var (r, g, b) = GetPixelColor(pos);
        string hex = $"#{r:X2}{g:X2}{b:X2}";

        HexText.Text = hex;
        RgbText.Text = $"rgb({r}, {g}, {b})";
        ColorSwatch.Background = new SolidColorBrush(
            Windows.UI.Color.FromArgb(255, r, g, b));

        ColorPreview.Visibility = Visibility.Visible;

        // Position the preview near cursor (offset so it doesn't overlap)
        double offsetX = 20;
        double offsetY = 20;
        double previewX = pos.X + offsetX;
        double previewY = pos.Y + offsetY;

        // Keep within screen bounds
        double canvasW = OverlayCanvas.ActualWidth;
        double canvasH = OverlayCanvas.ActualHeight;
        if (previewX + 180 > canvasW)
        {
            previewX = pos.X - offsetX - 180;
        }

        if (previewY + 50 > canvasH)
        {
            previewY = pos.Y - offsetY - 50;
        }

        Canvas.SetLeft(ColorPreview, previewX);
        Canvas.SetTop(ColorPreview, previewY);
    }

    private (byte R, byte G, byte B) GetPixelColor(Point pos)
    {
        if (_pixelData is null || _imageWidth == 0 || _imageHeight == 0)
        {
            return (0, 0, 0);
        }

        // Map canvas coordinates to image pixel coordinates
        double scaleX = (double)_imageWidth / OverlayCanvas.ActualWidth;
        double scaleY = (double)_imageHeight / OverlayCanvas.ActualHeight;
        int px = Math.Clamp((int)(pos.X * scaleX), 0, _imageWidth - 1);
        int py = Math.Clamp((int)(pos.Y * scaleY), 0, _imageHeight - 1);

        // BGRA pixel layout (4 bytes per pixel)
        int offset = (py * _imageWidth + px) * 4;
        if (offset + 2 >= _pixelData.Length)
        {
            return (0, 0, 0);
        }

        byte b = _pixelData[offset];
        byte g = _pixelData[offset + 1];
        byte r = _pixelData[offset + 2];
        return (r, g, b);
    }

    private async Task DecodePngPixelsAsync(byte[] pngData)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngData.AsBuffer()).AsTask().ConfigureAwait(false);
        stream.Seek(0);

        var decoder = await Windows.Graphics.Imaging.BitmapDecoder
            .CreateAsync(stream).AsTask().ConfigureAwait(false);
        var pixelProvider = await decoder
            .GetPixelDataAsync(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                new Windows.Graphics.Imaging.BitmapTransform(),
                Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation,
                Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage)
            .AsTask().ConfigureAwait(false);

        _pixelData = pixelProvider.DetachPixelData();
        _imageWidth = (int)decoder.PixelWidth;
        _imageHeight = (int)decoder.PixelHeight;

        Log.Debug("ColorPicker: decoded {W}x{H} image ({Bytes} bytes pixel data)",
            _imageWidth, _imageHeight, _pixelData.Length);
    }
}

/// <summary>Result from color picker overlay.</summary>
public sealed record ColorPickResult(string Hex, byte R, byte G, byte B);
