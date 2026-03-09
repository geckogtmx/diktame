
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.LLM;
/// <summary>
/// LLM provider for any endpoint that speaks the OpenAI Chat Completions API
/// (<c>POST {baseUrl}/v1/chat/completions</c> with <c>Authorization: Bearer {key}</c>).
///
/// This covers, without modification:
///   - OpenAI (gpt-4o, gpt-4o-mini, o1, o3, …)
///   - DeepSeek (deepseek-chat, deepseek-reasoner, …)
///   - OpenRouter (routes to 200+ models via a single endpoint)
///   - Groq (llama-3.3-70b-versatile, mixtral-8x7b, …)
///   - Together AI (meta-llama/*, mistralai/*, …)
///   - Fireworks AI
///   - Perplexity (sonar-pro, sonar-reasoning, …)
///   - Azure OpenAI (custom base URL per deployment)
///   - LM Studio / vLLM (local OpenAI-compatible servers)
///   - Any future provider that adopts the OpenAI spec
///
/// Port of V1's <c>OpenAIProcessor</c> (processor.py), extended with a configurable
/// base URL so all compatible providers share one implementation.
/// </summary>
public sealed class OpenAICompatibleProvider : ILLMProvider, IDisposable
{
    // ── Well-known endpoint presets ───────────────────────────────────────────

    /// <summary>OpenAI (api.openai.com)</summary>
    public static string OpenAIBaseUrl => "https://api.openai.com";

    /// <summary>DeepSeek</summary>
    public static string DeepSeekBaseUrl => "https://api.deepseek.com";

    /// <summary>OpenRouter — routes to 200+ models</summary>
    public static string OpenRouterBaseUrl => "https://openrouter.ai/api";

    /// <summary>Groq — fast inference for open-weight models</summary>
    public static string GroqBaseUrl => "https://api.groq.com/openai";

    /// <summary>Together AI</summary>
    public static string TogetherBaseUrl => "https://api.together.xyz";

    /// <summary>Fireworks AI</summary>
    public static string FireworksBaseUrl => "https://api.fireworks.ai/inference";

    /// <summary>Perplexity</summary>
    public static string PerplexityBaseUrl => "https://api.perplexity.ai";

    // ── Named constructors for common providers ───────────────────────────────

    /// <summary>Create a provider configured for OpenAI.</summary>
    public static OpenAICompatibleProvider ForOpenAI(string apiKey, string model = "gpt-4o-mini")
        => new(OpenAIBaseUrl, apiKey, model, "OpenAI");

    /// <summary>Create a provider configured for DeepSeek.</summary>
    public static OpenAICompatibleProvider ForDeepSeek(string apiKey, string model = "deepseek-chat")
        => new(DeepSeekBaseUrl, apiKey, model, "DeepSeek");

    /// <summary>Create a provider configured for OpenRouter.</summary>
    public static OpenAICompatibleProvider ForOpenRouter(string apiKey, string model = "openai/gpt-4o-mini")
        => new(OpenRouterBaseUrl, apiKey, model, "OpenRouter");

    /// <summary>Create a provider configured for Groq.</summary>
    public static OpenAICompatibleProvider ForGroq(string apiKey, string model = "llama-3.3-70b-versatile")
        => new(GroqBaseUrl, apiKey, model, "Groq");

    /// <summary>Create a provider configured for Together AI.</summary>
    public static OpenAICompatibleProvider ForTogether(string apiKey, string model = "meta-llama/Llama-3.3-70B-Instruct-Turbo")
        => new(TogetherBaseUrl, apiKey, model, "Together AI");

    /// <summary>Create a provider configured for Perplexity.</summary>
    public static OpenAICompatibleProvider ForPerplexity(string apiKey, string model = "sonar-pro")
        => new(PerplexityBaseUrl, apiKey, model, "Perplexity");

    /// <summary>
    /// Create a provider for a custom OpenAI-compatible local server
    /// (LM Studio, vLLM, Llamafile, text-generation-webui, etc.).
    /// </summary>
    public static OpenAICompatibleProvider ForLocalServer(string baseUrl, string model, string? apiKey = null)
        => new(baseUrl, apiKey ?? "local", model, "Local");

    // ── Core implementation ───────────────────────────────────────────────────

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpointUrl;
    private readonly string _serviceName;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => $"{_model} ({_serviceName})";

