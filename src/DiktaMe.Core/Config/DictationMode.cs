namespace DiktaMe.Core.Config;

/// <summary>
/// User-configurable dictation mode with dual-profile support.
/// Represents a named workflow (e.g., "Standard", "Professional", "Custom Interview Mode").
/// Port of V1's mode system, now with CRUD support and per-profile model selection.
/// </summary>
public sealed record DictationMode
{
    /// <summary>Unique ID (GUID) for this mode.</summary>
    public required string Id { get; init; }

    /// <summary>User-visible title (e.g., "Standard", "Professional", "Interview Notes").</summary>
    public required string Title { get; init; }

    /// <summary>Cloud profile configuration (Cloud STT + Cloud LLM).</summary>
    public required DictationProfile CloudProfile { get; init; }

    /// <summary>Local profile configuration (Whisper + Ollama).</summary>
    public required DictationProfile LocalProfile { get; init; }

    /// <summary>Whether this is a built-in mode (cannot be deleted).</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Sort order for UI display (0-based).</summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// Profile-specific configuration for a dictation mode.
/// Cloud profiles support per-mode model selection; Local profiles use global Ollama model.
/// </summary>
public sealed record DictationProfile
{
    /// <summary>System prompt for LLM processing (or null for raw mode).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Whether to use LLM processing (false = raw transcription).</summary>
    public bool UseLlm { get; init; } = true;

    /// <summary>
    /// LLM model name (Cloud profiles only).
    /// Examples: "gpt-4o", "claude-sonnet-4", "gemini-2.0-flash".
    /// For Local profiles, this is ignored (Ollama uses global model from AppSettings).
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>Hotkey string (e.g., "Ctrl+Alt+D"). Null = no hotkey assigned.</summary>
    public string? Hotkey { get; init; }
}
