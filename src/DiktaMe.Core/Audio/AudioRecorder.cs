
using System.Diagnostics;
using NAudio.Wave;
using Serilog;

namespace DiktaMe.Core.Audio;
/// <summary>
/// Records audio from a microphone input device and saves it to a
/// temporary WAV file (16 kHz, 16-bit, mono — Whisper-compatible).
/// Port of python/core/recorder.py from V1.
/// </summary>
public sealed class AudioRecorder : IAudioDataSource, IDisposable
{
    // ── Whisper-compatible format ────────────────────────────────────────────
    private const int SampleRate = 16_000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    private static readonly WaveFormat RecordingFormat =
        new(SampleRate, BitsPerSample, Channels);

    // ── State ────────────────────────────────────────────────────────────────
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentFilePath;
    private System.Timers.Timer? _autoStopTimer;
    private bool _stoppedByAutoStop;
    private bool _disposed;
    private Stopwatch? _recordingStopwatch;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Raised when recording begins.</summary>
    public event EventHandler<RecordingStartedEventArgs>? RecordingStarted;

    /// <summary>Raised when recording stops normally (via <see cref="StopRecordingAsync"/>).</summary>
    public event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;

    /// <summary>
    /// Raised when recording stops automatically because the maximum
    /// duration was reached. The file path is included in the args.
    /// </summary>
    public event EventHandler<RecordingStoppedEventArgs>? AutoStopped;

    /// <summary>
    /// Raised for each chunk of raw PCM audio data during recording.
    /// Subscribers can forward this data to a streaming STT provider.
    /// The WAV file writer continues to operate in parallel.
    /// </summary>
    /// <remarks>
    /// Fires on NAudio's callback thread. The buffer in
    /// <see cref="AudioDataAvailableEventArgs.PcmData"/> is reused between callbacks —
    /// subscribers must copy the data if they need it beyond the handler lifetime.
    /// </remarks>
    public event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;

    // ── Properties ───────────────────────────────────────────────────────────

    /// <summary>Whether a recording is currently in progress.</summary>
    public bool IsRecording { get; private set; }

    /// <summary>
    /// Duration in milliseconds of the last completed recording.
    /// 0 if no recording has been made yet.
    /// </summary>
    public long LastRecordingDurationMs { get; private set; }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts recording from the specified device.
    /// </summary>
    /// <param name="deviceLabel">
    /// Optional device name substring for fuzzy matching (e.g., "Headset").
    /// Falls back to the system default if null or not found.
    /// </param>
    /// <param name="deviceId">
    /// Optional numeric device ID string. Used if <paramref name="deviceLabel"/> is null.
    /// </param>
    /// <param name="maxDurationSeconds">
    /// Maximum recording duration in seconds. 0 means unlimited.
    /// When the limit is reached <see cref="AutoStopped"/> is raised.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if recording is already in progress.
    /// </exception>
    public void StartRecording(
        string? deviceLabel = null,
        string? deviceId = null,
        int maxDurationSeconds = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRecording)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        int deviceIndex = AudioDeviceManager.ResolveDeviceIndex(deviceLabel, deviceId);
        _currentFilePath = BuildTempFilePath();

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = RecordingFormat,
        };

        _writer = new WaveFileWriter(_currentFilePath, RecordingFormat);
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnWaveInRecordingStopped;

        _waveIn.StartRecording();
        IsRecording = true;

        // Start timing the recording
        _recordingStopwatch = Stopwatch.StartNew();

        Log.Information("AudioRecorder: started (device={DeviceIndex}, file={FilePath})",
            deviceIndex, _currentFilePath);

        RecordingStarted?.Invoke(this, new RecordingStartedEventArgs(deviceIndex, _currentFilePath));

        if (maxDurationSeconds > 0)
        {
            _autoStopTimer = new System.Timers.Timer(maxDurationSeconds * 1000d)
            {
                AutoReset = false,
            };
            _autoStopTimer.Elapsed += (_, _) => OnAutoStopTimerElapsed(maxDurationSeconds);
            _autoStopTimer.Start();
        }
    }

    /// <summary>
    /// Stops recording and flushes the WAV file.
    /// </summary>
    /// <returns>
    /// The path of the saved WAV file, or <c>null</c> if no recording was ever started.
    /// If the recording already stopped (e.g., via auto-stop), returns the file path from that recording.
    /// </returns>
    public Task<string?> StopRecordingAsync()
    {
        // If still recording, stop it now
        if (IsRecording)
        {
            StopInternal(isAutoStop: false);
        }

        // Return the file path from current or most recent recording
        // (handles case where auto-stop already stopped the recording)
        return Task.FromResult<string?>(_currentFilePath);
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private void OnAutoStopTimerElapsed(int maxDurationSeconds)
    {
        Log.Warning("AudioRecorder: auto-stopped after {MaxDuration}s", maxDurationSeconds);
        string? filePath = _currentFilePath;
        StopInternal(isAutoStop: true);
        AutoStopped?.Invoke(this, new RecordingStoppedEventArgs(filePath, wasAutoStopped: true, LastRecordingDurationMs));
    }

    private void StopInternal(bool isAutoStop)
    {
        _autoStopTimer?.Stop();
        _autoStopTimer?.Dispose();
        _autoStopTimer = null;

        _stoppedByAutoStop = isAutoStop;

        // Stop timing and save duration
        if (_recordingStopwatch is not null)
        {
            _recordingStopwatch.Stop();
            LastRecordingDurationMs = _recordingStopwatch.ElapsedMilliseconds;
            _recordingStopwatch = null;
        }

        // Capture then null before calling StopRecording, so Dispose() cannot
        // concurrently grab the same instance and double-stop it.
        WaveInEvent? waveIn = _waveIn;
        _waveIn = null;
        IsRecording = false;

        if (waveIn is not null)
        {
            waveIn.StopRecording();
            // OnWaveInRecordingStopped flushes the writer and fires RecordingStopped,
            // then disposes waveIn via the sender reference — not via _waveIn.
        }

        if (!isAutoStop)
        {
            Log.Information("AudioRecorder: stopped, file={FilePath}", _currentFilePath);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

        // Forward raw PCM to streaming subscribers (zero-copy slice of NAudio buffer)
        AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(e.Buffer, e.BytesRecorded));
    }

    private void OnWaveInRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // Dispose the sender (waveIn that just stopped).
        // _waveIn field may already be null (nulled in StopInternal/Dispose) — do not re-null.
        (sender as IDisposable)?.Dispose();

        // Flush and dispose the writer
        WaveFileWriter? writer = _writer;
        _writer = null;
        if (writer is not null)
        {
            try { writer.Flush(); } catch { /* best-effort */ }
            writer.Dispose();
        }

        if (e.Exception is not null)
        {
            Log.Error(e.Exception, "AudioRecorder: WaveIn stopped with error");
        }

        // Only raise RecordingStopped for manual stops; auto-stop raises AutoStopped separately
        if (!_stoppedByAutoStop && !_disposed)
        {
            RecordingStopped?.Invoke(this, new RecordingStoppedEventArgs(_currentFilePath, wasAutoStopped: false, LastRecordingDurationMs));
        }
    }

    private static string BuildTempFilePath()
    {
        string fileName = $"diktame_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.wav";
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes the recorder. Stops any active recording and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _autoStopTimer?.Stop();
        _autoStopTimer?.Dispose();
        _autoStopTimer = null;

        // Capture and null out before calling StopRecording/Dispose
        // to prevent double-dispose if OnWaveInRecordingStopped also runs.
        WaveInEvent? waveIn = _waveIn;
        WaveFileWriter? writer = _writer;
        _waveIn = null;
        _writer = null;
        _currentFilePath = null; // Clear file path on dispose
        IsRecording = false;

        if (waveIn is not null)
        {
            // StopRecording triggers OnWaveInRecordingStopped (on a NAudio callback thread),
            // which disposes waveIn via sender and flushes the writer.
            // Do NOT call waveIn.Dispose() here — the callback owns that responsibility.
            try { waveIn.StopRecording(); } catch { /* best-effort */ }
        }
        else if (writer is not null)
        {
            // No waveIn to stop, so flush/dispose the writer directly.
            try { writer.Flush(); } catch { /* best-effort */ }
            writer.Dispose();
        }
    }
}

