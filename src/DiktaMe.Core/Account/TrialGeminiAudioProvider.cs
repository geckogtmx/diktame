
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Security;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Account;
/// <summary>
/// STT provider that routes audio transcription through the managed Gemini proxy
/// at dikta.me (Supabase Edge Function) using a trial JWT for authentication.
/// Mirrors <see cref="GeminiAudioProvider"/> but uses Bearer JWT auth instead of API key.
/// Used when <see cref="Config.AuthMode"/> is <see cref="Config.AuthMode.Trial"/>.
/// </summary>
public sealed class TrialGeminiAudioProvider : ISTTProvider, IDisposable
{
    private const string ProxyUrl =
        "https://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/gemini-proxy";
    private const string TokenKey = "trial_token";

    private readonly SecureStorage _secureStorage;
    private readonly ITrialAccountService _trialService;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => "Gemini Audio (Trial)";

    public TrialGeminiAudioProvider(
        SecureStorage secureStorage,
        ITrialAccountService trialService,
        HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _trialService = trialService;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_secureStorage.RetrieveKey(TokenKey) is not null);

    /// <inheritdoc/>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TrialGeminiAudioProvider: no trial token available.");
        }

        var sw = Stopwatch.StartNew();

        byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken)
            .ConfigureAwait(false);
        string audioBase64 = Convert.ToBase64String(audioBytes);

        string prompt = BuildTranscriptionPrompt(language);
        string requestJson = BuildRequestJson(prompt, audioBase64);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ProxyUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(requestJson, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await _http.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("TrialGeminiAudioProvider: 401 — token expired, auto-logout");
                await _trialService.LogoutAsync(cancellationToken).ConfigureAwait(false);
                return new TranscriptionResult
                {
                    Text = string.Empty,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.Warning("TrialGeminiAudioProvider: 403 — quota exceeded");
                return new TranscriptionResult
                {
                    Text = string.Empty,
                    Provider = ProviderName,
                    LatencyMs = sw.ElapsedMilliseconds,
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"TrialGeminiAudioProvider: status {(int)response.StatusCode}: {errBody}");
            }

            sw.Stop();
            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            string transcript = ParseTranscript(json);

            // Report usage (approximate word count from transcript)
            int wordCount = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            _ = _trialService.RecordUsageAsync("gemini-audio", "gemini-2.5-flash", wordCount, cancellationToken);

            Log.Information("TrialGeminiAudioProvider: transcribed in {Ms}ms — \"{Preview}\"",
                sw.ElapsedMilliseconds,
                transcript.Length > 80 ? transcript[..80] + "..." : transcript);

            return new TranscriptionResult
            {
                Text = transcript,
                DetectedLanguage = language.Equals("auto", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : language,
                LatencyMs = sw.ElapsedMilliseconds,
                Provider = ProviderName,
            };
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrialGeminiAudioProvider: network error");
            throw new InvalidOperationException("TrialGeminiAudioProvider: network error.", ex);
        }
    }

    // ── Helpers (same as GeminiAudioProvider) ────────────────────────────────

    private static string BuildTranscriptionPrompt(string language)
    {
        if (language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return "Transcribe the following audio exactly. Output only the transcribed text, nothing else.";
        }

        return $"Transcribe the following audio exactly in {language}. Output only the transcribed text, nothing else.";
    }

    private static string BuildRequestJson(string prompt, string audioBase64)
    {
        string escapedPrompt = prompt
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);

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

    private static string ParseTranscript(string json)
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
                parts[0].TryGetProperty("text", out var text))
            {
                return text.GetString()?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "TrialGeminiAudioProvider: failed to parse transcript JSON");
            return string.Empty;
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
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
