
using System.Diagnostics;
using NAudio.CoreAudioApi;
using Serilog;

namespace DiktaMe.Core.Audio;
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

    /// <summary>Session → target (ducked) volume.</summary>
    private readonly Dictionary<AudioSessionControl, float> _targets = new();

    private bool _isDucked;
    private bool _disposed;
    private CancellationTokenSource? _rampCts;

    // ── Settings (mutable at runtime) ────────────────────────────────────────

    /// <summary>Whether audio ducking is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Volume level to duck to (0.0–1.0).  Applied to non-dIKta.me sessions.
    /// </summary>
    public float DuckLevel { get; set; } = DefaultDuckLevel;

    /// <summary>
    /// Duration in milliseconds to ramp volume down when ducking starts (0 = instant).
    /// </summary>
    public int RampDownMs { get; set; }

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
    /// Instant (no ramp). Use <see cref="DuckAsync"/> for ramped ducking.
    /// No-op if <see cref="IsEnabled"/> is false or already ducked.
    /// </summary>
    public void Duck()
    {
        if (!IsEnabled || _disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_isDucked)
            {
                return;
            }

            try
            {
                EnumerateAndSaveSessions();
                ApplyTargetVolumes();
                _isDucked = true;
                Log.Information("AudioDucker: ducked {Count} session(s) to {Level:P0} (instant)", _saved.Count, DuckLevel);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AudioDucker: Duck() failed");
            }
        }
    }

    /// <summary>
    /// Lowers the volume of all non-dIKta.me audio sessions to <see cref="DuckLevel"/>
    /// with a linear ramp over <see cref="RampDownMs"/> milliseconds.
    /// Falls back to instant ducking when <see cref="RampDownMs"/> is 0 or less.
    /// No-op if <see cref="IsEnabled"/> is false or already ducked.
    /// </summary>
    public async Task DuckAsync()
    {
        if (!IsEnabled || _disposed)
        {
            return;
        }

        int rampMs;

        lock (_lock)
        {
            if (_isDucked)
            {
                return;
            }

            rampMs = RampDownMs;

            try
            {
                EnumerateAndSaveSessions();

                if (rampMs <= 0 || _saved.Count == 0)
                {
                    ApplyTargetVolumes();
                    _isDucked = true;
                    Log.Information("AudioDucker: ducked {Count} session(s) to {Level:P0} (instant)", _saved.Count, DuckLevel);
                    return;
                }

                // Mark as ducked immediately to prevent re-entry
                _isDucked = true;
                _rampCts?.Cancel();
                _rampCts = new CancellationTokenSource();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AudioDucker: DuckAsync() failed during setup");
                return;
            }
        }

        // Ramp outside the lock so Restore() is not blocked
        Log.Information("AudioDucker: ramping {Count} session(s) to {Level:P0} over {Ms}ms",
            _targets.Count, DuckLevel, rampMs);

        var ct = _rampCts!.Token;
        const int stepMs = 20;
        int steps = Math.Max(1, rampMs / stepMs);
        var sw = Stopwatch.StartNew();

        try
        {
            for (int i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                float progress = (float)i / steps;

                lock (_lock)
                {
                    foreach (var (session, target) in _targets)
                    {
                        try
                        {
                            if (_saved.TryGetValue(session, out float original))
                            {
                                float vol = original + (target - original) * progress;
                                session.SimpleAudioVolume.Volume = vol;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "AudioDucker: ramp step failed for session");
                        }
                    }
                }

                if (i < steps)
                {
                    await Task.Delay(stepMs, ct).ConfigureAwait(false);
                }
            }

            sw.Stop();
            Log.Information("AudioDucker: ramp complete in {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Log.Information("AudioDucker: ramp cancelled after {Ms}ms", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Restores all previously-ducked sessions to their original volumes instantly.
    /// Cancels any in-progress ramp. No-op if not currently ducked.
    /// Use <see cref="RestoreAsync"/> for a gradual fade-in.
    /// </summary>
    public void Restore()
    {
        if (_disposed)
        {
            return;
        }

        // Cancel any in-progress ramp first (outside lock to avoid deadlock)
        _rampCts?.Cancel();

        lock (_lock)
        {
            if (!_isDucked)
            {
                return;
            }

            RestoreInternal();
        }
    }

    /// <summary>
    /// Restores all previously-ducked sessions with a linear ramp over <see cref="RampDownMs"/>.
    /// Falls back to instant restore when <see cref="RampDownMs"/> is 0 or less.
    /// Cancels any in-progress duck ramp. No-op if not currently ducked.
    /// </summary>
    public async Task RestoreAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Cancel any in-progress duck ramp
        _rampCts?.Cancel();

        int rampMs;
        Dictionary<AudioSessionControl, float>? snapshot;

        lock (_lock)
        {
            if (!_isDucked)
            {
                return;
            }

            rampMs = RampDownMs;

            if (rampMs <= 0 || _saved.Count == 0)
            {
                RestoreInternal();
                return;
            }

            // Take a snapshot of sessions to restore (targets = original volumes)
            snapshot = new Dictionary<AudioSessionControl, float>(_saved);

            // Read current (ducked) volumes as the starting point
            foreach (var session in snapshot.Keys.ToList())
            {
                try
                {
                    _targets[session] = session.SimpleAudioVolume.Volume;
                }
                catch
                {
                    // Session may have ended — will be cleaned up in RestoreInternal
                }
            }

            _rampCts = new CancellationTokenSource();
        }

        Log.Information("AudioDucker: restoring {Count} session(s) over {Ms}ms", snapshot.Count, rampMs);

        var ct = _rampCts!.Token;
        const int stepMs = 20;
        int steps = Math.Max(1, rampMs / stepMs);
        var sw = Stopwatch.StartNew();

        try
        {
            for (int i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                float progress = (float)i / steps;

                lock (_lock)
                {
                    foreach (var (session, originalVol) in snapshot)
                    {
                        try
                        {
                            if (_targets.TryGetValue(session, out float duckedVol))
                            {
                                float vol = duckedVol + (originalVol - duckedVol) * progress;
                                session.SimpleAudioVolume.Volume = vol;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "AudioDucker: restore ramp step failed for session");
                        }
                    }
                }

                if (i < steps)
                {
                    await Task.Delay(stepMs, ct).ConfigureAwait(false);
                }
            }

            sw.Stop();
            Log.Information("AudioDucker: restore ramp complete in {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Log.Information("AudioDucker: restore ramp cancelled after {Ms}ms — snapping to original", sw.ElapsedMilliseconds);
        }

        // Final cleanup — ensure volumes are exactly at original and clear state
        lock (_lock)
        {
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

    private void OnRecordingStarted(object? sender, RecordingStartedEventArgs e) => _ = DuckAsync();

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e) => _ = RestoreAsync();

    /// <summary>
    /// Enumerates all active non-self sessions and populates <see cref="_saved"/> and <see cref="_targets"/>.
    /// Must be called inside <see cref="_lock"/>.
    /// </summary>
    private void EnumerateAndSaveSessions()
    {
        _saved.Clear();
        _targets.Clear();

        EnumerateSessions(session =>
        {
            try
            {
                uint pid = session.GetProcessID;
                float current = session.SimpleAudioVolume.Volume;
                float target = Math.Min(current, DuckLevel);
                _saved[session] = current;
                _targets[session] = target;
                Log.Information("AudioDucker: will duck PID={Pid} ({Name}) {From:P0} → {To:P0}",
                    pid, GetProcessName(pid), current, target);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AudioDucker: failed to read session {Session}", GetSessionLabel(session));
            }
        });
    }

    /// <summary>
    /// Sets all sessions to their target volumes instantly. Must be called inside <see cref="_lock"/>.
    /// </summary>
    private void ApplyTargetVolumes()
    {
        foreach (var (session, target) in _targets)
        {
            try
            {
                session.SimpleAudioVolume.Volume = target;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AudioDucker: failed to apply target volume");
            }
        }
    }

    /// <summary>
    /// Enumerates active non-dIKta.me audio sessions across ALL active render
    /// endpoints and invokes <paramref name="action"/> on each.
    /// </summary>
    private void EnumerateSessions(Action<AudioSessionControl> action)
    {
        using var enumerator = new MMDeviceEnumerator();
        MMDeviceCollection devices;
        try
        {
            devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AudioDucker: failed to enumerate render endpoints");
            return;
        }

        int totalSessions = 0;
        int skipped = 0;

        for (int d = 0; d < devices.Count; d++)
        {
            var device = devices[d];
            try
            {
                var sessionManager = device.AudioSessionManager;
                sessionManager.RefreshSessions();
                var sessions = sessionManager.Sessions;

                Log.Debug("AudioDucker: device '{Name}' has {Count} session(s)",
                    device.FriendlyName, sessions.Count);

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    try
                    {
                        if (session.GetProcessID == (uint)_ownPid)
                        {
                            skipped++;
                            continue;
                        }

                        action(session);
                        totalSessions++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "AudioDucker: error inspecting session {Index} on '{Device}'",
                            i, device.FriendlyName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "AudioDucker: failed to enumerate sessions on '{Device}'",
                    device.FriendlyName);
            }
        }

        Log.Information("AudioDucker: enumerated {Total} actionable session(s) across {Devices} device(s), {Skipped} own-PID skipped",
            totalSessions, devices.Count, skipped);
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
        _targets.Clear();
        _isDucked = false;
        Log.Information("AudioDucker: restored {Count} session(s)", restored);
    }

    private static string GetSessionLabel(AudioSessionControl session)
    {
        try { return session.DisplayName; }
        catch { return "?"; }
    }

    private static string GetProcessName(uint pid)
    {
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch { return "?"; }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <summary>Disposes the ducker and restores all sessions.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _rampCts?.Cancel();

        lock (_lock)
        {
            if (_isDucked)
            {
                RestoreInternal();
            }
        }

        _rampCts?.Dispose();
    }
}
