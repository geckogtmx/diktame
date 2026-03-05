
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.Core.STT;
/// <summary>
/// STT provider backed by Deepgram via the REST Listen API.
/// Constructs request URL dynamically from <see cref="DeepgramSettings"/>.
/// API reference: https://developers.deepgram.com/reference/listen-file
/// </summary>
public sealed class DeepgramProvider : ISTTProvider, IDisposable
{
    private const string BaseUrl = "https://api.deepgram.com/v1/listen";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly DeepgramSettings _settings;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <summary>
    /// Initialises the provider with the given API key and settings.
    /// </summary>
    /// <param name="apiKey">Deepgram API key.</param>
    /// <param name="settings">Deepgram feature settings. Uses defaults if null.</param>
    /// <param name="httpClient">
    /// Optional shared <see cref="HttpClient"/> (for testing / connection pooling).
    /// If null, a new instance is created.
    /// </param>
    public DeepgramProvider(string apiKey, DeepgramSettings? settings = null, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Deepgram API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _settings = settings ?? new DeepgramSettings();
        ProviderName = $"Deepgram {_settings.Model}";
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    /// <inheritdoc/>
    /// <remarks>Returns <c>false</c> if the API key is not set; does not make a network call.</remarks>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));

    /// <inheritdoc/>
    /// <remarks>
    /// Sends the WAV file as binary request body.
    /// Language <c>"auto"</c> is mapped to Deepgram's <c>detect_language=true</c>.
    /// </remarks>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();

        string url = BuildListenUrl(language);

        byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiKey);
        request.Content = new ByteArrayContent(audioBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "DeepgramProvider: network error");
            throw;
        }

        sw.Stop();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Deepgram: invalid API key (401).");
        }

        if (response.StatusCode == (HttpStatusCode)429)
        {
            throw new InvalidOperationException("Deepgram: rate limit exceeded (429).");
        }

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

    // ── URL builder ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the Listen API URL from <see cref="DeepgramSettings"/> and language.
    /// </summary>
    internal string BuildListenUrl(string language)
        => $"{BaseUrl}?{BuildQueryParameters(_settings, language)}";

    /// <summary>
    /// Builds the shared query parameters for Deepgram Listen API (batch and streaming).
    /// Contains model, punctuate/smart_format, dictation, replacements, and language params.
    /// </summary>
    internal static string BuildQueryParameters(DeepgramSettings settings, string language)
    {
        var parts = new List<string>(8)
        {
            $"model={Uri.EscapeDataString(settings.Model)}",
        };

        if (settings.SmartFormat)
        {
            // smart_format is a superset of punctuate — don't add both
            parts.Add("smart_format=true");
        }
        else if (settings.Punctuate)
        {
            parts.Add("punctuate=true");
        }

        // Dictation requires punctuate (or smart_format which includes it)
        if (settings.Dictation && (settings.Punctuate || settings.SmartFormat))
        {
            parts.Add("dictation=true");
        }

        foreach (string replacement in settings.Replacements)
        {
            if (!string.IsNullOrWhiteSpace(replacement))
            {
                parts.Add($"replace={Uri.EscapeDataString(replacement)}");
            }
        }

        // Language
        if (language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("detect_language=true");
        }
        else
        {
            parts.Add($"language={Uri.EscapeDataString(language)}");
        }

        return string.Join('&', parts);
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
            {
                return string.Empty;
            }

            if (!results.TryGetProperty("channels", out var channels))
            {
                return string.Empty;
            }

            if (channels.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var channel = channels[0];
            if (!channel.TryGetProperty("alternatives", out var alts))
            {
                return string.Empty;
            }

            if (alts.GetArrayLength() == 0)
            {
                return string.Empty;
            }

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

            if (!root.TryGetProperty("results", out var results))
            {
                return null;
            }

            if (!results.TryGetProperty("channels", out var channels))
            {
                return null;
            }

            if (channels.GetArrayLength() == 0)
            {
                return null;
            }

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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _http.Dispose();
    }
}
