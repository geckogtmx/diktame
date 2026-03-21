
using System.Diagnostics;
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
