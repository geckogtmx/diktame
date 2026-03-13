using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Cloud TTS provider using Deepgram's Aura-2 REST API.
/// Sends text as JSON, receives raw linear16 PCM audio.
/// API reference: https://developers.deepgram.com/docs/text-to-speech
/// </summary>
public sealed class DeepgramTtsProvider : ITTSProvider
{
    private const string BaseUrl = "https://api.deepgram.com/v1/speak";
    public const int SampleRate = 24_000;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <summary>
    /// Creates a Deepgram TTS provider.
    /// </summary>
    /// <param name="apiKey">Deepgram API key (same as STT).</param>
    /// <param name="model">Voice model name (e.g. "aura-2-thalia-en"). Default: "aura-asteria-en".</param>
    /// <param name="httpClient">Optional shared HttpClient for testing / connection pooling.</param>
    public DeepgramTtsProvider(string apiKey, string model = "aura-asteria-en", HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Deepgram API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _model = model;
        ProviderName = $"Deepgram {_model}";
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
            // voiceId overrides the default model (Deepgram voices = models)
            string effectiveModel = !string.IsNullOrWhiteSpace(voiceId) ? voiceId : _model;
            string url = $"{BaseUrl}?model={Uri.EscapeDataString(effectiveModel)}&encoding=linear16&sample_rate={SampleRate}";

            string json = $"{{\"text\":{EscapeJsonString(text)}}}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("DeepgramTtsProvider: invalid API key (401)");
                return EmptyResult();
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                Log.Warning("DeepgramTtsProvider: rate limit exceeded (429)");
                return EmptyResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Log.Warning("DeepgramTtsProvider: HTTP {Status} — {Body}",
                    (int)response.StatusCode, body.Length > 200 ? body[..200] : body);
                return EmptyResult();
            }

            byte[] audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (audioBytes.Length == 0)
            {
                return EmptyResult();
            }

            // linear16 = 2 bytes per sample, mono
            var duration = TimeSpan.FromSeconds(audioBytes.Length / 2.0 / SampleRate);

            Log.Information("DeepgramTtsProvider: synthesized {Chars} chars in {Ms}ms ({Duration:F1}s audio)",
                text.Length, sw.ElapsedMilliseconds, duration.TotalSeconds);

            return new TtsResult(
                AudioData: audioBytes,
                Duration: duration,
                SampleRate: SampleRate,
                Provider: $"deepgram-{effectiveModel}",
                LatencyMs: sw.ElapsedMilliseconds,
                Format: "pcm");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DeepgramTtsProvider: synthesis failed");
            return EmptyResult();
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
        Provider: "deepgram",
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
