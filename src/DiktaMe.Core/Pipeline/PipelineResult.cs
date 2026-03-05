namespace DiktaMe.Core.Pipeline;

/// <summary>
/// The result produced by any pipeline after completing a full run.
/// Contains both the output text and a breakdown of stage latencies.
/// </summary>
public sealed record PipelineResult
{
    /// <summary>The final text that was (or would be) injected.</summary>
    public required string Text { get; init; }

    /// <summary>The raw transcription before LLM processing.</summary>
    public string? RawTranscript { get; init; }

    /// <summary>Whether the pipeline completed without error.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Human-readable error description when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The pipeline mode that produced this result (e.g. "dictate", "ask").</summary>
    public required string Mode { get; init; }

    // ── Per-stage latencies (0 when stage was skipped) ────────────────────

    /// <summary>
    /// Audio recording duration in milliseconds.
    /// 0 if no recording (e.g., Refine Auto, text-only Chat).
    /// </summary>
    public long RecordingMs { get; init; }

    /// <summary>Milliseconds spent transcribing audio.</summary>
    public long TranscriptionMs { get; init; }

    /// <summary>Milliseconds spent on LLM processing.</summary>
    public long ProcessingMs { get; init; }

    /// <summary>Milliseconds spent injecting text.</summary>
    public long InjectionMs { get; init; }

    /// <summary>Total wall-clock milliseconds for the entire pipeline.</summary>
    public long TotalMs { get; init; }

    /// <summary>Name of the STT provider that performed the transcription.</summary>
    public string? SttProvider { get; init; }

    /// <summary>Name of the LLM provider that processed the text (null if raw/skipped).</summary>
    public string? LlmProvider { get; init; }

    /// <summary>Number of words in <see cref="Text"/>.</summary>
    public int WordCount => string.IsNullOrWhiteSpace(Text)
        ? 0
        : Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Number of characters in <see cref="Text"/>.</summary>
    public int CharCount => Text?.Length ?? 0;

    // ── Factories ─────────────────────────────────────────────────────────

    internal static PipelineResult Failure(string mode, string error) => new()
    {
        Text = string.Empty,
        Mode = mode,
        IsSuccess = false,
        ErrorMessage = error,
    };
}
