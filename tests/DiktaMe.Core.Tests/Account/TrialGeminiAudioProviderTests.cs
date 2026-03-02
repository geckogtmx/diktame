
using System.Net;
using DiktaMe.Core.Account;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.Account;

[Collection("SecureStorage")]
public sealed class TrialGeminiAudioProviderTests : IDisposable
{
    private const string TokenKey = "trial_token";
    private const string TestToken = "eyJ.audio.token";
    private readonly SecureStorage _secureStorage = new();
    private readonly string _tempAudioFile;

    private static readonly string GeminiAudioResponse = """
        {
            "candidates": [{
                "content": {
                    "parts": [{ "text": "This is a test transcription." }]
                }
            }]
        }
        """;

    public TrialGeminiAudioProviderTests()
    {
        _tempAudioFile = Path.Combine(Path.GetTempPath(), $"test-audio-{Guid.NewGuid()}.wav");
        File.WriteAllBytes(_tempAudioFile, new byte[] { 0x52, 0x49, 0x46, 0x46 }); // RIFF header stub
    }

    public void Dispose()
    {
        _secureStorage.DeleteKey(TokenKey);
        if (File.Exists(_tempAudioFile))
        {
            File.Delete(_tempAudioFile);
        }
    }

    [Fact]
    public async Task TranscribeAsync_ValidToken_SendsBearerAuth()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new AudioFakeHandler(HttpStatusCode.OK, GeminiAudioResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiAudioProvider(_secureStorage, trialService.Object, http);

        await provider.TranscribeAsync(_tempAudioFile);

        handler.LastAuthScheme.Should().Be("Bearer");
        handler.LastAuthParameter.Should().Be(TestToken);
    }

    [Fact]
    public async Task TranscribeAsync_ValidResponse_ReturnsTranscription()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new AudioFakeHandler(HttpStatusCode.OK, GeminiAudioResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiAudioProvider(_secureStorage, trialService.Object, http);

        var result = await provider.TranscribeAsync(_tempAudioFile);

        result.Text.Should().Be("This is a test transcription.");
        result.Provider.Should().Be("Gemini Audio (Trial)");
    }

    [Fact]
    public async Task TranscribeAsync_403_ReturnsEmpty()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new AudioFakeHandler(HttpStatusCode.Forbidden, "");
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiAudioProvider(_secureStorage, trialService.Object, http);

        var result = await provider.TranscribeAsync(_tempAudioFile);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task TranscribeAsync_401_TriggersAutoLogout()
    {
        _secureStorage.StoreKey(TokenKey, TestToken);
        var trialService = CreateMockTrialService();
        var handler = new AudioFakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiAudioProvider(_secureStorage, trialService.Object, http);

        await provider.TranscribeAsync(_tempAudioFile);

        trialService.Verify(s => s.LogoutAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscribeAsync_NoToken_Throws()
    {
        _secureStorage.DeleteKey(TokenKey);
        var trialService = CreateMockTrialService();
        var handler = new AudioFakeHandler(HttpStatusCode.OK, GeminiAudioResponse);
        using var http = new HttpClient(handler);
        using var provider = new TrialGeminiAudioProvider(_secureStorage, trialService.Object, http);

        var act = () => provider.TranscribeAsync(_tempAudioFile);

        await act.Should().ThrowAsync<InvalidOperationException>();
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

internal sealed class AudioFakeHandler(HttpStatusCode statusCode, string body)
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
