namespace DiktaMe.Core.LLM;

/// <summary>
/// The core abstraction for LLM text-processing providers.
/// Implemented by <see cref="OpenAICompatibleProvider"/> (covers OpenAI, DeepSeek,
/// OpenRouter, Groq, Together AI, Fireworks, Perplexity, Azure OpenAI, LM Studio,
/// vLLM, and any other OpenAI-compatible endpoint), <see cref="GeminiProvider"/>,
/// <see cref="AnthropicProvider"/>, and <see cref="OllamaProvider"/>.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Processes input text through the LLM.
    /// </summary>
    /// <param name="text">The raw input text (e.g. transcribed speech).</param>
    /// <param name="systemPrompt">System-level instruction defining the model's behaviour.</param>
    /// <param name="mode">
    /// Workflow mode hint for logging/metrics (e.g. "dictate", "refine", "ask").
    /// Does not alter the request — callers are responsible for selecting the right prompt.
    /// </param>
    /// <returns>The processing result including output text, latency, and token counts.</returns>
    Task<LlmResult> ProcessAsync(string text, string systemPrompt, string mode = "dictate",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a multi-turn conversation through the LLM.
    /// Each provider builds the correct API-specific JSON from the conversation history.
    /// </summary>
    /// <param name="history">Ordered list of conversation turns (user/assistant alternating).</param>
    /// <param name="systemPrompt">System-level instruction defining the model's behaviour.</param>
    /// <param name="mode">Workflow mode hint for logging/metrics.</param>
    /// <returns>The processing result for the latest assistant response.</returns>
    Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this provider is currently reachable and configured.
    /// Should not throw — returns <c>false</c> on any error.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Display name of this provider, including the model, e.g. "GPT-4o-mini (OpenAI)".
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Processes an image with optional text query using a multimodal LLM.
    /// Default implementation throws NotSupportedException — providers opt in by overriding.
    /// </summary>
    Task<LlmResult> ProcessWithImageAsync(byte[] imageData, string mimeType,
        string text, string systemPrompt, string mode = "vision",
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not support multimodal/vision requests.");
    }
}

/// <summary>
/// The result of an LLM text-processing call.
/// Mirrors <c>TranscriptionResult</c> from the STT layer for consistency.
/// </summary>
public sealed record LlmResult
{
    /// <summary>The processed output text from the LLM.</summary>
    public required string Text { get; init; }

    /// <summary>The provider that produced this result.</summary>
    public required string Provider { get; init; }

    /// <summary>End-to-end latency in milliseconds (HTTP round-trip).</summary>
    public long LatencyMs { get; init; }

    /// <summary>Number of input tokens consumed, if reported by the provider.</summary>
    public int? InputTokens { get; init; }

    /// <summary>Number of output tokens generated, if reported by the provider.</summary>
    public int? OutputTokens { get; init; }

    /// <summary>Inference speed in tokens/sec, if reported by the provider (Ollama).</summary>
    public double? TokensPerSec { get; init; }

    /// <summary>Whether the result contains non-empty output text.</summary>
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Text);
}

/// <summary>A single turn in a conversation.</summary>
/// <param name="Role">"user" or "assistant".</param>
/// <param name="Content">The message text.</param>
/// <param name="ImageData">Optional image bytes (PNG/JPEG) for multimodal turns.</param>
/// <param name="ImageMimeType">MIME type of the image (e.g. "image/png").</param>
public sealed record ConversationTurn(
    string Role,
    string Content,
    byte[]? ImageData = null,
    string? ImageMimeType = null);

/// <summary>
/// Extension methods for <see cref="ILLMProvider"/> that provide model-aware routing.
/// </summary>
public static class LLMProviderExtensions
{
    /// <summary>
    /// Processes text through the LLM with optional per-mode model override.
    /// If the provider is a <see cref="LLMRouter"/> and a model name is specified,
    /// uses the model-aware overload. Otherwise falls back to the standard call.
    /// </summary>
    public static Task<LlmResult> ProcessWithModelAsync(
        this ILLMProvider provider,
        string text,
        string systemPrompt,
        string? modelName,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        if (provider is LLMRouter router && !string.IsNullOrWhiteSpace(modelName))
        {
            return router.ProcessAsync(text, systemPrompt, modelName, mode, cancellationToken);
        }

        return provider.ProcessAsync(text, systemPrompt, mode, cancellationToken);
    }

    /// <summary>
    /// Processes an image through the LLM with optional per-mode model override.
    /// If the provider is a <see cref="LLMRouter"/> and a model name is specified,
    /// uses the model-aware overload. Otherwise falls back to the standard call.
    /// </summary>
    public static Task<LlmResult> ProcessImageWithModelAsync(
        this ILLMProvider provider, byte[] imageData, string mimeType,
        string text, string systemPrompt, string? modelName,
        string mode = "vision", CancellationToken cancellationToken = default)
    {
        if (provider is LLMRouter router && !string.IsNullOrWhiteSpace(modelName))
        {
            return router.ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, modelName, mode, cancellationToken);
        }

        return provider.ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, mode, cancellationToken);
    }

    /// <summary>
    /// Processes a multi-turn conversation with optional per-mode model override.
    /// If the provider is a <see cref="LLMRouter"/> and a model name is specified,
    /// uses the model-aware overload. Otherwise falls back to the standard call.
    /// </summary>
    public static Task<LlmResult> ProcessConversationWithModelAsync(
        this ILLMProvider provider,
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string? modelName,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        if (provider is LLMRouter router && !string.IsNullOrWhiteSpace(modelName))
        {
            return router.ProcessConversationAsync(history, systemPrompt, modelName, mode, cancellationToken);
        }

        return provider.ProcessConversationAsync(history, systemPrompt, mode, cancellationToken);
    }
}