    /// <summary>
    /// Initialises the provider for any OpenAI-compatible endpoint.
    /// </summary>
    /// <param name="baseUrl">
    /// Base URL of the service, e.g. <c>https://api.openai.com</c>.
    /// The path <c>/v1/chat/completions</c> is appended automatically.
    /// </param>
    /// <param name="apiKey">Bearer token / API key.</param>
    /// <param name="model">Model identifier, e.g. <c>gpt-4o-mini</c>.</param>
    /// <param name="serviceName">Human-readable service label for <see cref="ProviderName"/>.</param>
    /// <param name="httpClient">Optional shared client (for tests / connection pooling).</param>
    public OpenAICompatibleProvider(
        string baseUrl,
        string apiKey,
        string model,
        string serviceName = "OpenAI-compatible",
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL must not be empty.", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must not be empty.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model must not be empty.", nameof(model));
        }

        _apiKey = apiKey;
        _model = model;
        _serviceName = serviceName;
        _endpointUrl = baseUrl.TrimEnd('/') + "/v1/chat/completions";

        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string safeText = SanitizeInput(text);
        string body = BuildRequestJson(_model, systemPrompt, safeText);

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException($"{_serviceName}: invalid API key (401).");
                }

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429 && attempt < MaxRetries - 1)
                    {
                        await DelayAsync(attempt).ConfigureAwait(false);
                        continue;
                    }
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"{_serviceName}: status {(int)response.StatusCode}: {errBody}");
                }

                sw.Stop();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                (string output, int? inTok, int? outTok) = ParseResponse(json);

                Log.Information("{Provider}: processed in {Ms}ms [{Mode}]",
                    ProviderName, sw.ElapsedMilliseconds, mode);

                return new LlmResult
                {
                    Text = output,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                    InputTokens = inTok,
                    OutputTokens = outTok,
                };
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "{Provider}: network error on attempt {A}/{Max}",
                    ProviderName, attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"{_serviceName}: all {MaxRetries} attempts failed.");
    }

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string body = BuildConversationRequestJson(_model, systemPrompt, history);
        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException($"{_serviceName}: invalid API key (401).");
                }

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429 && attempt < MaxRetries - 1)
                    {
                        await DelayAsync(attempt).ConfigureAwait(false);
                        continue;
                    }
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"{_serviceName}: status {(int)response.StatusCode}: {errBody}");
                }

                sw.Stop();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                (string output, int? inTok, int? outTok) = ParseResponse(json);

                Log.Information("{Provider}: conversation processed in {Ms}ms [{Mode}]",
                    ProviderName, sw.ElapsedMilliseconds, mode);

                return new LlmResult
                {
                    Text = output,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                    InputTokens = inTok,
                    OutputTokens = outTok,
                };
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "{Provider}: network error on attempt {A}/{Max}",
                    ProviderName, attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"{_serviceName}: all {MaxRetries} attempts failed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sanitizes user text to prevent prompt injection (matches V1 M1 security fix).
    /// Escapes triple backticks and the {text} template placeholder.
    /// </summary>
    private static string SanitizeInput(string text)
        => text.Replace("```", "'''").Replace("{text}", "[text]");

    private static string BuildRequestJson(string model, string systemPrompt, string userText)
    {
        // Manual JSON build to stay trim-safe (no JsonSerializer.Serialize on objects)
        string escapedSystem = EscapeJsonString(systemPrompt);
        string escapedUser = EscapeJsonString(userText);
        string escapedModel = EscapeJsonString(model);

        return $$"""
            {
              "model": "{{escapedModel}}",
              "messages": [
                { "role": "system", "content": "{{escapedSystem}}" },
                { "role": "user",   "content": "{{escapedUser}}"   }
              ],
              "temperature": 0.1,
              "max_tokens": 1024
            }
            """;
    }

    private static string BuildConversationRequestJson(
        string model, string systemPrompt, IReadOnlyList<ConversationTurn> history)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"model\":\"").Append(EscapeJsonString(model)).Append("\",");
        sb.Append("\"messages\":[");
        sb.Append("{\"role\":\"system\",\"content\":\"").Append(EscapeJsonString(systemPrompt)).Append("\"}");

        foreach (var turn in history)
        {
            sb.Append(",{\"role\":\"").Append(EscapeJsonString(turn.Role))
              .Append("\",\"content\":\"").Append(EscapeJsonString(SanitizeInput(turn.Content)))
              .Append("\"}");
        }

        sb.Append("],\"temperature\":0.1,\"max_tokens\":1024}");
        return sb.ToString();
    }

    /// <summary>
    /// Parses the Chat Completions response.
    /// Path: choices[0].message.content + usage.prompt_tokens / completion_tokens
    /// </summary>
    private static (string Text, int? InputTokens, int? OutputTokens) ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string text = string.Empty;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var msg = choices[0];
                if (msg.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    text = content.GetString()?.Trim() ?? string.Empty;
                }
            }

            int? inTok = null, outTok = null;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt))
                {
                    inTok = pt.GetInt32();
                }

                if (usage.TryGetProperty("completion_tokens", out var ct))
                {
                    outTok = ct.GetInt32();
                }
            }

            return (text, inTok, outTok);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "OpenAICompatibleProvider: failed to parse response JSON");
            return (string.Empty, null, null);
        }
    }

    private static string EscapeJsonString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static Task DelayAsync(int attempt)
        => Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));  // 1s, 2s, 4s

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _http.Dispose();
    }
}
