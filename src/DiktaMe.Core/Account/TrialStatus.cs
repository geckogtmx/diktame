
namespace DiktaMe.Core.Account;
/// <summary>
/// Response model for GET <c>/api/trial/status</c>.
/// Matches the JSON returned by the dikta.me API.
/// </summary>
public sealed record TrialStatus
{
    /// <summary>Number of words used so far.</summary>
    public int WordsUsed { get; init; }

    /// <summary>Total word quota for the trial.</summary>
    public int WordsQuota { get; init; }

    /// <summary>Days remaining on the trial.</summary>
    public int DaysRemaining { get; init; }

    /// <summary>ISO 8601 timestamp when the trial expires.</summary>
    public string ExpiresAt { get; init; } = string.Empty;

    /// <summary>Whether the trial is currently active.</summary>
    public bool TrialActive { get; init; }
}
