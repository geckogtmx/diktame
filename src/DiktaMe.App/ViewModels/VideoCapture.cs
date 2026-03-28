using DiktaMe.Core.Vision;
using ScreenRecorderLib;
using Serilog;

namespace DiktaMe.App.ViewModels;

/// <summary>
/// Records a screen region to MP4 using ScreenRecorderLib (Media Foundation + Windows.Graphics.Capture).
/// Supports optional mic audio capture.
/// </summary>
public sealed class VideoCapture : IDisposable
{
    private Recorder? _recorder;
    private readonly TaskCompletionSource<bool> _completionTcs = new();
    private bool _disposed;

    /// <summary>
    /// Records a screen region to an MP4 file.
    /// </summary>
    /// <param name="left">Left edge of capture region (screen coordinates).</param>
    /// <param name="top">Top edge of capture region (screen coordinates).</param>
    /// <param name="width">Width of capture region in pixels.</param>
    /// <param name="height">Height of capture region in pixels.</param>
    /// <param name="outputPath">Full path for the output .mp4 file.</param>
    /// <param name="options">Recording configuration.</param>
    /// <param name="cancellationToken">Cancellation (e.g. from Stop button or max duration).</param>
    public async Task RecordAsync(
        int left, int top, int width, int height,
        string outputPath,
        VideoRecordingOptions options,
        CancellationToken cancellationToken,
        string? monitorDeviceName = null,
        int monitorOffsetX = 0,
        int monitorOffsetY = 0)
    {
        if (_recorder is not null)
        {
            throw new InvalidOperationException("Already recording.");
        }

        // Ensure even dimensions (H.264 requirement)
        if (width % 2 != 0) width++;
        if (height % 2 != 0) height++;

        Log.Information("VideoCapture: starting {W}x{H} at ({L},{T}), fps={Fps}, bitrate={Kbps}kbps, mic={Mic}, sysAudio={Sys}, webcam={Webcam}, monitor={Mon}",
            width, height, left, top, options.FrameRateHz, options.BitrateKbps, options.EnableMicAudio, options.EnableSystemAudio, options.EnableWebcam, monitorDeviceName ?? "(main)");

        // Select the correct monitor for recording (critical for multi-monitor setups)
        DisplayRecordingSource? displaySource;
        if (!string.IsNullOrEmpty(monitorDeviceName))
        {
            displaySource = new DisplayRecordingSource { DeviceName = monitorDeviceName };
            Log.Debug("VideoCapture: using specific monitor {Dev}", monitorDeviceName);
        }
        else
        {
            displaySource = DisplayRecordingSource.MainMonitor;
            Log.Debug("VideoCapture: using MainMonitor {Dev}", displaySource?.DeviceName);
        }

        if (displaySource is null)
        {
            throw new InvalidOperationException("No display monitor found for recording.");
        }

        // Set crop region relative to the target monitor's origin
        // (absolute screen coords → monitor-local coords)
        int relLeft = left - monitorOffsetX;
        int relTop = top - monitorOffsetY;
        displaySource.SourceRect = new ScreenRect(relLeft, relTop, width, height);
        Log.Debug("VideoCapture: source DeviceName={Dev}, SourceRect=({L},{T} {W}x{H}), monitorOffset=({OX},{OY})",
            displaySource.DeviceName, relLeft, relTop, width, height, monitorOffsetX, monitorOffsetY);

        // Build source and overlay lists
        var sources = new List<RecordingSourceBase> { displaySource };
        var overlays = new List<RecordingOverlayBase>();

        if (options.EnableWebcam)
        {
            // List available video capture devices for selection
            var devices = Recorder.GetSystemVideoCaptureDevices();
            foreach (var dev in devices)
            {
                Log.Debug("VideoCapture: available camera — '{Name}' path='{Path}'",
                    dev.DeviceName, dev.DeviceName);
            }

            var anchor = options.WebcamPosition switch
            {
                "bottom-left" => Anchor.BottomLeft,
                "top-right" => Anchor.TopRight,
                "top-left" => Anchor.TopLeft,
                _ => Anchor.BottomRight,
            };

            var webcamOverlay = new VideoCaptureOverlay
            {
                AnchorPoint = anchor,
                Offset = new ScreenSize(20, 20),
                Size = new ScreenSize(options.WebcamBubbleSize, (int)(options.WebcamBubbleSize * 9.0 / 16.0)),
            };

            // Use specific device if configured, otherwise first available
            if (!string.IsNullOrEmpty(options.WebcamDeviceName))
            {
                webcamOverlay.DeviceName = options.WebcamDeviceName;
            }
            else if (devices.Count > 0)
            {
                // Prefer a real USB camera over virtual cameras (Snap Camera, OBS Virtual, etc.)
                var preferred = devices.FirstOrDefault(d =>
                    d.DeviceName.Contains("usb", StringComparison.OrdinalIgnoreCase)) ?? devices[0];
                webcamOverlay.DeviceName = preferred.DeviceName;
                Log.Information("VideoCapture: auto-selected camera '{Name}'", preferred.DeviceName);
            }
            overlays.Add(webcamOverlay);
            Log.Information("VideoCapture: webcam overlay enabled ({Size}px, bottom-right)",
                options.WebcamBubbleSize);
        }

        var recorderOptions = new RecorderOptions
        {
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Bitrate = options.BitrateKbps * 1000,
                Framerate = options.FrameRateHz,
                IsFixedFramerate = false,
                Encoder = new H264VideoEncoder(),
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = options.EnableMicAudio || options.EnableSystemAudio,
                IsInputDeviceEnabled = options.EnableMicAudio,
                IsOutputDeviceEnabled = options.EnableSystemAudio,
            },
            SourceOptions = new SourceOptions
            {
                RecordingSources = sources,
            },
            OverlayOptions = overlays.Count > 0
                ? new OverLayOptions { Overlays = overlays }
                : null,
        };

        _recorder = Recorder.CreateRecorder(recorderOptions);
        _recorder.OnRecordingComplete += OnComplete;
        _recorder.OnRecordingFailed += OnFailed;
        _recorder.OnStatusChanged += OnStatusChanged;

        using var reg = cancellationToken.Register(() =>
        {
            Log.Debug("VideoCapture: cancellation requested, stopping...");
            _recorder?.Stop();
        });

        _recorder.Record(outputPath);
        Log.Information("VideoCapture: recording started → {Path}", outputPath);

        await _completionTcs.Task.ConfigureAwait(false);
    }

    /// <summary>Signals the recording to stop gracefully.</summary>
    public void Stop()
    {
        _recorder?.Stop();
    }

    /// <summary>Pauses the recording.</summary>
    public void Pause()
    {
        _recorder?.Pause();
    }

    /// <summary>Resumes a paused recording.</summary>
    public void Resume()
    {
        _recorder?.Resume();
    }

    private void OnComplete(object? sender, RecordingCompleteEventArgs e)
    {
        Log.Information("VideoCapture: recording complete → {Path}", e.FilePath);
        _completionTcs.TrySetResult(true);
    }

    private void OnFailed(object? sender, RecordingFailedEventArgs e)
    {
        Log.Error("VideoCapture: recording failed — {Error}", e.Error);
        _completionTcs.TrySetException(new InvalidOperationException($"Recording failed: {e.Error}"));
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        Log.Debug("VideoCapture: status → {Status}", e.Status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recorder?.Dispose();
        _recorder = null;
    }
}
