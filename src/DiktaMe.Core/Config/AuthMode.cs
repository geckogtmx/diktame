
namespace DiktaMe.Core.Config;
/// <summary>
/// Authentication mode for the application.
/// Determines how LLM/STT requests are routed.
/// </summary>
public enum AuthMode
{
    /// <summary>No authentication — BYOK (bring your own key) only.</summary>
    None = 0,

    /// <summary>Trial mode — requests routed through managed Gemini proxy.</summary>
    Trial = 1,

    /// <summary>User has configured their own API keys.</summary>
    ApiKey = 2,

    /// <summary>Signed in via OAuth but using own API keys (not trial proxy).</summary>
    Account = 3,
}
