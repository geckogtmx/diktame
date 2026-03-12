using DiktaMe.Core.TTS;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class TTSRouterTests
{
    // ── Result helpers ───────────────────────────────────────────────────────

    private static TtsResult OkResult(string provider) => new(
        AudioData: [1, 2, 3],
        Duration: TimeSpan.FromSeconds(1),
        SampleRate: 24_000,
        Provider: provider,
        LatencyMs: 50,
        Format: "pcm");

    private static TtsResult EmptyResult(string provider) => new(
        AudioData: [],
        Duration: TimeSpan.Zero,
        SampleRate: 24_000,
        Provider: provider,
        LatencyMs: 0,
        Format: "pcm");

    // ── Mock helpers ─────────────────────────────────────────────────────────

    private static Mock<ITTSProvider> MockProvider(string name, bool streaming = false)
    {
        var mock = new Mock<ITTSProvider>();
        mock.Setup(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.SupportsStreaming).Returns(streaming);
        return mock;
    }

    // ── ProviderName ─────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_NullFallback_ReturnsPrimaryName()
    {
        var primary = MockProvider("Kokoro");
        var router = new TTSRouter(primary.Object);

        router.ProviderName.Should().Be("Kokoro");
    }

    [Fact]
    public void ProviderName_WithFallback_IncludesBoth()
    {
        var primary = MockProvider("Kokoro");
        var fallback = MockProvider("Deepgram");
        var router = new TTSRouter(primary.Object, fallback.Object);

        router.ProviderName.Should().Contain("Kokoro");
        router.ProviderName.Should().Contain("Deepgram");
    }

    // ── SupportsStreaming ────────────────────────────────────────────────────

    [Fact]
    public void SupportsStreaming_DelegatesToPrimary_True()
    {
        var primary = MockProvider("Test", streaming: true);
        var router = new TTSRouter(primary.Object);

        router.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public void SupportsStreaming_DelegatesToPrimary_False()
    {
        var primary = MockProvider("Test", streaming: false);
        var router = new TTSRouter(primary.Object);

        router.SupportsStreaming.Should().BeFalse();
    }

    // ── SynthesizeAsync — primary succeeds ───────────────────────────────────

    [Fact]
    public async Task Synthesize_PrimarySucceeds_ReturnsPrimaryResult()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult("Kokoro"));

        var router = new TTSRouter(primary.Object);
        var result = await router.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeTrue();
        result.Provider.Should().Be("Kokoro");
    }

    [Fact]
    public async Task Synthesize_PrimarySucceeds_FallbackNotCalled()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult("Kokoro"));

        var fallback = MockProvider("Deepgram");
        var router = new TTSRouter(primary.Object, fallback.Object);

        await router.SynthesizeAsync("hello");

        fallback.Verify(p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Synthesize_PassesVoiceId_ToPrimary()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", "af_sarah", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult("Kokoro"));

        var router = new TTSRouter(primary.Object);
        var result = await router.SynthesizeAsync("hello", "af_sarah");

        result.IsSuccess.Should().BeTrue();
        primary.Verify(p => p.SynthesizeAsync("hello", "af_sarah", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SynthesizeAsync — fallback on empty ──────────────────────────────────

    [Fact]
    public async Task Synthesize_PrimaryReturnsEmpty_UsesFallback()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult("Kokoro"));

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult("Deepgram"));

        var router = new TTSRouter(primary.Object, fallback.Object);
        var result = await router.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeTrue();
        result.Provider.Should().Be("Deepgram");
    }

    // ── SynthesizeAsync — fallback on exception ──────────────────────────────

    [Fact]
    public async Task Synthesize_PrimaryThrows_UsesFallback()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("model not loaded"));

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult("Deepgram"));

        var router = new TTSRouter(primary.Object, fallback.Object);
        var result = await router.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeTrue();
        result.Provider.Should().Be("Deepgram");
    }

    // ── SynthesizeAsync — both fail ──────────────────────────────────────────

    [Fact]
    public async Task Synthesize_BothFail_ReturnsEmptyResult()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("fallback failed"));

        var router = new TTSRouter(primary.Object, fallback.Object);
        var result = await router.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
        result.Provider.Should().Be("none");
    }

    [Fact]
    public async Task Synthesize_PrimaryFails_NoFallback_ReturnsEmptyResult()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("failed"));

        var router = new TTSRouter(primary.Object);
        var result = await router.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
        result.Provider.Should().Be("none");
    }

    [Fact]
    public async Task Synthesize_BothReturnEmpty_ReturnsEmptyResult()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult("Kokoro"));

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult("Deepgram"));

        var router = new TTSRouter(primary.Object, fallback.Object);
        var result = await router.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
    }

    // ── SynthesizeAsync — cancellation ───────────────────────────────────────

    [Fact]
    public async Task Synthesize_PrimaryCancelled_Throws()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var router = new TTSRouter(primary.Object);

        var act = () => router.SynthesizeAsync("hello");
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Synthesize_FallbackCancelled_Throws()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("primary failed"));

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.SynthesizeAsync("hello", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var router = new TTSRouter(primary.Object, fallback.Object);

        var act = () => router.SynthesizeAsync("hello");
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── IsAvailableAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_PrimaryAvailable_ReturnsTrue()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var router = new TTSRouter(primary.Object);

        (await router.IsAvailableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailable_PrimaryUnavailable_FallbackAvailable_ReturnsTrue()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var router = new TTSRouter(primary.Object, fallback.Object);

        (await router.IsAvailableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailable_BothUnavailable_ReturnsFalse()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var fallback = MockProvider("Deepgram");
        fallback.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var router = new TTSRouter(primary.Object, fallback.Object);

        (await router.IsAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailable_PrimaryUnavailable_NoFallback_ReturnsFalse()
    {
        var primary = MockProvider("Kokoro");
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var router = new TTSRouter(primary.Object);

        (await router.IsAvailableAsync()).Should().BeFalse();
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DisposesBothProviders()
    {
        var primary = MockProvider("Kokoro");
        var fallback = MockProvider("Deepgram");
        var router = new TTSRouter(primary.Object, fallback.Object);

        router.Dispose();

        primary.Verify(p => p.Dispose(), Times.Once);
        fallback.Verify(p => p.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_NullFallback_DisposesPrimaryOnly()
    {
        var primary = MockProvider("Kokoro");
        var router = new TTSRouter(primary.Object);

        router.Dispose();

        primary.Verify(p => p.Dispose(), Times.Once);
    }
}
