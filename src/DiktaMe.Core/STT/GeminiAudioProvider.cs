namespace DiktaMe.Core.STT;

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;

/// <summary>
/// STT provider that sends audio directly to Google Gemini's multimodal API.
/// Gemini understands audio natively — no separate speech pipeline needed.
/// Transcription is performed as a single generateContent call with the audio
/// file inline-encoded as base64.
/// API reference: https://ai.google.dev/gemini-api/docs/audio
/// Port of V1's CloudProcessor (Gemini branch) in processor.py.
/// </summary>
public sealed class GeminiAudioProvider : ISTTProvider, IDisposable
{
    // Default model — fast and capable for transcription
    private const string DefaultModel = "gemini-2.0-flash";
    private const string ApiBase =
        "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => $"Gemini {_model}";

    /// <summary>
    /// Initialises the provider with an API key and optional model override.
    /// </summary>
    /// <param name="apiKey">Google AI API key.</param>
    /// <param name="model">Gemini model identifier (default: <c>gemini-2.0-flash</c>).</param>
    /// <param name="httpClient">
    /// Optional shared <see cref="HttpClient"/> (for testing / connection pooling).
    /// </param>
    public GeminiAudioProvider(
        string apiKey,
        string model = DefaultModel,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Gemini API key must not be empty.", nameof(apiKey));

        _apiKey = apiKey;
        _model = model;
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync()
        => Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));

    /// <inheritdoc/>
    /// <remarks>
    /// The audio file is sent as an inline base64-encoded part alongside a
    /// transcription instruction. Language hint <c>"auto"</c> omits the hint
    /// so Gemini performs its own language detection.
    /// </remarks>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = Stopwatch.StartNew();

        byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath).ConfigureAwait(false);
        string audioBase64 = Convert.ToBase64String(audioBytes);

        string prompt = BuildTranscriptionPrompt(language);
        string requestJson = BuildRequestJson(prompt, audioBase64);

        string url = $"{ApiBase}/{_model}:generateContent?key={_apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(requestJson, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "GeminiAudioProvider: network error");
            throw;
        }

        sw.Stop();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Gemini: invalid API key (401).");

        if (response.StatusCode == (HttpStatusCode)429)
            throw new InvalidOperationException("Gemini: rate limit exceeded (429).");

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Gemini: unexpected status {(int)response.StatusCode}: {body}");
        }

        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string transcript = ParseTranscript(json);

        Log.Information("GeminiAudioProvider: transcribed in {Ms}ms — \"{Preview}\"",
            sw.ElapsedMilliseconds,
            transcript.Length > 80 ? transcript[..80] + "…" : transcript);

        return new TranscriptionResult
        {
            Text = transcript,
            DetectedLanguage = language.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? null   // Gemini doesn't return a structured lang field
                : language,
            LatencyMs = sw.ElapsedMilliseconds,
            Provider = ProviderName,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildTranscriptionPrompt(string language)
    {
        if (language.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return "Transcribe the following audio exactly. Output only the transcribed text, nothing else.";

        return $"Transcribe the following audio exactly in {language}. Output only the transcribed text, nothing else.";
    }

    private static string BuildRequestJson(string prompt, string audioBase64)
    {
        // Escape the prompt as a JSON string literal manually (prompt is an internal constant,
        // only contains ASCII — this avoids JsonSerializer.Serialize which is not trim-safe).
        string escapedPrompt = prompt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");

        return $$"""
            {
              "contents": [{
                "parts": [
                  { "text": "{{escapedPrompt}}" },
                  {
                    "inlineData": {
                      "mimeType": "audio/wav",
                      "data": "{{audioBase64}}"
                    }
                  }
                ]
              }]
            }
            """;
    }

    /// <summary>
    /// Parses the text from a Gemini generateContent response.
    /// Path: candidates[0].content.parts[0].text
    /// </summary>
    private static string ParseTranscript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates))
                return string.Empty;

            if (candidates.GetArrayLength() == 0)
                return string.Empty;

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var content))
                return string.Empty;

            if (!content.TryGetProperty("parts", out var parts))
                return string.Empty;

            if (parts.GetArrayLength() == 0)
                return string.Empty;

            return parts[0].TryGetProperty("text", out var text)
                ? text.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "GeminiAudioProvider: failed to parse transcript JSON");
            return string.Empty;
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
