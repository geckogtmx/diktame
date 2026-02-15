namespace DiktaMe.Core.STT;

/// <summary>
/// The core abstraction for Speech-to-Text providers.
/// Implemented by Deepgram, Gemini Audio, and Whisper.net.
/// </summary>
public interface ISTTProvider
{
    /// <summary>
    /// Transcribes an audio file to text.
    /// </summary>
    /// <param name="audioFilePath">Path to the WAV audio file (16kHz, 16-bit mono).</param>
    /// <param name="language">Language code (e.g., "en", "es", "auto").</param>
    /// <returns>The transcription result.</returns>
    Task<TranscriptionResult> TranscribeAsync(string audioFilePath, string language = "en");

    /// <summary>
    /// Checks whether this provider is currently available and configured.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Gets the display name of this provider (e.g., "Deepgram Nova-2").
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Represents the result of a speech-to-text transcription.
/// </summary>
public sealed record TranscriptionResult
{
    /// <summary>
    /// The transcribed text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The detected language code (e.g., "en", "es").
    /// </summary>
    public string? DetectedLanguage { get; init; }

    /// <summary>
    /// Transcription latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// The provider that performed the transcription.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Whether the transcription was successful.
    /// </summary>
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Text);
}
