namespace DiktaMe.Core.Tests.STT;

using System.Net;
using System.Net.Http;
using DiktaMe.Core.STT;

/// <summary>
/// Unit tests for <see cref="GeminiAudioProvider"/>.
/// Uses <see cref="FakeHttpHandler"/> — no real network calls.
/// </summary>
public sealed class GeminiAudioProviderTests : IDisposable
{
    // Minimal Gemini generateContent response
    private const string ValidResponse = """
        {
          "candidates": [{
            "content": {
              "parts": [{ "text": "hello from gemini" }],
              "role": "model"
            },
            "finishReason": "STOP"
          }]
        }
        """;

    private readonly string _tmpWav;

    public GeminiAudioProviderTests()
    {
        _tmpWav = Path.GetTempFileName();
        File.WriteAllBytes(_tmpWav, [0x52, 0x49, 0x46, 0x46]); // stub WAV bytes
    }

    public void Dispose() => File.Delete(_tmpWav);

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeminiAudioProvider(""));
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var provider = new GeminiAudioProvider("key");
        Assert.True(await provider.IsAvailableAsync());
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ContainsGemini()
    {
        using var provider = new GeminiAudioProvider("key");
        Assert.Contains("Gemini", provider.ProviderName);
    }

    [Fact]
    public void ProviderName_CustomModel_ContainsModelName()
    {
        using var provider = new GeminiAudioProvider("key", "gemini-1.5-pro");
        Assert.Contains("gemini-1.5-pro", provider.ProviderName);
    }

    // ── TranscribeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Transcribe_ValidResponse_ReturnsTranscript()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("ai_key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Equal("hello from gemini", result.Text);
        Assert.True(result.IsSuccess);
        Assert.Contains("Gemini", result.Provider);
    }

    [Fact]
    public async Task Transcribe_ApiKeyEmbeddedInUrl()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("my_secret_key", httpClient: http);

        await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Contains("my_secret_key", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Transcribe_AutoLanguage_ReturnsNullDetectedLanguage()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "auto");

        // Gemini doesn't return a structured language field
        Assert.Null(result.DetectedLanguage);
    }

    [Fact]
    public async Task Transcribe_SpecificLanguage_ReturnsLanguagePassthrough()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "fr");

        Assert.Equal("fr", result.DetectedLanguage);
    }

    // ── TranscribeAsync — error responses ─────────────────────────────────────

    [Fact]
    public async Task Transcribe_401_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("key", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    [Fact]
    public async Task Transcribe_429_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler((HttpStatusCode)429, "");
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("key", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    // ── JSON edge cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task Transcribe_EmptyCandidates_ReturnsEmptyText()
    {
        const string noCandidates = """{"candidates":[]}""";

        var handler = new FakeHttpHandler(HttpStatusCode.OK, noCandidates);
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Text);
    }

    [Fact]
    public async Task Transcribe_TextIsWhitespaceTrimmed()
    {
        const string paddedText = """
            {
              "candidates": [{
                "content": {
                  "parts": [{ "text": "  trimmed text  " }]
                }
              }]
            }
            """;

        var handler = new FakeHttpHandler(HttpStatusCode.OK, paddedText);
        using var http = new HttpClient(handler);
        using var provider = new GeminiAudioProvider("key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Equal("trimmed text", result.Text);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var provider = new GeminiAudioProvider("key");
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }
}
