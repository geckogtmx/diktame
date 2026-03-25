
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.LLM;
/// <summary>
/// LLM provider for a locally running Ollama server.
/// Uses the Ollama generate API (<c>POST /api/generate</c>) with keep-alive session management
/// and tokens/sec monitoring to detect CPU vs GPU inference.
/// Port of V1's <c>LocalProcessor</c> in processor.py.
/// </summary>
public sealed class OllamaProvider : ILLMProvider, IDisposable
{
    private const string DefaultBaseUrl = "http://localhost:11434";

    // Warn if inference drops below this rate — likely CPU fallback
    private const double SlowInferenceThresholdToksPerSec = 20.0;

    private readonly HttpClient _http;
    private readonly string _generateUrl;
    private readonly string _chatUrl;
    private readonly string _tagsUrl;
    private readonly string _model;
    private readonly string _keepAlive;
    private readonly int _numCtx;
    private bool _disposed;
    private bool _firstInference = true;

    /// <inheritdoc/>
    public string ProviderName => $"{_model} (Ollama)";

    /// <summary>Last measured inference speed in tokens/sec. Null if not yet measured.</summary>
    public double? LastTokensPerSec { get; private set; }

    /// <param name="model">Ollama model tag, e.g. <c>llama3.2</c>, <c>mistral</c>, <c>phi4</c>.</param>
    /// <param name="baseUrl">Ollama base URL (default: http://localhost:11434).</param>
    /// <param name="httpClient">Optional shared client.</param>
    /// <param name="keepAlive">Ollama keep-alive duration, e.g. <c>"10m"</c>, <c>"1h"</c>.</param>
    /// <param name="numCtx">Context window size (num_ctx parameter).</param>
    public OllamaProvider(
        string model,
        string baseUrl = DefaultBaseUrl,
        HttpClient? httpClient = null,
        string keepAlive = "10m",
        int numCtx = 2048)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model must not be empty.", nameof(model));
        }

        _model = model;
        _keepAlive = keepAlive;
        _numCtx = numCtx > 0 ? numCtx : 2048;
        string trimmed = baseUrl.TrimEnd('/');
        _generateUrl = trimmed + "/api/generate";
        _chatUrl = trimmed + "/api/chat";
        _tagsUrl = trimmed + "/api/tags";

        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(180), // vision models need more time for image processing
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <c>GET /api/tags</c> to verify Ollama is running.
    /// Returns <c>false</c> (not throws) if the server is unreachable.
    /// </remarks>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(_tagsUrl, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Warms up the model by sending a minimal prompt, loading it into VRAM.
    /// Call once at startup to avoid first-request latency.
    /// Fire-and-forget — does not throw.
    /// </summary>
    public async Task WarmUpAsync()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            Log.Debug("OllamaProvider: warming up model '{Model}'", _model);
            string body = BuildRequestJson(
                _model,
                "You are a text formatter. Output only the result.",
                "ping",
                keepAlive: _keepAlive,
                numPredict: 1,
                numCtx: _numCtx);

            using var request = new HttpRequestMessage(HttpMethod.Post, _generateUrl);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("OllamaProvider: model '{Model}' warmed up in {Ms}ms", _model, sw.ElapsedMilliseconds);
            }
            else
            {
                Log.Warning("OllamaProvider: warmup returned status {S} after {Ms}ms", (int)response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaProvider: warmup failed (will retry on first use)");
        }
    }

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string safeText = SanitizeInput(text);
        string body = BuildRequestJson(_model, systemPrompt, safeText, keepAlive: _keepAlive, numCtx: _numCtx);

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _generateUrl);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Warning("OllamaProvider: status {S} on attempt {A}: {Body}",
                        (int)response.StatusCode, attempt + 1, errBody);

                    if (attempt < MaxRetries - 1)
                    {
                        await DelayAsync(attempt).ConfigureAwait(false);
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Ollama: status {(int)response.StatusCode}: {errBody}");
                }

                sw.Stop();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                (string output, double? toksPerSec) = ParseResponse(json);

                // Track and log inference speed (GPU health indicator)
                LastTokensPerSec = toksPerSec;
                if (toksPerSec.HasValue)
                {
                    if (_firstInference)
                    {
                        _firstInference = false;
                        string device = toksPerSec.Value > 50 ? "GPU"
                            : toksPerSec.Value < SlowInferenceThresholdToksPerSec ? "CPU"
                            : "BORDERLINE";
                        Log.Information("OllamaProvider: first inference — {TokSec:F1} tok/s ({Device}), model '{Model}'",
                            toksPerSec.Value, device, _model);
                    }
                    else if (toksPerSec.Value < SlowInferenceThresholdToksPerSec)
                    {
                        Log.Warning("OllamaProvider: SLOW inference {T:F1} tok/s — GPU may not be active",
                            toksPerSec.Value);
                    }
                    else
                    {
                        Log.Information("OllamaProvider: {T:F1} tok/s", toksPerSec.Value);
                    }
                }

                Log.Information("{Provider}: processed in {Ms}ms [{Mode}]",
                    ProviderName, sw.ElapsedMilliseconds, mode);

                return new LlmResult
                {
                    Text = output,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                    TokensPerSec = toksPerSec,
                };
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "OllamaProvider: connection error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Ollama: all {MaxRetries} attempts failed.");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses the Ollama <c>/api/chat</c> endpoint (not <c>/api/generate</c>) for multi-turn
    /// conversations, which accepts a messages array with system/user/assistant roles.
    /// </remarks>
    public async Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string body = BuildConversationRequestJson(_model, systemPrompt, history, keepAlive: _keepAlive, numCtx: _numCtx);
        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _chatUrl);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Warning("OllamaProvider: chat status {S} on attempt {A}: {Body}",
                        (int)response.StatusCode, attempt + 1, errBody);

                    if (attempt < MaxRetries - 1)
                    {
                        await DelayAsync(attempt).ConfigureAwait(false);
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Ollama: status {(int)response.StatusCode}: {errBody}");
                }

                sw.Stop();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string output = ParseChatResponse(json);

                Log.Information("{Provider}: conversation processed in {Ms}ms [{Mode}]",
                    ProviderName, sw.ElapsedMilliseconds, mode);

                return new LlmResult
                {
                    Text = output,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "OllamaProvider: connection error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Ollama: all {MaxRetries} attempts failed.");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses the Ollama <c>/api/chat</c> endpoint with <c>images</c> array for vision models
    /// like moondream, llava, bakllava. The image is sent as base64-encoded data.
    /// </remarks>
    public async Task<LlmResult> ProcessWithImageAsync(
        byte[] imageData, string mimeType, string text, string systemPrompt,
        string mode = "vision", CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string base64Image = Convert.ToBase64String(imageData);
        string body = BuildVisionRequestJson(_model, systemPrompt, text, base64Image, _keepAlive, _numCtx);

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _chatUrl);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Warning("OllamaProvider: vision status {S} on attempt {A}: {Body}",
                        (int)response.StatusCode, attempt + 1, errBody);

                    if (attempt < MaxRetries - 1)
                    {
                        await DelayAsync(attempt).ConfigureAwait(false);
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Ollama: vision status {(int)response.StatusCode}: {errBody}");
                }

                sw.Stop();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string output = ParseChatResponse(json);

                Log.Information("{Provider}: vision processed in {Ms}ms [{Mode}]",
                    ProviderName, sw.ElapsedMilliseconds, mode);

                return new LlmResult
                {
                    Text = output,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "OllamaProvider: vision connection error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Ollama: all {MaxRetries} vision attempts failed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SanitizeInput(string text)
        => text.Replace("```", "'''").Replace("{text}", "[text]");

    private static string BuildRequestJson(
        string model,
        string systemPrompt,
        string userText,
        string keepAlive = "10m",
        int numPredict = 1024,
        int numCtx = 2048)
    {
        string escapedModel = EscapeJsonString(model);
        string escapedPrompt = EscapeJsonString($"{systemPrompt}\n\n{userText}");
        string escapedKeepAlive = EscapeJsonString(keepAlive);

        return $$"""
            {
              "model": "{{escapedModel}}",
              "prompt": "{{escapedPrompt}}",
              "stream": false,
              "options": { "temperature": 0.1, "num_ctx": {{numCtx}}, "num_predict": {{numPredict}} },
              "keep_alive": "{{escapedKeepAlive}}"
            }
            """;
    }

    private static string BuildConversationRequestJson(
        string model, string systemPrompt, IReadOnlyList<ConversationTurn> history,
        string keepAlive = "10m", int numCtx = 2048)
    {
        // Ollama /api/chat uses the same messages format as OpenAI
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

        sb.Append("],\"stream\":false,\"options\":{\"temperature\":0.1,\"num_ctx\":").Append(numCtx).Append("},\"keep_alive\":\"")
          .Append(EscapeJsonString(keepAlive)).Append("\"}");
        return sb.ToString();
    }

    /// <summary>
    /// Parses Ollama /api/chat response.
    /// Path: message.content
    /// </summary>
    private static string ParseChatResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "OllamaProvider: failed to parse chat response JSON");
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses Ollama generate response.
    /// Path: response (text), eval_count / eval_duration (tokens/sec)
    /// </summary>
    private static (string Text, double? TokensPerSec) ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string text = root.TryGetProperty("response", out var r)
                ? r.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            double? toksPerSec = null;
            if (root.TryGetProperty("eval_count", out var ec) &&
                root.TryGetProperty("eval_duration", out var ed))
            {
                long evalCount = ec.GetInt64();
                long evalDuration = ed.GetInt64(); // nanoseconds
                if (evalDuration > 0)
                {
                    toksPerSec = evalCount / (evalDuration / 1e9);
                }
            }

            return (text, toksPerSec);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "OllamaProvider: failed to parse response JSON");
            return (string.Empty, null);
        }
    }

    private static string BuildVisionRequestJson(
        string model, string systemPrompt, string userText, string base64Image,
        string keepAlive = "10m", int numCtx = 2048)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"model\":\"").Append(EscapeJsonString(model)).Append("\",");
        sb.Append("\"messages\":[");
        sb.Append("{\"role\":\"system\",\"content\":\"").Append(EscapeJsonString(systemPrompt)).Append("\"},");
        sb.Append("{\"role\":\"user\",\"content\":\"").Append(EscapeJsonString(userText));
        sb.Append("\",\"images\":[\"").Append(base64Image).Append("\"]}");
        sb.Append("],\"stream\":false,\"options\":{\"temperature\":0.1,\"num_ctx\":").Append(numCtx).Append("},\"keep_alive\":\"")
          .Append(EscapeJsonString(keepAlive)).Append("\"}");
        return sb.ToString();
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
