
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Security;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Account;

/// <summary>
/// STT provider that routes audio through the wallet proxy Edge Function.
/// Handles JWT auth, cost tracking, and wallet balance updates.
/// </summary>
public sealed class WalletDeepgramProxy : ISTTProvider, IDisposable
{
    private const string TokenKey = "trial_token"; // backward compat key name
    private const int MaxRetries = 3;

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly WalletManager _wallet;
    private readonly HttpClient _http;
    private bool _disposed;

    public string ProviderName => "Wallet Proxy (Deepgram)";

    /// <summary>
    /// Raised when the wallet proxy returns a 401, indicating the JWT has expired.
    /// </summary>
    public event Action? SessionExpired;

    /// <summary>
    /// Raised when the wallet proxy returns updated balance information.
    /// Parameter is the new balance in microdollars.
    /// </summary>
    public event Action<long>? BalanceUpdated;

    public WalletDeepgramProxy(
        SecureStorage secureStorage,
        SettingsManager settings,
        WalletManager wallet,
        HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _wallet = wallet;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) }; // longer for audio uploads
        _http.DefaultRequestHeaders.ConnectionClose = false;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            Log.Warning("WalletDeepgramProxy: no JWT token available");
            return new TranscriptionResult { Text = "", Provider = ProviderName };
        }

        string proxyUrl = _settings.Current.Account.WalletProxyUrl;
        var sw = Stopwatch.StartNew();

        // Compute audio duration from WAV file header for the result
        double? audioDurationSec = ComputeAudioDuration(audioFilePath);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                byte[] audioData = await File.ReadAllBytesAsync(audioFilePath, cancellationToken).ConfigureAwait(false);

                using var request = new HttpRequestMessage(HttpMethod.Post, proxyUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Multipart form: service identifier + audio binary + language
                using var content = new MultipartFormDataContent
                {
                    { new StringContent("deepgram"), "service" },
                    { new StringContent(language), "language" },
                    { new ByteArrayContent(audioData) { Headers = { ContentType = new MediaTypeHeaderValue("audio/wav") } }, "audio", "recording.wav" },
                };
                request.Content = content;

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        Log.Warning("WalletDeepgramProxy: 401 — session expired");
                        SessionExpired?.Invoke();
                        throw new InvalidOperationException("Session expired. Please sign in again.");

                    case HttpStatusCode.PaymentRequired:
                        Log.Warning("WalletDeepgramProxy: 402 — insufficient wallet balance");
                        UpdateBalanceFromHeaders(response);
                        throw new InvalidOperationException("Insufficient wallet balance. Please top up.");

                    case HttpStatusCode.ServiceUnavailable:
                        Log.Warning("WalletDeepgramProxy: 503 — service frozen");
                        throw new InvalidOperationException("Service temporarily unavailable.");

                    case HttpStatusCode.TooManyRequests:
                        Log.Warning("WalletDeepgramProxy: 429 — rate limited (attempt {Attempt})", attempt + 1);
                        if (attempt < MaxRetries - 1)
                        {
                            await Task.Delay((int)Math.Pow(2, attempt) * 1000, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        return new TranscriptionResult { Text = "Rate limited. Please try again.", Provider = ProviderName };
                }

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                sw.Stop();

                // Track cost and balance from response headers
                await RecordUsageFromHeadersAsync(response, cancellationToken).ConfigureAwait(false);

                // Parse response
                return ParseSttResponse(responseBody, sw.ElapsedMilliseconds, audioDurationSec);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "WalletDeepgramProxy: HTTP error (attempt {Attempt}), retrying", attempt + 1);
                await Task.Delay((int)Math.Pow(2, attempt) * 1000, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "WalletDeepgramProxy: request timed out (attempt {Attempt})", attempt + 1);
                if (attempt < MaxRetries - 1)
                {
                    continue;
                }
                return new TranscriptionResult { Text = "Request timed out.", Provider = ProviderName };
            }
        }

        return new TranscriptionResult { Text = "All retry attempts failed.", Provider = ProviderName };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

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

    private TranscriptionResult ParseSttResponse(string responseBody, long latencyMs, double? audioDurationSec)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Edge Function returns "transcript" for Deepgram, "text" for Gemini
            string text = root.TryGetProperty("transcript", out var txProp) ? txProp.GetString() ?? ""
                         : root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? ""
                         : "";
            string? language = root.TryGetProperty("detectedLanguage", out var langProp) ? langProp.GetString() : null;

            return new TranscriptionResult
            {
                Text = text,
                Provider = ProviderName,
                LatencyMs = latencyMs,
                DetectedLanguage = language,
                AudioDurationSec = audioDurationSec,
            };
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "WalletDeepgramProxy: failed to parse response JSON");
            return new TranscriptionResult { Text = responseBody, Provider = ProviderName, LatencyMs = latencyMs };
        }
    }

    private async Task RecordUsageFromHeadersAsync(
        HttpResponseMessage response,
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
                        "{\"service\":\"deepgram\"}",
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }

            UpdateBalanceFromHeaders(response);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WalletDeepgramProxy: failed to record usage from headers");
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

    private static double? ComputeAudioDuration(string wavPath)
    {
        try
        {
            using var fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            if (fs.Length < 44) return null;

            // Skip to byte rate (offset 28)
            fs.Position = 28;
            int byteRate = br.ReadInt32();
            if (byteRate <= 0) return null;

            // Data size starts at offset 40
            fs.Position = 40;
            int dataSize = br.ReadInt32();

            return (double)dataSize / byteRate;
        }
        catch
        {
            return null;
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
