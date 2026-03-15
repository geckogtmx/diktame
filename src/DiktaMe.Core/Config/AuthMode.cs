
namespace DiktaMe.Core.Config;
/// <summary>
/// Authentication mode for the application.
/// Determines how LLM/STT requests are routed.
/// </summary>
public enum AuthMode
{
    /// <summary>No authentication — BYOK (bring your own key) only.</summary>
    None = 0,

    /// <summary>Signed in, using managed Pay-As-You-Go wallet proxy.</summary>
    Wallet = 1,

    /// <summary>User has configured their own API keys.</summary>
    ApiKey = 2,

    /// <summary>Signed in via OAuth but using own API keys (not wallet proxy).</summary>
    Account = 3,
}
