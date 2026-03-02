
namespace DiktaMe.Core.Config;
/// <summary>
/// Non-sensitive account metadata persisted in settings.json.
/// Auth-only — no trial-specific fields.
/// </summary>
public sealed record AccountSettings
{
    /// <summary>Email address of the signed-in user.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>ISO 8601 timestamp of the last sync with the server.</summary>
    public string LastSynced { get; init; } = string.Empty;
}
