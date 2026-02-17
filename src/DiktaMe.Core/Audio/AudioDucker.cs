namespace DiktaMe.Core.Audio;

using NAudio.CoreAudioApi;
using Serilog;

/// <summary>
/// Ducks (temporarily lowers the volume of) all active audio sessions except
/// the current process while recording is in progress, then restores them.
/// Port of V1 audio ducking logic (pycaw-based) to NAudio WASAPI.
/// </summary>
public sealed class AudioDucker : IDisposable
{
    // ── Defaults ─────────────────────────────────────────────────────────────

    /// <summary>Default duck level (volume multiplier, 0.0–1.0).</summary>
    public const float DefaultDuckLevel = 0.20f;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly int _ownPid;
    private readonly object _lock = new();

    /// <summary>Session → original volume before ducking.</summary>
    private readonly Dictionary<AudioSessionControl, float> _saved = new();

    private bool _isDucked;
    private bool _disposed;

    // ── Settings (mutable at runtime) ────────────────────────────────────────

    /// <summary>Whether audio ducking is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Volume level to duck to (0.0–1.0).  Applied to non-dIKta.me sessions.
    /// </summary>
    public float DuckLevel { get; set; } = DefaultDuckLevel;

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="AudioDucker"/>.
    /// </summary>
    /// <param name="isEnabled">Initial enabled state (default true).</param>
    /// <param name="duckLevel">Initial duck level 0.0–1.0 (default 0.20).</param>
    public AudioDucker(bool isEnabled = true, float duckLevel = DefaultDuckLevel)
    {
        _ownPid = Environment.ProcessId;
        IsEnabled = isEnabled;
        DuckLevel = Math.Clamp(duckLevel, 0f, 1f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Lowers the volume of all non-dIKta.me audio sessions to <see cref="DuckLevel"/>.
    /// No-op if <see cref="IsEnabled"/> is false or already ducked.
    /// </summary>
    public void Duck()
    {
        if (!IsEnabled || _disposed) return;

        lock (_lock)
        {
            if (_isDucked) return;

            try
            {
                EnumerateSessions(session =>
                {
                    try
                    {
                        float current = session.SimpleAudioVolume.Volume;
                        _saved[session] = current;
                        session.SimpleAudioVolume.Volume = Math.Min(current, DuckLevel);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "AudioDucker: failed to duck session {Session}", GetSessionLabel(session));
                    }
                });

                _isDucked = true;
                Log.Debug("AudioDucker: ducked {Count} session(s) to {Level:P0}", _saved.Count, DuckLevel);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AudioDucker: Duck() failed");
            }
        }
    }

    /// <summary>
    /// Restores all previously-ducked sessions to their original volumes.
    /// No-op if not currently ducked.
    /// </summary>
    public void Restore()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (!_isDucked) return;
            RestoreInternal();
        }
    }

    // ── AudioRecorder event hooks ─────────────────────────────────────────────

    /// <summary>
    /// Wires this ducker to an <see cref="AudioRecorder"/> so ducking happens
    /// automatically at recording start/stop.
    /// </summary>
    public void AttachTo(AudioRecorder recorder)
    {
        recorder.RecordingStarted += OnRecordingStarted;
        recorder.RecordingStopped += OnRecordingStopped;
        recorder.AutoStopped += OnRecordingStopped;
    }

    /// <summary>
    /// Detaches from a previously-wired <see cref="AudioRecorder"/>.
    /// </summary>
    public void DetachFrom(AudioRecorder recorder)
    {
        recorder.RecordingStarted -= OnRecordingStarted;
        recorder.RecordingStopped -= OnRecordingStopped;
        recorder.AutoStopped -= OnRecordingStopped;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnRecordingStarted(object? sender, RecordingStartedEventArgs e) => Duck();

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e) => Restore();

    /// <summary>
    /// Enumerates active non-dIKta.me audio sessions on the default render device
    /// and invokes <paramref name="action"/> on each.
    /// </summary>
    private void EnumerateSessions(Action<AudioSessionControl> action)
    {
        using var enumerator = new MMDeviceEnumerator();

        MMDevice? device;
        try
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AudioDucker: no default render endpoint available");
            return;
        }

        using (device)
        {
            var sessionManager = device.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                try
                {
                    // Skip our own process
                    if (session.GetProcessID == (uint)_ownPid) continue;

                    // Skip sessions that are already at or below duck level
                    // (nothing to duck; still save them so Restore is a no-op)
                    action(session);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "AudioDucker: error inspecting session {Index}", i);
                }
            }
        }
    }

    private void RestoreInternal()
    {
        int restored = 0;
        foreach (var (session, volume) in _saved)
        {
            try
            {
                session.SimpleAudioVolume.Volume = volume;
                restored++;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AudioDucker: failed to restore session {Session}", GetSessionLabel(session));
            }
        }
        _saved.Clear();
        _isDucked = false;
        Log.Debug("AudioDucker: restored {Count} session(s)", restored);
    }

    private static string GetSessionLabel(AudioSessionControl session)
    {
        try { return session.DisplayName; }
        catch { return "?"; }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>Disposes the ducker and restores all sessions.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            if (_isDucked)
                RestoreInternal();
        }
    }
}
