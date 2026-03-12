using System.Text.RegularExpressions;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Transforms Markdown-formatted LLM responses into clean plain text
/// suitable for TTS synthesis. Strips formatting, expands symbols,
/// and truncates to a word limit.
/// </summary>
public static partial class TextCleaner
{
    /// <summary>
    /// Cleans text for speech synthesis.
    /// </summary>
    /// <param name="text">Raw text (may contain Markdown, URLs, symbols).</param>
    /// <param name="maxWords">Maximum words to speak. 0 = unlimited.</param>
    /// <returns>Clean text suitable for TTS input.</returns>
    public static string CleanForSpeech(string text, int maxWords = 500)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = text;

        // Order matters: structural elements first, then inline formatting, then symbols.
        result = RemoveCodeBlocks(result);
        result = RemoveUrls(result);
        result = ConvertHeaders(result);
        result = ConvertUnorderedListItems(result);
        result = ConvertOrderedListItems(result);
        result = RemoveBlockquoteMarkers(result);
        result = RemoveBold(result);
        result = RemoveItalicAsterisk(result);
        result = RemoveItalicUnderscore(result);
        result = RemoveInlineCode(result);
        result = ExpandCurrencyK(result);
        result = ExpandCurrencyM(result);
        result = ExpandCurrencyB(result);
        result = ExpandCurrencyPlain(result);
        result = ExpandPercent(result);
        result = ExpandArrowUnicode(result);
        result = ExpandArrowAscii(result);
        result = ExpandAmpersand(result);
        result = CollapseWhitespace(result);
        result = result.Trim();

        if (maxWords > 0)
            result = Truncate(result, maxWords);

