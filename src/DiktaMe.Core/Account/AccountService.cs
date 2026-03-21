
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.Core.Account;
/// <summary>
/// Manages the account lifecycle: browser-based OAuth login,
/// JWT token storage (via <see cref="SecureStorage"/>), and logout.
/// Wallet-specific operations (balance, transactions) live on WalletManager.
/// </summary>
public sealed class AccountService : IAccountService, IDisposable
{
    private const string TokenKey = "trial_token"; // kept for backward compat with existing keys.dat
    private const string RefreshTokenKey = "refresh_token";
    private const string LoginUrl = "https://dikta.me/login?mode=app";

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;

    public AccountService(SecureStorage secureStorage, SettingsManager settings)
    {
        _secureStorage = secureStorage;
        _settings = settings;
    }

    /// <inheritdoc />
    public bool HasValidToken => _secureStorage.RetrieveKey(TokenKey) is not null;

    /// <inheritdoc />
    public string? Email
    {
        get
        {
            string acctEmail = _settings.Current.Account.Email;
            return string.IsNullOrEmpty(acctEmail) ? null : acctEmail;
        }
    }

    /// <inheritdoc />
    public event Action<bool>? AuthStateChanged;

    // ── Login ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Login()
    {
        Log.Information("AccountService: opening browser for login");
        Process.Start(new ProcessStartInfo(LoginUrl) { UseShellExecute = true });
    }

    // ── Auth Callback ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task HandleAuthCallbackAsync(string token, string? refreshToken = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // Store JWT in encrypted storage
        _secureStorage.StoreKey(TokenKey, token);

        // Store refresh token for silent JWT renewal
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            _secureStorage.StoreKey(RefreshTokenKey, refreshToken);
        }

        // Extract claims from JWT payload
        string? email = JwtDecoder.ExtractEmail(token);
        string? displayName = JwtDecoder.ExtractDisplayName(token);
        string? avatarUrl = JwtDecoder.ExtractAvatarUrl(token);
        Log.Information("AccountService: auth callback — email={Email}, displayName={DisplayName}",
            email ?? "(none)", displayName ?? "(none)");

        // Set AuthMode.Wallet — all signed-in users are wallet users.
        // Wallet balance sync happens separately via WalletManager.
        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.Wallet,
            Account = _settings.Current.Account with
            {
                Email = email ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                AvatarUrl = avatarUrl ?? string.Empty,
            },
        }, cancellationToken).ConfigureAwait(false);

        // Notify UI immediately
        AuthStateChanged?.Invoke(true);
    }

    // ── Server Profile Sync ────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the server-side profile to sync fields that aren't in the JWT
    /// (e.g. avatar_url for email/password users who uploaded via the website).
    /// Non-fatal — logs warnings on failure.
    /// </summary>
    public async Task SyncProfileFromServerAsync(CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (token is null)
        {
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await http.GetAsync("https://dikta.me/api/account/me", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("AccountService: profile sync returned {StatusCode}", response.StatusCode);
                return;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? serverAvatarUrl = root.TryGetProperty("avatarUrl", out var av) && av.ValueKind == JsonValueKind.String
                ? av.GetString() : null;

            // Only update if the server has a different (non-empty) avatar URL
            string currentAvatar = _settings.Current.Account.AvatarUrl;
            if (!string.IsNullOrEmpty(serverAvatarUrl) && !string.Equals(serverAvatarUrl, currentAvatar, StringComparison.Ordinal))
            {
                await _settings.UpdateAsync(_settings.Current with
                {
                    Account = _settings.Current.Account with { AvatarUrl = serverAvatarUrl },
                }, cancellationToken).ConfigureAwait(false);
                Log.Information("AccountService: synced avatar URL from server");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AccountService: profile sync failed (non-fatal)");
        }
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _secureStorage.DeleteKey(TokenKey);
        _secureStorage.DeleteKey(RefreshTokenKey);

        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.None,
            Account = new AccountSettings(),
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("AccountService: logged out");
        AuthStateChanged?.Invoke(false);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        // No resources to dispose — kept for DI lifecycle compatibility
    }
}
