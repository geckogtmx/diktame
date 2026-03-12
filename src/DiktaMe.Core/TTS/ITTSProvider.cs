namespace DiktaMe.Core.TTS;

/// <summary>
/// Abstraction for text-to-speech synthesis providers.
/// Implemented by KokoroTtsProvider (local), DeepgramTtsProvider,
/// InworldTtsProvider, and OpenAITtsProvider (cloud).
/// Follows the same pattern as <see cref="LLM.ILLMProvider"/>.
/// </summary>
public interface ITTSProvider : IDisposable
{
    /// <summary>
    /// Display name of this provider, e.g. "Kokoro-ONNX (Local)" or "Deepgram Aura-2".
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this provider supports streaming audio generation
    /// (audio starts playing before full text is synthesized).
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Synthesize text to audio bytes (full generation).
    /// </summary>
    /// <param name="text">The cleaned text to speak.</param>
    /// <param name="voiceId">Provider-specific voice identifier. Null = provider default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audio data with metadata.</returns>
    Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this provider is currently reachable and configured.
    /// Should not throw — returns <c>false</c> on any error.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