        return result;
    }

    // ── Fenced code blocks ──────────────────────────────────────────────────

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.ExplicitCapture)]
    private static partial Regex CodeBlockRegex();

    private static string RemoveCodeBlocks(string text)
        => CodeBlockRegex().Replace(text, "Code block omitted.");

    // ── URLs ────────────────────────────────────────────────────────────────

    [GeneratedRegex(@"https?://\S+", RegexOptions.ExplicitCapture)]
    private static partial Regex UrlRegex();

    private static string RemoveUrls(string text)
        => UrlRegex().Replace(text, "Link omitted.");

    // ── Markdown headers ────────────────────────────────────────────────────

    [GeneratedRegex(@"^#{1,6}\s+(?<text>.+)$", RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex HeaderRegex();

    private static string ConvertHeaders(string text)
        => HeaderRegex().Replace(text, "${text}.");

    // ── Unordered list items (-, *, +) ──────────────────────────────────────

    [GeneratedRegex(@"^\s*[-*+]\s+(?<text>.+)$", RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex UnorderedListRegex();

    private static string ConvertUnorderedListItems(string text)
        => UnorderedListRegex().Replace(text, "${text}.");

    // ── Ordered list items (1. 2. etc.) ─────────────────────────────────────

    [GeneratedRegex(@"^\s*\d+\.\s+(?<text>.+)$", RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex OrderedListRegex();

    private static string ConvertOrderedListItems(string text)
        => OrderedListRegex().Replace(text, "${text}.");

    // ── Blockquote markers ──────────────────────────────────────────────────

    [GeneratedRegex(@"^>\s?", RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex BlockquoteRegex();

    private static string RemoveBlockquoteMarkers(string text)
        => BlockquoteRegex().Replace(text, "");

    // ── Bold (**text**) — must run BEFORE italic ────────────────────────────

    [GeneratedRegex(@"\*\*(?<text>.+?)\*\*", RegexOptions.ExplicitCapture)]
    private static partial Regex BoldRegex();

    private static string RemoveBold(string text)
        => BoldRegex().Replace(text, "${text}");

    // ── Italic (*text*) ─────────────────────────────────────────────────────

    [GeneratedRegex(@"\*(?<text>.+?)\*", RegexOptions.ExplicitCapture)]
    private static partial Regex ItalicAsteriskRegex();

    private static string RemoveItalicAsterisk(string text)
        => ItalicAsteriskRegex().Replace(text, "${text}");

    // ── Italic (_text_) ─────────────────────────────────────────────────────

    [GeneratedRegex(@"_(?<text>.+?)_", RegexOptions.ExplicitCapture)]
    private static partial Regex ItalicUnderscoreRegex();

    private static string RemoveItalicUnderscore(string text)
        => ItalicUnderscoreRegex().Replace(text, "${text}");

    // ── Inline code (`text`) ────────────────────────────────────────────────

    [GeneratedRegex(@"`(?<text>[^`]+)`", RegexOptions.ExplicitCapture)]
    private static partial Regex InlineCodeRegex();

    private static string RemoveInlineCode(string text)
        => InlineCodeRegex().Replace(text, "${text}");

    // ── Currency: $NNk → "NN thousand dollars" ──────────────────────────────

    [GeneratedRegex(@"\$(?<n>\d+(?:\.\d+)?)k\b", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex CurrencyKRegex();

    private static string ExpandCurrencyK(string text)
        => CurrencyKRegex().Replace(text, "${n} thousand dollars");

    // ── Currency: $NNm → "NN million dollars" ───────────────────────────────

    [GeneratedRegex(@"\$(?<n>\d+(?:\.\d+)?)m\b", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex CurrencyMRegex();

    private static string ExpandCurrencyM(string text)
        => CurrencyMRegex().Replace(text, "${n} million dollars");

    // ── Currency: $NNb → "NN billion dollars" ───────────────────────────────

    [GeneratedRegex(@"\$(?<n>\d+(?:\.\d+)?)b\b", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex CurrencyBRegex();

    private static string ExpandCurrencyB(string text)
        => CurrencyBRegex().Replace(text, "${n} billion dollars");

    // ── Currency: $NN → "NN dollars" ────────────────────────────────────────

    [GeneratedRegex(@"\$(?<n>\d+(?:,\d{3})*(?:\.\d+)?)", RegexOptions.ExplicitCapture)]
    private static partial Regex CurrencyPlainRegex();

    private static string ExpandCurrencyPlain(string text)
        => CurrencyPlainRegex().Replace(text, "${n} dollars");

    // ── Percent: NN% → "NN percent" ─────────────────────────────────────────

    [GeneratedRegex(@"(?<n>\d+)%", RegexOptions.ExplicitCapture)]
    private static partial Regex PercentRegex();

    private static string ExpandPercent(string text)
        => PercentRegex().Replace(text, "${n} percent");

    // ── Arrow: → → "arrow" ─────────────────────────────────────────────────

    [GeneratedRegex(@"→", RegexOptions.ExplicitCapture)]
    private static partial Regex ArrowUnicodeRegex();

    private static string ExpandArrowUnicode(string text)
        => ArrowUnicodeRegex().Replace(text, " arrow ");

    // ── Arrow: -> → "arrow" ────────────────────────────────────────────────

    [GeneratedRegex(@"->", RegexOptions.ExplicitCapture)]
    private static partial Regex ArrowAsciiRegex();

    private static string ExpandArrowAscii(string text)
        => ArrowAsciiRegex().Replace(text, " arrow ");

    // ── Ampersand: & → "and" ────────────────────────────────────────────────

    [GeneratedRegex(@"&", RegexOptions.ExplicitCapture)]
    private static partial Regex AmpersandRegex();

    private static string ExpandAmpersand(string text)
        => AmpersandRegex().Replace(text, " and ");

    // ── Whitespace collapse ─────────────────────────────────────────────────

    [GeneratedRegex(@"\s{2,}", RegexOptions.ExplicitCapture)]
    private static partial Regex WhitespaceRegex();

    private static string CollapseWhitespace(string text)
        => WhitespaceRegex().Replace(text, " ");

    // ── Truncation ──────────────────────────────────────────────────────────

    private static string Truncate(string text, int maxWords)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
            return text;

        var truncated = string.Join(' ', words.Take(maxWords));
        if (!truncated.EndsWith('.'))
            truncated += ".";
        return truncated + " Full text has been injected.";
    }
}
