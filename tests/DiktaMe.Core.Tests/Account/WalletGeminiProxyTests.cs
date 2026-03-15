
using System.Net;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace DiktaMe.Core.Tests.Account;

public sealed class WalletGeminiProxyTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly WalletManager _wallet;
    private readonly SettingsManager _settings;

    public WalletGeminiProxyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"diktame_wgp_test_{Guid.NewGuid()}.db");
        _wallet = new WalletManager(_dbPath);
        _settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_wgp_{Guid.NewGuid()}.json"));
    }

    // ── IsAvailableAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailableAsync_WithoutToken_ReturnsFalse()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet);

        bool available = await proxy.IsAvailableAsync();

        available.Should().BeFalse();
    }

    // ── ProcessAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithoutToken_ReturnsEmptyResult()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet);

        var result = await proxy.ProcessAsync("hello", "You are helpful.", "dictate");

        result.Text.Should().BeEmpty();
        result.Provider.Should().Be("Wallet Proxy (Gemini)");
    }

    [Fact]
    public async Task ProcessAsync_On401_RaisesSessionExpired()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var http = CreateMockHttpClient(HttpStatusCode.Unauthorized, "");
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet, http);

        bool sessionExpiredFired = false;
        proxy.SessionExpired += () => sessionExpiredFired = true;

        var result = await proxy.ProcessAsync("hello", "prompt", "dictate");

        sessionExpiredFired.Should().BeTrue();
        result.Text.Should().Contain("expired");
    }

    [Fact]
    public async Task ProcessAsync_On402_ReturnsInsufficientBalance()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var response = new HttpResponseMessage(HttpStatusCode.PaymentRequired);
        response.Headers.Add("X-Wallet-Balance", "0");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet, http);

        var result = await proxy.ProcessAsync("hello", "prompt", "dictate");

        result.Text.Should().Contain("Insufficient");
    }

    [Fact]
    public async Task ProcessAsync_On503_ReturnsServiceUnavailable()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var http = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, "");
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet, http);

        var result = await proxy.ProcessAsync("hello", "prompt", "dictate");

        result.Text.Should().Contain("unavailable");
    }

    [Fact]
    public async Task ProcessAsync_OnSuccess_ParsesResponse()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"Processed text","inputTokens":10,"outputTokens":5}"""),
        };
        response.Headers.Add("X-Wallet-Cost", "500"); // 500 microdollars
        response.Headers.Add("X-Wallet-Balance", "999500");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet, http);

        var result = await proxy.ProcessAsync("hello", "prompt", "dictate");

        result.Text.Should().Be("Processed text");
        result.InputTokens.Should().Be(10);
        result.OutputTokens.Should().Be(5);
        result.Provider.Should().Be("Wallet Proxy (Gemini)");
    }

    [Fact]
    public async Task ProcessAsync_OnSuccess_RecordsUsageInWallet()
    {
        await _wallet.InitAsync();
        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);
        var storage = CreateStorageWithToken();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"done"}"""),
        };
        response.Headers.Add("X-Wallet-Cost", "1500");
        response.Headers.Add("X-Wallet-Balance", "998500");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet, http);

        await proxy.ProcessAsync("hello", "prompt", "dictate");

        var txns = await _wallet.GetTransactionsAsync(10);
        txns.Should().Contain(t => t.Type == WalletTransactionType.Usage && t.AmountMicro == -1500);
    }

    [Fact]
    public async Task ProcessAsync_OnSuccess_RaisesBalanceUpdated()
    {
        await _wallet.InitAsync();
        var storage = CreateStorageWithToken();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"ok"}"""),
        };
        response.Headers.Add("X-Wallet-Cost", "100");
        response.Headers.Add("X-Wallet-Balance", "999900");
        var http = CreateMockHttpClient(response);
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet, http);

        long? reportedBalance = null;
        proxy.BalanceUpdated += b => reportedBalance = b;

        await proxy.ProcessAsync("hi", "p", "dictate");

        reportedBalance.Should().Be(999900);
    }

    // ── ProcessConversationAsync (defense-in-depth) ──────────────────────────

    [Fact]
    public async Task ProcessConversationAsync_AlwaysThrows()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet);

        var history = new List<ConversationTurn> { new("user", "hello") };
        var act = () => proxy.ProcessConversationAsync(history, "system prompt");

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // ── ProviderName ─────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsExpectedValue()
    {
        var storage = CreateEmptyStorage();
        using var proxy = new WalletGeminiProxy(storage, _settings, _wallet);

        proxy.ProviderName.Should().Be("Wallet Proxy (Gemini)");
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
