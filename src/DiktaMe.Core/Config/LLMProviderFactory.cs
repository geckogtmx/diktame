
using System.Collections.Concurrent;
using System.Net.Http;
using DiktaMe.Core.LLM;

namespace DiktaMe.Core.Config;
/// <summary>
/// Creates LLM providers by name, pulling API keys from <see cref="Security.SecureStorage"/>
/// and constructing the appropriate concrete type.
/// Caches provider instances by type+model to reuse HTTP connections across pipeline calls.
/// </summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private readonly Security.SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly ConcurrentDictionary<string, ILLMProvider> _cache = new(StringComparer.Ordinal);

    public LLMProviderFactory(Security.SecureStorage secureStorage, SettingsManager settings)
    {
        _secureStorage = secureStorage;
        _settings = settings;
    }

    /// <inheritdoc/>
    public ILLMProvider CreateProvider(string providerType, string? apiKey = null, string? model = null)
        => CreateProvider(providerType, apiKey, model, keepAlive: null);

    /// <summary>
    /// Creates (or retrieves from cache) an LLM provider with an optional keep_alive override.
    /// The override only applies to Ollama providers and controls how long the model stays resident in VRAM.
    /// </summary>
    public ILLMProvider CreateProvider(string providerType, string? apiKey, string? model, string? keepAlive)
    {
        string type = providerType.ToLowerInvariant();

        // Stateless — no caching needed
        if (type is "none" or "skip")
        {
            return new NullLlmProvider();
        }

        string effectiveModel = ResolveModel(type, model);
        string cacheKey = keepAlive is not null
            ? $"{type}:{effectiveModel}:{keepAlive}"
            : $"{type}:{effectiveModel}";

        return _cache.GetOrAdd(cacheKey, _ => CreateProviderCore(type, apiKey, effectiveModel, keepAlive));
    }

    private ILLMProvider CreateProviderCore(string type, string? apiKey, string effectiveModel, string? keepAlive = null)
    {
        // If no key passed, try secure storage
        string? key = apiKey ?? _secureStorage.RetrieveKey(type);

        return type switch
        {
            "gemini" => new GeminiProvider(
                key ?? throw new InvalidOperationException("Gemini API key not configured."),
                model: effectiveModel),

            "anthropic" or "claude" => new AnthropicProvider(
                key ?? throw new InvalidOperationException("Anthropic API key not configured."),
                model: effectiveModel),

            "openai" => OpenAICompatibleProvider.ForOpenAI(
                key ?? throw new InvalidOperationException("OpenAI API key not configured."),
                model: effectiveModel),

            "deepseek" => OpenAICompatibleProvider.ForDeepSeek(
                key ?? throw new InvalidOperationException("DeepSeek API key not configured."),
                model: effectiveModel),

            "openrouter" => OpenAICompatibleProvider.ForOpenRouter(
                key ?? throw new InvalidOperationException("OpenRouter API key not configured."),
                model: effectiveModel),

            "groq" => OpenAICompatibleProvider.ForGroq(
                key ?? throw new InvalidOperationException("Groq API key not configured."),
                model: effectiveModel),

            "together" => OpenAICompatibleProvider.ForTogether(
                key ?? throw new InvalidOperationException("Together AI API key not configured."),
                model: effectiveModel),

            "perplexity" => OpenAICompatibleProvider.ForPerplexity(
                key ?? throw new InvalidOperationException("Perplexity API key not configured."),
                model: effectiveModel),

            "ollama" => CreateOllamaProvider(effectiveModel, keepAlive),

            _ => throw new NotSupportedException($"Unknown LLM provider type: '{type}'."),
        };
    }

    private OllamaProvider CreateOllamaProvider(string effectiveModel, string? keepAliveOverride = null)
    {
        // Persistent HTTP client with keep-alive for connection reuse (matches V1 requests.Session())
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(180), // Vision models may need longer
        };
        http.DefaultRequestHeaders.ConnectionClose = false; // HTTP keep-alive

        return new OllamaProvider(
            model: effectiveModel,
            baseUrl: _settings.Current.OllamaBaseUrl ?? "http://localhost:11434",
            httpClient: http,
            keepAlive: keepAliveOverride ?? _settings.Current.OllamaKeepAlive ?? "10m",
            numCtx: _settings.Current.OllamaNumCtx > 0 ? _settings.Current.OllamaNumCtx : 2048);
    }

    private string ResolveModel(string type, string? model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        return type switch
        {
            "gemini" => "gemini-2.5-flash",
            "anthropic" or "claude" => "claude-3-5-haiku-20241022",
            "openai" => "gpt-4o-mini",
            "deepseek" => "deepseek-chat",
            "openrouter" => "openai/gpt-4o-mini",
            "groq" => "llama-3.3-70b-versatile",
            "together" => "meta-llama/Llama-3.3-70B-Instruct-Turbo",
            "perplexity" => "sonar-pro",
            "ollama" => _settings.Current.OllamaModel,
            _ => model ?? string.Empty,
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
