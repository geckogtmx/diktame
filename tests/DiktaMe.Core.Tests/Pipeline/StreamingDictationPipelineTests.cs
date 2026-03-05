using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using FluentAssertions;
using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using Moq;

namespace DiktaMe.Core.Tests.Pipeline;

/// <summary>
/// Tests for <see cref="StreamingDictationPipeline"/>.
/// Uses Moq for <see cref="IStreamingSTTProvider"/> and <see cref="FakeAudioDataSource"/>
/// for simulating audio events.
/// </summary>
public sealed class StreamingDictationPipelineTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static TextInjector NullInjector()
    {
        var kbMock = new Mock<IKeyboardSimulator>();
        kbMock.Setup(k => k.ModifiedKeyStroke(It.IsAny<VirtualKeyCode>(), It.IsAny<VirtualKeyCode>()))
              .Returns(kbMock.Object);
        kbMock.Setup(k => k.KeyPress(It.IsAny<VirtualKeyCode>()))
              .Returns(kbMock.Object);

        var simMock = new Mock<IInputSimulator>();
        simMock.Setup(s => s.Keyboard).Returns(kbMock.Object);

        return new TextInjector(simMock.Object);
    }

    private static DictationOptions DefaultOptions(string language = "en") => new()
    {
        Language = language,
        RawMode = true,
        Injection = new PipelineInjectionOptions { TrailingSpace = true },
    };

    /// <summary>
    /// Creates a mock <see cref="IStreamingSTTProvider"/> that connects immediately,
    /// reports as connected, and closes cleanly.
    /// </summary>
    private static Mock<IStreamingSTTProvider> OkStreamingProvider()
    {
        var m = new Mock<IStreamingSTTProvider>();
        m.SetupGet(p => p.IsConnected).Returns(true);
        m.SetupGet(p => p.ProviderName).Returns("MockStreaming");

        m.Setup(p => p.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);

        m.Setup(p => p.SendAudioAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);

        m.Setup(p => p.CloseAsync(It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);

        m.Setup(p => p.DisposeAsync())
         .Returns(ValueTask.CompletedTask);

        return m;
    }

    /// <summary>
    /// Runs the pipeline in a background task, simulates events, then awaits the result.
    /// </summary>
    private static async Task<PipelineResult> RunWithEvents(
        StreamingDictationPipeline pipeline,
        FakeAudioDataSource audio,
        DictationOptions options,
        Action<FakeAudioDataSource> simulate,
        CancellationToken ct = default)
    {
        var task = Task.Run(() => pipeline.RunAsync(audio, options, ct), CancellationToken.None);
        await Task.Delay(50); // let pipeline connect and subscribe
        simulate(audio);
        return await task;
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SingleFinal_InjectsText()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            stt.Raise(p => p.FinalTranscriptReceived += null,
                stt.Object,
                new StreamingTranscriptEventArgs("hello world", isFinal: true, confidence: 0.99, duration: TimeSpan.FromSeconds(1.5)));
            a.SimulateStop(2000);
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hello world");
    }

    [Fact]
    public async Task MultipleFinals_InjectsEachAndAccumulates()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("hello", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(0.5)));
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("world", isFinal: true, confidence: 0.95, duration: TimeSpan.FromSeconds(0.5)));
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("test", isFinal: true, confidence: 0.88, duration: TimeSpan.FromSeconds(0.5)));
            a.SimulateStop(3000);
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hello world test");
    }

    [Fact]
    public async Task EmptyTranscript_SuccessWithEmptyText()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            // No finals — user stayed silent
            a.SimulateStop(1000);
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task SnippetExpansion_ExpandsOnEachFinal()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();

        // Create snippet manager with a temp file
        string tmpFile = Path.Combine(Path.GetTempPath(), $"snippets_{Guid.NewGuid():N}.json");
        try
        {
            var snippets = new SnippetManager(tmpFile);
            snippets.Add("my email", "test@example.com");

            await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector(), snippets);

            var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
            {
                stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                    new StreamingTranscriptEventArgs("send to my email", isFinal: true, confidence: 0.99, duration: TimeSpan.FromSeconds(1)));
                a.SimulateStop(2000);
            });

            result.IsSuccess.Should().BeTrue();
            result.Text.Should().Contain("test@example.com");
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [Fact]
    public async Task PipelineResult_HasCorrectMetrics()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("test", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(1)));
            a.SimulateStop(5000);
        });

        result.Mode.Should().Be("dictate");
        result.SttProvider.Should().Be("MockStreaming");
        result.LlmProvider.Should().BeNull();
        result.RecordingMs.Should().Be(5000);
        result.TranscriptionMs.Should().Be(0);
        result.ProcessingMs.Should().Be(0);
        result.TotalMs.Should().BeGreaterThan(0);
    }

    // ── State changes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FiresStreamingState()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var states = new List<PipelineState>();
        pipeline.StateChanged += (_, s) => states.Add(s);

        await RunWithEvents(pipeline, audio, DefaultOptions(), a => a.SimulateStop());

        states.Should().Contain(PipelineState.Streaming);
    }

    [Fact]
    public async Task EndsWithIdleState()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var states = new List<PipelineState>();
        pipeline.StateChanged += (_, s) => states.Add(s);

        await RunWithEvents(pipeline, audio, DefaultOptions(), a => a.SimulateStop());

        states.Last().Should().Be(PipelineState.Idle);
    }

    [Fact]
    public async Task ErrorSetsErrorState()
    {
        var stt = OkStreamingProvider();
        stt.Setup(p => p.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("connection failed"));

        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var states = new List<PipelineState>();
        pipeline.StateChanged += (_, s) => states.Add(s);

        var result = await pipeline.RunAsync(audio, DefaultOptions());

        states.Should().Contain(PipelineState.Error);
        result.IsSuccess.Should().BeFalse();
    }

    // ── Events & lifecycle ────────────────────────────────────────────────────

    [Fact]
    public async Task FiresCompletedEvent()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        PipelineResult? completed = null;
        pipeline.Completed += (_, r) => completed = r;

        await RunWithEvents(pipeline, audio, DefaultOptions(), a => a.SimulateStop());

        completed.Should().NotBeNull();
        completed!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AudioChunks_ForwardedToProvider()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            a.SimulateAudioChunk([1, 2, 3, 4]);
            a.SimulateAudioChunk([5, 6, 7, 8]);
            a.SimulateAudioChunk([9, 10, 11, 12]);
            a.SimulateStop();
        });

        stt.Verify(p => p.SendAudioAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(3));
    }

    [Fact]
    public async Task RecordingStop_TriggersCloseAsync()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        await RunWithEvents(pipeline, audio, DefaultOptions(), a => a.SimulateStop());

        stt.Verify(p => p.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectsWithCorrectLanguage()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        await RunWithEvents(pipeline, audio, DefaultOptions(language: "es"), a => a.SimulateStop());

        stt.Verify(p => p.ConnectAsync("es", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectFails_ReturnsFailure()
    {
        var stt = OkStreamingProvider();
        stt.Setup(p => p.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("connect error"));

        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await pipeline.RunAsync(audio, DefaultOptions());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("connect error");
    }

    [Fact]
    public async Task SendAudioFails_LogsAndContinues()
    {
        int callCount = 0;
        var stt = OkStreamingProvider();
        stt.Setup(p => p.SendAudioAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
           .Returns(() =>
           {
               callCount++;
               if (callCount == 2)
               {
                   return Task.FromException(new InvalidOperationException("send failed"));
               }

               return Task.CompletedTask;
           });

        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            a.SimulateAudioChunk([1, 2]);
            a.SimulateAudioChunk([3, 4]); // This one fails
            a.SimulateAudioChunk([5, 6]);
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("hello", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(1)));
            a.SimulateStop();
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hello");
    }

    [Fact]
    public async Task Cancelled_ReturnsCancelledResult()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        using var cts = new CancellationTokenSource();

        var task = Task.Run(() => pipeline.RunAsync(audio, DefaultOptions(), cts.Token), CancellationToken.None);
        await Task.Delay(50);
        cts.Cancel();

        var result = await task;

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task ProviderError_LogsButCompletes()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            // Provider raises error event
            stt.Raise(p => p.ErrorOccurred += null, stt.Object,
                new StreamingErrorEventArgs("rate limit exceeded"));

            // Then a final arrives — pipeline should still work
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("hello", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(1)));
            a.SimulateStop();
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hello");
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyFinals_Skipped()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        var injector = NullInjector();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, injector);

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            // Empty finals should be skipped
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("", isFinal: true, confidence: 0.0, duration: TimeSpan.Zero));
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("hello", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(1)));
            a.SimulateStop();
        });

        result.Text.Should().Be("hello");
    }

    [Fact]
    public async Task PartialTranscripts_NotInjected()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            // Only subscribe to FinalTranscriptReceived, partials are ignored
            stt.Raise(p => p.PartialTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("hel", isFinal: false, confidence: 0.5, duration: TimeSpan.FromSeconds(0.3)));
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("hello", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(1)));
            a.SimulateStop();
        });

        // Only the final should be in the result
        result.Text.Should().Be("hello");
    }

    [Fact]
    public async Task AutoStop_WorksSameAsManualStop()
    {
        var stt = OkStreamingProvider();
        var audio = new FakeAudioDataSource();
        await using var pipeline = new StreamingDictationPipeline(stt.Object, NullInjector());

        var result = await RunWithEvents(pipeline, audio, DefaultOptions(), a =>
        {
            stt.Raise(p => p.FinalTranscriptReceived += null, stt.Object,
                new StreamingTranscriptEventArgs("auto stop test", isFinal: true, confidence: 0.9, duration: TimeSpan.FromSeconds(1)));
            a.SimulateAutoStop(60000);
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("auto stop test");
        result.RecordingMs.Should().Be(60000);
        stt.Verify(p => p.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    [Fact]
    public void SupportsStreaming_TrueForDeepgram()
    {
        var factory = new STTProviderFactory(
            new DiktaMe.Core.Security.SecureStorage(),
            new SettingsManager());

        factory.SupportsStreaming("deepgram").Should().BeTrue();
        factory.SupportsStreaming("Deepgram").Should().BeTrue();
    }

    [Fact]
    public void SupportsStreaming_FalseForWhisper()
    {
        var factory = new STTProviderFactory(
            new DiktaMe.Core.Security.SecureStorage(),
            new SettingsManager());

        factory.SupportsStreaming("whisper").Should().BeFalse();
        factory.SupportsStreaming("gemini-audio").Should().BeFalse();
    }
}
