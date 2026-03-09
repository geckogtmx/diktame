
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Resources;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.Core.Account;
/// <summary>
/// LLM provider that routes requests through the managed Gemini proxy
/// at dikta.me (Supabase Edge Function) using a trial JWT for authentication.
/// Used when <see cref="Config.AuthMode"/> is <see cref="Config.AuthMode.Trial"/>.
/// </summary>
public sealed class TrialGeminiProvider : ILLMProvider, IDisposable
{
    private const string ProxyUrl =
        "https://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/gemini-proxy";
    private const string TokenKey = "trial_token";

    private readonly SecureStorage _secureStorage;
    private readonly IAccountService _accountService;
    private readonly ITrialService _trialService;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => "Gemini (Trial)";

    public TrialGeminiProvider(
        SecureStorage secureStorage,
        IAccountService accountService,
        ITrialService trialService,
        HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _accountService = accountService;
        _trialService = trialService;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_secureStorage.RetrieveKey(TokenKey) is not null);

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TrialGeminiProvider: no trial token available.");
        }

        string safeText = SanitizeInput(text);
        string userContent = $"{systemPrompt}\n\n{safeText}";
        string body = BuildRequestJson(userContent);

        var sw = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ProxyUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("TrialGeminiProvider: 401 — token expired or invalid, auto-logout");
                await _accountService.LogoutAsync(cancellationToken).ConfigureAwait(false);
                return new LlmResult
                {
                    Text = CoreStrings.Trial_SessionExpired,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.Warning("TrialGeminiProvider: 403 — quota exceeded");
                return new LlmResult
                {
                    Text = CoreStrings.Trial_QuotaExceeded,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"TrialGeminiProvider: status {(int)response.StatusCode}: {errBody}");
            }

            sw.Stop();
            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            (string output, int? inTok, int? outTok) = ParseResponse(json);

            // Report usage asynchronously (fire-and-forget, don't block the result)
            int wordCount = output.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            _ = _trialService.RecordUsageAsync("gemini", "gemini-2.5-flash", wordCount, cancellationToken);

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
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrialGeminiProvider: network error");
            throw new InvalidOperationException("TrialGeminiProvider: network error.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TrialGeminiProvider: no trial token available.");
        }

        string body = BuildConversationRequestJson(systemPrompt, history);
        var sw = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ProxyUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("TrialGeminiProvider: 401 — token expired or invalid, auto-logout");
                await _accountService.LogoutAsync(cancellationToken).ConfigureAwait(false);
                return new LlmResult
                {
                    Text = CoreStrings.Trial_SessionExpired,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.Warning("TrialGeminiProvider: 403 — quota exceeded");
                return new LlmResult
                {
                    Text = CoreStrings.Trial_QuotaExceeded,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"TrialGeminiProvider: status {(int)response.StatusCode}: {errBody}");
            }

            sw.Stop();
            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            (string output, int? inTok, int? outTok) = ParseResponse(json);

            int wordCount = output.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            _ = _trialService.RecordUsageAsync("gemini", "gemini-2.5-flash", wordCount, cancellationToken);

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
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrialGeminiProvider: network error");
            throw new InvalidOperationException("TrialGeminiProvider: network error.", ex);
        }
    }

    // ── Helpers (reuse GeminiProvider patterns) ──────────────────────────────

    private static string SanitizeInput(string text)
        => text.Replace("```", "'''").Replace("{text}", "[text]");

    private static string BuildRequestJson(string userContent)
    {
        string escaped = EscapeJsonString(userContent);
        return $$"""
            {
              "contents": [{ "parts": [{ "text": "{{escaped}}" }] }],
              "generationConfig": { "temperature": 0.1, "maxOutputTokens": 1024 }
            }
            """;
    }

    private static string BuildConversationRequestJson(
        string systemPrompt, IReadOnlyList<ConversationTurn> history)
    {
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

        sb.Append("],\"generationConfig\":{\"temperature\":0.1,\"maxOutputTokens\":1024}}");
        return sb.ToString();
    }

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
            Log.Warning(ex, "TrialGeminiProvider: failed to parse response JSON");
            return (string.Empty, null, null);
        }
    }

    private static string EscapeJsonString(string s)
        => s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
