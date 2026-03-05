using DiktaMe.Core.Audio;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Audio;

/// <summary>
/// Tests for <see cref="AudioDataAvailableEventArgs"/>.
/// </summary>
public sealed class AudioDataAvailableEventArgsTests
{
    [Fact]
    public void PcmData_SlicesBufferToBytesRecorded()
    {
        // NAudio allocates buffers larger than BytesRecorded
        byte[] buffer = [1, 2, 3, 4, 5, 0, 0, 0];
        int bytesRecorded = 5;

        var args = new AudioDataAvailableEventArgs(buffer, bytesRecorded);

        args.PcmData.Length.Should().Be(5);
        args.PcmData.Span[0].Should().Be(1);
        args.PcmData.Span[4].Should().Be(5);
        args.BytesRecorded.Should().Be(5);
    }

    [Fact]
    public void PcmData_ZeroBytesRecorded_ReturnsEmptySlice()
    {
        byte[] buffer = new byte[100];

        var args = new AudioDataAvailableEventArgs(buffer, 0);

        args.PcmData.Length.Should().Be(0);
        args.BytesRecorded.Should().Be(0);
    }

    [Fact]
    public void PcmData_FullBuffer_ReturnsEntireBuffer()
    {
        byte[] buffer = [10, 20, 30];

        var args = new AudioDataAvailableEventArgs(buffer, buffer.Length);

        args.PcmData.Length.Should().Be(3);
        args.BytesRecorded.Should().Be(3);
    }

    [Fact]
    public void PcmData_ReferencesOriginalBuffer_NoCopy()
    {
        byte[] buffer = [1, 2, 3, 4];
        var args = new AudioDataAvailableEventArgs(buffer, 4);

        // Mutating the original buffer should be visible through PcmData
        // (proving it's a reference, not a copy)
        buffer[0] = 99;

        args.PcmData.Span[0].Should().Be(99);
    }
}
