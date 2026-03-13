using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class TTSProviderFactoryTests : IDisposable
{
    private readonly string _tempPath;
    private readonly string _keysPath;
    private readonly TTSProviderFactory _factory;

    public TTSProviderFactoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"diktame-ttsfactory-{Guid.NewGuid()}.json");
        _keysPath = Path.Combine(Path.GetTempPath(), $"diktame-ttsfactory-keys-{Guid.NewGuid()}.dat");
        var settings = new SettingsManager(_tempPath);
        var secureStorage = new SecureStorage(_keysPath);
        _factory = new TTSProviderFactory(secureStorage, settings);
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

    [Fact]
    public void CreateProvider_KokoroGpuVariant_ReturnsCachedInstance()
    {
        var first = _factory.CreateProvider("kokoro", "gpu");
        var second = _factory.CreateProvider("kokoro", "gpu");

        first.Should().BeSameAs(second);
    }

    // ── Cloud providers — key present ────────────────────────────────────────

    [Fact]
    public void CreateProvider_Deepgram_WithKey_ReturnsDeepgramTtsProvider()
    {
        var storage = new SecureStorage(_keysPath);
        storage.StoreKey("deepgram", "dg_test_key");

        var factory = new TTSProviderFactory(storage, new SettingsManager(_tempPath));
        var provider = factory.CreateProvider("deepgram");

        provider.Should().BeOfType<DeepgramTtsProvider>();
        provider.ProviderName.Should().Contain("Deepgram");
        factory.Dispose();
    }

    [Fact]
    public void CreateProvider_OpenAI_WithKey_ReturnsOpenAITtsProvider()
    {
        var storage = new SecureStorage(_keysPath);
        storage.StoreKey("openai", "sk_test_key");

        var factory = new TTSProviderFactory(storage, new SettingsManager(_tempPath));
        var provider = factory.CreateProvider("openai");

        provider.Should().BeOfType<OpenAITtsProvider>();
        provider.ProviderName.Should().Contain("OpenAI");
        factory.Dispose();
    }

    [Fact]
    public void CreateProvider_Inworld_WithKey_ReturnsInworldTtsProvider()
    {
        var storage = new SecureStorage(_keysPath);
        storage.StoreKey("inworld", "iw_test_key");

        var factory = new TTSProviderFactory(storage, new SettingsManager(_tempPath));
        var provider = factory.CreateProvider("inworld");

        provider.Should().BeOfType<InworldTtsProvider>();
        provider.ProviderName.Should().Contain("Inworld");
        factory.Dispose();
    }

    // ── Cloud providers — key missing ─────────────────────────────────────────

    [Theory]
    [InlineData("deepgram")]
    [InlineData("openai")]
    [InlineData("inworld")]
    public void CreateProvider_CloudProvider_NoKey_Throws(string type)
    {
        // Factory with empty secure storage — no keys stored
        var storage = new SecureStorage(_keysPath);
        var factory = new TTSProviderFactory(storage, new SettingsManager(_tempPath));

        var act = () => factory.CreateProvider(type);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{type}*key*");
        factory.Dispose();
    }

    // ── Cloud caching ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateProvider_CloudSameType_ReturnsCachedInstance()
    {
        var storage = new SecureStorage(_keysPath);
        storage.StoreKey("deepgram", "dg_key");

        var factory = new TTSProviderFactory(storage, new SettingsManager(_tempPath));
        var first = factory.CreateProvider("deepgram");
        var second = factory.CreateProvider("deepgram");

        first.Should().BeSameAs(second);
        factory.Dispose();
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
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { /* best effort */ }
        try { if (File.Exists(_keysPath)) File.Delete(_keysPath); } catch { /* best effort */ }
    }
}
