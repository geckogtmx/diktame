
namespace DiktaMe.Core.LLM;

/// <summary>
/// Metadata for an AI model available via a configured LLM provider.
/// Used by UI to populate model selection dropdowns from real API availability.
/// </summary>
public sealed record ModelInfo
{
    /// <summary>Unique model identifier (e.g., "gpt-4o-mini", "claude-sonnet-4").</summary>
    public required string ModelId { get; init; }

    /// <summary>Human-readable display name for the model.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Provider that hosts this model (e.g., "OpenAI", "Anthropic", "Ollama").</summary>
    public required string Provider { get; init; }

    /// <summary>True if the model is currently available (API key valid, model accessible).</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Optional context window size in tokens.</summary>
    public int? ContextWindow { get; init; }

    /// <summary>Optional additional metadata (e.g., capabilities, pricing tier).</summary>
    public string? Description { get; init; }
}
