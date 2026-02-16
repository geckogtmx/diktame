namespace DiktaMe.Core.Tests.STT;

using System.Net;
using System.Net.Http;
using DiktaMe.Core.STT;

/// <summary>
/// Unit tests for <see cref="DeepgramProvider"/>.
/// Uses a stub <see cref="HttpMessageHandler"/> — no real network calls.
/// </summary>
public sealed class DeepgramProviderTests : IDisposable
{
    // Minimal real Deepgram listen response JSON
    private const string ValidResponse = """
        {
          "results": {
            "channels": [{
              "alternatives": [{
                "transcript": "hello world",
                "confidence": 0.99
              }],
              "detected_language": "en"
            }]
          }
        }
        """;

    private readonly string _tmpWav;

    public DeepgramProviderTests()
    {
        // Create a minimal dummy WAV file (content doesn't matter for unit tests)
        _tmpWav = Path.GetTempFileName();
        File.WriteAllBytes(_tmpWav, [0x52, 0x49, 0x46, 0x46]); // "RIFF" header stub
    }

    public void Dispose() => File.Delete(_tmpWav);

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DeepgramProvider(""));
    }

    [Fact]
    public void Constructor_WhitespaceApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DeepgramProvider("   "));
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var provider = new DeepgramProvider("dg_test_key");
        Assert.True(await provider.IsAvailableAsync());
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_IsDeepgramNova2()
    {
        using var provider = new DeepgramProvider("key");
        Assert.Equal("Deepgram Nova-2", provider.ProviderName);
    }

    // ── TranscribeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Transcribe_ValidResponse_ReturnsTranscript()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("dg_key", http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Equal("hello world", result.Text);
        Assert.True(result.IsSuccess);
        Assert.Equal("Deepgram Nova-2", result.Provider);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task Transcribe_AutoLanguage_SendsDetectLanguageParam()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("dg_key", http);

        await provider.TranscribeAsync(_tmpWav, "auto");

        Assert.Contains("detect_language=true", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Transcribe_SpecificLanguage_SendsLanguageParam()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("dg_key", http);

        await provider.TranscribeAsync(_tmpWav, "es");

        Assert.Contains("language=es", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Transcribe_RequestHasTokenAuthorization()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("my_dg_key", http);

        await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Equal("Token", handler.LastAuthScheme);
        Assert.Equal("my_dg_key", handler.LastAuthParameter);
    }

    // ── TranscribeAsync — error responses ─────────────────────────────────────

    [Fact]
    public async Task Transcribe_401_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("bad_key", http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    [Fact]
    public async Task Transcribe_429_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler((HttpStatusCode)429, "");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    [Fact]
    public async Task Transcribe_500_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "error body");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    // ── JSON edge cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task Transcribe_EmptyChannels_ReturnsEmptyText()
    {
        const string emptyChannels = """{"results":{"channels":[]}}""";

        var handler = new FakeHttpHandler(HttpStatusCode.OK, emptyChannels);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Text);
    }

    [Fact]
    public async Task Transcribe_MalformedJson_ReturnsEmptyText()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "not-json-{{{{");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.False(result.IsSuccess);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var provider = new DeepgramProvider("key");
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }
}

/// <summary>Stub HTTP handler that returns a canned response.</summary>
internal sealed class FakeHttpHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}
