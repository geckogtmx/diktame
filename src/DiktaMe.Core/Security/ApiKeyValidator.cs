namespace DiktaMe.Core.Security;

/// <summary>
/// Validates API key formats for known providers.
/// All methods are static and do not make network calls.
/// </summary>
public static class ApiKeyValidator
{
    /// <summary>OpenAI keys begin with "sk-" and are at least 48 characters.</summary>
    public static bool IsValidOpenAI(string? key)
        => !string.IsNullOrWhiteSpace(key)
        && key.StartsWith("sk-", StringComparison.Ordinal)
        && key.Length >= 48;

    /// <summary>Anthropic keys begin with "sk-ant-".</summary>
    public static bool IsValidAnthropic(string? key)
        => !string.IsNullOrWhiteSpace(key)
        && key.StartsWith("sk-ant-", StringComparison.Ordinal)
        && key.Length >= 20;

    /// <summary>
    /// Google Gemini API keys start with "AIza" (standard issuance) or are at
    /// least 30 non-all-digit characters (legacy/alternate flavors).
    /// </summary>
    public static bool IsValidGemini(string? key)
        => !string.IsNullOrWhiteSpace(key)
        && (key.StartsWith("AIza", StringComparison.Ordinal)
            || (key.Length >= 30 && !key.All(char.IsDigit)));

    /// <summary>
    /// Deepgram API keys are at least 30 characters and contain non-digit
    /// characters (valid keys are hex with letters; all-digit is always garbage).
    /// </summary>
    public static bool IsValidDeepgram(string? key)
        => !string.IsNullOrWhiteSpace(key)
        && key.Length >= 30
        && !key.All(char.IsDigit);

    /// <summary>
    /// Returns true for any non-whitespace key (used for OpenAI-compatible
    /// endpoints whose key format is not standardised).
    /// </summary>
    public static bool IsValidGeneric(string? key)
        => !string.IsNullOrWhiteSpace(key);
}
