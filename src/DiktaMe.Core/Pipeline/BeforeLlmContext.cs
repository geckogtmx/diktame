namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Mutable context passed to plugins before LLM processing.
/// Plugins can append to <see cref="AdditionalSystemContext"/> to inject extra instructions.
/// </summary>
public sealed class BeforeLlmContext
{
    public required string UserText { get; init; }
    public required string SystemPrompt { get; init; }
    public required string Mode { get; init; }

    /// <summary>
    /// Plugins append context here. This text is added to the system prompt before LLM processing.
    /// </summary>
    public string AdditionalSystemContext { get; set; } = string.Empty;
}
