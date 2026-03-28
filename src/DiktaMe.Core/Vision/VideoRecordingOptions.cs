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

    /// <summary>Capture system/desktop audio via WASAPI loopback. Default true (V4). Mixes with mic if both enabled.</summary>
    public bool EnableSystemAudio { get; init; } = true;

    /// <summary>Enable webcam overlay (picture-in-picture bubble) in the recording. Default false.</summary>
    public bool EnableWebcam { get; init; }

    /// <summary>Webcam bubble size in pixels (width and height — circular crop). Default 200.</summary>
    public int WebcamBubbleSize { get; init; } = 200;

    /// <summary>Webcam device name/path. Null = auto-select first available camera.</summary>
    public string? WebcamDeviceName { get; init; }

    /// <summary>Webcam overlay position. Default "bottom-right". Values: "bottom-right", "bottom-left", "top-right", "top-left".</summary>
    public string WebcamPosition { get; init; } = "bottom-right";
}
