using DiktaMe.Core.STT;
using FluentAssertions;

namespace DiktaMe.Core.Tests.STT;

/// <summary>
/// Tests for streaming STT event args classes.
/// </summary>
public sealed class StreamingSTTEventArgsTests
{
    // ── StreamingTranscriptEventArgs ──────────────────────────────────────────

    [Fact]
    public void TranscriptArgs_StoresAllProperties()
    {
        var args = new StreamingTranscriptEventArgs(
            "hello world", isFinal: true, confidence: 0.95, duration: TimeSpan.FromSeconds(1.5));

        args.Transcript.Should().Be("hello world");
        args.IsFinal.Should().BeTrue();
        args.Confidence.Should().BeApproximately(0.95, 0.001);
        args.Duration.Should().Be(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void TranscriptArgs_PartialResult_IsFinalIsFalse()
    {
        var args = new StreamingTranscriptEventArgs(
            "partial", isFinal: false, confidence: 0.5, duration: TimeSpan.Zero);

        args.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void TranscriptArgs_EmptyTranscript_IsAllowed()
    {
        var args = new StreamingTranscriptEventArgs(
            "", isFinal: true, confidence: 0.0, duration: TimeSpan.Zero);

        args.Transcript.Should().BeEmpty();
    }

    // ── StreamingErrorEventArgs ──────────────────────────────────────────────

    [Fact]
    public void ErrorArgs_StoresMessage()
    {
        var args = new StreamingErrorEventArgs("Connection lost");

        args.Message.Should().Be("Connection lost");
        args.Exception.Should().BeNull();
    }

    [Fact]
    public void ErrorArgs_WithException_StoresBoth()
    {
        var ex = new InvalidOperationException("test");
        var args = new StreamingErrorEventArgs("Something failed", ex);

        args.Message.Should().Be("Something failed");
        args.Exception.Should().BeSameAs(ex);
    }
}
