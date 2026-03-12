using System.Net;
using System.Net.Http;
using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

/// <summary>
/// Unit tests for <see cref="DeepgramTtsProvider"/>.
/// Uses a <see cref="TtsFakeHandler"/> to avoid real network calls.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeepgramTtsProviderTests
{
    // 4 bytes of fake PCM audio (2 samples at 16-bit)
    private static readonly byte[] FakeAudio = [0x01, 0x00, 0x02, 0x00];

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DeepgramTtsProvider(""));
    }

    [Fact]
    public void Constructor_WhitespaceApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DeepgramTtsProvider("   "));
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_DefaultModel_ContainsAura()
    {
        using var provider = new DeepgramTtsProvider("key");
        provider.ProviderName.Should().Contain("aura-asteria-en");
    }

    [Fact]
    public void ProviderName_CustomModel_ReflectsModel()
    {
        using var provider = new DeepgramTtsProvider("key", model: "aura-2-thalia-en");
        provider.ProviderName.Should().Be("Deepgram aura-2-thalia-en");
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var provider = new DeepgramTtsProvider("dg_key");
        (await provider.IsAvailableAsync()).Should().BeTrue();
    }

    // ── SynthesizeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Synthesize_ValidResponse_ReturnsTtsResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("dg_key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello world");

        result.IsSuccess.Should().BeTrue();
        result.AudioData.Should().Equal(FakeAudio);
        result.SampleRate.Should().Be(24_000);
        result.Format.Should().Be("pcm");
        result.Provider.Should().Contain("deepgram");
        result.LatencyMs.Should().BeGreaterOrEqualTo(0);
        result.Duration.TotalSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Synthesize_SendsCorrectUrl()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("dg_key", model: "aura-2-thalia-en", httpClient: http);

        await provider.SynthesizeAsync("hello");

        string url = handler.LastRequestUri?.ToString() ?? "";
        url.Should().Contain("api.deepgram.com/v1/speak");
        url.Should().Contain("model=aura-2-thalia-en");
        url.Should().Contain("encoding=linear16");
        url.Should().Contain("sample_rate=24000");
    }

    [Fact]
    public async Task Synthesize_SendsTokenAuthorization()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("my_dg_key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        handler.LastAuthScheme.Should().Be("Token");
        handler.LastAuthParameter.Should().Be("my_dg_key");
    }

    [Fact]
    public async Task Synthesize_RequestBodyContainsText()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("test text");

        handler.LastBody.Should().Contain("\"text\"");
        handler.LastBody.Should().Contain("test text");
    }

    [Fact]
    public async Task Synthesize_VoiceIdOverridesModel()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("key", model: "aura-asteria-en", httpClient: http);

        await provider.SynthesizeAsync("hello", voiceId: "aura-2-thalia-en");

        string url = handler.LastRequestUri?.ToString() ?? "";
        url.Should().Contain("model=aura-2-thalia-en");
        url.Should().NotContain("model=aura-asteria-en");
    }

    // ── SynthesizeAsync — empty/whitespace ────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Synthesize_EmptyText_ReturnsEmptyResult(string? text)
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync(text!);

        result.IsSuccess.Should().BeFalse();
        result.AudioData.Should().BeEmpty();
    }

    // ── SynthesizeAsync — error responses ─────────────────────────────────────

    [Fact]
    public async Task Synthesize_401_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.Unauthorized, []);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("bad_key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_429_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler((HttpStatusCode)429, []);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_500_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.InternalServerError, []);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramTtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var provider = new DeepgramTtsProvider("key");
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        ex.Should().BeNull();
    }

    [Fact]
    public void SupportsStreaming_ReturnsFalse()
    {
        using var provider = new DeepgramTtsProvider("key");
        provider.SupportsStreaming.Should().BeFalse();
    }
}

/// <summary>
/// Stub HTTP handler that returns a fixed byte[] response for TTS tests.
/// Captures request details for assertions.
/// </summary>
internal sealed class TtsFakeHandler(HttpStatusCode statusCode, byte[] audioBytes)
    : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;

        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent(audioBytes),
        };
    }
}
