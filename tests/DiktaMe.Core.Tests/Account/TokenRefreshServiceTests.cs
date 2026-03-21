
using System.Net;
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace DiktaMe.Core.Tests.Account;

public sealed class TokenRefreshServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;

    public TokenRefreshServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"diktame-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _secureStorage = new SecureStorage(Path.Combine(_tempDir, "keys.dat"));
        _settings = new SettingsManager(Path.Combine(_tempDir, "settings.json"));
    }

    private static string BuildJwt(long expUnix, string email = "test@example.com")
    {
        string header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}""");
        string body = Base64UrlEncode(JsonSerializer.Serialize(new
        {
            email,
            exp = expUnix,
            user_metadata = new { full_name = "Test User" },
        }));
        string signature = Base64UrlEncode("fake-signature");
        return $"{header}.{body}.{signature}";
    }

    private static string Base64UrlEncode(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });

        return new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(5) };
    }

    [Fact]
    public async Task TryRefreshAsync_Success_StoresNewTokens()
    {
        // Store an old refresh token
        _secureStorage.StoreKey("refresh_token", "old-refresh-token");

        // Build a new JWT that the "server" will return
        long newExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        string newJwt = BuildJwt(newExpiry);
        string responseJson = JsonSerializer.Serialize(new
        {
            access_token = newJwt,
            refresh_token = "new-refresh-token",
            expires_at = newExpiry,
        });

        var http = CreateMockHttpClient(HttpStatusCode.OK, responseJson);
        using var sut = new TokenRefreshService(_secureStorage, _settings, http);

        bool result = await sut.TryRefreshAsync();

        result.Should().BeTrue();
        _secureStorage.RetrieveKey("trial_token").Should().Be(newJwt);
        _secureStorage.RetrieveKey("refresh_token").Should().Be("new-refresh-token");
        _settings.Current.Account.Email.Should().Be("test@example.com");
        _settings.Current.Account.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task TryRefreshAsync_401Response_ReturnsFalse()
    {
        _secureStorage.StoreKey("refresh_token", "expired-refresh-token");

        var http = CreateMockHttpClient(HttpStatusCode.Unauthorized, """{"error":"Invalid refresh token"}""");
        using var sut = new TokenRefreshService(_secureStorage, _settings, http);

        bool result = await sut.TryRefreshAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_NoRefreshToken_ReturnsFalse()
    {
        // Don't store any refresh token
        var http = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        using var sut = new TokenRefreshService(_secureStorage, _settings, http);

        bool result = await sut.TryRefreshAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndRefreshAsync_JwtFarFromExpiry_DoesNotRefresh()
    {
        // Store a JWT that expires in 2 hours (well above 10-minute threshold)
        long expiry = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        string jwt = BuildJwt(expiry);
        _secureStorage.StoreKey("trial_token", jwt);
        _secureStorage.StoreKey("refresh_token", "some-rt");

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var http = new HttpClient(handler.Object);
        using var sut = new TokenRefreshService(_secureStorage, _settings, http);

        await sut.CheckAndRefreshAsync();

        // Should NOT have made any HTTP call
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRefreshAsync_JwtNearExpiry_AttemptsRefresh()
    {
        // Store a JWT that expires in 5 minutes (below 10-minute threshold)
        long expiry = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        string jwt = BuildJwt(expiry);
        _secureStorage.StoreKey("trial_token", jwt);
        _secureStorage.StoreKey("refresh_token", "some-rt");

        // Return a valid refresh response
        long newExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        string newJwt = BuildJwt(newExpiry);
        string responseJson = JsonSerializer.Serialize(new
        {
            access_token = newJwt,
            refresh_token = "new-rt",
            expires_at = newExpiry,
        });

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });

        var http = new HttpClient(handler.Object);
        using var sut = new TokenRefreshService(_secureStorage, _settings, http);

        await sut.CheckAndRefreshAsync();

        // Should have made exactly 1 HTTP call
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // New token should be stored
        _secureStorage.RetrieveKey("trial_token").Should().Be(newJwt);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}
