using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Cloud TTS provider using OpenAI's TTS API.
/// Sends JSON with text/model/voice, receives raw PCM audio (24 kHz, 16-bit signed, little-endian, mono).
/// API reference: https://platform.openai.com/docs/guides/text-to-speech
/// </summary>
public sealed class OpenAITtsProvider : ITTSProvider
{
    private const string BaseUrl = "https://api.openai.com/v1/audio/speech";
    public const int SampleRate = 24_000;
    private const string DefaultVoice = "alloy";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly double _speed;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <summary>
    /// Creates an OpenAI TTS provider.
    /// </summary>
    /// <param name="apiKey">OpenAI API key (BYOK — same key as LLM).</param>
    /// <param name="model">TTS model: "tts-1", "tts-1-hd", or "gpt-4o-mini-tts".</param>
    /// <param name="speed">Speaking rate (0.25–4.0). Default: 1.0.</param>
    /// <param name="httpClient">Optional shared HttpClient for testing / connection pooling.</param>
    public OpenAITtsProvider(string apiKey, string model = "tts-1", double speed = 1.0, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _model = model;
        _speed = Math.Clamp(speed, 0.25, 4.0);
        ProviderName = $"OpenAI {_model}";
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
            string speedStr = _speed.ToString("F2", CultureInfo.InvariantCulture);

            string json = $"{{\"model\":{EscapeJsonString(_model)},"
                + $"\"input\":{EscapeJsonString(text)},"
                + $"\"voice\":{EscapeJsonString(effectiveVoice)},"
                + $"\"response_format\":\"pcm\","
                + $"\"speed\":{speedStr}}}";

            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("OpenAITtsProvider: invalid API key (401)");
                return EmptyResult();
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                Log.Warning("OpenAITtsProvider: rate limit exceeded (429)");
                return EmptyResult();
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Log.Warning("OpenAITtsProvider: HTTP {Status} — {Body}",
                    (int)response.StatusCode, body.Length > 200 ? body[..200] : body);
                return EmptyResult();
            }

            byte[] audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (audioBytes.Length == 0)
            {
                return EmptyResult();
            }

            // PCM: 24 kHz, 16-bit signed, mono → 2 bytes per sample
            var duration = TimeSpan.FromSeconds(audioBytes.Length / 2.0 / SampleRate);

            Log.Information("OpenAITtsProvider: synthesized {Chars} chars in {Ms}ms ({Duration:F1}s audio, voice={Voice})",
                text.Length, sw.ElapsedMilliseconds, duration.TotalSeconds, effectiveVoice);

            return new TtsResult(
                AudioData: audioBytes,
                Duration: duration,
                SampleRate: SampleRate,
                Provider: $"openai-{_model}",
                LatencyMs: sw.ElapsedMilliseconds,
                Format: "pcm");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenAITtsProvider: synthesis failed");
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
        Provider: "openai",
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
