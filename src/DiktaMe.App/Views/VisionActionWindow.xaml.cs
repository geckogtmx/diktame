using System.Runtime.InteropServices.WindowsRuntime;
using DiktaMe.App.Services;
using DiktaMe.Core.Vision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage.Streams;
using Windows.System;

namespace DiktaMe.App.Views;

/// <summary>
/// Two-step Vision modal: shows captured thumbnail, lets user pick an action
/// (Clipboard / Chat / Note), optional text query, and Local/Cloud toggle.
/// Returns <see cref="VisionActionResult"/> via <see cref="GetResultAsync"/>.
/// </summary>
public sealed partial class VisionActionWindow : Window
{
    private readonly TaskCompletionSource<VisionActionResult?> _tcs = new();

    public VisionActionWindow(bool isLocalVision)
    {
        InitializeComponent();

        // Set initial Local/Cloud toggle
        LocalRadio.IsChecked = isLocalVision;
        CloudRadio.IsChecked = !isLocalVision;

        // Window sizing — compact modal
        AppWindow.Resize(new SizeInt32(420, 440));

        // Set icon
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        // Always on top so it stays visible
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        // Theme integration
        var themeService = App.Current.Services.GetRequiredService<ThemeService>();
        var palette = ThemeService.GetPalette(themeService.CurrentTheme);
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = palette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        }
        InjectControlBrushes(palette);

        themeService.ThemeChanged += (_, themeName) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var p = ThemeService.GetPalette(themeName);
                if (Content is FrameworkElement r)
                {
                    r.RequestedTheme = p.IsDark ? ElementTheme.Dark : ElementTheme.Light;
                }
                InjectControlBrushes(p);
            });
        };

        // Esc to cancel (both from text input and from anywhere in the window)
        AppWindow.Closing += (_, _) => _tcs.TrySetResult(null);
        if (Content is UIElement contentElement)
        {
            contentElement.KeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.Escape)
                {
                    _tcs.TrySetResult(null);
                    Close();
                    e.Handled = true;
                }
            };
        }
    }

    /// <summary>
    /// Loads the captured image as a thumbnail preview.
    /// Call before <see cref="Activate"/>.
    /// </summary>
    public async Task SetThumbnailAsync(byte[] pngData)
    {
        var bitmap = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngData.AsBuffer()).AsTask().ConfigureAwait(false);
        stream.Seek(0);

        DispatcherQueue.TryEnqueue(() =>
        {
            _ = bitmap.SetSourceAsync(stream);
            ThumbnailImage.Source = bitmap;
        });

        // Small delay to let the image load
        await Task.Delay(50).ConfigureAwait(false);
    }

    /// <summary>
    /// Centers the window on the given monitor bounds.
    /// </summary>
    public void CenterOnMonitor((int X, int Y, int Width, int Height) monitorBounds)
    {
        int x = monitorBounds.X + (monitorBounds.Width - 420) / 2;
        int y = monitorBounds.Y + (monitorBounds.Height - 440) / 2;
        AppWindow.Move(new PointInt32(x, y));
    }

    /// <summary>Awaits the user's action choice. Null = cancelled.</summary>
    public Task<VisionActionResult?> GetResultAsync() => _tcs.Task;

    private VisionActionResult BuildResult(VisionAction action)
    {
        string? query = string.IsNullOrWhiteSpace(QueryInput.Text) ? null : QueryInput.Text.Trim();
        bool useLocal = LocalRadio.IsChecked == true;
        bool skipAi = NoneRadio.IsChecked == true;
        return new VisionActionResult(action, query, useLocal, skipAi);
    }

    private void Complete(VisionAction action)
    {
        _tcs.TrySetResult(BuildResult(action));
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e) => Complete(VisionAction.Save);
    private void OnClipboard(object sender, RoutedEventArgs e) => Complete(VisionAction.Clipboard);
    private void OnChat(object sender, RoutedEventArgs e) => Complete(VisionAction.Chat);
    private void OnNote(object sender, RoutedEventArgs e) => Complete(VisionAction.Note);
    private void OnOcr(object sender, RoutedEventArgs e) => Complete(VisionAction.Ocr);
    private void OnTable(object sender, RoutedEventArgs e) => Complete(VisionAction.Table);

    private void QueryInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            _tcs.TrySetResult(null);
            Close();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            // Enter defaults to Clipboard action (fastest path)
            Complete(VisionAction.Clipboard);
            e.Handled = true;
        }
    }

    private void InjectControlBrushes(ThemePalette palette)
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }
        foreach (var (key, color) in ThemeService.GetControlBrushValues(palette))
        {
            root.Resources[key] = new SolidColorBrush(color);
        }
    }
}
