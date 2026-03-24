
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.LLM;
/// <summary>
/// LLM provider backed by the Google Gemini generateContent API.
/// Supports both API key (query param) and OAuth Bearer token auth (ya29.* tokens),
/// matching V1's <c>CloudProcessor</c> in processor.py.
/// </summary>
public sealed class GeminiProvider : ILLMProvider, IDisposable
{
    private const string DefaultModel = "gemini-2.5-flash";
    private const string ApiBase =
        "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly bool _isOAuth;
    private bool _disposed;

    /// <summary>
    /// When true, adds Google Search grounding to requests.
    /// The model autonomously decides whether to search based on the query.
    /// </summary>
    public bool WebSearchEnabled { get; set; }

    /// <inheritdoc/>
    public string ProviderName => $"{_model} (Gemini)";

    /// <param name="apiKey">
    /// Gemini API key (AIza…) or OAuth Bearer token (ya29.…).
    /// </param>
    /// <param name="model">Gemini model ID (default: gemini-2.5-flash).</param>
    /// <param name="httpClient">Optional shared client.</param>
    public GeminiProvider(
        string apiKey,
        string model = DefaultModel,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Gemini API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _model = model;
        _isOAuth = apiKey.StartsWith("ya29.", StringComparison.Ordinal);
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

        // Combine system prompt + user text into a single user turn
        // (Gemini doesn't have a dedicated system role in the basic API)
        string userContent = $"{systemPrompt}\n\n{safeText}";
        string body = BuildRequestJson(userContent);

        // OAuth path needs full "models/{model}" because ApiBase gets "/models" stripped
        // API key path uses raw model name because ApiBase already ends with "/models"
        string url = _isOAuth
            ? $"{ApiBase.Replace("/models", "", StringComparison.Ordinal)}/models/{_model}:generateContent"
            : $"{ApiBase}/{_model}:generateContent?key={_apiKey}";

        Log.Debug("GeminiProvider: calling URL={Url}, isOAuth={IsOAuth}, model={Model}",
            url.Replace(_apiKey, "***"), _isOAuth, _model);

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                if (_isOAuth)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException("Gemini: invalid API key or OAuth token (401).");
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
                        $"Gemini: status {(int)response.StatusCode}: {errBody}");
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
                Log.Warning(ex, "GeminiProvider: network error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Gemini: all {MaxRetries} attempts failed.");
    }

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string body = BuildConversationRequestJson(systemPrompt, history);

        string url = _isOAuth
            ? $"{ApiBase.Replace("/models", "", StringComparison.Ordinal)}/models/{_model}:generateContent"
            : $"{ApiBase}/{_model}:generateContent?key={_apiKey}";

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                if (_isOAuth)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException("Gemini: invalid API key or OAuth token (401).");
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
                        $"Gemini: status {(int)response.StatusCode}: {errBody}");
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
                Log.Warning(ex, "GeminiProvider: network error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Gemini: all {MaxRetries} attempts failed.");
    }

    /// <summary>
    /// Processes an image with optional text query using Gemini's multimodal API.
    /// </summary>
    public async Task<LlmResult> ProcessWithImageAsync(
        byte[] imageData, string mimeType,
        string text, string systemPrompt, string mode = "vision",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string base64 = Convert.ToBase64String(imageData);
        string safeText = SanitizeInput(text);
        string body = BuildImageRequestJson(base64, mimeType, safeText, systemPrompt);

        string url = _isOAuth
            ? $"{ApiBase.Replace("/models", "", StringComparison.Ordinal)}/models/{_model}:generateContent"
            : $"{ApiBase}/{_model}:generateContent?key={_apiKey}";

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                if (_isOAuth)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException("Gemini: invalid API key or OAuth token (401).");
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
                        $"Gemini: status {(int)response.StatusCode}: {errBody}");
                }

                sw.Stop();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                (string output, int? inTok, int? outTok) = ParseResponse(json);

                Log.Information("{Provider}: image processed in {Ms}ms [{Mode}]",
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
                Log.Warning(ex, "GeminiProvider: network error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Gemini: all {MaxRetries} attempts failed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SanitizeInput(string text)
        => text.Replace("```", "'''").Replace("{text}", "[text]");

    private string BuildRequestJson(string userContent)
    {
        string escaped = EscapeJsonString(userContent);
        string tools = WebSearchEnabled ? ",\"tools\":[{\"google_search\":{}}]" : "";
        return $$"""
            {
              "contents": [{ "parts": [{ "text": "{{escaped}}" }] }],
              "generationConfig": { "temperature": 0.1, "maxOutputTokens": 1024 }{{tools}}
            }
            """;
    }

    private string BuildConversationRequestJson(
        string systemPrompt, IReadOnlyList<ConversationTurn> history)
    {
        // Gemini uses "user" and "model" roles (not "assistant")
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"systemInstruction\":{\"parts\":[{\"text\":\"")
          .Append(EscapeJsonString(systemPrompt)).Append("\"}]},");
        sb.Append("\"contents\":[");

        for (int i = 0; i < history.Count; i++)
        {
            if (i > 0) { sb.Append(','); }
            string role = string.Equals(history[i].Role, "assistant", StringComparison.Ordinal)
                ? "model" : "user";
            sb.Append("{\"role\":\"").Append(role)
              .Append("\",\"parts\":[{\"text\":\"")
              .Append(EscapeJsonString(SanitizeInput(history[i].Content)))
              .Append("\"}]}");
        }

        sb.Append("],\"generationConfig\":{\"temperature\":0.1,\"maxOutputTokens\":1024}");

        if (WebSearchEnabled)
        {
            sb.Append(",\"tools\":[{\"google_search\":{}}]");
        }

        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Path: candidates[0].content.parts[0].text
    /// Token counts: usageMetadata.promptTokenCount / candidatesTokenCount
    /// </summary>
    private static (string Text, int? InputTokens, int? OutputTokens) ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string text = string.Empty;
            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var t))
            {
                text = t.GetString()?.Trim() ?? string.Empty;
            }

            int? inTok = null, outTok = null;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var pt))
                {
                    inTok = pt.GetInt32();
                }

                if (usage.TryGetProperty("candidatesTokenCount", out var ct))
                {
                    outTok = ct.GetInt32();
                }
            }

            return (text, inTok, outTok);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "GeminiProvider: failed to parse response JSON");
            return (string.Empty, null, null);
        }
    }

    private static string BuildImageRequestJson(
        string base64Data, string mimeType, string userText, string systemPrompt)
    {
        string escapedMime = EscapeJsonString(mimeType);
        string escapedText = EscapeJsonString(userText);
        string escapedSystem = EscapeJsonString(systemPrompt);

        return $$"""
            {
              "contents": [{ "parts": [
                { "inlineData": { "mimeType": "{{escapedMime}}", "data": "{{base64Data}}" } },
                { "text": "{{escapedText}}" }
              ] }],
              "systemInstruction": { "parts": [{ "text": "{{escapedSystem}}" }] },
              "generationConfig": { "temperature": 0.3, "maxOutputTokens": 4096 }
            }
            """;
    }

    private static string EscapeJsonString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static Task DelayAsync(int attempt)
        => Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

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
