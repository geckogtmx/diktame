
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.Core.LLM;

/// <summary>
/// Queries all configured LLM providers to discover available models via real API calls.
/// Merges results from multiple concurrent API providers for UI model selection dropdowns.
/// </summary>
public sealed class ModelListService : IDisposable
{
    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly HttpClient _http;
    private bool _disposed;

    public ModelListService(SecureStorage secureStorage, SettingsManager settings, HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>
    /// Queries all providers with valid API keys and returns a merged list of available models.
    /// Each provider is queried in parallel; failures are logged and skipped.
    /// </summary>
    public async Task<List<ModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<List<ModelInfo>>>
        {
            QueryOpenAIModelsAsync(cancellationToken),
            QueryAnthropicModelsAsync(cancellationToken),
            QueryGeminiModelsAsync(cancellationToken),
            QueryOpenRouterModelsAsync(cancellationToken),
            QueryOllamaModelsAsync(cancellationToken),
        };

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var allModels = new List<ModelInfo>();
        foreach (var providerModels in results)
        {
            allModels.AddRange(providerModels);
        }

        Log.Information("ModelListService: Discovered {Count} models from {Providers} providers",
            allModels.Count, results.Count(r => r.Count > 0));

        return allModels;
    }

    // ── OpenAI: GET /v1/models ──────────────────────────────────────────────

