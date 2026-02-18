using DiktaMe.Core.Input;

namespace DiktaMe.Core.Tests.Input;

/// <summary>
/// Tests for HotkeyManager lifecycle, registration, and conflict detection.
/// Actual WM_HOTKEY delivery is not tested here (requires a real message loop
/// receiving input) — hotkey press events are exercised via integration tests.
/// </summary>
public class HotkeyManagerTests : IDisposable
{
    private readonly HotkeyManager _manager = new();

    public void Dispose() => _manager.Dispose();

    // ── Start guard ───────────────────────────────────────────────────────────

    [Fact]
    public void Register_BeforeStart_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _manager.Register(HotkeyId.Dictate, "Ctrl+Alt+D"));
    }

    // ── Disposed guard ────────────────────────────────────────────────────────

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        _manager.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _manager.Start());
    }

    [Fact]
    public void Register_AfterDispose_ThrowsObjectDisposedException()
    {
        _manager.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _manager.Register(HotkeyId.Dictate, "Ctrl+Alt+D"));
    }

    // ── Double start ─────────────────────────────────────────────────────────

    [Fact]
    public void Start_CalledTwice_DoesNotThrow()
    {
        _manager.Start();
        Thread.Sleep(100); // let pump thread initialise
        var ex = Record.Exception(() => _manager.Start());
        Assert.Null(ex);
    }

    // ── Registration / unregistration lifecycle ───────────────────────────────

    [Fact]
    public void Register_ValidHotkey_ReturnsTrue_OrFalseWithEvent()
    {
        _manager.Start();
        Thread.Sleep(100);

        bool? registrationResult = null;
        HotkeyRegistrationFailedEventArgs? failArgs = null;
        _manager.RegistrationFailed += (_, e) => failArgs = e;

        registrationResult = _manager.Register(HotkeyId.Dictate, "Ctrl+Alt+D");

        if (!registrationResult.Value)
        {
            // Another app holds this hotkey — verify the event was raised
            Assert.NotNull(failArgs);
            Assert.Equal(HotkeyId.Dictate, failArgs!.Id);
            Assert.Equal("Ctrl+Alt+D", failArgs.HotkeyString);
        }
        // else: registered successfully — no assertions needed beyond true return
    }

    [Fact]
    public void Register_InvalidHotkeyString_ReturnsFalse_WithEvent()
    {
        _manager.Start();
        Thread.Sleep(100);

        HotkeyRegistrationFailedEventArgs? failArgs = null;
        _manager.RegistrationFailed += (_, e) => failArgs = e;

        bool result = _manager.Register(HotkeyId.Dictate, "NotAHotkey");

        Assert.False(result);
        Assert.NotNull(failArgs);
        Assert.Equal(HotkeyId.Dictate, failArgs!.Id);
    }

    [Fact]
    public void Unregister_WhenNotRegistered_DoesNotThrow()
    {
        _manager.Start();
        Thread.Sleep(100);

        var ex = Record.Exception(() => _manager.Unregister(HotkeyId.Dictate));
        Assert.Null(ex);
    }

    [Fact]
    public void UnregisterAll_DoesNotThrow()
    {
        _manager.Start();
        Thread.Sleep(100);

        _ = _manager.Register(HotkeyId.Dictate, "Ctrl+Alt+D");
        var ex = Record.Exception(() => _manager.UnregisterAll());
        Assert.Null(ex);
    }

    // ── Reregister ────────────────────────────────────────────────────────────

    [Fact]
    public void Reregister_DoesNotThrow()
    {
        _manager.Start();
        Thread.Sleep(100);

        _ = _manager.Register(HotkeyId.Dictate, "Ctrl+Alt+D");
        var ex = Record.Exception(() => _manager.Reregister(HotkeyId.Dictate, "Ctrl+Alt+A"));
        Assert.Null(ex);
    }

    // ── HotkeyId enum values ──────────────────────────────────────────────────

    [Fact]
    public void HotkeyId_AllSixSlotsAreDefined()
    {
        var ids = Enum.GetValues<HotkeyId>();
        Assert.Equal(6, ids.Length);
        Assert.Contains(HotkeyId.Dictate, ids);
        Assert.Contains(HotkeyId.Ask, ids);
        Assert.Contains(HotkeyId.Translate, ids);
        Assert.Contains(HotkeyId.Refine, ids);
        Assert.Contains(HotkeyId.Oops, ids);
        Assert.Contains(HotkeyId.Note, ids);
    }

    // ── RegistrationFailed event args ─────────────────────────────────────────

    [Fact]
    public void RegistrationFailedArgs_HaveExpectedProperties()
    {
        var args = new HotkeyRegistrationFailedEventArgs(HotkeyId.Ask, "Ctrl+Alt+A", "reason");

        Assert.Equal(HotkeyId.Ask, args.Id);
        Assert.Equal("Ctrl+Alt+A", args.HotkeyString);
        Assert.Equal("reason", args.Reason);
    }

    // ── HotkeyPressedEventArgs ────────────────────────────────────────────────

    [Fact]
    public void HotkeyPressedArgs_HasExpectedId()
    {
        var args = new HotkeyPressedEventArgs(HotkeyId.Translate);
        Assert.Equal(HotkeyId.Translate, args.Id);
    }
}
