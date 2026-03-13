using DiktaMe.Core.TTS;
using FluentAssertions;
using NAudio.CoreAudioApi;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class TtsPlayerServiceTests : IDisposable
{
    private readonly TtsPlayerService _player = new();

    // ── Constructor defaults ────────────────────────────────────────────────

    [Fact]
    public void Ctor_DefaultState_IsIdle()
    {
        _player.IsPlaying.Should().BeFalse();
        _player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Ctor_DefaultVolume_Is80Percent()
    {
        _player.Volume.Should().BeApproximately(0.8f, 0.001f);
    }

    // ── Volume clamping ─────────────────────────────────────────────────────

    [Fact]
    public void Volume_SetValidValue_IsApplied()
    {
        _player.Volume = 0.5f;
        _player.Volume.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void Volume_SetBelowZero_ClampedToZero()
    {
        _player.Volume = -1f;
        _player.Volume.Should().Be(0f);
    }

    [Fact]
    public void Volume_SetAboveOne_ClampedToOne()
    {
        _player.Volume = 2f;
        _player.Volume.Should().Be(1f);
    }

    // ── Idle state safety ───────────────────────────────────────────────────

    [Fact]
    public void Stop_WhenIdle_DoesNotThrow()
    {
        var act = () => _player.Stop();
        act.Should().NotThrow();
    }

    [Fact]
    public void Pause_WhenIdle_DoesNotThrow()
    {
        var act = () => _player.Pause();
        act.Should().NotThrow();
    }

    [Fact]
    public void Resume_WhenIdle_DoesNotThrow()
    {
        var act = () => _player.Resume();
        act.Should().NotThrow();
    }

    // ── Position / Duration when idle ───────────────────────────────────────

    [Fact]
    public void Position_WhenIdle_IsZero()
    {
        _player.Position.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Duration_WhenIdle_IsZero()
    {
        _player.Duration.Should().Be(TimeSpan.Zero);
    }

    // ── Dispose safety ──────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var act = () =>
        {
            _player.Dispose();
            _player.Dispose();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public async Task PlayAsync_AfterDispose_ReturnsImmediately()
    {
        _player.Dispose();
        var pcm = GenerateTestPcm(sampleRate: 22050, durationSeconds: 0.05);

        // Should not throw, just return
        await _player.PlayAsync(pcm, 22050);

        _player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_EmptyData_ReturnsImmediately()
    {
        await _player.PlayAsync([], 22050);
        _player.IsPlaying.Should().BeFalse();
    }

    // ── Hardware-dependent playback tests ────────────────────────────────────

    [Fact]
    public async Task PlayAsync_ShortAudio_RaisesStartedAndFinished()
    {
        if (!HasAudioOutputDevice())
        {
            return;
        }

        var started = false;
        var finished = false;
        _player.PlaybackStarted += (_, _) => started = true;
        _player.PlaybackFinished += (_, _) => finished = true;

        var pcm = GenerateTestPcm(sampleRate: 22050, durationSeconds: 0.05);
        await _player.PlayAsync(pcm, 22050);

        started.Should().BeTrue("PlaybackStarted should fire on play");
        finished.Should().BeTrue("PlaybackFinished should fire on natural completion");
    }

    [Fact]
    public async Task Stop_DuringPlayback_RaisesStoppedNotFinished()
    {
        if (!HasAudioOutputDevice())
        {
            return;
        }

        var stopped = false;
        var finished = false;
        _player.PlaybackStopped += (_, _) => stopped = true;
        _player.PlaybackFinished += (_, _) => finished = true;

        var pcm = GenerateTestPcm(sampleRate: 22050, durationSeconds: 2.0);

        // Start playback in background, then stop it
        var playTask = _player.PlayAsync(pcm, 22050);

        // Brief delay to ensure playback has started
        await Task.Delay(100);
        _player.Stop();

        await playTask;

        stopped.Should().BeTrue("PlaybackStopped should fire on Stop()");
        finished.Should().BeFalse("PlaybackFinished should NOT fire on user Stop()");
    }

    [Fact]
    public async Task PlayAsync_Cancellation_StopsPlayback()
    {
        if (!HasAudioOutputDevice())
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        var pcm = GenerateTestPcm(sampleRate: 22050, durationSeconds: 2.0);

        var playTask = _player.PlayAsync(pcm, 22050, cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await playTask;

        _player.IsPlaying.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a short 440 Hz sine wave as 16-bit mono PCM data for testing.
    /// </summary>
    private static byte[] GenerateTestPcm(int sampleRate = 22050, double durationSeconds = 0.1)
    {
        int sampleCount = (int)(sampleRate * durationSeconds);
        var buffer = new byte[sampleCount * 2]; // 16-bit = 2 bytes per sample
        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleRate;
            short sample = (short)(Math.Sin(2 * Math.PI * 440 * t) * short.MaxValue / 4);
            BitConverter.TryWriteBytes(buffer.AsSpan(i * 2), sample);
        }
        return buffer;
    }

    private static bool HasAudioOutputDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device is not null;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _player.Dispose();
}