// ── Event args ────────────────────────────────────────────────────────────────

/// <summary>Event data for <see cref="AudioRecorder.RecordingStarted"/>.</summary>
public sealed class RecordingStartedEventArgs(int deviceIndex, string filePath) : EventArgs
{
    /// <summary>The WaveIn device index used.</summary>
    public int DeviceIndex { get; } = deviceIndex;

    /// <summary>The temp WAV file path being written to.</summary>
    public string FilePath { get; } = filePath;
}

/// <summary>
/// Event data for <see cref="AudioRecorder.RecordingStopped"/> and
/// <see cref="AudioRecorder.AutoStopped"/>.
/// </summary>
public sealed class RecordingStoppedEventArgs(string? filePath, bool wasAutoStopped, long durationMs = 0) : EventArgs
{
    /// <summary>Path to the saved WAV file, or null if saving failed.</summary>
    public string? FilePath { get; } = filePath;

    /// <summary>True when stopped by the duration limit; false for manual stop.</summary>
    public bool WasAutoStopped { get; } = wasAutoStopped;

    /// <summary>Duration of the recording in milliseconds.</summary>
    public long DurationMs { get; } = durationMs;
}

/// <summary>
/// Event data for <see cref="AudioRecorder.AudioDataAvailable"/>.
/// Contains raw PCM audio bytes from the recording device.
/// </summary>
/// <remarks>
/// <see cref="PcmData"/> references NAudio's internal buffer which is reused across callbacks.
/// Subscribers that need the data beyond the event handler lifetime must copy it
/// (e.g., <c>PcmData.ToArray()</c>).
/// </remarks>
public sealed class AudioDataAvailableEventArgs(byte[] buffer, int bytesRecorded) : EventArgs
{
    /// <summary>
    /// Raw PCM audio data (16kHz, 16-bit, mono). Slice of NAudio's internal buffer.
    /// Only valid for the duration of the event handler — copy if needed later.
    /// </summary>
    public ReadOnlyMemory<byte> PcmData { get; } = buffer.AsMemory(0, bytesRecorded);

    /// <summary>Number of valid PCM bytes in this chunk.</summary>
    public int BytesRecorded { get; } = bytesRecorded;
}
