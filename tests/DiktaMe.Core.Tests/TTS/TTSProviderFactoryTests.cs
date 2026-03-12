using DiktaMe.Core.Config;
using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class TTSProviderFactoryTests : IDisposable
{
    private readonly string _tempPath;
    private readonly TTSProviderFactory _factory;

    public TTSProviderFactoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"diktame-ttsfactory-{Guid.NewGuid()}.json");
        var settings = new SettingsManager(_tempPath);
        _factory = new TTSProviderFactory(settings);
    }

    // ── None / Skip ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("none")]
    [InlineData("skip")]
    [InlineData("None")]
    [InlineData("SKIP")]
    public void CreateProvider_NoneOrSkip_ReturnsNullProvider(string type)
    {
        var provider = _factory.CreateProvider(type);

        provider.Should().NotBeNull();
        provider.ProviderName.Should().Be("None");
        provider.SupportsStreaming.Should().BeFalse();
    }

    [Fact]
    public async Task NullProvider_SynthesizeAsync_ReturnsEmpty()
    {
        var provider = _factory.CreateProvider("none");
        var result = await provider.SynthesizeAsync("hello");

        result.IsSuccess.Should().BeFalse();
        result.AudioData.Should().BeEmpty();
    }

    [Fact]
    public async Task NullProvider_IsAvailable_ReturnsTrue()
    {
        var provider = _factory.CreateProvider("none");
        (await provider.IsAvailableAsync()).Should().BeTrue();
    }

    // ── Unknown provider ─────────────────────────────────────────────────────

    [Fact]
    public void CreateProvider_UnknownType_Throws()
    {
        var act = () => _factory.CreateProvider("unknown");
        act.Should().Throw<NotSupportedException>();
    }

    // ── Caching ──────────────────────────────────────────────────────────────

    [Fact]
    public void CreateProvider_SameType_ReturnsCachedInstance()
    {
        // Kokoro instances are cached by type:variant key
        // "none" is NOT cached (returns new NullTtsProvider each time)
        var first = _factory.CreateProvider("kokoro", "int8");
        var second = _factory.CreateProvider("kokoro", "int8");

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void CreateProvider_DifferentVariants_ReturnsDifferentInstances()
    {
        var int8 = _factory.CreateProvider("kokoro", "int8");
        var fp16 = _factory.CreateProvider("kokoro", "fp16");

        int8.Should().NotBeSameAs(fp16);
    }

    [Fact]
    public void CreateProvider_NoneNotCached_ReturnsFreshEachTime()
    {
        var first = _factory.CreateProvider("none");
        var second = _factory.CreateProvider("none");

        // NullTtsProvider is stateless, new instance each call
        first.Should().NotBeSameAs(second);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DisposesAllCachedProviders()
    {
        _ = _factory.CreateProvider("kokoro", "int8");

        var act = () => _factory.Dispose();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); }
        catch { /* best effort */ }
    }
}
