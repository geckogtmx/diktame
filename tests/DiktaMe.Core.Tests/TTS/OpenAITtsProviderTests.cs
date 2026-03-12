using System.Net;
using System.Net.Http;
using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

/// <summary>
/// Unit tests for <see cref="OpenAITtsProvider"/>.
/// Uses <see cref="TtsFakeHandler"/> to avoid real network calls.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OpenAITtsProviderTests
{
    private static readonly byte[] FakeAudio = [0x01, 0x00, 0x02, 0x00];

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OpenAITtsProvider(""));
    }

    [Fact]
    public void Constructor_WhitespaceApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OpenAITtsProvider("   "));
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_DefaultModel_ContainsTts1()
    {
        using var provider = new OpenAITtsProvider("key");
        provider.ProviderName.Should().Be("OpenAI tts-1");
    }

    [Fact]
    public void ProviderName_CustomModel_ReflectsModel()
    {
        using var provider = new OpenAITtsProvider("key", model: "tts-1-hd");
        provider.ProviderName.Should().Be("OpenAI tts-1-hd");
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var provider = new OpenAITtsProvider("oai_key");
        (await provider.IsAvailableAsync()).Should().BeTrue();
    }

    // ── SynthesizeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Synthesize_ValidResponse_ReturnsTtsResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("oai_key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello world");

        result.IsSuccess.Should().BeTrue();
        result.AudioData.Should().Equal(FakeAudio);
        result.SampleRate.Should().Be(24_000);
        result.Format.Should().Be("pcm");
        result.Provider.Should().Contain("openai");
        result.LatencyMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Synthesize_SendsCorrectUrl()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        string url = handler.LastRequestUri?.ToString() ?? "";
        url.Should().Contain("api.openai.com/v1/audio/speech");
    }

    [Fact]
    public async Task Synthesize_SendsBearerAuthorization()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("my_oai_key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        handler.LastAuthScheme.Should().Be("Bearer");
        handler.LastAuthParameter.Should().Be("my_oai_key");
    }

    [Fact]
    public async Task Synthesize_RequestBodyContainsExpectedFields()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("key", model: "tts-1", httpClient: http);

        await provider.SynthesizeAsync("test text", voiceId: "nova");

        handler.LastBody.Should().Contain("\"model\"");
        handler.LastBody.Should().Contain("tts-1");
        handler.LastBody.Should().Contain("\"input\"");
        handler.LastBody.Should().Contain("test text");
        handler.LastBody.Should().Contain("\"voice\"");
        handler.LastBody.Should().Contain("nova");
        handler.LastBody.Should().Contain("\"response_format\"");
        handler.LastBody.Should().Contain("pcm");
        handler.LastBody.Should().Contain("\"speed\"");
    }

    [Fact]
    public async Task Synthesize_DefaultVoiceIsAlloy()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        handler.LastBody.Should().Contain("alloy");
    }

    [Fact]
    public async Task Synthesize_VoiceIdOverridesDefault()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("hello", voiceId: "shimmer");

        handler.LastBody.Should().Contain("shimmer");
        handler.LastBody.Should().NotContain("alloy");
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
        using var provider = new OpenAITtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync(text!);

        result.IsSuccess.Should().BeFalse();
    }

    // ── SynthesizeAsync — error responses ─────────────────────────────────────

    [Fact]
    public async Task Synthesize_401_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.Unauthorized, []);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("bad_key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_429_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler((HttpStatusCode)429, []);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_500_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.InternalServerError, []);
        using var http = new HttpClient(handler);
        using var provider = new OpenAITtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    // ── Speed clamping ────────────────────────────────────────────────────────

    [Fact]
    public async Task Synthesize_SpeedClampedToRange()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, FakeAudio);
        using var http = new HttpClient(handler);
        // Speed 10.0 should be clamped to 4.0
        using var provider = new OpenAITtsProvider("key", speed: 10.0, httpClient: http);

        await provider.SynthesizeAsync("hello");

        handler.LastBody.Should().Contain("4.00");
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var provider = new OpenAITtsProvider("key");
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        ex.Should().BeNull();
    }

    [Fact]
    public void SupportsStreaming_ReturnsFalse()
    {
        using var provider = new OpenAITtsProvider("key");
        provider.SupportsStreaming.Should().BeFalse();
    }
}
