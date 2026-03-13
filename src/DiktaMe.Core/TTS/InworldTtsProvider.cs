using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Cloud TTS provider using Inworld's Voice API (REST, non-streaming).
/// Sends JSON with text/voice/model, receives JSON with base64-encoded audio.
/// API reference: https://docs.inworld.ai/docs/tts-api/reference/
/// </summary>
public sealed class InworldTtsProvider : ITTSProvider
{
    private const string BaseUrl = "https://api.inworld.ai/tts/v1/voice";
    public const int SampleRate = 24_000;
    private const string DefaultVoice = "Timothy";
    private const string DefaultModel = "inworld-tts-1.5-max";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <summary>
    /// Creates an Inworld TTS provider.
    /// </summary>
    /// <param name="apiKey">Inworld API key (Base64 format).</param>
    /// <param name="model">Model ID: "inworld-tts-1.5-max" or "inworld-tts-1.5-mini".</param>
    /// <param name="httpClient">Optional shared HttpClient for testing / connection pooling.</param>
    public InworldTtsProvider(string apiKey, string model = DefaultModel, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Inworld API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _model = model;
        ProviderName = $"Inworld {_model}";
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));

    /// <inheritdoc/>
    public async Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return EmptyResult();
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();

        try
        {
            string effectiveVoice = !string.IsNullOrWhiteSpace(voiceId) ? voiceId : DefaultVoice;

            string json = $"{{\"text\":{EscapeJsonString(text)},"
                + $"\"voiceId\":{EscapeJsonString(effectiveVoice)},"
                + $"\"modelId\":{EscapeJsonString(_model)},"
                + $"\"audioConfig\":{{\"audioEncoding\":\"LINEAR16\",\"sampleRateHertz\":{SampleRate}}}}}";

            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("InworldTtsProvider: invalid API key (401)");
                return EmptyResult();
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                Log.Warning("InworldTtsProvider: rate limit exceeded (429)");
                return EmptyResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Log.Warning("InworldTtsProvider: HTTP {Status} — {Body}",
                    (int)response.StatusCode, errorBody.Length > 200 ? errorBody[..200] : errorBody);
                return EmptyResult();
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            byte[] audioBytes = ParseAudioContent(responseJson);

            sw.Stop();

            if (audioBytes.Length == 0)
            {
                return EmptyResult();
            }

            // LINEAR16 = 2 bytes per sample, mono
            var duration = TimeSpan.FromSeconds(audioBytes.Length / 2.0 / SampleRate);

            Log.Information("InworldTtsProvider: synthesized {Chars} chars in {Ms}ms ({Duration:F1}s audio, voice={Voice})",
                text.Length, sw.ElapsedMilliseconds, duration.TotalSeconds, effectiveVoice);

            return new TtsResult(
                AudioData: audioBytes,
                Duration: duration,
                SampleRate: SampleRate,
                Provider: $"inworld-{_model}",
                LatencyMs: sw.ElapsedMilliseconds,
                Format: "pcm");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "InworldTtsProvider: synthesis failed");
            return EmptyResult();
        }
    }

    /// <summary>
    /// Parses audio content from Inworld's JSON response.
    /// Extracts base64-encoded audio and strips WAV header if present.
    /// </summary>
    internal static byte[] ParseAudioContent(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("audioContent", out var audioElement))
            {
                return [];
            }

            string? base64 = audioElement.GetString();
            if (string.IsNullOrEmpty(base64))
            {
                return [];
            }

            byte[] raw = Convert.FromBase64String(base64);

            // Strip WAV header (44 bytes) if present — Inworld may wrap PCM in WAV container
            if (raw.Length > 44 && raw[0] == 'R' && raw[1] == 'I' && raw[2] == 'F' && raw[3] == 'F')
            {
                return raw[44..];
            }

            return raw;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "InworldTtsProvider: failed to parse response JSON");
            return [];
        }
        catch (FormatException ex)
        {
            Log.Warning(ex, "InworldTtsProvider: failed to decode base64 audio");
            return [];
        }
    }

    /// <summary>Escapes a string for safe embedding in a JSON value.</summary>
    private static string EscapeJsonString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static TtsResult EmptyResult() => new(
        AudioData: [],
        Duration: TimeSpan.Zero,
        SampleRate: SampleRate,
        Provider: "inworld",
        LatencyMs: 0,
        Format: "pcm");

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
