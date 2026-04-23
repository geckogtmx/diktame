
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
            QueryRequestyModelsAsync(cancellationToken),
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

    /// <summary>
    /// Fetches the live model list for a single provider. Primarily used by
    /// the wizard, which lets the user test a newly-typed API key and pick
    /// a specific model BEFORE committing the key to <see cref="SecureStorage"/>.
    /// Pass <paramref name="overrideKey"/> to use a not-yet-persisted key;
    /// otherwise the stored key is used exactly as <see cref="GetAvailableModelsAsync"/>.
    /// </summary>
    /// <param name="providerType">Lowercase type id — "openai", "anthropic", "gemini", "openrouter", "requesty".</param>
    /// <param name="overrideKey">Optional not-yet-persisted key; takes precedence over SecureStorage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cloud models for the provider. Empty list on failure (failures are logged, never thrown).</returns>
    public Task<List<ModelInfo>> GetModelsForProviderAsync(
        string providerType,
        string? overrideKey = null,
        CancellationToken cancellationToken = default)
    {
        return providerType.ToLowerInvariant() switch
        {
            "openai" => QueryOpenAIModelsAsync(cancellationToken, overrideKey),
            "anthropic" => QueryAnthropicModelsAsync(cancellationToken, overrideKey),
            "gemini" => QueryGeminiModelsAsync(cancellationToken, overrideKey),
            "openrouter" => QueryOpenRouterModelsAsync(cancellationToken, overrideKey),
            "requesty" => QueryRequestyModelsAsync(cancellationToken, overrideKey),
            _ => Task.FromResult<List<ModelInfo>>([]),
        };
    }

    // ── OpenAI: GET /v1/models ──────────────────────────────────────────────

    private async Task<List<ModelInfo>> QueryOpenAIModelsAsync(CancellationToken ct, string? overrideKey = null)
    {
        string? apiKey = !string.IsNullOrWhiteSpace(overrideKey)
            ? overrideKey
            : _secureStorage.RetrieveKey("openai");
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

    private async Task<List<ModelInfo>> QueryAnthropicModelsAsync(CancellationToken ct, string? overrideKey = null)
    {
        string? apiKey = !string.IsNullOrWhiteSpace(overrideKey)
            ? overrideKey
            : _secureStorage.RetrieveKey("anthropic");
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

    private async Task<List<ModelInfo>> QueryGeminiModelsAsync(CancellationToken ct, string? overrideKey = null)
    {
        string? apiKey = !string.IsNullOrWhiteSpace(overrideKey)
            ? overrideKey
            : _secureStorage.RetrieveKey("gemini");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            string url = "https://generativelanguage.googleapis.com/v1beta/models";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
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

    private async Task<List<ModelInfo>> QueryOpenRouterModelsAsync(CancellationToken ct, string? overrideKey = null)
    {
        string? apiKey = !string.IsNullOrWhiteSpace(overrideKey)
            ? overrideKey
            : _secureStorage.RetrieveKey("openrouter");
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

    // ── Requesty: GET /v1/models ────────────────────────────────────────────
    // Requesty is an OpenAI-compatible LLM gateway at router.requesty.ai. Its
    // /v1/models endpoint returns the OpenAI-style { data: [...] } envelope
    // with provider/model-style IDs (e.g. "openai/gpt-4o-mini", "anthropic/
    // claude-3.5-sonnet"). Note: those IDs visually collide with OpenRouter's
    // naming scheme, so ResolveProviderFromModelId cannot yet distinguish a
    // user's Requesty selection from an OpenRouter one — BUG-031 follow-up
    // tracks the namespacing fix needed for clean routing.

    private async Task<List<ModelInfo>> QueryRequestyModelsAsync(CancellationToken ct, string? overrideKey = null)
    {
        string? apiKey = !string.IsNullOrWhiteSpace(overrideKey)
            ? overrideKey
            : _secureStorage.RetrieveKey("requesty");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://router.requesty.ai/v1/models");
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
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    // DisplayName falls back to ID when the response omits a human label
                    // (Requesty's /v1/models currently does not expose one).
                    string displayName = model.TryGetProperty("name", out var n)
                        ? n.GetString() ?? id
                        : id;

                    int? contextLength = model.TryGetProperty("context_length", out var cl) && cl.TryGetInt32(out int ctx)
                        ? ctx
                        : null;

                    // Namespace the ID with a "requesty:" prefix so
                    // ResolveProviderFromModelId can distinguish it from
                    // OpenRouter's identical "provider/model" scheme.
                    // LLMProviderFactory strips the prefix before the HTTP call.
                    string namespacedId = id.StartsWith("requesty:", StringComparison.Ordinal)
                        ? id
                        : $"requesty:{id}";

                    models.Add(new ModelInfo
                    {
                        ModelId = namespacedId,
                        DisplayName = displayName,
                        Provider = "Requesty",
                        IsAvailable = true,
                        ContextWindow = contextLength,
                    });
                }
            }

            Log.Debug("ModelListService: Requesty returned {Count} models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ModelListService: Failed to query Requesty models");
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

        // Requesty (namespaced) — MUST come before the "provider/model" slash
        // branch below, since Requesty IDs share that visual scheme.
        if (normalized.StartsWith("requesty:", StringComparison.Ordinal))
        {
            return "requesty";
        }

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

    // ── Filtering ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Filters out models the user has disabled in Cloud LLM settings.
    /// Uses blacklist approach — models not in <paramref name="disabledIds"/> are kept.
    /// </summary>
    public static List<ModelInfo> FilterEnabled(List<ModelInfo> allModels, IReadOnlyCollection<string> disabledIds)
    {
        if (disabledIds.Count == 0)
        {
            return allModels;
        }

        return allModels.Where(m => !disabledIds.Contains(m.ModelId)).ToList();
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
