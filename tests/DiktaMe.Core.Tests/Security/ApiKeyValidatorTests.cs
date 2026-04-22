
using DiktaMe.Core.Security;
using Xunit;

namespace DiktaMe.Core.Tests.Security;
public sealed class ApiKeyValidatorTests
{
    // ── OpenAI ────────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz012345678901234567890", true)]   // 48+ chars, sk- prefix
    [InlineData("sk-proj-abcdefghijklmnopqrstuvwxyz0123456789012345678", true)] // newer sk-proj- format
    [InlineData("sk-short", false)]         // too short
    [InlineData("notsk-something", false)]  // wrong prefix
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidOpenAI_ReturnsExpected(string? key, bool expected)
        => Assert.Equal(expected, ApiKeyValidator.IsValidOpenAI(key));

    // ── Anthropic ─────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("sk-ant-api03-something-long-enough-here", true)]
    [InlineData("sk-ant-x", false)]     // too short
    [InlineData("sk-not-ant", false)]   // wrong prefix
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidAnthropic_ReturnsExpected(string? key, bool expected)
        => Assert.Equal(expected, ApiKeyValidator.IsValidAnthropic(key));

    // ── Gemini ────────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("AIzaSyABCDEFGHIJKLMNOPQRSTUVWXYZ0123456", true)]  // 39 chars — Google AI key
    [InlineData("AIzaShort", true)]                                  // AIza prefix accepted even if short
    [InlineData("legacyFormat30CharsLongEnough!", true)]             // len >= 30 and non-digit
    [InlineData("123456789012345678901234567890", false)]            // all-digit garbage 30 chars
    [InlineData("short", false)]   // too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidGemini_ReturnsExpected(string? key, bool expected)
        => Assert.Equal(expected, ApiKeyValidator.IsValidGemini(key));

    // ── Deepgram ──────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789", true)]  // 36 chars mixed
    [InlineData("123456789012345678901234567890", false)]       // 30 all-digit chars → reject
    [InlineData("tooshort", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidDeepgram_ReturnsExpected(string? key, bool expected)
        => Assert.Equal(expected, ApiKeyValidator.IsValidDeepgram(key));

    // ── Generic ───────────────────────────────────────────────────────────────

    [Theory, Trait("Category", "Unit")]
    [InlineData("anything-goes", true)]
    [InlineData("x", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidGeneric_ReturnsExpected(string? key, bool expected)
        => Assert.Equal(expected, ApiKeyValidator.IsValidGeneric(key));
}
