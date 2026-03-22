
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Account;

public sealed class AccountServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"diktame-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _secureStorage = new SecureStorage(Path.Combine(_tempDir, "keys.dat"));
        _settings = new SettingsManager(Path.Combine(_tempDir, "settings.json"));
        _sut = new AccountService(_secureStorage, _settings);
    }

    private static string BuildJwt(object payload)
    {
        string header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}""");
        string body = Base64UrlEncode(JsonSerializer.Serialize(payload));
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

    [Fact]
    public async Task HandleAuthCallback_WithRefreshToken_StoresBothTokens()
    {
        string jwt = BuildJwt(new { email = "test@example.com", exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() });
        string refreshToken = "refresh-token-123";

        await _sut.HandleAuthCallbackAsync(jwt, refreshToken);

        _secureStorage.RetrieveKey("trial_token").Should().Be(jwt);
        _secureStorage.RetrieveKey("refresh_token").Should().Be(refreshToken);
    }

    [Fact]
    public async Task HandleAuthCallback_WithNullRefreshToken_StoresOnlyAccessToken()
    {
        string jwt = BuildJwt(new { email = "test@example.com", exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() });

        await _sut.HandleAuthCallbackAsync(jwt, refreshToken: null);

        _secureStorage.RetrieveKey("trial_token").Should().Be(jwt);
        _secureStorage.RetrieveKey("refresh_token").Should().BeNull();
    }

    [Fact]
    public async Task HandleAuthCallback_SetsDisplayNameFromJwt()
    {
        string jwt = BuildJwt(new
        {
            email = "john@example.com",
            user_metadata = new { full_name = "John Doe", avatar_url = "https://photo.url/pic.jpg" },
        });

        await _sut.HandleAuthCallbackAsync(jwt, "rt");

        _settings.Current.Account.DisplayName.Should().Be("John Doe");
        _settings.Current.Account.AvatarUrl.Should().Be("https://photo.url/pic.jpg");
    }

    [Fact]
    public async Task HandleAuthCallback_PreservesExistingAuthMode()
    {
        // Pre-set AuthMode to something other than None
        await _settings.UpdateAsync(_settings.Current with { AuthMode = AuthMode.Account });

        string jwt = BuildJwt(new { email = "test@example.com" });
        await _sut.HandleAuthCallbackAsync(jwt);

        // Sign-in should NOT override the user's AuthMode setting
        _settings.Current.AuthMode.Should().Be(AuthMode.Account);
        _settings.Current.Account.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Logout_ClearsBothTokens()
    {
        // First sign in
        string jwt = BuildJwt(new { email = "test@example.com" });
        await _sut.HandleAuthCallbackAsync(jwt, "refresh-token");

        // Then log out
        await _sut.LogoutAsync();

        _secureStorage.RetrieveKey("trial_token").Should().BeNull();
        _secureStorage.RetrieveKey("refresh_token").Should().BeNull();
        _settings.Current.AuthMode.Should().Be(AuthMode.None);
        _settings.Current.Account.Email.Should().BeEmpty();
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}
