using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class TextCleanerTests
{
    // ── Edge cases ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void CleanForSpeech_EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        TextCleaner.CleanForSpeech(input!).Should().BeEmpty();
    }

    [Fact]
    public void CleanForSpeech_PlainText_PassesThrough()
    {
        TextCleaner.CleanForSpeech("Hello world").Should().Be("Hello world");
    }

    // ── Headers ─────────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_H1_StripsHashAndAddsPeriod()
    {
        TextCleaner.CleanForSpeech("# Summary").Should().Be("Summary.");
    }

    [Fact]
    public void CleanForSpeech_H3_StripsHashesAndAddsPeriod()
    {
        TextCleaner.CleanForSpeech("### Details").Should().Be("Details.");
    }

    [Fact]
    public void CleanForSpeech_MultipleHeaders_AllConverted()
    {
        var input = "# Title\n\nSome text\n\n## Subtitle";
        var result = TextCleaner.CleanForSpeech(input);
        result.Should().Contain("Title.");
        result.Should().Contain("Subtitle.");
        result.Should().NotContain("#");
    }

    // ── Bold / Italic ───────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_Bold_StripsMarkers()
    {
        TextCleaner.CleanForSpeech("The **budget** is set")
            .Should().Be("The budget is set");
    }

    [Fact]
    public void CleanForSpeech_Italic_StripsAsteriskMarkers()
    {
        TextCleaner.CleanForSpeech("This is *important* text")
            .Should().Be("This is important text");
    }

    [Fact]
    public void CleanForSpeech_ItalicUnderscore_StripsMarkers()
    {
        TextCleaner.CleanForSpeech("This is _important_ text")
            .Should().Be("This is important text");
    }

    [Fact]
    public void CleanForSpeech_BoldItalicNested_StripsBothMarkers()
    {
        TextCleaner.CleanForSpeech("This is ***very important*** text")
            .Should().Be("This is very important text");
    }

    // ── Inline code ─────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_InlineCode_StripsBackticks()
    {
        TextCleaner.CleanForSpeech("Use the `print` function")
            .Should().Be("Use the print function");
    }

    // ── Code blocks ─────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_CodeBlock_ReplacedWithOmitted()
    {
        var input = "Before\n```\nvar x = 1;\n```\nAfter";
        var result = TextCleaner.CleanForSpeech(input);
        result.Should().Contain("Code block omitted.");
        result.Should().NotContain("var x = 1");
    }

    [Fact]
    public void CleanForSpeech_CodeBlockWithLanguageTag_ReplacedWithOmitted()
    {
        var input = "Example:\n```csharp\npublic class Foo {}\n```\nDone.";
        var result = TextCleaner.CleanForSpeech(input);
        result.Should().Contain("Code block omitted.");
        result.Should().NotContain("public class Foo");
    }

    // ── Lists ───────────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_UnorderedListDash_StripsMarkersAddsPeriods()
    {
        var input = "- Item one\n- Item two";
        var result = TextCleaner.CleanForSpeech(input);
        result.Should().Contain("Item one.");
        result.Should().Contain("Item two.");
        result.Should().NotContain("-");
    }

    [Fact]
    public void CleanForSpeech_UnorderedListAsterisk_StripsMarkersAddsPeriods()
    {
        var result = TextCleaner.CleanForSpeech("* First\n* Second");
        result.Should().Contain("First.");
        result.Should().Contain("Second.");
    }

    [Fact]
    public void CleanForSpeech_OrderedList_StripsNumbersAddsPeriods()
    {
        var input = "1. Alpha\n2. Beta\n3. Gamma";
        var result = TextCleaner.CleanForSpeech(input);
        result.Should().Contain("Alpha.");
        result.Should().Contain("Beta.");
        result.Should().Contain("Gamma.");
        result.Should().NotStartWith("1.");
    }

    // ── Blockquotes ─────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_Blockquote_StripsMarkerKeepsText()
    {
        TextCleaner.CleanForSpeech("> This is a quote")
            .Should().Be("This is a quote");
    }

    // ── URLs ────────────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_HttpsUrl_ReplacedWithLinkOmitted()
    {
        TextCleaner.CleanForSpeech("Visit https://example.com/page for details")
            .Should().Be("Visit Link omitted. for details");
    }

    [Fact]
    public void CleanForSpeech_HttpUrl_ReplacedWithLinkOmitted()
    {
        TextCleaner.CleanForSpeech("See http://docs.example.com")
            .Should().Contain("Link omitted.");
    }

    // ── Currency ────────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_DollarK_ExpandsToThousandDollars()
    {
        TextCleaner.CleanForSpeech("Budget is $50k")
            .Should().Be("Budget is 50 thousand dollars");
    }

    [Fact]
    public void CleanForSpeech_DollarM_ExpandsToMillionDollars()
    {
        TextCleaner.CleanForSpeech("Revenue hit $2.5m last year")
            .Should().Contain("2.5 million dollars");
    }

    [Fact]
    public void CleanForSpeech_DollarB_ExpandsToBillionDollars()
    {
        TextCleaner.CleanForSpeech("Valued at $3b")
            .Should().Contain("3 billion dollars");
    }

    [Fact]
    public void CleanForSpeech_PlainDollar_ExpandsToDollars()
    {
        TextCleaner.CleanForSpeech("It costs $50")
            .Should().Be("It costs 50 dollars");
    }

    [Fact]
    public void CleanForSpeech_DollarWithCommas_ExpandsToDollars()
    {
        TextCleaner.CleanForSpeech("Price is $1,500")
            .Should().Contain("1,500 dollars");
    }

    // ── Symbols ─────────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_Percent_ExpandsToPercent()
    {
        TextCleaner.CleanForSpeech("Growth was 25%")
            .Should().Be("Growth was 25 percent");
    }

    [Fact]
    public void CleanForSpeech_Ampersand_ExpandsToAnd()
    {
        TextCleaner.CleanForSpeech("R&D department")
            .Should().Contain("R and D");
    }

    [Fact]
    public void CleanForSpeech_UnicodeArrow_ExpandsToArrow()
    {
        TextCleaner.CleanForSpeech("Input → Output")
            .Should().Contain("arrow");
    }

    [Fact]
    public void CleanForSpeech_AsciiArrow_ExpandsToArrow()
    {
        TextCleaner.CleanForSpeech("A -> B")
            .Should().Contain("arrow");
    }

    // ── Whitespace ──────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_ExcessWhitespace_CollapsedToSingleSpace()
    {
        TextCleaner.CleanForSpeech("Hello    world   today")
            .Should().Be("Hello world today");
    }

    [Fact]
    public void CleanForSpeech_LeadingTrailingWhitespace_Trimmed()
    {
        TextCleaner.CleanForSpeech("  Hello world  ")
            .Should().Be("Hello world");
    }

    // ── Truncation ──────────────────────────────────────────────────────────

    [Fact]
    public void CleanForSpeech_UnderMaxWords_NoTruncation()
    {
        var input = "one two three four five";
        TextCleaner.CleanForSpeech(input, maxWords: 10)
            .Should().Be("one two three four five");
    }

    [Fact]
    public void CleanForSpeech_OverMaxWords_TruncatesWithSuffix()
    {
        var words = Enumerable.Range(1, 20).Select(i => $"word{i}");
        var input = string.Join(" ", words);
        var result = TextCleaner.CleanForSpeech(input, maxWords: 5);
        result.Should().EndWith("Full text has been injected.");
        result.Should().StartWith("word1");
    }

    [Fact]
    public void CleanForSpeech_MaxWordsZero_NoTruncation()
    {
        var words = Enumerable.Range(1, 600).Select(i => $"word{i}");
        var input = string.Join(" ", words);
        var result = TextCleaner.CleanForSpeech(input, maxWords: 0);
        result.Should().NotContain("Full text has been injected.");
    }

    // ── Complex integration scenarios ───────────────────────────────────────

    [Fact]
    public void CleanForSpeech_FullMarkdownDocument_CleanedCorrectly()
    {
        var input = "## Summary\n\nThe **budget** is $50k.\n- Item 1\n- Item 2\n```code block```";
        var result = TextCleaner.CleanForSpeech(input);
        result.Should().Contain("Summary.");
        result.Should().Contain("budget");
        result.Should().Contain("50 thousand dollars");
        result.Should().Contain("Item 1.");
        result.Should().Contain("Item 2.");
        result.Should().Contain("Code block omitted.");
        result.Should().NotContain("**");
        result.Should().NotContain("##");
    }
}
