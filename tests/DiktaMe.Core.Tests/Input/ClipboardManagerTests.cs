using DiktaMe.Core.Input;

namespace DiktaMe.Core.Tests.Input;

/// <summary>
/// Tests for ClipboardManager Win32 clipboard read/write.
/// Requires a real Windows clipboard (not headless/locked session).
/// Tests must run serially (not in parallel) to avoid clipboard race conditions.
/// </summary>
[Collection("Clipboard")] // Disable parallel execution - real clipboard is shared resource
public class ClipboardManagerTests
{
    // ── SetText / GetText roundtrip ───────────────────────────────────────────

    [Fact]
    public void SetText_ThenGetText_ReturnsExactText()
    {
        const string text = "Hello, dIKta.me!";
        ClipboardManager.SetText(text);
        string result = ClipboardManager.GetText();
        Assert.Equal(text, result);
    }

    [Fact]
    public void SetText_WithUnicode_RoundTrips()
    {
        const string text = "Héllo wörld — こんにちは";
        ClipboardManager.SetText(text);
        string result = ClipboardManager.GetText();
        Assert.Equal(text, result);
    }

    [Fact]
    public void SetText_Null_ClearsClipboard()
    {
        // Arrange — put something on the clipboard first
        ClipboardManager.SetText("something");

        // Act
        ClipboardManager.SetText(null);

        // Assert — clipboard is empty or cleared
        string result = ClipboardManager.GetText();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SetText_Empty_ClearsClipboard()
    {
        ClipboardManager.SetText("something");
        ClipboardManager.SetText(string.Empty);
        string result = ClipboardManager.GetText();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetText_WhenEmpty_ReturnsEmptyString()
    {
        ClipboardManager.SetText(null);
        string result = ClipboardManager.GetText();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetText_ReturnsNonNull()
    {
        string result = ClipboardManager.GetText();
        Assert.NotNull(result);
    }

    // ── Save/restore pattern used by TextInjector ─────────────────────────────

    [Fact]
    public void SaveRestore_PreservesOriginalContent()
    {
        const string original = "original clipboard content";
        ClipboardManager.SetText(original);

        // Simulate what TextInjector does: overwrite, then restore
        string saved = ClipboardManager.GetText();
        ClipboardManager.SetText("injected text");
        ClipboardManager.SetText(saved);

        string restored = ClipboardManager.GetText();
        Assert.Equal(original, restored);
    }
}
