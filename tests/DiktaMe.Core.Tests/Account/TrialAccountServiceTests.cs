
using System.Net;
using System.Text;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Account;

[Collection("SecureStorage")]
public sealed class TrialAccountServiceTests : IDisposable
{
    private readonly string _tempSettingsPath;
    private readonly SettingsManager _settings;
    private readonly SecureStorage _secureStorage;
    private const string TokenKey = "trial_token";

    public TrialAccountServiceTests()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"diktame-test-{Guid.NewGuid()}.json");
        _settings = new SettingsManager(_tempSettingsPath);
        _secureStorage = new SecureStorage();
    }

    public void Dispose()
    {
        _secureStorage.DeleteKey(TokenKey);
        if (File.Exists(_tempSettingsPath))
        {
            File.Delete(_tempSettingsPath);
        }

        if (File.Exists(_tempSettingsPath + ".tmp"))
        {
            File.Delete(_tempSettingsPath + ".tmp");
        }
    }

    private static string BuildTestJwt(string email = "test@example.com")
    {
        string header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}""");
        string body = Base64UrlEncode($$$"""{"email":"{{{email}}}","exp":9999999999}""");
        string sig = Base64UrlEncode("sig");
        return $"{header}.{body}.{sig}";
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
    public async Task HandleAuthCallback_StoresTokenInSecureStorage()
    {
        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":0,"wordsQuota":15000,"daysRemaining":15,"expiresAt":"2026-04-01","trialActive":true}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);
        string jwt = BuildTestJwt();

        await svc.HandleAuthCallbackAsync(jwt);

        _secureStorage.RetrieveKey(TokenKey).Should().Be(jwt);
    }

    [Fact]
    public async Task HandleAuthCallback_TrialActive_UpgradesToTrial()
    {
        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":100,"wordsQuota":15000,"daysRemaining":14,"expiresAt":"2026-04-01","trialActive":true}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);
        string jwt = BuildTestJwt("alice@example.com");

        await svc.HandleAuthCallbackAsync(jwt);

        // RefreshStatusAsync upgrades Account → Trial when trialActive=true
        _settings.Current.AuthMode.Should().Be(AuthMode.Trial);
        _settings.Current.Trial.TrialEmail.Should().Be("alice@example.com");
        _settings.Current.Account.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task HandleAuthCallback_TrialInactive_StaysAccount()
    {
        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":0,"wordsQuota":0,"daysRemaining":0,"expiresAt":"","trialActive":false}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);
        string jwt = BuildTestJwt("bob@example.com");

        await svc.HandleAuthCallbackAsync(jwt);

        // No trial → stays Account
        _settings.Current.AuthMode.Should().Be(AuthMode.Account);
        _settings.Current.Account.Email.Should().Be("bob@example.com");
    }

    [Fact]
    public async Task HandleAuthCallback_SetsAccountEmail()
    {
        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":0,"wordsQuota":15000,"daysRemaining":15,"expiresAt":"2026-04-01","trialActive":true}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);
        string jwt = BuildTestJwt("charlie@example.com");

        await svc.HandleAuthCallbackAsync(jwt);

        _settings.Current.Account.Email.Should().Be("charlie@example.com");
        svc.Email.Should().Be("charlie@example.com");
    }

    [Fact]
    public async Task HandleAuthCallback_RaisesAuthStateChanged()
    {
        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":0,"wordsQuota":15000,"daysRemaining":15,"expiresAt":"2026-04-01","trialActive":true}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        bool? authState = null;
        svc.AuthStateChanged += signedIn => authState = signedIn;

        await svc.HandleAuthCallbackAsync(BuildTestJwt());

        authState.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshStatus_ValidResponse_UpdatesAppSettings()
    {
        // Pre-store a token
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());

        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":500,"wordsQuota":15000,"daysRemaining":10,"expiresAt":"2026-03-15","trialActive":true}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        var status = await svc.RefreshStatusAsync();

        status.Should().NotBeNull();
        status!.WordsUsed.Should().Be(500);
        _settings.Current.Trial.TrialWordsUsed.Should().Be(500);
        _settings.Current.Trial.TrialDaysRemaining.Should().Be(10);
    }

    [Fact]
    public async Task RefreshStatus_TrialActive_UpgradesAccountToTrial()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());
        await _settings.UpdateAsync(_settings.Current with { AuthMode = AuthMode.Account });

        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":0,"wordsQuota":15000,"daysRemaining":15,"expiresAt":"2026-04-01","trialActive":true}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        await svc.RefreshStatusAsync();

        _settings.Current.AuthMode.Should().Be(AuthMode.Trial);
    }

    [Fact]
    public async Task RefreshStatus_TrialExpired_DowngradesTrialToAccount()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());
        await _settings.UpdateAsync(_settings.Current with { AuthMode = AuthMode.Trial });

        var handler = new FakeHandler(HttpStatusCode.OK,
            """{"wordsUsed":15000,"wordsQuota":15000,"daysRemaining":0,"expiresAt":"2025-01-01","trialActive":false}""");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        await svc.RefreshStatusAsync();

        _settings.Current.AuthMode.Should().Be(AuthMode.Account);
    }

    [Fact]
    public async Task RefreshStatus_401_TriggersLogout()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());
        await _settings.UpdateAsync(_settings.Current with { AuthMode = AuthMode.Trial });

        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        var status = await svc.RefreshStatusAsync();

        status.Should().BeNull();
        _secureStorage.RetrieveKey(TokenKey).Should().BeNull();
        _settings.Current.AuthMode.Should().Be(AuthMode.None);
    }

    [Fact]
    public async Task RecordUsage_PostsToEndpoint()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        await svc.RecordUsageAsync("gemini", "gemini-2.5-flash", 42);

        handler.LastBody.Should().Contain("\"wordsUsed\":42");
        handler.LastBody.Should().Contain("\"provider\":\"gemini\"");
    }

    [Fact]
    public async Task Logout_ClearsTokenAndResetsAuthMode()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());
        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.Trial,
            Account = new AccountSettings { Email = "test@example.com" },
            Trial = new TrialSettings { TrialEmail = "test@example.com", TrialWordsUsed = 100 },
        });

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        await svc.LogoutAsync();

        _secureStorage.RetrieveKey(TokenKey).Should().BeNull();
        _settings.Current.AuthMode.Should().Be(AuthMode.None);
        _settings.Current.Trial.TrialEmail.Should().BeEmpty();
        _settings.Current.Account.Email.Should().BeEmpty();
    }

    [Fact]
    public async Task Logout_RaisesAuthStateChangedFalse()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        bool? authState = null;
        svc.AuthStateChanged += signedIn => authState = signedIn;

        await svc.LogoutAsync();

        authState.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_RaisesStatusChangedNull()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        TrialStatus? received = new TrialStatus(); // non-null default
        svc.StatusChanged += status => received = status;

        await svc.LogoutAsync();

        received.Should().BeNull();
    }

    [Fact]
    public void HasValidToken_WithStoredToken_ReturnsTrue()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        svc.HasValidToken.Should().BeTrue();
    }

    [Fact]
    public void HasValidToken_WithoutToken_ReturnsFalse()
    {
        _secureStorage.DeleteKey(TokenKey);

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        svc.HasValidToken.Should().BeFalse();
    }

    [Fact]
    public async Task IsTrialActive_WhenTrialMode_ReturnsTrue()
    {
        _secureStorage.StoreKey(TokenKey, BuildTestJwt());
        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.Trial,
            Trial = new TrialSettings { TrialActive = true },
        });

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        svc.IsTrialActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsTrialActive_WhenAccountMode_ReturnsFalse()
    {
        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.Account,
            Trial = new TrialSettings { TrialActive = false },
        });

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        svc.IsTrialActive.Should().BeFalse();
    }

    [Fact]
    public async Task Email_PrefersAccountEmail_OverTrialEmail()
    {
        await _settings.UpdateAsync(_settings.Current with
        {
            Account = new AccountSettings { Email = "account@example.com" },
            Trial = new TrialSettings { TrialEmail = "trial@example.com" },
        });

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        svc.Email.Should().Be("account@example.com");
    }

    [Fact]
    public async Task Email_FallsBackToTrialEmail_WhenAccountEmailEmpty()
    {
        await _settings.UpdateAsync(_settings.Current with
        {
            Account = new AccountSettings { Email = string.Empty },
            Trial = new TrialSettings { TrialEmail = "legacy@example.com" },
        });

        var handler = new FakeHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(handler);
        using var svc = new TrialAccountService(_secureStorage, _settings, http);

        svc.Email.Should().Be("legacy@example.com");
    }
}

internal sealed class FakeHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;

        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
    }
}
