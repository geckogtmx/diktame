
using System.Net;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.LLM;

[Collection("SecureStorage")]
public sealed class LLMRouterTrialTests : IDisposable
{
    private readonly string _tempSettingsPath;
    private readonly SettingsManager _settings;
    private readonly SecureStorage _secureStorage = new();

    private static readonly string GeminiResponse = """
        {
            "candidates": [{
                "content": {
                    "parts": [{ "text": "trial result" }]
                }
            }],
            "usageMetadata": {
                "promptTokenCount": 5,
                "candidatesTokenCount": 2
            }
        }
        """;

    public LLMRouterTrialTests()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"diktame-router-test-{Guid.NewGuid()}.json");
        _settings = new SettingsManager(_tempSettingsPath);
    }

    public void Dispose()
    {
        _secureStorage.DeleteKey("trial_token");
        if (File.Exists(_tempSettingsPath))
        {
            File.Delete(_tempSettingsPath);
        }

        if (File.Exists(_tempSettingsPath + ".tmp"))
        {
            File.Delete(_tempSettingsPath + ".tmp");
        }
    }

    [Fact]
    public async Task ProcessAsync_AuthModeTrial_RoutesToTrialProvider()
    {
        // Arrange: AuthMode = Trial + store a token so TrialGeminiProvider can proceed
        await _settings.UpdateAsync(_settings.Current with { AuthMode = AuthMode.Trial });
        _secureStorage.StoreKey("trial_token", "eyJ.test.token");

        var accountService = new Mock<IAccountService>();
        accountService.Setup(s => s.LogoutAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var trialService = new Mock<ITrialService>();
        trialService.Setup(s => s.RecordUsageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RouterFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var trialProvider = new TrialGeminiProvider(_secureStorage, accountService.Object, trialService.Object, http);

        var primaryMock = new Mock<ILLMProvider>();
        primaryMock.Setup(p => p.ProviderName).Returns("Primary");
        primaryMock.Setup(p => p.ProcessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult { Text = "primary result", Provider = "Primary" });

        var factory = new Mock<ILLMProviderFactory>();
        var router = new LLMRouter(
            primary: primaryMock.Object,
            factory: factory.Object,
            settings: _settings,
            trialProvider: trialProvider);

        // Act
        var result = await router.ProcessAsync("hello", "prompt");

        // Assert — trial provider was used
        result.Text.Should().Be("trial result");
        result.Provider.Should().Be("Gemini (Trial)");
    }

    [Fact]
    public async Task ProcessAsync_AuthModeNone_RoutesToPrimaryProvider()
    {
        // Arrange: AuthMode = None (default) — trial provider should be skipped
        var primaryMock = new Mock<ILLMProvider>();
        primaryMock.Setup(p => p.ProviderName).Returns("Primary");
        primaryMock.Setup(p => p.ProcessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult { Text = "primary result", Provider = "Primary" });

        var accountService = new Mock<IAccountService>();
        var trialService = new Mock<ITrialService>();
        var handler = new RouterFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var trialProvider = new TrialGeminiProvider(_secureStorage, accountService.Object, trialService.Object, http);

        var factory = new Mock<ILLMProviderFactory>();
        var router = new LLMRouter(
            primary: primaryMock.Object,
            factory: factory.Object,
            settings: _settings,
            trialProvider: trialProvider);

        // Act
        var result = await router.ProcessAsync("hello", "prompt");

        // Assert — primary provider was used, not trial
        result.Text.Should().Be("primary result");
        result.Provider.Should().Be("Primary");
        handler.WasCalled.Should().BeFalse("trial provider should not have been invoked");
    }
}

internal sealed class RouterFakeHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public bool WasCalled { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        WasCalled = true;
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}
