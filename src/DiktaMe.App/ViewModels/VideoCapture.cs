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
        CancellationToken cancellationToken)
    {
        if (_recorder is not null)
        {
            throw new InvalidOperationException("Already recording.");
        }

        // Ensure even dimensions (H.264 requirement)
        if (width % 2 != 0) width++;
        if (height % 2 != 0) height++;

        Log.Information("VideoCapture: starting {W}x{H} at ({L},{T}), fps={Fps}, bitrate={Kbps}kbps, audio={Audio}",
            width, height, left, top, options.FrameRateHz, options.BitrateKbps, options.EnableMicAudio);

        var displaySource = DisplayRecordingSource.MainMonitor;
        Log.Debug("VideoCapture: MainMonitor source = {Source} (null={IsNull})",
            displaySource?.DeviceName, displaySource is null);

        if (displaySource is null)
        {
            throw new InvalidOperationException("No display monitor found for recording.");
        }

        // Set crop region on the display source
        displaySource.SourceRect = new ScreenRect(left, top, width, height);
        Log.Debug("VideoCapture: source DeviceName={Dev}, SourceRect=({L},{T} {W}x{H})",
            displaySource.DeviceName, left, top, width, height);

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
                IsAudioEnabled = options.EnableMicAudio,
                IsInputDeviceEnabled = options.EnableMicAudio,
                IsOutputDeviceEnabled = false, // Don't capture system audio (V1/V2)
            },
            SourceOptions = new SourceOptions
            {
                RecordingSources = [displaySource],
            },
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
