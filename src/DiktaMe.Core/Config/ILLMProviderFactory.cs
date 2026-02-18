
using DiktaMe.Core.LLM;

namespace DiktaMe.Core.Config;
/// <summary>
/// Creates <see cref="ILLMProvider"/> instances based on provider type, API key, and model.
/// Used by <see cref="PipelineFactory"/> to build mode-aware providers from settings.
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>
    /// Creates an LLM provider for the given type.
    /// </summary>
    /// <param name="providerType">
    /// Provider type name: "gemini", "openai", "anthropic", "deepseek", "openrouter",
    /// "groq", "together", "perplexity", "ollama", "none".
    /// </param>
    /// <param name="apiKey">API key (required for cloud providers; ignored for Ollama).</param>
    /// <param name="model">Model identifier override. Empty = provider default.</param>
    ILLMProvider CreateProvider(string providerType, string? apiKey = null, string? model = null);
}
