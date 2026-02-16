namespace DiktaMe.Core.LLM;

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;

/// <summary>
/// LLM provider backed by the Anthropic Messages API.
/// Supports all Claude models (claude-3-5-haiku, claude-3-5-sonnet, claude-3-opus, etc.).
/// Port of V1's <c>AnthropicProcessor</c> in processor.py.
/// </summary>
public sealed class AnthropicProvider : ILLMProvider, IDisposable
{
    private const string MessagesUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    private const string DefaultModel = "claude-3-5-haiku-20241022";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => $"{_model} (Anthropic)";

    /// <param name="apiKey">Anthropic API key (sk-ant-…).</param>
    /// <param name="model">Claude model ID (default: claude-3-5-haiku-20241022).</param>
    /// <param name="httpClient">Optional shared client.</param>
    public AnthropicProvider(
        string apiKey,
        string model = DefaultModel,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Anthropic API key must not be empty.", nameof(apiKey));

        _apiKey = apiKey;
        _model = model;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync()
        => Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate")
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
                using var request = new HttpRequestMessage(HttpMethod.Post, MessagesUrl);
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", ApiVersion);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Anthropic: invalid API key (401).");

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429 && attempt < MaxRetries - 1)
                    {
                        await DelayAsync(attempt).ConfigureAwait(false);
                        continue;
                    }
                    string errBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"Anthropic: status {(int)response.StatusCode}: {errBody}");
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
                Log.Warning(ex, "AnthropicProvider: network error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await DelayAsync(attempt).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Anthropic: all {MaxRetries} attempts failed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SanitizeInput(string text)
        => text.Replace("```", "'''").Replace("{text}", "[text]");

    private static string BuildRequestJson(string model, string systemPrompt, string userText)
    {
        string escapedSystem = EscapeJsonString(systemPrompt);
        string escapedUser = EscapeJsonString(userText);
        string escapedModel = EscapeJsonString(model);

        return $$"""
            {
              "model": "{{escapedModel}}",
              "system": "{{escapedSystem}}",
              "messages": [{ "role": "user", "content": "{{escapedUser}}" }],
              "max_tokens": 1024
            }
            """;
    }

    /// <summary>
    /// Path: content[0].text
    /// Token counts: usage.input_tokens / output_tokens
    /// </summary>
    private static (string Text, int? InputTokens, int? OutputTokens) ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string text = string.Empty;
            if (root.TryGetProperty("content", out var content) &&
                content.GetArrayLength() > 0 &&
                content[0].TryGetProperty("text", out var t))
            {
                text = t.GetString()?.Trim() ?? string.Empty;
            }

            int? inTok = null, outTok = null;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("input_tokens", out var it)) inTok = it.GetInt32();
                if (usage.TryGetProperty("output_tokens", out var ot)) outTok = ot.GetInt32();
            }

            return (text, inTok, outTok);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "AnthropicProvider: failed to parse response JSON");
            return (string.Empty, null, null);
        }
    }

    private static string EscapeJsonString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static Task DelayAsync(int attempt)
        => Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}
