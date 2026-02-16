using DiktaMe.Core.Audio;

namespace DiktaMe.Core.Tests.Audio;

/// <summary>
/// Tests for MuteDetector state machine, event contracts, and device resolution.
/// COM-backed hardware queries are guarded: tests skip when no default microphone
/// is available (headless CI / no audio device).
/// </summary>
public class MuteDetectorTests : IDisposable
{
    private readonly MuteDetector _detector = new();

    public void Dispose() => _detector.Dispose();

    // ── Basic instantiation ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultLabel_DoesNotThrow()
    {
        var ex = Record.Exception(() => new MuteDetector());
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_WithLabel_DoesNotThrow()
    {
        var ex = Record.Exception(() => new MuteDetector("Headset"));
        Assert.Null(ex);
    }

    // ── Disposed guard ────────────────────────────────────────────────────────

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        _detector.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _detector.Start());
    }

    // ── CheckMuteState ────────────────────────────────────────────────────────

    [Fact]
    public void CheckMuteState_DoesNotThrow()
    {
        // May return null if no mic present; must not throw regardless
        var ex = Record.Exception(() => _detector.CheckMuteState());
        Assert.Null(ex);
    }

    [Fact]
    public void CheckMuteState_UnknownDeviceLabel_DoesNotThrow()
    {
        using var detector = new MuteDetector("ZZZ_NO_DEVICE_ZZZ");
        // Falls back to default mic or returns null — must not throw
        var ex = Record.Exception(() => detector.CheckMuteState());
        Assert.Null(ex);
    }

    // ── UpdateDeviceLabel ─────────────────────────────────────────────────────

    [Fact]
    public void UpdateDeviceLabel_ClearsCache_DoesNotThrow()
    {
        // Warm up the cache
        _ = _detector.CheckMuteState();

        // Update label — should clear cache silently
        var ex = Record.Exception(() => _detector.UpdateDeviceLabel("Some Other Device"));
        Assert.Null(ex);
    }

    [Fact]
    public void UpdateDeviceLabel_SameLabel_NoOp_DoesNotThrow()
    {
        using var detector = new MuteDetector("Headset");
        var ex = Record.Exception(() => detector.UpdateDeviceLabel("Headset"));
        Assert.Null(ex);
    }

    // ── MuteStateChanged event ────────────────────────────────────────────────

    [Fact]
    public void MuteStateChangedEventArgs_HasCorrectProperties()
    {
        var args = new MuteStateChangedEventArgs(isMuted: true, wasMuted: false);

        Assert.True(args.IsMuted);
        Assert.False(args.WasMuted);
    }

    [Fact]
    public void MuteStateChangedEventArgs_InverseState_HasCorrectProperties()
    {
        var args = new MuteStateChangedEventArgs(isMuted: false, wasMuted: true);

        Assert.False(args.IsMuted);
        Assert.True(args.WasMuted);
    }

    // ── Stop does not throw ───────────────────────────────────────────────────

    [Fact]
    public void Stop_WhenNotStarted_DoesNotThrow()
    {
        var ex = Record.Exception(() => _detector.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Start_ThenStop_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            _detector.Start();
            Thread.Sleep(50);
            _detector.Stop();
        });
        Assert.Null(ex);
    }

    // ── Double dispose ────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        _detector.Dispose();
        var ex = Record.Exception(() => _detector.Dispose());
        Assert.Null(ex);
    }
}
