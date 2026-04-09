using System.Runtime.InteropServices.WindowsRuntime;
using DiktaMe.App.Services;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.STT;
using DiktaMe.Core.Vision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
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
    private bool _isRecording;
    private AudioRecorder? _recorder;
    private string? _recordingPath;
    private Storyboard? _pulseStoryboard;

    public VisionActionWindow()
    {
        InitializeComponent();

        // Default to None (no AI) — user explicitly selects Local/Cloud when needed
        NoneRadio.IsChecked = true;

        // Window sizing — compact modal
        AppWindow.Resize(new SizeInt32(530, 440));

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

        // Live theme switching — RequestedTheme is already set by ThemeService.ApplyTheme()
        themeService.ThemeChanged += (_, themeName) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var p = ThemeService.GetPalette(themeName);
                    InjectControlBrushes(p);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "VisionActionWindow: CRASH in ThemeChanged handler");
                }
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
        int x = monitorBounds.X + (monitorBounds.Width - 530) / 2;
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
    private void OnColor(object sender, RoutedEventArgs e) => Complete(VisionAction.Color);
    private void OnRecord(object sender, RoutedEventArgs e) => Complete(VisionAction.Record);
    private void OnEdit(object sender, RoutedEventArgs e) => Complete(VisionAction.Edit);

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

    private async void OnMicToggle(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            await StopMicRecordingAsync();
        }
        else
        {
            StartMicRecordingAsync();
        }
    }

    private void StartMicRecordingAsync()
    {
        try
        {
            _isRecording = true;
            MicIcon.Glyph = "\uE71A"; // Stop icon
            MicButton.Background = new SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            StartRecordingPulse();

            var settings = App.Current.Services.GetRequiredService<SettingsManager>();
            var audioSettings = settings.Current.Audio;
            _recorder = new AudioRecorder();

            string? deviceLabel = string.IsNullOrEmpty(audioSettings.DeviceName) ? null : audioSettings.DeviceName;
            _recorder.StartRecording(deviceLabel: deviceLabel, maxDurationSeconds: 30);

            Log.Debug("VisionActionWindow: mic recording started");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "VisionActionWindow: mic recording failed to start");
            ResetMicButton();
        }
    }

    private async Task StopMicRecordingAsync()
    {
        try
        {
            _recordingPath = _recorder is not null
                ? await _recorder.StopRecordingAsync()
                : null;

            StopRecordingPulse();
            MicIcon.Glyph = "\uE720"; // Mic icon
            MicButton.Background = null;
            _isRecording = false;

            Log.Debug("VisionActionWindow: mic recording stopped, transcribing...");

            if (_recordingPath is null || !File.Exists(_recordingPath))
            {
                return;
            }

            // Transcribe using the configured STT provider
            var settings = App.Current.Services.GetRequiredService<SettingsManager>();
            var sttFactory = App.Current.Services.GetRequiredService<STTProviderFactory>();
            string sttProvider = "whisper"; // Default to local for quick dictation in modal

            // Use the active mode's STT if available
            if (settings.Current.ModeProfiles.TryGetValue("dictate_0", out var ms))
            {
                sttProvider = ms.SttProvider;
            }

            var stt = sttFactory.CreateProvider(sttProvider);
            if (stt is null)
            {
                Log.Warning("VisionActionWindow: no STT provider available");
                return;
            }

            string language = settings.Current.General.Language;
            var result = await stt.TranscribeAsync(_recordingPath, language);

            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                // Append to existing text (don't replace what user already typed)
                string existing = QueryInput.Text?.Trim() ?? "";
                QueryInput.Text = string.IsNullOrEmpty(existing)
                    ? result.Text.Trim()
                    : $"{existing} {result.Text.Trim()}";
                Log.Information("VisionActionWindow: mic transcribed {Chars} chars", result.Text.Length);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "VisionActionWindow: mic transcription failed");
        }
        finally
        {
            ResetMicButton();
            // Clean up temp file
            try
            {
                if (_recordingPath is not null && File.Exists(_recordingPath))
                {
                    File.Delete(_recordingPath);
                }
            }
            catch { /* best-effort cleanup */ }
        }
    }

    private void ResetMicButton()
    {
        _isRecording = false;
        MicIcon.Glyph = "\uE720"; // Mic icon
        MicButton.Background = null;
        StopRecordingPulse();
    }

    private void StartRecordingPulse()
    {
        var animation = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(600)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(animation, RecordingDot);
        Storyboard.SetTargetProperty(animation, "Opacity");
        _pulseStoryboard = new Storyboard();
        _pulseStoryboard.Children.Add(animation);
        _pulseStoryboard.Begin();
    }

    private void StopRecordingPulse()
    {
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
        RecordingDot.Opacity = 0;
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
