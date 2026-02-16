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
    Task<LlmResult> ProcessAsync(string text, string systemPrompt, string mode = "dictate");

    /// <summary>
    /// Checks whether this provider is currently reachable and configured.
    /// Should not throw — returns <c>false</c> on any error.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Display name of this provider, including the model, e.g. "GPT-4o-mini (OpenAI)".
    /// </summary>
    string ProviderName { get; }
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

    /// <summary>Whether the result contains non-empty output text.</summary>
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Text);
}
