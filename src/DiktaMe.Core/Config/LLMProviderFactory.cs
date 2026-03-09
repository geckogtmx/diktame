
using DiktaMe.Core.LLM;

namespace DiktaMe.Core.Config;
/// <summary>
/// Creates LLM providers by name, pulling API keys from <see cref="Security.SecureStorage"/>
/// and constructing the appropriate concrete type.
/// </summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private readonly Security.SecureStorage _secureStorage;
    private readonly SettingsManager _settings;

    public LLMProviderFactory(Security.SecureStorage secureStorage, SettingsManager settings)
    {
        _secureStorage = secureStorage;
        _settings = settings;
    }

    /// <inheritdoc/>
    public ILLMProvider CreateProvider(string providerType, string? apiKey = null, string? model = null)
    {
        string type = providerType.ToLowerInvariant();

        // If no key passed, try secure storage
        string? key = apiKey ?? _secureStorage.RetrieveKey(type);

        return type switch
        {
            "gemini" => new GeminiProvider(
                key ?? throw new InvalidOperationException("Gemini API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash" : model),

            "anthropic" or "claude" => new AnthropicProvider(
                key ?? throw new InvalidOperationException("Anthropic API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "claude-3-5-haiku-20241022" : model),

            "openai" => OpenAICompatibleProvider.ForOpenAI(
                key ?? throw new InvalidOperationException("OpenAI API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model),

            "deepseek" => OpenAICompatibleProvider.ForDeepSeek(
                key ?? throw new InvalidOperationException("DeepSeek API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "deepseek-chat" : model),

            "openrouter" => OpenAICompatibleProvider.ForOpenRouter(
                key ?? throw new InvalidOperationException("OpenRouter API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "openai/gpt-4o-mini" : model),

            "groq" => OpenAICompatibleProvider.ForGroq(
                key ?? throw new InvalidOperationException("Groq API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "llama-3.3-70b-versatile" : model),

            "together" => OpenAICompatibleProvider.ForTogether(
                key ?? throw new InvalidOperationException("Together AI API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "meta-llama/Llama-3.3-70B-Instruct-Turbo" : model),

            "perplexity" => OpenAICompatibleProvider.ForPerplexity(
                key ?? throw new InvalidOperationException("Perplexity API key not configured."),
                model: string.IsNullOrWhiteSpace(model) ? "sonar-pro" : model),

            "ollama" => new OllamaProvider(
                model: string.IsNullOrWhiteSpace(model) ? _settings.Current.OllamaModel : model),

            "none" or "skip" => new NullLlmProvider(),

            _ => throw new NotSupportedException($"Unknown LLM provider type: '{providerType}'."),
        };
    }
}

/// <summary>
/// A no-op LLM provider that always returns an empty result.
/// Used when the user selects "Skip LLM" for a mode.
/// </summary>
internal sealed class NullLlmProvider : ILLMProvider
{
    public string ProviderName => "None";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmResult { Text = string.Empty, Provider = ProviderName });

    public Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmResult { Text = string.Empty, Provider = ProviderName });
}
