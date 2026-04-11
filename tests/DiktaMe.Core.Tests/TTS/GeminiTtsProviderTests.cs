
using System.Net;
using System.Net.Http;
using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

/// <summary>
/// Unit tests for <see cref="GeminiTtsProvider"/>.
/// Uses <see cref="GeminiFakeHandler"/> to avoid real network calls.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GeminiTtsProviderTests
{
    // 6 bytes of fake PCM audio → base64 "AQACAAMABAAFAA=="
    private static readonly byte[] FakeAudio = [0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04, 0x00, 0x05, 0x00];
    private static readonly string FakeAudioBase64 = Convert.ToBase64String(FakeAudio);

    private static string GeminiTtsResponse(string base64Audio) => $$"""
        {
          "candidates": [{
            "content": {
              "parts": [{
                "inlineData": { "data": "{{base64Audio}}" }
              }]
            }
          }]
        }
        """;

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeminiTtsProvider(""));
    }

    [Fact]
    public void Constructor_WhitespaceApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeminiTtsProvider("   "));
    }

    [Fact]
    public void ProviderName_ContainsGemini()
    {
        using var p = new GeminiTtsProvider("key");
        p.ProviderName.Should().Contain("Gemini");
    }

    // ── SynthesizeAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SynthesizeAsync_ValidResponse_ReturnsAudioBytes()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, GeminiTtsResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("Hello world");

        result.AudioData.Should().BeEquivalentTo(FakeAudio);
        result.SampleRate.Should().Be(GeminiTtsProvider.SampleRate);
        result.Format.Should().Be("pcm");
        result.LatencyMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task SynthesizeAsync_EmptyText_ReturnsEmptyResult()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, "should not be called");
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("");

        result.AudioData.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SynthesizeAsync_WhitespaceText_ReturnsEmptyResult()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, "should not be called");
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("   ");

        result.AudioData.Should().BeEmpty();
    }

    [Fact]
    public async Task SynthesizeAsync_401Unauthorized_ReturnsEmptyResult()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("bad-key", httpClient: http);

        var result = await p.SynthesizeAsync("Hello");

        result.AudioData.Should().BeEmpty();
    }

    [Fact]
    public async Task SynthesizeAsync_429RateLimit_ReturnsEmptyResult()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.TooManyRequests, "rate limited");
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("Hello");

        result.AudioData.Should().BeEmpty();
        // Should have retried (3 attempts total)
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task SynthesizeAsync_MalformedJson_ReturnsEmptyResult()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, "{broken json");
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("Hello");

        result.AudioData.Should().BeEmpty();
    }

    [Fact]
    public async Task SynthesizeAsync_Cancelled_ThrowsOperationCancelled()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, GeminiTtsResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => p.SynthesizeAsync("Hello", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SynthesizeAsync_OAuthToken_UsesBearerAuth()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, GeminiTtsResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("ya29.fake_oauth_token", httpClient: http);

        await p.SynthesizeAsync("Hello");

        handler.LastAuthScheme.Should().Be("Bearer");
        handler.LastAuthParameter.Should().Be("ya29.fake_oauth_token");
    }

    [Fact]
    public async Task SynthesizeAsync_ApiKey_UsesXGoogApiKeyHeader()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, GeminiTtsResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("AIzaSyTestKey", httpClient: http);

        await p.SynthesizeAsync("Hello");

        handler.LastApiKeyHeader.Should().Be("AIzaSyTestKey");
        handler.LastAuthScheme.Should().BeNull();
    }

    [Fact]
    public async Task SynthesizeAsync_500ServerError_ReturnsEmptyResult()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.InternalServerError, "server error");
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("Hello");

        result.AudioData.Should().BeEmpty();
    }

    [Fact]
    public async Task SynthesizeAsync_EmptyAudioInResponse_ReturnsEmptyResult()
    {
        const string noAudioResponse = """
            {
              "candidates": [{
                "content": { "parts": [{ "inlineData": { "data": "" } }] }
              }]
            }
            """;
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, noAudioResponse);
        using var http = new HttpClient(handler);
        using var p = new GeminiTtsProvider("key", httpClient: http);

        var result = await p.SynthesizeAsync("Hello");

        result.AudioData.Should().BeEmpty();
    }

    // ── IsAvailable ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var p = new GeminiTtsProvider("key");
        (await p.IsAvailableAsync()).Should().BeTrue();
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SynthesizeAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var handler = new GeminiFakeHandler(HttpStatusCode.OK, GeminiTtsResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        var p = new GeminiTtsProvider("key", httpClient: http);
        p.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => p.SynthesizeAsync("Hello"));
    }
}

// ── Fake handler for Gemini TTS (returns JSON string content) ────────────────

internal sealed class GeminiFakeHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }
    public string? LastApiKeyHeader { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;
        if (request.Headers.TryGetValues("x-goog-api-key", out var vals))
        {
            LastApiKeyHeader = string.Join(",", vals);
        }

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}
