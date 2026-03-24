namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Mutable context passed to plugins after transcription completes.
/// Plugins can modify <see cref="Transcript"/> before it reaches LLM processing.
/// </summary>
public sealed class AfterTranscriptionContext
{
    public required string RawTranscript { get; init; }
    public required string Mode { get; init; }

    /// <summary>
    /// Plugins can modify the transcript before it reaches LLM processing.
    /// </summary>
    public string Transcript { get; set; } = string.Empty;
}
