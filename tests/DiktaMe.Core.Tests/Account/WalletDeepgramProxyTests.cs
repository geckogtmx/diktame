
using System.Net;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace DiktaMe.Core.Tests.Account;

public sealed class WalletDeepgramProxyTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly WalletManager _wallet;
    private readonly SettingsManager _settings;

    public WalletDeepgramProxyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"diktame_wdp_test_{Guid.NewGuid()}.db");
        _wallet = new WalletManager(_dbPath);
        _settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_wdp_{Guid.NewGuid()}.json"));
    }

    // ── IsAvailableAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailableAsync_WithoutToken_ReturnsFalse()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet);

        bool available = await proxy.IsAvailableAsync();

        available.Should().BeFalse();
    }

    // ── TranscribeAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeAsync_WithoutToken_ReturnsEmptyResult()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet);

        var result = await proxy.TranscribeAsync("fake.wav", "en");

        result.Text.Should().BeEmpty();
        result.Provider.Should().Be("Wallet Proxy (Deepgram)");
    }

    [Fact]
    public async Task TranscribeAsync_On401_ThrowsAndRaisesSessionExpired()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var wavPath = CreateDummyWav();
        var http = CreateMockHttpClient(HttpStatusCode.Unauthorized, "");
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet, http);

        bool sessionExpiredFired = false;
        proxy.SessionExpired += () => sessionExpiredFired = true;

        var act = () => proxy.TranscribeAsync(wavPath, "en");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
        sessionExpiredFired.Should().BeTrue();
        CleanupFile(wavPath);
    }

    [Fact]
    public async Task TranscribeAsync_On402_ThrowsInsufficientBalance()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var wavPath = CreateDummyWav();
        var response = new HttpResponseMessage(HttpStatusCode.PaymentRequired);
        response.Headers.Add("X-Wallet-Balance", "0");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet, http);

        var act = () => proxy.TranscribeAsync(wavPath, "en");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*wallet balance*");
        CleanupFile(wavPath);
    }

    [Fact]
    public async Task TranscribeAsync_On503_ThrowsServiceUnavailable()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var wavPath = CreateDummyWav();
        var http = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, "");
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet, http);

        var act = () => proxy.TranscribeAsync(wavPath, "en");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unavailable*");
        CleanupFile(wavPath);
    }

    [Fact]
    public async Task TranscribeAsync_OnSuccess_ParsesResponse()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var wavPath = CreateDummyWav();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"Hello world","detectedLanguage":"en"}"""),
        };
        response.Headers.Add("X-Wallet-Cost", "300");
        response.Headers.Add("X-Wallet-Balance", "999700");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet, http);

        var result = await proxy.TranscribeAsync(wavPath, "en");

        result.Text.Should().Be("Hello world");
        result.DetectedLanguage.Should().Be("en");
        result.Provider.Should().Be("Wallet Proxy (Deepgram)");
        CleanupFile(wavPath);
    }

    [Fact]
    public async Task TranscribeAsync_OnSuccess_RecordsUsageInWallet()
    {
        await _wallet.InitAsync();
        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);
        var storage = CreateStorageWithToken();
        var wavPath = CreateDummyWav();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"done"}"""),
        };
        response.Headers.Add("X-Wallet-Cost", "800");
        response.Headers.Add("X-Wallet-Balance", "999200");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet, http);

        await proxy.TranscribeAsync(wavPath, "en");

        var txns = await _wallet.GetTransactionsAsync(10);
        txns.Should().Contain(t => t.Type == WalletTransactionType.Usage && t.AmountMicro == -800);
        CleanupFile(wavPath);
    }

    [Fact]
    public async Task TranscribeAsync_OnSuccess_RaisesBalanceUpdated()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var wavPath = CreateDummyWav();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"ok"}"""),
        };
        response.Headers.Add("X-Wallet-Cost", "100");
        response.Headers.Add("X-Wallet-Balance", "999900");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet, http);

        long? reportedBalance = null;
        proxy.BalanceUpdated += b => reportedBalance = b;

        await proxy.TranscribeAsync(wavPath, "en");

        reportedBalance.Should().Be(999900);
        CleanupFile(wavPath);
    }

    // ── ProviderName ─────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsExpectedValue()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletDeepgramProxy(storage, _settings, _wallet);

        proxy.ProviderName.Should().Be("Wallet Proxy (Deepgram)");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SecureStorage CreateEmptyStorage()
    {
        string path = Path.Combine(Path.GetTempPath(), $"diktame_keys_{Guid.NewGuid()}.dat");
        return new SecureStorage(path);
    }

    private static SecureStorage CreateStorageWithToken()
    {
        var storage = CreateEmptyStorage();
        storage.StoreKey("trial_token", "test-jwt-token-12345");
        return storage;
    }

    private static string CreateDummyWav()
    {
        string path = Path.Combine(Path.GetTempPath(), $"diktame_test_{Guid.NewGuid()}.wav");
        int sampleRate = 16000;
        int channels = 1;
        int bitsPerSample = 16;
        int durationSamples = sampleRate / 2; // 0.5 seconds
        int dataSize = durationSamples * channels * (bitsPerSample / 8);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * (bitsPerSample / 8));
        bw.Write((short)(channels * (bitsPerSample / 8)));
        bw.Write((short)bitsPerSample);
        bw.Write("data"u8);
        bw.Write(dataSize);
        bw.Write(new byte[dataSize]);

        return path;
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode status, string content)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(content),
        };
        return CreateMockHttpClient(response);
    }

    private static HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(5) };
    }

    private static void CleanupFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort */ }
    }

    public async ValueTask DisposeAsync()
    {
        _wallet.Dispose();
        await Task.Delay(50);
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* best effort */ }
        }
    }
}
