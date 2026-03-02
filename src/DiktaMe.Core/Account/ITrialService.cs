
namespace DiktaMe.Core.Account;
/// <summary>
/// Trial-specific operations — status sync and usage reporting.
/// Consumers that need trial data should depend on this interface.
/// </summary>
public interface ITrialService
{
    /// <summary>
    /// Fetches the latest trial status from the server.
    /// Returns null on network error. Triggers auto-logout on HTTP 401.
    /// </summary>
    Task<TrialStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports word usage to the server for quota tracking.
    /// </summary>
    Task RecordUsageAsync(string provider, string model, int wordsUsed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the trial is currently active (signed in + server confirms trial).
    /// </summary>
    bool IsTrialActive { get; }

    /// <summary>
    /// Raised when trial status changes (login, logout, status refresh).
    /// The argument is the new status, or null on logout/no trial.
    /// </summary>
    event Action<TrialStatus?>? StatusChanged;
}
