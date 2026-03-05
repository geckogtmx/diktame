namespace DiktaMe.Core.STT;

/// <summary>
/// Abstraction for real-time streaming Speech-to-Text providers.
/// Fundamentally different from <see cref="ISTTProvider"/> (batch):
/// streaming is event-driven with partial/final transcript callbacks,
/// while batch is request/response with a file path.
/// </summary>
public interface IStreamingSTTProvider : IAsyncDisposable
{
    /// <summary>
    /// Opens a streaming connection to the STT service.
    /// </summary>
    /// <param name="language">Language code (e.g., "en", "es"). "auto" for detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chunk of raw PCM audio data to the streaming service.
    /// </summary>
    /// <param name="pcmData">Raw PCM audio bytes (16kHz, 16-bit, mono).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAudioAsync(ReadOnlyMemory<byte> pcmData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals end of audio and waits for remaining final transcripts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the streaming connection is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>Display name of this provider (e.g., "Deepgram nova-3 Streaming").</summary>
    string ProviderName { get; }

    /// <summary>Raised when an interim (partial) transcript is received. May change as more audio arrives.</summary>
    event EventHandler<StreamingTranscriptEventArgs>? PartialTranscriptReceived;

    /// <summary>Raised when a finalized transcript segment is received. Will not change.</summary>
    event EventHandler<StreamingTranscriptEventArgs>? FinalTranscriptReceived;

    /// <summary>Raised when the streaming connection encounters an error.</summary>
    event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
}

/// <summary>
/// Event data for <see cref="IStreamingSTTProvider.PartialTranscriptReceived"/> and
/// <see cref="IStreamingSTTProvider.FinalTranscriptReceived"/>.
/// </summary>
public sealed class StreamingTranscriptEventArgs(string transcript, bool isFinal, double confidence, TimeSpan duration) : EventArgs
{
    /// <summary>The transcript text for this segment.</summary>
    public string Transcript { get; } = transcript;

    /// <summary>Whether this is a final (immutable) transcript or an interim (partial) result.</summary>
    public bool IsFinal { get; } = isFinal;

    /// <summary>Confidence score (0.0–1.0) if provided by the service.</summary>
    public double Confidence { get; } = confidence;

    /// <summary>Audio duration covered by this transcript segment.</summary>
    public TimeSpan Duration { get; } = duration;
}

/// <summary>
/// Event data for <see cref="IStreamingSTTProvider.ErrorOccurred"/>.
/// </summary>
public sealed class StreamingErrorEventArgs(string message, Exception? exception = null) : EventArgs
{
    /// <summary>Human-readable error description.</summary>
    public string Message { get; } = message;

    /// <summary>The underlying exception, if any.</summary>
    public Exception? Exception { get; } = exception;
}
