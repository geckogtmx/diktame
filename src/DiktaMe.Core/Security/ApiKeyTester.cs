using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.Security;

/// <summary>
/// Validates an API key end-to-end by making a minimum authenticated request
/// against the provider. Each method returns a structured result rather than
/// throwing so the Settings UI can render inline success/failure.
/// </summary>
public sealed class ApiKeyTester : IDisposable
{
    private readonly SecureStorage _storage;
    private readonly HttpClient _http;
    private bool _disposed;

    public ApiKeyTester(SecureStorage storage, HttpClient? httpClient = null)
    {
        _storage = storage;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Tests a provider's API key. If <paramref name="overrideKey"/> is non-empty
    /// it is used directly (so the user can test a typed-but-not-saved key),
    /// otherwise the stored key is read from <see cref="SecureStorage"/>.
    /// </summary>
    /// <param name="provider">Lowercase provider id (openai/anthropic/gemini/deepgram/openrouter/requesty/inworld).</param>
    /// <param name="overrideKey">Optional unsaved key to test instead of the stored one.</param>
    public async Task<ApiKeyTestResult> TestAsync(string provider, string? overrideKey = null, CancellationToken ct = default)
    {
        string? key = !string.IsNullOrWhiteSpace(overrideKey)
            ? overrideKey
            : _storage.RetrieveKey(provider);

        if (string.IsNullOrWhiteSpace(key))
        {
            return new ApiKeyTestResult(false, "No key to test", 0);
        }

        return provider.ToLowerInvariant() switch
        {
            "openai" => await TestOpenAIAsync(key, ct).ConfigureAwait(false),
            "anthropic" => await TestAnthropicAsync(key, ct).ConfigureAwait(false),
            "gemini" => await TestGeminiAsync(key, ct).ConfigureAwait(false),
            "deepgram" => await TestDeepgramAsync(key, ct).ConfigureAwait(false),
            "openrouter" => await TestOpenRouterAsync(key, ct).ConfigureAwait(false),
            "requesty" => await TestRequestyAsync(key, ct).ConfigureAwait(false),
            "inworld" => await TestInworldAsync(key, ct).ConfigureAwait(false),
            _ => new ApiKeyTestResult(false, "Unknown provider", 0),
        };
    }

    // ── Provider probes ────────────────────────────────────────────────────

    private async Task<ApiKeyTestResult> TestOpenAIAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return await RunAsync("openai", req, countFromDataArray: true, ct).ConfigureAwait(false);
    }

    private async Task<ApiKeyTestResult> TestAnthropicAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models?limit=1");
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        return await RunAsync("anthropic", req, countFromDataArray: true, ct).ConfigureAwait(false);
    }

    private async Task<ApiKeyTestResult> TestGeminiAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1");
        req.Headers.Add("x-goog-api-key", key);
        return await RunAsync("gemini", req, countFromDataArray: false, ct, modelsArrayProperty: "models").ConfigureAwait(false);
    }

    private async Task<ApiKeyTestResult> TestDeepgramAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.deepgram.com/v1/projects");
        req.Headers.Authorization = new AuthenticationHeaderValue("Token", key);
        return await RunAsync("deepgram", req, countFromDataArray: false, ct, modelsArrayProperty: "projects").ConfigureAwait(false);
    }

    private async Task<ApiKeyTestResult> TestOpenRouterAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/auth/key");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return await RunAsync("openrouter", req, countFromDataArray: false, ct).ConfigureAwait(false);
    }

    private async Task<ApiKeyTestResult> TestRequestyAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://router.requesty.ai/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return await RunAsync("requesty", req, countFromDataArray: true, ct).ConfigureAwait(false);
    }

    private async Task<ApiKeyTestResult> TestInworldAsync(string key, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.inworld.ai/tts/v1/voices");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", key);
        return await RunAsync("inworld", req, countFromDataArray: false, ct).ConfigureAwait(false);
    }

    // ── Shared send + response parse ──────────────────────────────────────

    private async Task<ApiKeyTestResult> RunAsync(
        string provider,
        HttpRequestMessage request,
        bool countFromDataArray,
        CancellationToken ct,
        string? modelsArrayProperty = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            sw.Stop();
            int ms = (int)sw.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                Log.Debug("ApiKeyTester: {Provider} returned HTTP {Code}", provider, code);
                return new ApiKeyTestResult(false,
                    string.Create(CultureInfo.InvariantCulture, $"Invalid key (HTTP {code})"),
                    ms);
            }

            int count = 0;
            if (countFromDataArray || modelsArrayProperty is not null)
            {
                try
                {
                    string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    string prop = modelsArrayProperty ?? "data";
                    if (doc.RootElement.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        count = arr.GetArrayLength();
                    }
                }
                catch (JsonException) { /* fall through — 200 OK alone is success */ }
            }

            string message = count > 1
                ? string.Create(CultureInfo.InvariantCulture, $"Connected — {count} items · {ms} ms")
                : string.Create(CultureInfo.InvariantCulture, $"Connected · {ms} ms");

            Log.Debug("ApiKeyTester: {Provider} ok ({Ms} ms, {Count} items)", provider, ms, count);
            return new ApiKeyTestResult(true, message, ms);
        }
        catch (OperationCanceledException)
        {
            return new ApiKeyTestResult(false, "Timed out", (int)sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            Log.Debug(ex, "ApiKeyTester: {Provider} network error", provider);
            return new ApiKeyTestResult(false, "Network error", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ApiKeyTester: {Provider} unexpected error", provider);
            return new ApiKeyTestResult(false, "Unexpected error", (int)sw.ElapsedMilliseconds);
        }
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
