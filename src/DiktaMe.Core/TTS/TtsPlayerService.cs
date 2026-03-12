using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// NAudio-based audio playback service for TTS output.
/// Provides play/pause/resume/stop with volume control and state events.
/// Uses <see cref="WasapiOut"/> (shared mode) for low-latency WASAPI playback.
/// Thread-safe — all state transitions are serialized via a lock.
/// </summary>
public sealed class TtsPlayerService : ITtsPlayerService
{
    private readonly object _lock = new();
    private WasapiOut? _waveOut;
    private RawSourceWaveStream? _waveStream;
    private MemoryStream? _memoryStream;
    private TaskCompletionSource<bool>? _playbackTcs;
    private bool _userStopped;
    private bool _disposed;

    private enum State { Idle, Playing, Paused }
    private State _state = State.Idle;

    // ── Public state ────────────────────────────────────────────────────────

    /// <summary>Whether audio is currently playing.</summary>
    public bool IsPlaying => _state == State.Playing;

    /// <summary>Whether audio playback is paused.</summary>
    public bool IsPaused => _state == State.Paused;

    /// <summary>Current playback position.</summary>
    public TimeSpan Position
    {
        get
        {
            lock (_lock)
            {
                return _waveStream?.CurrentTime ?? TimeSpan.Zero;
            }
        }
    }

    /// <summary>Total audio duration.</summary>
    public TimeSpan Duration
    {
        get
        {
            lock (_lock)
            {
                return _waveStream?.TotalTime ?? TimeSpan.Zero;
            }
        }
    }

    // ── Volume ──────────────────────────────────────────────────────────────

    private float _volume = 0.8f;

    /// <summary>
    /// Playback volume (0.0–1.0). Clamped to valid range.
    /// Can be changed while playing.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            lock (_lock)
            {
                if (_waveOut is not null && _state != State.Idle)
                {
                    try { _waveOut.Volume = _volume; }
                    catch (InvalidOperationException) { /* not yet initialized */ }
                }
            }
        }
    }

    // ── Events ──────────────────────────────────────────────────────────────

    /// <summary>Raised when playback starts.</summary>
    public event EventHandler? PlaybackStarted;

    /// <summary>Raised when playback reaches the end of the audio naturally.</summary>
    public event EventHandler? PlaybackFinished;

    /// <summary>Raised when playback is stopped by user action (Stop() call).</summary>
    public event EventHandler? PlaybackStopped;

    // ── Playback ────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays raw PCM audio data. Stops any current playback first.
    /// Returns when playback completes or is stopped.
    /// </summary>
    /// <param name="audioData">Raw PCM audio bytes (16-bit signed, mono).</param>
    /// <param name="sampleRate">Sample rate in Hz (e.g. 24000).</param>
    /// <param name="cancellationToken">Cancels playback when triggered.</param>
    public async Task PlayAsync(byte[] audioData, int sampleRate, CancellationToken cancellationToken = default)
    {
        if (_disposed || audioData.Length == 0)
            return;

        // Stop any existing playback
        Stop();

        var format = new WaveFormat(sampleRate, 16, 1);
        var memStream = new MemoryStream(audioData);
        var waveStream = new RawSourceWaveStream(memStream, format);

        WasapiOut waveOut;
        try
        {
            waveOut = new WasapiOut(AudioClientShareMode.Shared, 100);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TtsPlayerService: no audio output device available");
            waveStream.Dispose();
            memStream.Dispose();
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        waveOut.PlaybackStopped += (_, _) =>
        {
            bool wasUserStopped;
            lock (_lock)
            {
                wasUserStopped = _userStopped;
                if (_state != State.Idle)
                {
                    _state = State.Idle;
                    CleanupLocked();
                }
            }

            if (!wasUserStopped)
                PlaybackFinished?.Invoke(this, EventArgs.Empty);

            tcs.TrySetResult(true);
        };

        lock (_lock)
        {
            _memoryStream = memStream;
            _waveStream = waveStream;
            _waveOut = waveOut;
            _playbackTcs = tcs;
            _userStopped = false;

            waveOut.Init(waveStream);
            try { waveOut.Volume = _volume; }
            catch (InvalidOperationException) { /* volume not supported in this mode */ }
            waveOut.Play();
            _state = State.Playing;
        }

        PlaybackStarted?.Invoke(this, EventArgs.Empty);

        using var registration = cancellationToken.Register(() => Stop());
        await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>Pauses playback. No-op if not playing.</summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (_state != State.Playing || _waveOut is null)
                return;
            _waveOut.Pause();
            _state = State.Paused;
        }
    }

    /// <summary>Resumes paused playback. No-op if not paused.</summary>
    public void Resume()
    {
        lock (_lock)
        {
            if (_state != State.Paused || _waveOut is null)
                return;
            _waveOut.Play();
            _state = State.Playing;
        }
    }

    /// <summary>
    /// Stops playback immediately. No-op if idle.
    /// Raises <see cref="PlaybackStopped"/> if playback was active.
    /// </summary>
    public void Stop()
    {
        TaskCompletionSource<bool>? tcs;

        lock (_lock)
        {
            if (_state == State.Idle || _waveOut is null)
                return;

            _userStopped = true;
            _state = State.Idle;
            tcs = _playbackTcs;

            // Stop triggers WasapiOut.PlaybackStopped event on the NAudio thread.
            // We already set _state and _userStopped, so the event handler will
            // see _userStopped=true and skip the PlaybackFinished event.
            try { _waveOut.Stop(); }
            catch (Exception ex) { Log.Warning(ex, "TtsPlayerService: error during Stop"); }

            CleanupLocked();
        }

        PlaybackStopped?.Invoke(this, EventArgs.Empty);
        tcs?.TrySetResult(true);
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────

    private void CleanupLocked()
    {
        _waveOut?.Dispose();
        _waveOut = null;
        _waveStream?.Dispose();
        _waveStream = null;
        _memoryStream?.Dispose();
        _memoryStream = null;
        _playbackTcs = null;
    }

    /// <summary>Disposes the service and stops any active playback.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
