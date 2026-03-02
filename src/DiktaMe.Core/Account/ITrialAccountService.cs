
namespace DiktaMe.Core.Account;
/// <summary>
/// Manages the trial account lifecycle: browser login, token storage,
/// status sync, usage reporting, and logout.
/// </summary>
public interface ITrialAccountService
{
    /// <summary>
    /// Opens the user's default browser to the dikta.me login page
    /// with <c>mode=app</c> for desktop OAuth flow.
    /// </summary>
    void Login();

    /// <summary>
    /// Processes the JWT received from the <c>diktame://auth?token=...</c> deeplink.
    /// Stores the token in SecureStorage, extracts email, and syncs trial status.
    /// </summary>
    Task HandleAuthCallbackAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the latest trial status from the server.
    /// Returns null on network error. Triggers auto-logout on HTTP 401.
    /// </summary>
    Task<TrialStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports word usage to the server for quota tracking.
    /// Also updates local <see cref="Config.TrialSettings.TrialWordsUsed"/> optimistically.
    /// </summary>
    Task RecordUsageAsync(string provider, string model, int wordsUsed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the JWT token and resets AuthMode/TrialSettings to defaults.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a trial token exists in SecureStorage.
    /// </summary>
    bool HasValidToken { get; }

    /// <summary>
    /// Cached email from the current trial session (from AppSettings.Trial.TrialEmail).
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Raised when trial status changes (login, logout, status refresh).
    /// The argument is the new status, or null on logout.
    /// </summary>
    event Action<TrialStatus?>? StatusChanged;
}
