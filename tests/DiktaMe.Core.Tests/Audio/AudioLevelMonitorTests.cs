
using DiktaMe.Core.Audio;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Audio;

public class AudioLevelMonitorTests
{
    [Fact]
    public void Start_SetsIsActive()
    {
        var monitor = new AudioLevelMonitor();

        monitor.Start();

        monitor.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Stop_ClearsLevelAndDeactivates()
    {
        var monitor = new AudioLevelMonitor();
        monitor.Start();

        // Feed loud audio to raise level
        byte[] loud = CreatePcmData(short.MaxValue, 100);
        monitor.UpdateLevel(loud.AsMemory(), loud.Length);
        monitor.SmoothedLevel.Should().BeGreaterThan(0);

        monitor.Stop();

        monitor.SmoothedLevel.Should().Be(0f);
        monitor.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateLevel_SilentAudio_LevelStaysNearZero()
    {
        var monitor = new AudioLevelMonitor();
        monitor.Start();

        byte[] silent = new byte[200]; // all zeros = silence
        monitor.UpdateLevel(silent.AsMemory(), 200);

        monitor.SmoothedLevel.Should().BeLessThan(0.01f);
    }

    [Fact]
    public void UpdateLevel_LoudAudio_LevelIncreasesSignificantly()
    {
        var monitor = new AudioLevelMonitor();
        monitor.Start();

        byte[] loud = CreatePcmData(20000, 100);
        monitor.UpdateLevel(loud.AsMemory(), loud.Length);

        monitor.SmoothedLevel.Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void Smoothing_DecaysGradually()
    {
        var monitor = new AudioLevelMonitor();
        monitor.Start();

        // Feed loud audio
        byte[] loud = CreatePcmData(30000, 100);
        monitor.UpdateLevel(loud.AsMemory(), loud.Length);
        float afterLoud = monitor.SmoothedLevel;

        // Feed silence — level should decay but not immediately zero
        byte[] silent = new byte[200];
        monitor.UpdateLevel(silent.AsMemory(), 200);

        monitor.SmoothedLevel.Should().BeLessThan(afterLoud);
        monitor.SmoothedLevel.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void UpdateLevel_TooFewBytes_DoesNothing()
    {
        var monitor = new AudioLevelMonitor();
        monitor.Start();

        // Only 1 byte — not enough for a 16-bit sample
        monitor.UpdateLevel(new byte[] { 0xFF }.AsMemory(), 1);

        monitor.SmoothedLevel.Should().Be(0f);
    }

    private static byte[] CreatePcmData(short amplitude, int sampleCount)
    {
        byte[] buffer = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            buffer[i * 2] = (byte)(amplitude & 0xFF);
            buffer[i * 2 + 1] = (byte)((amplitude >> 8) & 0xFF);
        }
        return buffer;
    }
}
