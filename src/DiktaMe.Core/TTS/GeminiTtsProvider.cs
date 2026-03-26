using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Cloud TTS provider using Google's Gemini TTS API.
/// Sends text via the generateContent endpoint with responseModalities=["AUDIO"],
/// receives base64-encoded PCM audio (24 kHz, 16-bit signed, little-endian, mono).
/// Reuses the same Gemini API key already configured for LLM.
/// </summary>
public sealed class GeminiTtsProvider : ITTSProvider
{
    private const string ApiBase =
        "https://generativelanguage.googleapis.com/v1beta/models";
    public const int SampleRate = 24_000;
    private const string DefaultVoice = "Kore";
    private const string DefaultModel = "gemini-2.5-flash-preview-tts";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly bool _isOAuth;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <summary>
    /// Creates a Gemini TTS provider.
    /// </summary>
    /// <param name="apiKey">Gemini API key (AIza…) or OAuth Bearer token (ya29.…).</param>
    /// <param name="model">TTS model: "gemini-2.5-flash-preview-tts" or "gemini-2.5-pro-preview-tts".</param>
    /// <param name="httpClient">Optional shared HttpClient for testing / connection pooling.</param>
    public GeminiTtsProvider(string apiKey, string model = DefaultModel, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Gemini API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _model = model;
        _isOAuth = apiKey.StartsWith("ya29.", StringComparison.Ordinal);
        ProviderName = $"Gemini {_model}";
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
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

        string effectiveVoice = !string.IsNullOrWhiteSpace(voiceId) ? voiceId : DefaultVoice;
        string json = BuildRequestJson(text, effectiveVoice);

        string url = $"{ApiBase}/{_model}:generateContent";

        var sw = Stopwatch.StartNew();

        const int MaxRetries = 3;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                if (_isOAuth)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }
                else
                {
                    request.Headers.Add("x-goog-api-key", _apiKey);
                }

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Log.Warning("GeminiTtsProvider: invalid API key (401)");
                    return EmptyResult();
                }

                if ((int)response.StatusCode == 429)
                {
                    if (attempt < MaxRetries - 1)
                    {
                        Log.Warning("GeminiTtsProvider: rate limited (429), retrying attempt {A}/{Max}",
                            attempt + 1, MaxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    Log.Warning("GeminiTtsProvider: rate limit exceeded after {Max} attempts", MaxRetries);
                    return EmptyResult();
                }

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    Log.Warning("GeminiTtsProvider: HTTP {Status} — {Body}",
                        (int)response.StatusCode, body.Length > 200 ? body[..200] : body);
                    return EmptyResult();
                }

                string responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                byte[] audioBytes = ParseAudioResponse(responseJson);
                sw.Stop();

                if (audioBytes.Length == 0)
                {
                    Log.Warning("GeminiTtsProvider: response contained no audio data");
                    return EmptyResult();
                }

                // PCM: 24 kHz, 16-bit signed, mono → 2 bytes per sample
                var duration = TimeSpan.FromSeconds(audioBytes.Length / 2.0 / SampleRate);

                Log.Information("GeminiTtsProvider: synthesized {Chars} chars in {Ms}ms ({Duration:F1}s audio, voice={Voice})",
                    text.Length, sw.ElapsedMilliseconds, duration.TotalSeconds, effectiveVoice);

                return new TtsResult(
                    AudioData: audioBytes,
                    Duration: duration,
                    SampleRate: SampleRate,
                    Provider: $"gemini-{_model}",
                    LatencyMs: sw.ElapsedMilliseconds,
                    Format: "pcm");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Log.Warning(ex, "GeminiTtsProvider: network error on attempt {A}/{Max}",
                    attempt + 1, MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GeminiTtsProvider: synthesis failed");
                return EmptyResult();
            }
        }

        return EmptyResult();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string BuildRequestJson(string text, string voiceName)
    {
        string escapedText = EscapeJsonString(text);
        string escapedVoice = EscapeJsonString(voiceName);

        return $$"""
            {
              "contents": [{ "parts": [{ "text": {{escapedText}} }] }],
              "generationConfig": {
                "responseModalities": ["AUDIO"],
                "speechConfig": {
                  "voiceConfig": {
                    "prebuiltVoiceConfig": {
                      "voiceName": {{escapedVoice}}
                    }
                  }
                }
              }
            }
            """;
    }

    /// <summary>
    /// Extracts PCM audio bytes from the Gemini generateContent response.
    /// Path: candidates[0].content.parts[0].inlineData.data (base64-encoded).
    /// </summary>
    private static byte[] ParseAudioResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("inlineData", out var inlineData) &&
                inlineData.TryGetProperty("data", out var data))
            {
                string base64 = data.GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(base64))
                {
                    return Convert.FromBase64String(base64);
                }
            }

            Log.Warning("GeminiTtsProvider: could not find audio data in response");
            return [];
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "GeminiTtsProvider: failed to parse response JSON");
            return [];
        }
    }

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
        Provider: "gemini",
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
