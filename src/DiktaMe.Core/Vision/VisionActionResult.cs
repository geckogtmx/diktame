namespace DiktaMe.Core.Vision;

/// <summary>What the user chose to do with the captured image.</summary>
public enum VisionAction
{
    /// <summary>Save screenshot to file — no AI inference.</summary>
    Save,

    /// <summary>Run vision pipeline, copy result to clipboard.</summary>
    Clipboard,

    /// <summary>Open QuickChat with image attached as conversation context.</summary>
    Chat,

    /// <summary>Run vision pipeline, then record voice note — full multimodal note entry.</summary>
    Note,

    /// <summary>Dedicated OCR — extract text exactly as written, copy to clipboard.</summary>
    Ocr,

    /// <summary>Extract tabular data as TSV, copy to clipboard.</summary>
    Table,
}

/// <summary>
/// Result returned by VisionActionWindow after the user picks an action.
/// Null result means the user cancelled.
/// </summary>
public sealed record VisionActionResult(
    VisionAction Action,
    string? UserQuery,
    bool UseLocal,
    bool SkipAi = false);
