
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiktaMe.Core.Tests.LLM;

public sealed class LLMRouterWalletTests
{
    private readonly Mock<ILLMProvider> _primaryMock = new();
    private readonly Mock<ILLMProvider> _walletMock = new();
    private readonly Mock<ILLMProviderFactory> _factoryMock = new();

    public LLMRouterWalletTests()
    {
        _primaryMock.SetupGet(p => p.ProviderName).Returns("Primary");
        _walletMock.SetupGet(p => p.ProviderName).Returns("Wallet Proxy");
    }

    [Fact]
    public async Task ProcessAsync_WhenWalletMode_RoutesToWalletProvider()
    {
        var settings = await CreateSettingsWithAuthMode(AuthMode.Wallet);
        var expectedResult = new LlmResult { Text = "wallet result", Provider = "Wallet Proxy" };
        _walletMock.Setup(w => w.ProcessAsync("hello", "prompt", "dictate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        ILLMProvider router = new LLMRouter(
            primary: _primaryMock.Object,
            factory: _factoryMock.Object,
            settings: settings,
            walletProvider: _walletMock.Object);

        var result = await router.ProcessAsync("hello", "prompt", "dictate");

        result.Text.Should().Be("wallet result");
        _walletMock.Verify(w => w.ProcessAsync("hello", "prompt", "dictate", It.IsAny<CancellationToken>()), Times.Once);
        _primaryMock.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenNotWalletMode_SkipsWalletProvider()
    {
        var settings = await CreateSettingsWithAuthMode(AuthMode.ApiKey);
        var expectedResult = new LlmResult { Text = "primary result", Provider = "Primary" };
        _primaryMock.Setup(p => p.ProcessAsync("hello", "prompt", "dictate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        ILLMProvider router = new LLMRouter(
            primary: _primaryMock.Object,
            factory: _factoryMock.Object,
            settings: settings,
            walletProvider: _walletMock.Object);

        var result = await router.ProcessAsync("hello", "prompt", "dictate");

        result.Text.Should().Be("primary result");
        _walletMock.Verify(w => w.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessConversationAsync_WhenWalletMode_SkipsWallet_FallsToByok()
    {
        var settings = await CreateSettingsWithAuthMode(AuthMode.Wallet);
        var history = new List<ConversationTurn> { new("user", "hello") };
        var expectedResult = new LlmResult { Text = "primary chat result", Provider = "Primary" };
        _primaryMock.Setup(p => p.ProcessConversationAsync(history, "prompt", "chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        ILLMProvider router = new LLMRouter(
            primary: _primaryMock.Object,
            factory: _factoryMock.Object,
            settings: settings,
            walletProvider: _walletMock.Object);

        var result = await router.ProcessConversationAsync(history, "prompt", "chat");

        result.Text.Should().Be("primary chat result");
        // Wallet should NEVER be called for chat
        _walletMock.Verify(
            w => w.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenWalletMode_ButNoWalletProvider_FallsToPrimary()
    {
        var settings = await CreateSettingsWithAuthMode(AuthMode.Wallet);
        var expectedResult = new LlmResult { Text = "primary result", Provider = "Primary" };
        _primaryMock.Setup(p => p.ProcessAsync("hello", "prompt", "dictate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        ILLMProvider router = new LLMRouter(
            primary: _primaryMock.Object,
            factory: _factoryMock.Object,
            settings: settings,
            walletProvider: null);

        var result = await router.ProcessAsync("hello", "prompt", "dictate");

        result.Text.Should().Be("primary result");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<SettingsManager> CreateSettingsWithAuthMode(AuthMode mode)
    {
        string path = Path.Combine(Path.GetTempPath(), $"diktame_lrw_{Guid.NewGuid()}.json");
        var settings = new SettingsManager(path);
        await settings.LoadAsync();
        var updated = settings.Current with { AuthMode = mode };
        await settings.UpdateAsync(updated);
        return settings;
    }
}
