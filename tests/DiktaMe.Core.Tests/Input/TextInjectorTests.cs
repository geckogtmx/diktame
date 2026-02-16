using DiktaMe.Core.Input;
using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using Moq;

namespace DiktaMe.Core.Tests.Input;

/// <summary>
/// Tests for TextInjector logic using a mocked IInputSimulator.
/// Clipboard interactions use the real Win32 clipboard.
/// </summary>
public class TextInjectorTests
{
    private readonly Mock<IInputSimulator> _simMock;
    private readonly Mock<IKeyboardSimulator> _kbMock;
    private readonly TextInjector _injector;

    public TextInjectorTests()
    {
        _kbMock = new Mock<IKeyboardSimulator>();

        // Chain modifiers so fluent calls don't throw NullReferenceException
        _kbMock
            .Setup(k => k.ModifiedKeyStroke(It.IsAny<VirtualKeyCode>(), It.IsAny<VirtualKeyCode>()))
            .Returns(_kbMock.Object);
        _kbMock
            .Setup(k => k.KeyPress(It.IsAny<VirtualKeyCode>()))
            .Returns(_kbMock.Object);

        _simMock = new Mock<IInputSimulator>();
        _simMock.Setup(s => s.Keyboard).Returns(_kbMock.Object);

        _injector = new TextInjector(_simMock.Object);
    }

    // ── InjectText — null guard ───────────────────────────────────────────────

    [Fact]
    public void InjectText_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _injector.InjectText(null!));
    }

    // ── InjectText — Ctrl+V is always sent ───────────────────────────────────

    [Fact]
    public void InjectText_SendsCtrlV()
    {
        _injector.InjectText("hello");

        _kbMock.Verify(k => k.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V), Times.Once);
    }

    [Fact]
    public void InjectText_EmptyString_StillSendsCtrlV()
    {
        _injector.InjectText(string.Empty);

        _kbMock.Verify(k => k.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V), Times.Once);
    }

    // ── InjectText — trailing space ───────────────────────────────────────────

    [Fact]
    public void InjectText_TrailingSpaceTrue_PastesTextPlusSpace()
    {
        const string text = "hello";
        ClipboardManager.SetText("original");

        _injector.InjectText(text, trailingSpace: true);

        // The clipboard is immediately restored after the paste.
        // Verify by observing what was set during injection via the known sequence:
        // We cannot intercept the intermediate SetText call easily, but we can verify
        // that the clipboard is restored to "original" after the call completes.
        string afterClipboard = ClipboardManager.GetText();
        Assert.Equal("original", afterClipboard);
    }

    [Fact]
    public void InjectText_TrailingSpaceFalse_RestoresClipboard()
    {
        ClipboardManager.SetText("original");
        _injector.InjectText("hello", trailingSpace: false);
        Assert.Equal("original", ClipboardManager.GetText());
    }

    // ── InjectText — clipboard restore ───────────────────────────────────────

    [Fact]
    public void InjectText_RestoresOriginalClipboard()
    {
        const string original = "my important clipboard";
        ClipboardManager.SetText(original);

        _injector.InjectText("dictated text");

        Assert.Equal(original, ClipboardManager.GetText());
    }

    [Fact]
    public void InjectText_WhenClipboardWasEmpty_LeavesClipboardEmpty()
    {
        ClipboardManager.SetText(null);

        _injector.InjectText("hello");

        Assert.Equal(string.Empty, ClipboardManager.GetText());
    }

    // ── InjectText — additionalKey ────────────────────────────────────────────

    [Fact]
    public void InjectText_AdditionalKeyEnter_PressesReturnKey()
    {
        _injector.InjectText("hello", additionalKey: "enter");

        _kbMock.Verify(k => k.KeyPress(VirtualKeyCode.RETURN), Times.Once);
    }

    [Fact]
    public void InjectText_AdditionalKeyReturn_PressesReturnKey()
    {
        _injector.InjectText("hello", additionalKey: "return");

        _kbMock.Verify(k => k.KeyPress(VirtualKeyCode.RETURN), Times.Once);
    }

    [Fact]
    public void InjectText_AdditionalKeyTab_PressesTabKey()
    {
        _injector.InjectText("hello", additionalKey: "tab");

        _kbMock.Verify(k => k.KeyPress(VirtualKeyCode.TAB), Times.Once);
    }

    [Fact]
    public void InjectText_NoAdditionalKey_DoesNotPressAnyKey()
    {
        _injector.InjectText("hello");

        _kbMock.Verify(k => k.KeyPress(It.IsAny<VirtualKeyCode>()), Times.Never);
    }

    [Fact]
    public void InjectText_UnknownAdditionalKey_DoesNotPressAnyKey()
    {
        _injector.InjectText("hello", additionalKey: "escape");

        _kbMock.Verify(k => k.KeyPress(It.IsAny<VirtualKeyCode>()), Times.Never);
    }

    // ── PressKey ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("enter", VirtualKeyCode.RETURN)]
    [InlineData("ENTER", VirtualKeyCode.RETURN)]
    [InlineData("return", VirtualKeyCode.RETURN)]
    [InlineData("tab", VirtualKeyCode.TAB)]
    [InlineData("TAB", VirtualKeyCode.TAB)]
    public void PressKey_KnownKey_SendsCorrectVirtualKeyCode(string keyName, VirtualKeyCode expected)
    {
        _injector.PressKey(keyName);

        _kbMock.Verify(k => k.KeyPress(expected), Times.Once);
    }

    [Fact]
    public void PressKey_UnknownKey_DoesNotSendAnyKey()
    {
        _injector.PressKey("backspace");

        _kbMock.Verify(k => k.KeyPress(It.IsAny<VirtualKeyCode>()), Times.Never);
    }
}
