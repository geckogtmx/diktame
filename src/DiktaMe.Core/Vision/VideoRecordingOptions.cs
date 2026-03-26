namespace DiktaMe.Core.Vision;

/// <summary>
/// Configuration for a video recording session.
/// </summary>
public sealed record VideoRecordingOptions
{
    /// <summary>Maximum recording duration in seconds. Default 120s (2 minutes).</summary>
    public int MaxDurationSeconds { get; init; } = 120;

    /// <summary>Target frame rate for recording. Default 30 FPS.</summary>
    public int FrameRateHz { get; init; } = 30;

    /// <summary>Video bitrate in kbps. Default 5000 (5 Mbps).</summary>
    public int BitrateKbps { get; init; } = 5000;

    /// <summary>Capture microphone audio and mux into MP4. Default true (V2).</summary>
    public bool EnableMicAudio { get; init; } = true;
}
