using System.Net;
using System.Net.Http;
using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

/// <summary>
/// Unit tests for <see cref="InworldTtsProvider"/>.
/// Uses <see cref="TtsFakeHandler"/> with JSON responses containing base64 audio.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InworldTtsProviderTests
{
    // 4 bytes of fake PCM audio (2 samples at 16-bit)
    private static readonly byte[] FakeAudio = [0x01, 0x00, 0x02, 0x00];
    private static readonly string FakeAudioBase64 = Convert.ToBase64String(FakeAudio);

    /// <summary>Builds a valid Inworld JSON response with base64 audio.</summary>
    private static byte[] MakeJsonResponse(string base64Audio)
    {
        string json = $"{{\"audioContent\":\"{base64Audio}\"}}";
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InworldTtsProvider(""));
    }

    [Fact]
    public void Constructor_WhitespaceApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InworldTtsProvider("   "));
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_DefaultModel_ContainsTts15Max()
    {
        using var provider = new InworldTtsProvider("key");
        provider.ProviderName.Should().Contain("inworld-tts-1.5-max");
    }

    [Fact]
    public void ProviderName_CustomModel_ReflectsModel()
    {
        using var provider = new InworldTtsProvider("key", model: "inworld-tts-1.5-mini");
        provider.ProviderName.Should().Be("Inworld inworld-tts-1.5-mini");
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var provider = new InworldTtsProvider("iw_key");
        (await provider.IsAvailableAsync()).Should().BeTrue();
    }

    // ── SynthesizeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Synthesize_ValidResponse_ReturnsTtsResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("iw_key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello world");

        result.IsSuccess.Should().BeTrue();
        result.AudioData.Should().Equal(FakeAudio);
        result.SampleRate.Should().Be(24_000);
        result.Format.Should().Be("pcm");
        result.Provider.Should().Contain("inworld");
        result.LatencyMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Synthesize_SendsCorrectUrl()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        string url = handler.LastRequestUri?.ToString() ?? "";
        url.Should().Contain("api.inworld.ai/tts/v1/voice");
    }

    [Fact]
    public async Task Synthesize_SendsBasicAuthorization()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("my_iw_key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        handler.LastAuthScheme.Should().Be("Basic");
        handler.LastAuthParameter.Should().Be("my_iw_key");
    }

    [Fact]
    public async Task Synthesize_RequestBodyContainsExpectedFields()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("test text", voiceId: "Ashley");

        handler.LastBody.Should().Contain("\"text\"");
        handler.LastBody.Should().Contain("test text");
        handler.LastBody.Should().Contain("\"voiceId\"");
        handler.LastBody.Should().Contain("Ashley");
        handler.LastBody.Should().Contain("\"modelId\"");
        handler.LastBody.Should().Contain("\"audioConfig\"");
        handler.LastBody.Should().Contain("LINEAR16");
        handler.LastBody.Should().Contain("24000");
    }

    [Fact]
    public async Task Synthesize_DefaultVoiceIsTimothy()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("hello");

        handler.LastBody.Should().Contain("Timothy");
    }

    [Fact]
    public async Task Synthesize_VoiceIdOverridesDefault()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        await provider.SynthesizeAsync("hello", voiceId: "Deborah");

        handler.LastBody.Should().Contain("Deborah");
        handler.LastBody.Should().NotContain("Timothy");
    }

    // ── ParseAudioContent ─────────────────────────────────────────────────────

    [Fact]
    public void ParseAudioContent_ValidBase64_ReturnsBytes()
    {
        string json = $"{{\"audioContent\":\"{FakeAudioBase64}\"}}";
        var result = InworldTtsProvider.ParseAudioContent(json);
        result.Should().Equal(FakeAudio);
    }

    [Fact]
    public void ParseAudioContent_WavHeader_StripsFirst44Bytes()
    {
        // Build fake WAV: "RIFF" header (44 bytes) + PCM data
        var wavData = new byte[48];
        wavData[0] = (byte)'R';
        wavData[1] = (byte)'I';
        wavData[2] = (byte)'F';
        wavData[3] = (byte)'F';
        wavData[44] = 0x01;
        wavData[45] = 0x02;
        wavData[46] = 0x03;
        wavData[47] = 0x04;

        string base64 = Convert.ToBase64String(wavData);
        string json = $"{{\"audioContent\":\"{base64}\"}}";

        var result = InworldTtsProvider.ParseAudioContent(json);
        result.Should().Equal([0x01, 0x02, 0x03, 0x04]);
    }

    [Fact]
    public void ParseAudioContent_MissingField_ReturnsEmpty()
    {
        var result = InworldTtsProvider.ParseAudioContent("{}");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAudioContent_EmptyBase64_ReturnsEmpty()
    {
        var result = InworldTtsProvider.ParseAudioContent("{\"audioContent\":\"\"}");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAudioContent_InvalidJson_ReturnsEmpty()
    {
        var result = InworldTtsProvider.ParseAudioContent("not-json{{");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseAudioContent_InvalidBase64_ReturnsEmpty()
    {
        var result = InworldTtsProvider.ParseAudioContent("{\"audioContent\":\"!!!invalid!!!\"}");
        result.Should().BeEmpty();
    }

    // ── SynthesizeAsync — empty/whitespace ────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Synthesize_EmptyText_ReturnsEmptyResult(string? text)
    {
        var handler = new TtsFakeHandler(HttpStatusCode.OK, MakeJsonResponse(FakeAudioBase64));
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync(text!);

        result.IsSuccess.Should().BeFalse();
    }

    // ── SynthesizeAsync — error responses ─────────────────────────────────────

    [Fact]
    public async Task Synthesize_401_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.Unauthorized, []);
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("bad_key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_429_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler((HttpStatusCode)429, []);
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_500_ReturnsEmptyResult()
    {
        var handler = new TtsFakeHandler(HttpStatusCode.InternalServerError, []);
        using var http = new HttpClient(handler);
        using var provider = new InworldTtsProvider("key", httpClient: http);

        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var provider = new InworldTtsProvider("key");
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        ex.Should().BeNull();
    }

    [Fact]
    public void SupportsStreaming_ReturnsFalse()
    {
        using var provider = new InworldTtsProvider("key");
        provider.SupportsStreaming.Should().BeFalse();
    }
}
