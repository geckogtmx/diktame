namespace DiktaMe.Core.LLM;

/// <summary>
/// The core abstraction for LLM providers.
/// Implemented by Gemini, Anthropic, OpenAI, and Ollama.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Processes text through the LLM with a system prompt and mode context.
    /// </summary>
    /// <param name="text">The input text to process.</param>
    /// <param name="systemPrompt">The system prompt defining behavior.</param>
    /// <param name="mode">The workflow mode context (e.g., "dictate", "refine", "ask").</param>
    /// <returns>The processed text result.</returns>
    Task<string> ProcessAsync(string text, string systemPrompt, string mode);

    /// <summary>
    /// Checks whether this provider is currently available and configured.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Gets the display name of this provider (e.g., "Gemini 2.0 Flash").
    /// </summary>
    string ProviderName { get; }
}
