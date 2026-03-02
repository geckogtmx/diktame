
using System.Net;
using System.Text;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.Account;

[Collection("SecureStorage")]
public sealed class TrialGeminiProviderTests : IDisposable
{
    private const string TokenKey = "trial_token";
    private const string TestToken = "eyJ.test.token";
    private readonly SecureStorage _secureStorage = new();

    private static readonly string GeminiResponse = """
        {
            "candidates": [{
                "content": {
                    "parts": [{ "text": "Hello, world!" }]
                }
            }],
            "usageMetadata": {
                "promptTokenCount": 10,
                "candidatesTokenCount": 5
            }
        }
        """;

    public void Dispose()
    {
        _secureStorage.DeleteKey(TokenKey);
    }

    [Fact]
    public async Task ProcessAsync_ValidToken_SendsBearerAuth()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        await provider.ProcessAsync("hello", "prompt");

        handler.LastAuthScheme.Should().Be("Bearer");
        handler.LastAuthParameter.Should().Be(TestToken);
    }

    [Fact]
    public async Task ProcessAsync_ValidResponse_ReturnsText()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        var result = await provider.ProcessAsync("hello", "prompt");

        result.Text.Should().Be("Hello, world!");
        result.Provider.Should().Be("Gemini (Trial)");
        result.InputTokens.Should().Be(10);
        result.OutputTokens.Should().Be(5);
    }

    [Fact]
    public async Task ProcessAsync_403_ReturnsQuotaExceededResult()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.Forbidden, "");
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        var result = await provider.ProcessAsync("hello", "prompt");

        result.Text.Should().Contain("quota exceeded");
    }

    [Fact]
    public async Task ProcessAsync_401_TriggersAutoLogout()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        await provider.ProcessAsync("hello", "prompt");

        trialService.Verify(s => s.LogoutAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_NoToken_Throws()
    {
        _secureStorage.DeleteKey(TokenKey);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        var act = () => provider.ProcessAsync("hello", "prompt");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task IsAvailable_WithToken_ReturnsTrue()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        bool available = await provider.IsAvailableAsync();

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailable_WithoutToken_ReturnsFalse()
    {
        _secureStorage.DeleteKey(TokenKey);
        var trialService = CreateMockTrialService();
        var handler = new ProviderFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiProvider(_secureStorage, trialService.Object, http);

        bool available = await provider.IsAvailableAsync();

        available.Should().BeFalse();
    }

    private static Mock<ITrialAccountService> CreateMockTrialService()
    {
        var mock = new Mock<ITrialAccountService>();
        mock.Setup(s => s.RecordUsageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(s => s.LogoutAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }
}

internal sealed class ProviderFakeHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}
