namespace DiktaMe.Core.Vision;

/// <summary>Determines which area of the screen to capture.</summary>
public enum CaptureMode
{
    /// <summary>Capture the currently focused window.</summary>
    ActiveWindow,

    /// <summary>Capture a user-defined rectangular region.</summary>
    Region,

    /// <summary>Capture the active monitor (full screen).</summary>
    FullScreen,

    /// <summary>Capture all monitors (entire virtual screen).</summary>
    AllMonitors,
}

/// <summary>Where the Vision pipeline delivers its result.</summary>
public enum VisionOutputMode
{
    /// <summary>Inject the response text into the active window.</summary>
    Inject,

    /// <summary>Copy the response text to the clipboard.</summary>
    Clipboard,

    /// <summary>Show a toast notification only.</summary>
    ToastOnly,

    /// <summary>Toast notification + inject into active window.</summary>
    ToastInject,

    /// <summary>Toast notification + copy to clipboard.</summary>
    ToastClipboard,
}

/// <summary>
/// Configuration for a single Vision pipeline invocation.
/// </summary>
public sealed record VisionOptions
{
    /// <summary>System prompt sent to the vision-capable LLM.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>Default user query when no spoken instruction is provided.</summary>
    public string DefaultQuery { get; init; } = "Describe what you see and extract any visible text.";

    /// <summary>
    /// Maximum dimension (width or height) in pixels.
    /// Images larger than this are down-scaled before upload.
    /// </summary>
    public int MaxImageDimensionPx { get; init; } = 2048;

    /// <summary>Maximum tokens the model should return.</summary>
    public int MaxResponseTokens { get; init; } = 4096;

    /// <summary>Sampling temperature for the vision model.</summary>
    public double Temperature { get; init; } = 0.3;

    /// <summary>
    /// Override LLM model for this pipeline run.
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>How to deliver the model's response.</summary>
    public VisionOutputMode OutputMode { get; init; } = VisionOutputMode.Inject;
}
