namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Injection-time options shared across all pipelines that write text.
/// </summary>
public sealed record PipelineInjectionOptions
{
    /// <summary>
    /// Append a trailing space after the injected text so successive
    /// dictations land with natural word spacing. Default: true.
    /// </summary>
    public bool TrailingSpace { get; init; } = true;

    /// <summary>
    /// Optional key to press after pasting: "enter", "return", or "tab".
    /// Null or empty means no additional key. Default: null.
    /// </summary>
    public string? AdditionalKey { get; init; }

    /// <summary>
    /// HWND of the window that should receive Ctrl+C / Ctrl+V keystrokes.
    /// Captured at hotkey press time; used to restore focus before capture and injection.
    /// <see cref="IntPtr.Zero"/> means no tracking (legacy behaviour).
    /// </summary>
    public IntPtr SourceWindowHandle { get; init; }
}

/// <summary>
/// Options for the Dictation pipeline.
/// </summary>
public sealed record DictationOptions
{
    /// <summary>
    /// System prompt forwarded to the LLM.
    /// Must not be null when <see cref="RawMode"/> is false.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// When true the LLM step is skipped entirely and the raw
    /// transcription is injected as-is (V1 "raw" mode).
    /// </summary>
    public bool RawMode { get; init; }

    /// <summary>
    /// Override LLM model for this pipeline run (Cloud profiles only).
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>Language hint passed to the STT provider (default "en").</summary>
    public string Language { get; init; } = "en";

    /// <summary>Injection behaviour.</summary>
    public PipelineInjectionOptions Injection { get; init; } = new();

    /// <summary>
    /// Audio recording duration in milliseconds (passed from AudioRecorder).
    /// Used to populate PipelineResult.RecordingMs for telemetry.
    /// </summary>
    public long RecordingDurationMs { get; init; }
}

/// <summary>
/// Options for the Refine pipeline.
/// </summary>
public sealed record RefineOptions
{
    /// <summary>
    /// System prompt that contains the instruction for the LLM.
    /// In autopilot mode this is a fixed cleanup prompt.
    /// In instruction mode the spoken instruction is substituted into the prompt.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Override LLM model for this pipeline run (Cloud profiles only).
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>Language hint for audio transcription (default "en").</summary>
    public string Language { get; init; } = "en";

    /// <summary>Injection behaviour.</summary>
    public PipelineInjectionOptions Injection { get; init; } = new();

    /// <summary>
    /// Audio recording duration in milliseconds (0 for Refine Auto mode).
    /// Used to populate PipelineResult.RecordingMs for telemetry.
    /// </summary>
    public long RecordingDurationMs { get; init; }

    /// <summary>
    /// Pre-captured selected text (captured on the UI thread before the pipeline starts).
    /// When non-null, the pipeline skips <c>CaptureSelection</c> and uses this text directly.
    /// This avoids focus race conditions when the source window is DiktaMe itself.
    /// </summary>
    public string? PreCapturedText { get; init; }
}

/// <summary>
/// Options for the Ask pipeline.
/// </summary>
public sealed record AskOptions
{
    /// <summary>System prompt that defines Q&amp;A behaviour.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Override LLM model for this pipeline run (Cloud profiles only).
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>Language hint for audio transcription (default "en").</summary>
    public string Language { get; init; } = "en";

    /// <summary>
    /// Audio recording duration in milliseconds (passed from AudioRecorder).
    /// Used to populate PipelineResult.RecordingMs for telemetry.
    /// </summary>
    public long RecordingDurationMs { get; init; }
}

/// <summary>
/// Options for the Translate pipeline.
/// </summary>
public sealed record TranslateOptions
{
    /// <summary>System prompt that includes the target-language translation instruction.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Override LLM model for this pipeline run (Cloud profiles only).
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Language hint for the STT provider. "auto" enables auto-detection
    /// so the source language need not be known in advance.
    /// </summary>
    public string Language { get; init; } = "auto";

    /// <summary>Injection behaviour.</summary>
    public PipelineInjectionOptions Injection { get; init; } = new();

    /// <summary>
    /// Audio recording duration in milliseconds (passed from AudioRecorder).
    /// Used to populate PipelineResult.RecordingMs for telemetry.
    /// </summary>
    public long RecordingDurationMs { get; init; }
}

/// <summary>
/// Options for the Chat pipeline (Quick Chat overlay).
/// </summary>
public sealed record ChatOptions
{
    /// <summary>System prompt that defines conversational Q&amp;A behaviour.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Override LLM model for this pipeline run (Cloud profiles only).
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// The user's question as plain text.
    /// When set, the STT step is skipped (text was typed in the overlay).
    /// Mutually exclusive with <see cref="AudioFilePath"/>.
    /// </summary>
    public string? TextInput { get; init; }

    /// <summary>
    /// Path to a WAV file recorded via the Mic button.
    /// When set, transcription is performed in raw mode (no LLM cleanup).
    /// Mutually exclusive with <see cref="TextInput"/>.
    /// </summary>
    public string? AudioFilePath { get; init; }

    /// <summary>Language hint for audio transcription (default "en").</summary>
    public string Language { get; init; } = "en";

    /// <summary>
    /// When true, enables web search grounding for Gemini models.
    /// The model will use Google Search to provide up-to-date answers.
    /// </summary>
    public bool WebSearchEnabled { get; init; }
}

/// <summary>
/// Options for the Note pipeline.
/// </summary>
public sealed record NoteOptions
{
    /// <summary>System prompt for note formatting (pass null to skip LLM).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Override LLM model for this pipeline run (Cloud profiles only).
    /// When null, uses the global model from AppSettings.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>Language hint for audio transcription (default "en").</summary>
    public string Language { get; init; } = "en";

    /// <summary>Full path to the notes file (e.g. "%APPDATA%/DiktaMe/notes.md").</summary>
    public required string NotesFilePath { get; init; }

    /// <summary>Timestamp format string for prepended note headers.</summary>
    public string TimestampFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Audio recording duration in milliseconds (passed from AudioRecorder).
    /// Used to populate PipelineResult.RecordingMs for telemetry.
    /// </summary>
    public long RecordingDurationMs { get; init; }
}
