
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiktaMe.Core.Tests.LLM;

/// <summary>
/// Verifies LLMRouter populates LlmResult.ErrorMessage on routing/provider failures
/// so pipelines can surface actionable toasts and history.db records an error_message
/// instead of silently swallowing errors (BUG-030).
/// </summary>
public sealed class LLMRouterErrorTests
{
    [Fact]
    public async Task ProcessAsync_WhenFactoryThrows_ReturnsErrorMessage()
    {
        var primary = new Mock<ILLMProvider>();
        primary.SetupGet(p => p.ProviderName).Returns("Primary");

        var factory = new Mock<ILLMProviderFactory>();
        factory.Setup(f => f.CreateProvider(
                   It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Throws(new InvalidOperationException("Anthropic API key not configured."));

        var router = new LLMRouter(primary.Object, factory.Object);

        var result = await router.ProcessAsync(
            text: "hello",
            systemPrompt: "prompt",
            modelName: "claude-3-5-haiku-20241022",
            mode: "dictate");

        result.IsSuccess.Should().BeFalse();
        result.Text.Should().BeEmpty();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("claude-3-5-haiku-20241022");
        result.ErrorMessage.Should().Contain("API keys");
    }

    [Fact]
    public async Task ProcessConversationAsync_WhenFactoryThrows_ReturnsErrorMessage()
    {
        var primary = new Mock<ILLMProvider>();
        primary.SetupGet(p => p.ProviderName).Returns("Primary");

        var factory = new Mock<ILLMProviderFactory>();
        factory.Setup(f => f.CreateProvider(
                   It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Throws(new InvalidOperationException("OpenAI API key not configured."));

        var router = new LLMRouter(primary.Object, factory.Object);

        var history = new List<ConversationTurn> { new("user", "hello") };
        var result = await router.ProcessConversationAsync(
            history, systemPrompt: "prompt", modelName: "gpt-4o-mini", mode: "chat");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("gpt-4o-mini");
    }

    [Fact]
    public async Task ProcessWithImageAsync_WhenFactoryThrows_ReturnsVisionErrorMessage()
    {
        var primary = new Mock<ILLMProvider>();
        primary.SetupGet(p => p.ProviderName).Returns("Primary");

        var factory = new Mock<ILLMProviderFactory>();
        factory.Setup(f => f.CreateProvider(
                   It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .Throws(new InvalidOperationException("Anthropic API key not configured."));

        var router = new LLMRouter(primary.Object, factory.Object);

        var result = await router.ProcessWithImageAsync(
            imageData: [1, 2, 3],
            mimeType: "image/png",
            text: "describe",
            systemPrompt: "prompt",
            modelName: "claude-3-5-haiku-20241022",
            mode: "vision");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("Vision");
    }

    [Fact]
    public async Task ProcessAsync_WhenPrimaryThrowsAndNoFallback_ReturnsErrorMessage()
    {
        var primary = new Mock<ILLMProvider>();
        primary.SetupGet(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.ProcessAsync(
                   It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("connection refused"));

        var factory = new Mock<ILLMProviderFactory>();
        var router = new LLMRouter(primary.Object, factory.Object);

        var result = await router.ProcessAsync("hello", "prompt");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("Primary");
    }
}
