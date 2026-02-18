using DiktaMe.Core.Input;

namespace DiktaMe.Core.Tests.Input;

/// <summary>
/// Tests for HotkeyParser — hotkey string parsing into Win32 modifier flags + VK code.
/// </summary>
public class HotkeyParserTests
{
    // Win32 modifier flag constants (mirrored from HotkeyParser internals)
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    // ── Null / empty ─────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        bool ok = HotkeyParser.TryParse(null!, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryParse_Empty_ReturnsFalse()
    {
        bool ok = HotkeyParser.TryParse("", out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryParse_OnlyModifiers_ReturnsFalse()
    {
        bool ok = HotkeyParser.TryParse("Ctrl+Alt", out _, out _);
        Assert.False(ok);
    }

    // ── Letter keys ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ctrl+Alt+D", ModControl | ModAlt, (uint)'D')]
    [InlineData("Control+Alt+D", ModControl | ModAlt, (uint)'D')]
    [InlineData("ctrl+alt+d", ModControl | ModAlt, (uint)'D')]
    [InlineData("Ctrl+Alt+A", ModControl | ModAlt, (uint)'A')]
    [InlineData("Ctrl+Alt+T", ModControl | ModAlt, (uint)'T')]
    [InlineData("Ctrl+Alt+R", ModControl | ModAlt, (uint)'R')]
    [InlineData("Ctrl+Alt+V", ModControl | ModAlt, (uint)'V')]
    [InlineData("Ctrl+Alt+N", ModControl | ModAlt, (uint)'N')]
    public void TryParse_CtrlAltLetter_ParsesCorrectly(string input, uint expectedMod, uint expectedVk)
    {
        bool ok = HotkeyParser.TryParse(input, out uint mod, out uint vk);

        Assert.True(ok);
        Assert.Equal(expectedMod, mod);
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void TryParse_Shift_SetsShiftModifier()
    {
        bool ok = HotkeyParser.TryParse("Ctrl+Shift+A", out uint mod, out uint vk);

        Assert.True(ok);
        Assert.Equal(ModControl | ModShift, mod);
        Assert.Equal((uint)'A', vk);
    }

    [Fact]
    public void TryParse_Win_SetsWinModifier()
    {
        bool ok = HotkeyParser.TryParse("Win+D", out uint mod, out uint vk);

        Assert.True(ok);
        Assert.Equal(ModWin, mod);
        Assert.Equal((uint)'D', vk);
    }

    // ── Digit keys ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ctrl+1", (uint)'1')]
    [InlineData("Ctrl+0", (uint)'0')]
    [InlineData("Ctrl+9", (uint)'9')]
    public void TryParse_DigitKey_ParsesCorrectly(string input, uint expectedVk)
    {
        bool ok = HotkeyParser.TryParse(input, out _, out uint vk);

        Assert.True(ok);
        Assert.Equal(expectedVk, vk);
    }

    // ── Function keys ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("F1", 0x70u)]
    [InlineData("Ctrl+F1", 0x70u)]
    [InlineData("Alt+F5", 0x74u)]
    [InlineData("F12", 0x7Bu)]
    public void TryParse_FunctionKey_ParsesCorrectly(string input, uint expectedVk)
    {
        bool ok = HotkeyParser.TryParse(input, out _, out uint vk);

        Assert.True(ok);
        Assert.Equal(expectedVk, vk);
    }

    // ── Named keys ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ctrl+Enter", 0x0Du)]
    [InlineData("Ctrl+Tab", 0x09u)]
    [InlineData("Alt+Space", 0x20u)]
    [InlineData("Ctrl+Escape", 0x1Bu)]
    public void TryParse_NamedKey_ParsesCorrectly(string input, uint expectedVk)
    {
        bool ok = HotkeyParser.TryParse(input, out _, out uint vk);

        Assert.True(ok);
        Assert.Equal(expectedVk, vk);
    }

    // ── No modifier (bare key) ────────────────────────────────────────────────

    [Fact]
    public void TryParse_BareLetterKey_NoModifiers()
    {
        bool ok = HotkeyParser.TryParse("D", out uint mod, out uint vk);

        Assert.True(ok);
        Assert.Equal(0u, mod);
        Assert.Equal((uint)'D', vk);
    }

    // ── Unknown key ──────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_UnknownKey_ReturnsFalse()
    {
        bool ok = HotkeyParser.TryParse("Ctrl+Alt+XYZ", out _, out _);
        Assert.False(ok);
    }
}
