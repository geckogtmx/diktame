namespace DiktaMe.Core.Audio;

using NAudio.CoreAudioApi;
using Serilog;

/// <summary>
/// Monitors the mute state of a microphone input device using the Windows
/// Core Audio API (via NAudio's MMDeviceEnumerator).
/// Polls every <see cref="PollIntervalMs"/> milliseconds and raises
/// <see cref="MuteStateChanged"/> when the mute state changes.
/// Port of python/core/mute_detector.py from V1.
/// </summary>
public sealed class MuteDetector : IDisposable
{
    private const int PollIntervalMs = 3_000;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the mute state of the monitored device changes.
    /// </summary>
    public event EventHandler<MuteStateChangedEventArgs>? MuteStateChanged;

    // ── State ────────────────────────────────────────────────────────────────

    private string? _deviceLabel;
    private MMDevice? _deviceCache;
    private bool? _lastKnownMuteState;
    private readonly System.Timers.Timer _pollTimer;
    private bool _disposed;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the mute detector for the specified device.
    /// </summary>
    /// <param name="deviceLabel">
    /// Optional device name substring for fuzzy matching (e.g., "Wave:3").
    /// If null or not found, the system default microphone is used.
    /// </param>
    public MuteDetector(string? deviceLabel = null)
    {
        _deviceLabel = deviceLabel;

        _pollTimer = new System.Timers.Timer(PollIntervalMs) { AutoReset = true };
        _pollTimer.Elapsed += (_, _) => PollOnce();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts polling for mute state changes.
    /// An immediate poll is performed before the first timer interval.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PollOnce(); // immediate first check
        _pollTimer.Start();
        Log.Debug("MuteDetector: started (device='{DeviceLabel}', interval={Interval}ms)",
            _deviceLabel ?? "default", PollIntervalMs);
    }

    /// <summary>Stops polling.</summary>
    public void Stop()
    {
        _pollTimer.Stop();
        Log.Debug("MuteDetector: stopped");
    }

    /// <summary>
    /// Updates the monitored device label and clears the device cache so
    /// the next poll re-discovers the device.
    /// </summary>
    public void UpdateDeviceLabel(string? newLabel)
    {
        if (newLabel == _deviceLabel) return;
        _deviceLabel = newLabel;
        _deviceCache?.Dispose();
        _deviceCache = null;
        Log.Information("MuteDetector: device label updated to '{DeviceLabel}'", newLabel ?? "default");
    }

    /// <summary>
    /// Performs a single mute state check immediately.
    /// Returns <c>null</c> if the state cannot be determined.
    /// </summary>
    public bool? CheckMuteState()
    {
        try
        {
            MMDevice? device = GetOrResolveDevice();
            if (device is null) return null;

            return device.AudioEndpointVolume.Mute;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MuteDetector: error checking mute state; clearing device cache");
            _deviceCache?.Dispose();
            _deviceCache = null;
            return null;
        }
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private void PollOnce()
    {
        bool? current = CheckMuteState();
        if (current is null) return;

        if (current != _lastKnownMuteState)
        {
            bool previous = _lastKnownMuteState ?? !current.Value;
            _lastKnownMuteState = current;
            Log.Information("MuteDetector: mute state changed → {IsMuted}", current);
            MuteStateChanged?.Invoke(this, new MuteStateChangedEventArgs(current.Value, previous));
        }
    }

    private MMDevice? GetOrResolveDevice()
    {
        if (_deviceCache is not null)
            return _deviceCache;

        using var enumerator = new MMDeviceEnumerator();

        // Try fuzzy label match on all active capture (input) devices
        if (!string.IsNullOrWhiteSpace(_deviceLabel))
        {
            MMDeviceCollection inputs = enumerator.EnumerateAudioEndPoints(
                DataFlow.Capture, DeviceState.Active);

            foreach (MMDevice device in inputs)
            {
                if (device.FriendlyName.Contains(_deviceLabel, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("MuteDetector: matched device '{FriendlyName}'", device.FriendlyName);
                    _deviceCache = device;
                    return _deviceCache;
                }
                else
                {
                    device.Dispose();
                }
            }

            Log.Warning("MuteDetector: device '{DeviceLabel}' not found; falling back to default", _deviceLabel);
        }

        // Fall back to system default microphone
        try
        {
            _deviceCache = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return _deviceCache;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MuteDetector: no default microphone available");
            return null;
        }
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer.Stop();
        _pollTimer.Dispose();

        _deviceCache?.Dispose();
        _deviceCache = null;
    }
}

// ── Event args ────────────────────────────────────────────────────────────────

/// <summary>Event data for <see cref="MuteDetector.MuteStateChanged"/>.</summary>
public sealed class MuteStateChangedEventArgs(bool isMuted, bool wasMuted) : EventArgs
{
    /// <summary>The new mute state.</summary>
    public bool IsMuted { get; } = isMuted;

    /// <summary>The previous mute state.</summary>
    public bool WasMuted { get; } = wasMuted;
}
