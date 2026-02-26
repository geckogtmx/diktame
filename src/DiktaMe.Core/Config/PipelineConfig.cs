namespace DiktaMe.Core.Config;

/// <summary>
/// Fixed configuration for utility pipelines (Ask, Refine, Translate, Note, Chat).
/// These are NOT user-creatable — they have fixed behaviors with dual-profile customization.
/// Unlike dictation modes, these pipelines cannot be added/deleted, only configured.
/// </summary>
public sealed record PipelineConfig
{
    /// <summary>Pipeline type (e.g., "ask", "refine", "translate", "note", "chat").</summary>
    public required string PipelineType { get; init; }

    /// <summary>Cloud profile (prompt + model).</summary>
    public required UtilityProfile CloudProfile { get; init; }

    /// <summary>Local profile (prompt only; Ollama model is global).</summary>
    public required UtilityProfile LocalProfile { get; init; }

    /// <summary>Hotkey string (e.g., "Ctrl+Alt+A" for Ask). Null = no hotkey assigned.</summary>
    public string? Hotkey { get; init; }
}

/// <summary>
/// Profile configuration for utility pipelines.
/// Simpler than DictationProfile (no UseLlm flag, LLM is always used except for Note's optional mode).
/// </summary>
public sealed record UtilityProfile
{
    /// <summary>System prompt (required for Ask/Refine/Translate/Chat, optional for Note).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// LLM model name (Cloud profiles only).
    /// Examples: "gpt-4o-mini", "claude-sonnet-4", "gemini-2.0-flash".
    /// For Local profiles, this is null (Ollama uses global model from AppSettings).
    /// </summary>
    public string? ModelName { get; init; }
}
