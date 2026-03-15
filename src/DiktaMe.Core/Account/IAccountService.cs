
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
    /// Stores the token, extracts email, sets <see cref="Config.AuthMode.Wallet"/>.
    /// </summary>
    Task HandleAuthCallbackAsync(string token, CancellationToken cancellationToken = default);

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
}
