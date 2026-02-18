
using DiktaMe.Core.STT;
using Moq;

namespace DiktaMe.Core.Tests.STT;
/// <summary>
/// Unit tests for <see cref="STTRouter"/>.
/// All providers are mocked — no network calls.
/// </summary>
public sealed class STTRouterTests
{
    private static TranscriptionResult OkResult(string text, string provider) => new()
    {
        Text = text,
        Provider = provider,
        LatencyMs = 10,
    };

    private static TranscriptionResult EmptyResult(string provider) => new()
    {
        Text = string.Empty,
        Provider = provider,
        LatencyMs = 0,
    };

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_NullFallback_ReturnsPrimaryName()
    {
        var primary = new Mock<ISTTProvider>();
        primary.Setup(p => p.ProviderName).Returns("PrimaryProvider");

        var router = new STTRouter(primary.Object);

        Assert.Equal("PrimaryProvider", router.ProviderName);
    }

    [Fact]
    public void ProviderName_WithFallback_IncludesBothNames()
    {
        var primary = new Mock<ISTTProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");

        var fallback = new Mock<ISTTProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");

        var router = new STTRouter(primary.Object, fallback.Object);

        Assert.Contains("Primary", router.ProviderName);
        Assert.Contains("Fallback", router.ProviderName);
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_PrimaryAvailable_ReturnsTrue()
    {
        var primary = new Mock<ISTTProvider>();
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var router = new STTRouter(primary.Object);

        Assert.True(await router.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailable_PrimaryUnavailable_NoFallback_ReturnsFalse()
    {
        var primary = new Mock<ISTTProvider>();
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var router = new STTRouter(primary.Object);

        Assert.False(await router.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailable_PrimaryUnavailable_FallbackAvailable_ReturnsTrue()
    {
        var primary = new Mock<ISTTProvider>();
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var fallback = new Mock<ISTTProvider>();
        fallback.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var router = new STTRouter(primary.Object, fallback.Object);

        Assert.True(await router.IsAvailableAsync());
    }

    // ── TranscribeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Transcribe_PrimarySucceeds_ReturnsPrimaryResult()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var primary = new Mock<ISTTProvider>();
            primary.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(OkResult("hello", "Primary"));

            var router = new STTRouter(primary.Object);

            var result = await router.TranscribeAsync(tmpFile, "en");

            Assert.Equal("hello", result.Text);
            Assert.Equal("Primary", result.Provider);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── TranscribeAsync — fallback on empty ───────────────────────────────────

    [Fact]
    public async Task Transcribe_PrimaryReturnsEmpty_UsesFallback()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var primary = new Mock<ISTTProvider>();
            primary.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(EmptyResult("Primary"));
            primary.Setup(p => p.ProviderName).Returns("Primary");

            var fallback = new Mock<ISTTProvider>();
            fallback.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(OkResult("fallback text", "Fallback"));
            fallback.Setup(p => p.ProviderName).Returns("Fallback");

            var router = new STTRouter(primary.Object, fallback.Object);

            var result = await router.TranscribeAsync(tmpFile, "en");

            Assert.Equal("fallback text", result.Text);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── TranscribeAsync — fallback on exception ───────────────────────────────

    [Fact]
    public async Task Transcribe_PrimaryThrows_UsesFallback()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var primary = new Mock<ISTTProvider>();
            primary.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("network error"));
            primary.Setup(p => p.ProviderName).Returns("Primary");

            var fallback = new Mock<ISTTProvider>();
            fallback.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(OkResult("fallback text", "Fallback"));
            fallback.Setup(p => p.ProviderName).Returns("Fallback");

            var router = new STTRouter(primary.Object, fallback.Object);

            var result = await router.TranscribeAsync(tmpFile, "en");

            Assert.Equal("fallback text", result.Text);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── TranscribeAsync — both fail ───────────────────────────────────────────

    [Fact]
    public async Task Transcribe_BothFail_ReturnsEmptyResult()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var primary = new Mock<ISTTProvider>();
            primary.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("error"));
            primary.Setup(p => p.ProviderName).Returns("Primary");

            var fallback = new Mock<ISTTProvider>();
            fallback.Setup(p => p.TranscribeAsync(tmpFile, "en", It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("error"));
            fallback.Setup(p => p.ProviderName).Returns("Fallback");

            var router = new STTRouter(primary.Object, fallback.Object);

            var result = await router.TranscribeAsync(tmpFile, "en");

            Assert.False(result.IsSuccess);
            Assert.Empty(result.Text);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── GetCapabilitiesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCapabilities_ReflectsAvailability()
    {
        var primary = new Mock<ISTTProvider>();
        primary.Setup(p => p.ProviderName).Returns("Deepgram");
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var fallback = new Mock<ISTTProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Gemini");
        fallback.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var router = new STTRouter(primary.Object, fallback.Object);

        var caps = await router.GetCapabilitiesAsync();

        Assert.True(caps.PrimaryAvailable);
        Assert.False(caps.FallbackAvailable);
        Assert.Equal("Deepgram", caps.BestAvailable);
    }
}
