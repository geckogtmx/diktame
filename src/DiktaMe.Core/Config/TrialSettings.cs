
namespace DiktaMe.Core.Config;
/// <summary>
/// Non-sensitive trial account metadata persisted in settings.json.
/// The JWT token itself is stored in <see cref="Security.SecureStorage"/> under key "trial_token".
/// </summary>
public sealed record TrialSettings
{
    /// <summary>Email address associated with the trial account.</summary>
    public string TrialEmail { get; init; } = string.Empty;

    /// <summary>Number of words used so far in the trial.</summary>
    public int TrialWordsUsed { get; init; }

    /// <summary>Total word quota for the trial (default 15,000).</summary>
    public int TrialWordsQuota { get; init; } = 15_000;

    /// <summary>Days remaining on the trial.</summary>
    public int TrialDaysRemaining { get; init; }

    /// <summary>ISO 8601 timestamp when the trial expires.</summary>
    public string TrialExpiresAt { get; init; } = string.Empty;

    /// <summary>Whether the trial is currently active (not expired and under quota).</summary>
    public bool TrialActive { get; init; }

    /// <summary>ISO 8601 timestamp of the last status sync with the server.</summary>
    public string TrialLastSynced { get; init; } = string.Empty;
}
