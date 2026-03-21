
namespace DiktaMe.Core.Config;
/// <summary>
/// Non-sensitive account and wallet metadata persisted in settings.json.
/// The JWT token itself is stored in <see cref="Security.SecureStorage"/>.
/// </summary>
public sealed record AccountSettings
{
    /// <summary>Email address of the signed-in user.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Display name from OAuth provider (or email prefix fallback).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Avatar URL from OAuth provider (Google/GitHub profile picture).</summary>
    public string AvatarUrl { get; init; } = string.Empty;

    /// <summary>ISO 8601 timestamp of the last sync with the server.</summary>
    public string LastSynced { get; init; } = string.Empty;

    /// <summary>
    /// Cached wallet balance in microdollars (1 USD = 1,000,000) for UI startup display.
    /// Authoritative balance is always server-side — this is a UI cache only.
    /// </summary>
    public long WalletBalanceMicro { get; init; }

    /// <summary>
    /// Base URL for the wallet proxy Edge Function. Overridable for dev/staging.
    /// </summary>
    public string WalletProxyUrl { get; init; } = "https://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/wallet-proxy";
}
