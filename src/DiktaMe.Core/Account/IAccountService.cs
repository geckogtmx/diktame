
namespace DiktaMe.Core.Account;
/// <summary>
/// Authentication-only service — login, logout, token, email.
/// Wallet operations (balance, transactions) live on WalletManager.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Opens the user's default browser to the dikta.me login page.
    /// </summary>
    void Login();

    /// <summary>
    /// Processes the JWT received from the <c>diktame://auth?token=...</c> deeplink.
    /// Stores the token, extracts email/display name/avatar into settings.
    /// </summary>
    Task HandleAuthCallbackAsync(string token, string? refreshToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the JWT token and resets auth state.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a valid auth token exists in SecureStorage.
    /// </summary>
    bool HasValidToken { get; }

    /// <summary>
    /// Cached email from the current session.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Raised when auth state changes (login or logout).
    /// True = signed in, false = signed out.
    /// </summary>
    event Action<bool>? AuthStateChanged;

    /// <summary>
    /// Fetches the server-side profile to sync fields not in the JWT
    /// (e.g. avatar_url for email/password users who uploaded via the website).
    /// </summary>
    Task SyncProfileFromServerAsync(CancellationToken cancellationToken = default);
}
