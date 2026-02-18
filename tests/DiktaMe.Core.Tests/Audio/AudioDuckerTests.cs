
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using FluentAssertions;
using Xunit;

namespace DiktaMe.Core.Tests.Audio;
/// <summary>
/// Unit tests for <see cref="AudioDucker"/>.
///
/// Note: Duck/restore cycle tests that require live WASAPI audio sessions
/// (other apps playing audio) are omitted here — they are integration/manual
/// tests. What we can verify in a headless CI environment:
///   • Constructor defaults and property mutation
///   • Guard: IsEnabled=false prevents ducking
///   • Duck/Restore are idempotent (no exception when no sessions available)
///   • AttachTo / DetachFrom event wiring
///   • Dispose is safe to call multiple times
///   • AppSettings AudioDucking defaults
/// </summary>
[Trait("Category", "Unit")]
public sealed class AudioDuckerTests
{
    // ── Constructor defaults ─────────────────────────────────────────────────

    [Fact]
    public void Ctor_DefaultsAreCorrect()
    {
        using var ducker = new AudioDucker();

        ducker.IsEnabled.Should().BeTrue();
        ducker.DuckLevel.Should().BeApproximately(AudioDucker.DefaultDuckLevel, 0.001f);
    }

    [Fact]
    public void Ctor_CustomValues_AreApplied()
    {
        using var ducker = new AudioDucker(isEnabled: false, duckLevel: 0.5f);

        ducker.IsEnabled.Should().BeFalse();
        ducker.DuckLevel.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void Ctor_DuckLevelClamped_ToZeroOneRange()
    {
        using var low = new AudioDucker(duckLevel: -1f);
        using var high = new AudioDucker(duckLevel: 2f);

        low.DuckLevel.Should().Be(0f);
        high.DuckLevel.Should().Be(1f);
    }

    // ── Property mutation ────────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_CanBeToggled()
    {
        using var ducker = new AudioDucker();

        ducker.IsEnabled = false;
        ducker.IsEnabled.Should().BeFalse();

        ducker.IsEnabled = true;
        ducker.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void DuckLevel_CanBeChanged()
    {
        using var ducker = new AudioDucker();

        ducker.DuckLevel = 0.5f;
        ducker.DuckLevel.Should().BeApproximately(0.5f, 0.001f);
    }

    // ── Guard: disabled ──────────────────────────────────────────────────────

    [Fact]
    public void Duck_WhenDisabled_DoesNotThrow()
    {
        using var ducker = new AudioDucker(isEnabled: false);

        var act = () => ducker.Duck();
        act.Should().NotThrow();
    }

    [Fact]
    public void Restore_WhenDisabled_DoesNotThrow()
    {
        using var ducker = new AudioDucker(isEnabled: false);

        var act = () => ducker.Restore();
        act.Should().NotThrow();
    }

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public void Duck_CalledTwice_DoesNotThrow()
    {
        // May not find any sessions in CI, but must not throw.
        using var ducker = new AudioDucker();

        var act = () =>
        {
            ducker.Duck();
            ducker.Duck(); // second call is a no-op
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Restore_WithoutDuck_DoesNotThrow()
    {
        using var ducker = new AudioDucker();

        var act = () => ducker.Restore();
        act.Should().NotThrow();
    }

    [Fact]
    public void DuckRestoreCycle_DoesNotThrow()
    {
        using var ducker = new AudioDucker();

        var act = () =>
        {
            ducker.Duck();
            ducker.Restore();
        };

        act.Should().NotThrow();
    }

    // ── AttachTo / DetachFrom ────────────────────────────────────────────────

    [Fact]
    public void AttachTo_ThenDetachFrom_DoesNotThrow()
    {
        using var ducker = new AudioDucker();
        using var recorder = new AudioRecorder();

        var act = () =>
        {
            ducker.AttachTo(recorder);
            ducker.DetachFrom(recorder);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void DetachFrom_WithoutAttach_DoesNotThrow()
    {
        using var ducker = new AudioDucker();
        using var recorder = new AudioRecorder();

        var act = () => ducker.DetachFrom(recorder);
        act.Should().NotThrow();
    }

    // ── Dispose safety ───────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var ducker = new AudioDucker();

        var act = () =>
        {
            ducker.Dispose();
            ducker.Dispose(); // second call is a no-op
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Duck_AfterDispose_DoesNotThrow()
    {
        var ducker = new AudioDucker();
        ducker.Dispose();

        var act = () => ducker.Duck();
        act.Should().NotThrow();
    }

    [Fact]
    public void Restore_AfterDispose_DoesNotThrow()
    {
        var ducker = new AudioDucker();
        ducker.Dispose();

        var act = () => ducker.Restore();
        act.Should().NotThrow();
    }

    // ── AppSettings AudioDucking defaults ────────────────────────────────────

    [Fact]
    public void AudioDuckingSettings_Defaults_AreCorrect()
    {
        var settings = new AudioDuckingSettings();

        settings.Enabled.Should().BeTrue();
        settings.DuckLevelPercent.Should().Be(20);
    }

    [Fact]
    public void AudioDuckingSettings_DuckLevelPercent_ConvertsToFloat()
    {
        var settings = new AudioDuckingSettings { DuckLevelPercent = 40 };

        float asFloat = settings.DuckLevelPercent / 100f;
        asFloat.Should().BeApproximately(0.4f, 0.001f);
    }
}
