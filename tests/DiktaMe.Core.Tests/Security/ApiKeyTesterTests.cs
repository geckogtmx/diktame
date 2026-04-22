using System.Net;
using System.Net.Http;
using System.Text;
using DiktaMe.Core.Security;
using FluentAssertions;
using Xunit;

namespace DiktaMe.Core.Tests.Security;

public sealed class ApiKeyTesterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SecureStorage _storage;

    public ApiKeyTesterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storage = new SecureStorage(Path.Combine(_tempDir, "keys.dat"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── Happy-path: 200 with data array ───────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("openai", "{\"data\":[{\"id\":\"m1\"},{\"id\":\"m2\"}]}", 2)]
    [InlineData("anthropic", "{\"data\":[{\"id\":\"m1\"}]}", 1)]
    [InlineData("gemini", "{\"models\":[{\"name\":\"m1\"}]}", 1)]
    [InlineData("deepgram", "{\"projects\":[{\"id\":\"p1\"}]}", 1)]
    [InlineData("requesty", "{\"data\":[]}", 0)]
    [InlineData("openrouter", "{\"data\":{\"label\":\"my key\"}}", 0)]
    [InlineData("inworld", "{\"voices\":[]}", 0)]
    public async Task TestAsync_200_ReturnsOk(string provider, string body, int expectedCount)
    {
        using var tester = MakeTester(provider, HttpStatusCode.OK, body);
        var result = await tester.TestAsync(provider, "dummy-key");

        result.Ok.Should().BeTrue();
        result.Message.Should().StartWith("Connected");
        if (expectedCount > 1)
        {
            result.Message.Should().Contain($"{expectedCount} items");
        }
    }

    // ── 401 auth failure ──────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("gemini")]
    [InlineData("deepgram")]
    [InlineData("openrouter")]
    [InlineData("requesty")]
    [InlineData("inworld")]
    public async Task TestAsync_401_ReturnsInvalid(string provider)
    {
        using var tester = MakeTester(provider, HttpStatusCode.Unauthorized, "{}");
        var result = await tester.TestAsync(provider, "bad-key");

        result.Ok.Should().BeFalse();
        result.Message.Should().Contain("401");
    }

    // ── Network error ─────────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public async Task TestAsync_NetworkError_ReturnsNetworkError()
    {
        var handler = new MockHandler((_, _) => throw new HttpRequestException("boom"));
        using var http = new HttpClient(handler);
        using var tester = new ApiKeyTester(_storage, http);

        var result = await tester.TestAsync("openai", "any");

        result.Ok.Should().BeFalse();
        result.Message.Should().Be("Network error");
    }

    // ── No key to test ────────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public async Task TestAsync_NoStoredNoOverride_ReturnsNoKeyMessage()
    {
        using var http = new HttpClient(new MockHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        using var tester = new ApiKeyTester(_storage, http);

        var result = await tester.TestAsync("openai");

        result.Ok.Should().BeFalse();
        result.Message.Should().Be("No key to test");
    }

    // ── Override takes precedence over stored ─────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public async Task TestAsync_OverrideUsedOverStored()
    {
        _storage.StoreKey("openai", "stored-key");
        string? seenAuth = null;
        var handler = new MockHandler((req, _) =>
        {
            seenAuth = req.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        using var http = new HttpClient(handler);
        using var tester = new ApiKeyTester(_storage, http);

        await tester.TestAsync("openai", "override-key");

        seenAuth.Should().Be("override-key");
    }

    [Fact, Trait("Category", "Unit")]
    public async Task TestAsync_UnknownProvider_ReturnsUnknown()
    {
        using var tester = new ApiKeyTester(_storage, new HttpClient());
        var result = await tester.TestAsync("nope", "dummy");
        result.Ok.Should().BeFalse();
        result.Message.Should().Be("Unknown provider");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private ApiKeyTester MakeTester(string provider, HttpStatusCode status, string body)
    {
        var handler = new MockHandler((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
        var http = new HttpClient(handler);
        return new ApiKeyTester(_storage, http);
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _fn;
        public MockHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> fn) => _fn = fn;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_fn(request, ct));
    }
}
