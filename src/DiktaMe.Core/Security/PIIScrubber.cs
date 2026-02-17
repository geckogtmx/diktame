namespace DiktaMe.Core.Security;

using System.Text.RegularExpressions;

/// <summary>
/// Redacts personally identifiable information from text using compiled regex patterns.
/// Applied at the pipeline output layer based on the user's configured privacy level.
/// Port of V1's PII scrubbing logic.
/// </summary>
public sealed class PIIScrubber
{
    private const string Redacted = "[REDACTED]";

    // ── Compiled patterns ─────────────────────────────────────────────────────

    // Email addresses
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // US phone numbers (various formats)
    private static readonly Regex PhonePattern = new(
        @"(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}",
        RegexOptions.Compiled);

    // Credit card numbers (basic pattern — 13-19 digit groups)
    private static readonly Regex CreditCardPattern = new(
        @"\b(?:\d[ \-]?){13,19}\b",
        RegexOptions.Compiled);

    // US Social Security Numbers (xxx-xx-xxxx)
    private static readonly Regex SsnPattern = new(
        @"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b",
        RegexOptions.Compiled);

    // API key prefixes (accidental paste detection)
    private static readonly Regex ApiKeyPattern = new(
        @"\b(?:sk-ant-[a-zA-Z0-9\-_]{10,}|sk-[a-zA-Z0-9]{20,}|AIza[a-zA-Z0-9\-_]{30,})\b",
        RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scrubs all known PII patterns from the input text.
    /// Returns the sanitised string with PII replaced by "[REDACTED]".
    /// </summary>
    public static string Scrub(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = ApiKeyPattern.Replace(text, Redacted);
        text = EmailPattern.Replace(text, Redacted);
        text = SsnPattern.Replace(text, Redacted);
        text = PhonePattern.Replace(text, Redacted);
        text = CreditCardPattern.Replace(text, Redacted);

        return text;
    }

    /// <summary>
    /// Returns true if the text contains any detectable PII patterns.
    /// Useful for logging a warning without actually scrubbing.
    /// </summary>
    public static bool ContainsPII(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return ApiKeyPattern.IsMatch(text)
            || EmailPattern.IsMatch(text)
            || SsnPattern.IsMatch(text)
            || PhonePattern.IsMatch(text)
            || CreditCardPattern.IsMatch(text);
    }
}
