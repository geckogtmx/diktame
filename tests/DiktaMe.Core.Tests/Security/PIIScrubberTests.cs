
using DiktaMe.Core.Security;
using Xunit;

namespace DiktaMe.Core.Tests.Security;
public sealed class PIIScrubberTests
{
    // ── Email ─────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public void Scrub_RedactsEmail()
    {
        string result = PIIScrubber.Scrub("Contact me at user@example.com please.");
        Assert.DoesNotContain("user@example.com", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact, Trait("Category", "Unit")]
    public void Scrub_RedactsMultipleEmails()
    {
        string result = PIIScrubber.Scrub("a@b.com and c@d.org");
        Assert.Equal(2, CountOccurrences(result, "[REDACTED]"));
    }

    // ── Phone ─────────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("Call me at 555-867-5309")]
    [InlineData("Call me at (555) 867-5309")]
    [InlineData("Call me at 5558675309")]
    [InlineData("Call me at +1 555 867 5309")]
    public void Scrub_RedactsPhoneNumber(string input)
    {
        string result = PIIScrubber.Scrub(input);
        Assert.Contains("[REDACTED]", result);
    }

    // ── SSN ───────────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("My SSN is 123-45-6789")]
    [InlineData("SSN: 123 45 6789")]
    public void Scrub_RedactsSsn(string input)
    {
        string result = PIIScrubber.Scrub(input);
        Assert.Contains("[REDACTED]", result);
    }

    // ── API Keys ──────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz01234567890")]           // OpenAI-style
    [InlineData("sk-ant-api03-ABCDEFGHIJKLMNabcdefghijklmn-xxxx")]    // Anthropic-style
    [InlineData("AIzaSyABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]            // Gemini-style
    public void Scrub_RedactsApiKeyAccidentalPaste(string key)
    {
        string input = $"Here is my key: {key}";
        string result = PIIScrubber.Scrub(input);
        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain(key, result);
    }

    // ── Clean text ────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public void Scrub_LeavesCleanTextUnchanged()
    {
        const string Clean = "The quick brown fox jumps over the lazy dog.";
        Assert.Equal(Clean, PIIScrubber.Scrub(Clean));
    }

    [Fact, Trait("Category", "Unit")]
    public void Scrub_EmptyStringReturnsEmpty()
        => Assert.Equal(string.Empty, PIIScrubber.Scrub(string.Empty));

    // ── ContainsPII ───────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public void ContainsPII_TrueForEmail()
        => Assert.True(PIIScrubber.ContainsPII("user@example.com"));

    [Fact, Trait("Category", "Unit")]
    public void ContainsPII_FalseForCleanText()
        => Assert.False(PIIScrubber.ContainsPII("Hello world"));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
