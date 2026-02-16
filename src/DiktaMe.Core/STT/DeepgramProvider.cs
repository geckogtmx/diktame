namespace DiktaMe.Core.STT;

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Serilog;

/// <summary>
/// STT provider backed by Deepgram Nova-2 via the REST Listen API.
/// API reference: https://developers.deepgram.com/reference/listen-file
/// Port of V1's cloud STT integration.
/// </summary>
public sealed class DeepgramProvider : ISTTProvider, IDisposable
{
    private const string ListenUrl =
        "https://api.deepgram.com/v1/listen?model=nova-2&smart_format=false";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => "Deepgram Nova-2";

    /// <summary>
    /// Initialises the provider with the given API key.
    /// </summary>
    /// <param name="apiKey">Deepgram API key.</param>
    /// <param name="httpClient">
    /// Optional shared <see cref="HttpClient"/> (for testing / connection pooling).
    /// If null, a new instance is created.
    /// </param>
    public DeepgramProvider(string apiKey, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Deepgram API key must not be empty.", nameof(apiKey));

        _apiKey = apiKey;
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    /// <inheritdoc/>
    /// <remarks>Returns <c>false</c> if the API key is not set; does not make a network call.</remarks>
    public Task<bool> IsAvailableAsync()
        => Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));

    /// <inheritdoc/>
    /// <remarks>
    /// Sends the WAV file as binary request body.
    /// Language <c>"auto"</c> is mapped to Deepgram's <c>detect_language=true</c>.
    /// </remarks>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();

        // Build URL — append language or detect_language param
        string url = language.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? ListenUrl + "&detect_language=true"
            : ListenUrl + $"&language={Uri.EscapeDataString(language)}";

        byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiKey);
        request.Content = new ByteArrayContent(audioBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "DeepgramProvider: network error");
            throw;
        }

        sw.Stop();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Deepgram: invalid API key (401).");

        if (response.StatusCode == (HttpStatusCode)429)
            throw new InvalidOperationException("Deepgram: rate limit exceeded (429).");

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Deepgram: unexpected status {(int)response.StatusCode}: {body}");
        }

        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string transcript = ParseTranscript(json);
        string? detectedLang = language.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? ParseDetectedLanguage(json)
            : null;

        Log.Information("DeepgramProvider: transcribed in {Ms}ms — \"{Preview}\"",
            sw.ElapsedMilliseconds,
            transcript.Length > 80 ? transcript[..80] + "…" : transcript);

        return new TranscriptionResult
        {
            Text = transcript,
            DetectedLanguage = detectedLang,
            LatencyMs = sw.ElapsedMilliseconds,
            Provider = ProviderName,
        };
    }

    // ── JSON parsing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the transcript from the Deepgram Listen API JSON response.
    /// Path: results.channels[0].alternatives[0].transcript
    /// </summary>
    private static string ParseTranscript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var results))
                return string.Empty;

            if (!results.TryGetProperty("channels", out var channels))
                return string.Empty;

            if (channels.GetArrayLength() == 0)
                return string.Empty;

            var channel = channels[0];
            if (!channel.TryGetProperty("alternatives", out var alts))
                return string.Empty;

            if (alts.GetArrayLength() == 0)
                return string.Empty;

            return alts[0].TryGetProperty("transcript", out var t)
                ? t.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "DeepgramProvider: failed to parse transcript JSON");
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses the detected language from the Deepgram response.
    /// Path: results.channels[0].detected_language
    /// </summary>
    private static string? ParseDetectedLanguage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var results)) return null;
            if (!results.TryGetProperty("channels", out var channels)) return null;
            if (channels.GetArrayLength() == 0) return null;

            var channel = channels[0];
            return channel.TryGetProperty("detected_language", out var lang)
                ? lang.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}
