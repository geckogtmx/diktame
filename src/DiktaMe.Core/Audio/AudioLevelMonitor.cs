
using Serilog;

namespace DiktaMe.Core.Audio;

/// <summary>
/// Monitors audio input level from PCM data, providing a smoothed
/// 0.0–1.0 value suitable for driving visual effects.
/// Thread-safe: updated from NAudio callback thread, read from UI thread.
/// </summary>
public sealed class AudioLevelMonitor
{
    private volatile float _smoothedLevel;
    private volatile bool _isActive;
    private int _updateCount; // debug: count callbacks for throttled logging

    /// <summary>
    /// Current smoothed audio level (0.0 to 1.0). Thread-safe to read from any thread.
    /// </summary>
    public float SmoothedLevel => _smoothedLevel;

    /// <summary>
    /// Whether the monitor is actively tracking audio input.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Computes peak level from 16-bit PCM data and applies exponential smoothing.
    /// Call from <see cref="AudioRecorder.AudioDataAvailable"/> handler.
    /// </summary>
    public void UpdateLevel(ReadOnlyMemory<byte> pcmData, int bytesRecorded)
    {
        if (bytesRecorded < 2)
        {
            return;
        }

        var span = pcmData.Span;
        int sampleCount = bytesRecorded / 2;
        int maxAbs = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            int offset = i * 2;
            short sample = (short)(span[offset] | (span[offset + 1] << 8));
            int abs = Math.Abs(sample);
            if (abs > maxAbs)
            {
                maxAbs = abs;
            }
        }

        float peak = maxAbs / 32768f; // normalize to 0.0–1.0

        // Voice-meter style: instant attack, fast decay
        // Attack: jump directly to peak (like a real VU meter needle snapping up)
        // Decay: drop ~30% per callback (~100ms at 16kHz) for visible fall-off
        float current = _smoothedLevel;
        _smoothedLevel = peak > current
            ? peak                              // instant attack — snap to peak
            : current * 0.7f + peak * 0.3f;    // fast decay — drops visibly each frame

        // Debug: log every ~10th callback (~1/sec at 16kHz with 1600-sample chunks)
        _updateCount++;
        if (_updateCount % 10 == 0)
        {
            Log.Debug("[VFX] AudioLevelMonitor.UpdateLevel: peak={Peak:F3} smoothed={Smoothed:F3} samples={Samples} bytes={Bytes}",
                peak, _smoothedLevel, sampleCount, bytesRecorded);
        }
    }

    /// <summary>Start tracking (called when recording begins).</summary>
    public void Start()
    {
        _isActive = true;
        _smoothedLevel = 0f;
        _updateCount = 0;
        Log.Information("[VFX] AudioLevelMonitor.Start() — monitoring active");
    }

    /// <summary>Stop tracking and reset level (called when recording stops).</summary>
    public void Stop()
    {
        Log.Information("[VFX] AudioLevelMonitor.Stop() — monitoring stopped after {Count} callbacks", _updateCount);
        _isActive = false;
        _smoothedLevel = 0f;
    }
}
