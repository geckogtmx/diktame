
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.Core.Account;

/// <summary>
/// LLM provider that routes requests through the wallet proxy Edge Function.
/// Handles JWT auth, cost tracking, and wallet balance updates.
/// <para><see cref="ProcessConversationAsync"/> always throws <see cref="NotSupportedException"/>
/// as a defense-in-depth measure — chat is excluded from wallet credits.</para>
/// </summary>
public sealed class WalletGeminiProxy : ILLMProvider, IDisposable
{
    private const string TokenKey = "trial_token"; // backward compat key name
    private const int MaxRetries = 3;

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly WalletManager _wallet;
    private readonly HttpClient _http;
    private bool _disposed;

    public string ProviderName => "Wallet Proxy (Gemini)";

    /// <summary>
    /// Raised when the wallet proxy returns a 401, indicating the JWT has expired.
    /// The UI layer should prompt the user to re-authenticate.
    /// </summary>
    public event Action? SessionExpired;

    /// <summary>
    /// Raised when the wallet proxy returns a 402 (insufficient funds) or updated balance.
    /// Parameter is the new balance in microdollars.
    /// </summary>
    public event Action<long>? BalanceUpdated;

    public WalletGeminiProxy(
        SecureStorage secureStorage,
        SettingsManager settings,
        WalletManager wallet,
        HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _wallet = wallet;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.ConnectionClose = false;
    }

    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            Log.Warning("WalletGeminiProxy: no JWT token available");
            return new LlmResult { Text = "", Provider = ProviderName };
        }

        string proxyUrl = _settings.Current.Account.WalletProxyUrl;
        var sw = Stopwatch.StartNew();

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var requestBody = new JsonObject
                {
                    ["service"] = "gemini",
                    ["text"] = text,
                    ["systemPrompt"] = systemPrompt,
                    ["mode"] = mode,
                };

                string json = requestBody.ToJsonString();
                using var request = new HttpRequestMessage(HttpMethod.Post, proxyUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        Log.Warning("WalletGeminiProxy: 401 — session expired");
                        SessionExpired?.Invoke();
                        return new LlmResult
                        {
                            Text = Resources.CoreStrings.Wallet_SessionExpired,
                            Provider = ProviderName,
                        };

                    case HttpStatusCode.PaymentRequired:
                        Log.Warning("WalletGeminiProxy: 402 — insufficient wallet balance");
                        UpdateBalanceFromHeaders(response);
                        return new LlmResult
                        {
                            Text = Resources.CoreStrings.Wallet_InsufficientBalance,
                            Provider = ProviderName,
                        };

                    case HttpStatusCode.ServiceUnavailable:
                        Log.Warning("WalletGeminiProxy: 503 — service frozen");
                        return new LlmResult { Text = "Service temporarily unavailable.", Provider = ProviderName };

                    case HttpStatusCode.TooManyRequests:
                        Log.Warning("WalletGeminiProxy: 429 — rate limited (attempt {Attempt})", attempt + 1);
                        if (attempt < MaxRetries - 1)
                        {
                            await Task.Delay((int)Math.Pow(2, attempt) * 1000, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        return new LlmResult { Text = "Rate limited. Please try again.", Provider = ProviderName };
                }

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                sw.Stop();

                // Track cost and balance from response headers
                await RecordUsageFromHeadersAsync(response, mode, cancellationToken).ConfigureAwait(false);

                // Parse response
                return ParseLlmResponse(responseBody, sw.ElapsedMilliseconds);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "WalletGeminiProxy: HTTP error (attempt {Attempt}), retrying", attempt + 1);
                await Task.Delay((int)Math.Pow(2, attempt) * 1000, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "WalletGeminiProxy: request timed out (attempt {Attempt})", attempt + 1);
                if (attempt < MaxRetries - 1)
                {
                    continue;
                }
                return new LlmResult { Text = "Request timed out.", Provider = ProviderName };
            }
        }

        return new LlmResult { Text = "All retry attempts failed.", Provider = ProviderName };
    }

    /// <summary>
    /// Chat is excluded from wallet credits. This method always throws.
    /// Defense-in-depth: even if the router somehow routes chat here, we reject it.
    /// </summary>
    public Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "WalletGeminiProxy does not support conversation mode. " +
            "Chat is excluded from wallet credits — use BYOK or local providers.");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        // Quick health check — verify the proxy endpoint is reachable
        try
        {
            string proxyUrl = _settings.Current.Account.WalletProxyUrl;
            using var request = new HttpRequestMessage(HttpMethod.Head, proxyUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.StatusCode != HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private LlmResult ParseLlmResponse(string responseBody, long latencyMs)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
            int? inputTokens = root.TryGetProperty("inputTokens", out var inTok) ? inTok.GetInt32() : null;
            int? outputTokens = root.TryGetProperty("outputTokens", out var outTok) ? outTok.GetInt32() : null;

            return new LlmResult
            {
                Text = text,
                Provider = ProviderName,
                LatencyMs = latencyMs,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
            };
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "WalletGeminiProxy: failed to parse response JSON");
            return new LlmResult { Text = responseBody, Provider = ProviderName, LatencyMs = latencyMs };
        }
    }

    private async Task RecordUsageFromHeadersAsync(
        HttpResponseMessage response,
        string mode,
        CancellationToken cancellationToken)
    {
        try
        {
            if (response.Headers.TryGetValues("X-Wallet-Cost", out var costValues))
            {
                string costStr = costValues.FirstOrDefault() ?? "0";
                if (long.TryParse(costStr, System.Globalization.CultureInfo.InvariantCulture, out long costMicro)
                    && costMicro > 0)
                {
                    await _wallet.InsertTransactionAsync(
                        -costMicro,
                        WalletTransactionType.Usage,
                        $"{{\"service\":\"gemini\",\"mode\":\"{mode}\"}}",
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }

            UpdateBalanceFromHeaders(response);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WalletGeminiProxy: failed to record usage from headers");
        }
    }

    private void UpdateBalanceFromHeaders(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Wallet-Balance", out var balValues))
        {
            string balStr = balValues.FirstOrDefault() ?? "";
            if (long.TryParse(balStr, System.Globalization.CultureInfo.InvariantCulture, out long balanceMicro))
            {
                BalanceUpdated?.Invoke(balanceMicro);
            }
        }
    }

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