    private async Task<List<ModelInfo>> QueryOpenAIModelsAsync(CancellationToken ct)
    {
        string? apiKey = _secureStorage.RetrieveKey("openai");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var models = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var model in data.EnumerateArray())
                {
                    string id = model.GetProperty("id").GetString() ?? "";

                    // Only include chat-capable models (gpt-*, o1-*, o3-*, chatgpt-*)
                    if (!IsChatModel(id))
                    {
                        continue;
                    }

                    models.Add(new ModelInfo
                    {
                        ModelId = id,
                        DisplayName = id,
                        Provider = "OpenAI",
                        IsAvailable = true,
                    });
                }
            }

            Log.Debug("ModelListService: OpenAI returned {Count} chat models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ModelListService: Failed to query OpenAI models");
            return [];
        }
    }

    // ── Anthropic: GET /v1/models ───────────────────────────────────────────

    private async Task<List<ModelInfo>> QueryAnthropicModelsAsync(CancellationToken ct)
    {
        string? apiKey = _secureStorage.RetrieveKey("anthropic");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var models = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var model in data.EnumerateArray())
                {
                    string id = model.GetProperty("id").GetString() ?? "";
                    string displayName = model.TryGetProperty("display_name", out var dn)
                        ? dn.GetString() ?? id
                        : id;

                    models.Add(new ModelInfo
                    {
                        ModelId = id,
                        DisplayName = displayName,
                        Provider = "Anthropic",
                        IsAvailable = true,
                    });
                }
            }

            Log.Debug("ModelListService: Anthropic returned {Count} models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ModelListService: Failed to query Anthropic models");
            return [];
        }
    }

    // ── Google Gemini: GET /v1beta/models ────────────────────────────────────

    private async Task<List<ModelInfo>> QueryGeminiModelsAsync(CancellationToken ct)
    {
        string? apiKey = _secureStorage.RetrieveKey("gemini");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var models = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var data))
            {
                foreach (var model in data.EnumerateArray())
                {
                    string name = model.GetProperty("name").GetString() ?? "";
                    string displayName = model.TryGetProperty("displayName", out var dn)
                        ? dn.GetString() ?? name
                        : name;

                    // name is "models/gemini-2.5-flash" — strip prefix for clean model ID
                    string id = name.StartsWith("models/", StringComparison.Ordinal)
                        ? name["models/".Length..]
                        : name;

                    // Only include generateContent-capable models
                    if (model.TryGetProperty("supportedGenerationMethods", out var methods))
                    {
                        bool supportsChat = false;
                        foreach (var method in methods.EnumerateArray())
                        {
                            if (string.Equals(method.GetString(), "generateContent", StringComparison.Ordinal))
                            {
                                supportsChat = true;
                                break;
                            }
                        }

                        if (!supportsChat)
                        {
                            continue;
                        }
                    }

                    // Exclude non-text models (image gen, robotics, embeddings)
                    // that support generateContent but aren't useful for text chat
                    if (IsNonTextGeminiModel(id))
                    {
                        continue;
                    }

                    models.Add(new ModelInfo
                    {
                        ModelId = id,
                        DisplayName = displayName,
                        Provider = "Google",
                        IsAvailable = true,
                    });
                }
            }

            Log.Debug("ModelListService: Gemini returned {Count} models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ModelListService: Failed to query Gemini models");
            return [];
        }
    }

    // ── OpenRouter: GET /api/v1/models ──────────────────────────────────────

    private async Task<List<ModelInfo>> QueryOpenRouterModelsAsync(CancellationToken ct)
    {
        string? apiKey = _secureStorage.RetrieveKey("openrouter");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var models = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var model in data.EnumerateArray())
                {
                    string id = model.GetProperty("id").GetString() ?? "";
                    string displayName = model.TryGetProperty("name", out var n)
                        ? n.GetString() ?? id
                        : id;

                    // Only include models that output text (exclude image/audio generators)
                    if (model.TryGetProperty("architecture", out var arch)
                        && arch.TryGetProperty("modality", out var modality))
                    {
                        string mod = modality.GetString() ?? "";
                        if (!mod.EndsWith("text", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    int? contextLength = model.TryGetProperty("context_length", out var cl)
                        ? cl.GetInt32()
                        : null;

                    models.Add(new ModelInfo
                    {
                        ModelId = id,
                        DisplayName = displayName,
                        Provider = "OpenRouter",
                        IsAvailable = true,
                        ContextWindow = contextLength,
                    });
                }
            }

            Log.Debug("ModelListService: OpenRouter returned {Count} models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ModelListService: Failed to query OpenRouter models");
            return [];
        }
    }

    // ── Ollama: GET /api/tags ───────────────────────────────────────────────

    private async Task<List<ModelInfo>> QueryOllamaModelsAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync("http://localhost:11434/api/tags", ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var models = new List<ModelInfo>();
            if (doc.RootElement.TryGetProperty("models", out var data))
            {
                foreach (var model in data.EnumerateArray())
                {
                    string name = model.GetProperty("name").GetString() ?? "";

                    models.Add(new ModelInfo
                    {
                        ModelId = name,
                        DisplayName = name,
                        Provider = "Ollama (Local)",
                        IsAvailable = true,
                    });
                }
            }

            Log.Debug("ModelListService: Ollama returned {Count} local models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ModelListService: Failed to query Ollama models (is Ollama running?)");
            return [];
        }
    }

    // ── Provider resolution ─────────────────────────────────────────────────

    /// <summary>
    /// Resolves which provider should handle a given model ID based on name prefix matching.
    /// Used by <see cref="LLMRouter"/> to dynamically create the correct provider for per-mode model selection.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "gpt-4o-mini").</param>
    /// <returns>The provider type name ("openai", "anthropic", "gemini", "openrouter", "ollama").</returns>
    public static string ResolveProviderFromModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
        }

        // Normalize to lowercase for prefix matching
        string normalized = modelId.ToLowerInvariant();

        // OpenAI models
        if (normalized.StartsWith("gpt-", StringComparison.Ordinal) ||
            normalized.StartsWith("o1-", StringComparison.Ordinal) ||
            normalized.StartsWith("o3-", StringComparison.Ordinal) ||
            normalized.StartsWith("o4-", StringComparison.Ordinal) ||
            normalized.StartsWith("chatgpt-", StringComparison.Ordinal))
        {
            return "openai";
        }

        // Anthropic models
        if (normalized.StartsWith("claude-", StringComparison.Ordinal))
        {
            return "anthropic";
        }

        // Google models
        if (normalized.StartsWith("gemini-", StringComparison.Ordinal) ||
            normalized.StartsWith("models/gemini-", StringComparison.Ordinal))
        {
            return "gemini";
        }

        // DeepSeek models
        if (normalized.StartsWith("deepseek-", StringComparison.Ordinal))
        {
            return "deepseek";
        }

        // OpenRouter models (provider/model format like "anthropic/claude-3-opus")
        if (normalized.Contains('/', StringComparison.Ordinal))
        {
            return "openrouter";
        }

        // Default to Ollama for unrecognized models (local models can have arbitrary names)
        Log.Debug("ModelListService: Unknown model prefix '{ModelId}', defaulting to Ollama", modelId);
        return "ollama";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the Gemini model ID looks like a non-text model
    /// (image generation, robotics, embeddings, etc.) that supports generateContent
    /// but isn't useful for multi-turn text chat.
    /// </summary>
    private static bool IsNonTextGeminiModel(string modelId)
    {
        string lower = modelId.ToLowerInvariant();
        return lower.Contains("imagen", StringComparison.Ordinal)
            || lower.Contains("robotics", StringComparison.Ordinal)
            || lower.Contains("embedding", StringComparison.Ordinal)
            || lower.Contains("aqa", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the model ID looks like a chat-capable OpenAI model.
    /// Filters out embedding, TTS, whisper, DALL-E, and moderation models.
    /// </summary>
    private static bool IsChatModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        string lower = modelId.ToLowerInvariant();

        // Include: gpt-*, o1-*, o3-*, o4-*, chatgpt-*
        if (lower.StartsWith("gpt-", StringComparison.Ordinal) ||
            lower.StartsWith("o1-", StringComparison.Ordinal) ||
            lower.StartsWith("o3-", StringComparison.Ordinal) ||
            lower.StartsWith("o4-", StringComparison.Ordinal) ||
            lower.StartsWith("chatgpt-", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _http.Dispose();
            _disposed = true;
        }
    }
}
