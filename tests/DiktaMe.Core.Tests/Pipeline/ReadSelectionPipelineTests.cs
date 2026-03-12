using DiktaMe.Core.Config;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.TTS;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.Pipeline;

/// <summary>
/// Unit tests for <see cref="ReadSelectionPipeline"/> — SPEC_003 Phase C.
/// Mocks ITTSProvider and ITtsPlayerService to test the pipeline in isolation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReadSelectionPipelineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TtsResult OkTtsResult(string provider = "MockTTS") => new(
        AudioData: new byte[100],
        Duration: TimeSpan.FromSeconds(2),
        SampleRate: 24_000,
        Provider: provider,
        LatencyMs: 50,
        Format: "pcm");

    private static TtsResult EmptyTtsResult() => new(
        AudioData: [],
        Duration: TimeSpan.Zero,
        SampleRate: 24_000,
        Provider: "MockTTS",
        LatencyMs: 0,
        Format: "pcm");

    private static Mock<ITTSProvider> OkTts()
    {
        var m = new Mock<ITTSProvider>();
        m.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTtsResult());
        return m;
    }

    private static Mock<ITTSProvider> EmptyTts()
    {
        var m = new Mock<ITTSProvider>();
        m.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyTtsResult());
        return m;
    }

    private static Mock<ITTSProvider> ThrowingTts(string message = "TTS model not loaded")
    {
        var m = new Mock<ITTSProvider>();
        m.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(message));
        return m;
    }

    private static Mock<ITTSProvider> CancellingTts()
    {
        var m = new Mock<ITTSProvider>();
        m.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        return m;
    }

    private static Mock<ITtsPlayerService> OkPlayer()
    {
        var m = new Mock<ITtsPlayerService>();
        m.Setup(p => p.PlayAsync(
                It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        m.SetupProperty(p => p.Volume, 0.8f);
        return m;
    }

    private static SettingsManager MockSettings(
        bool ttsEnabled = true,
        int maxWords = 500,
        int volumePercent = 80,
        string voiceId = "",
        PrivacyLevel privacy = PrivacyLevel.Full)
    {
        string tempPath = Path.Combine(
            Path.GetTempPath(), $"diktame_rsp_test_{Guid.NewGuid()}.json");
        var settings = new SettingsManager(tempPath);
        settings.UpdateAsync(settings.Current with
        {
            Tts = new TtsSettings
            {
                Enabled = ttsEnabled,
                Provider = "kokoro",
                MaxSpeechWords = maxWords,
                VolumePercent = volumePercent,
                VoiceId = voiceId,
            },
            Privacy = settings.Current.Privacy with
            {
                Level = privacy,
            },
        }).Wait();
        return settings;
    }

    private static ReadSelectionPipeline CreatePipeline(
        Mock<ITTSProvider>? tts = null,
        Mock<ITtsPlayerService>? player = null,
        SettingsManager? settings = null)
    {
        return new ReadSelectionPipeline(
            (tts ?? OkTts()).Object,
            (player ?? OkPlayer()).Object,
            settings ?? MockSettings());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Happy path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_HappyPath_SynthesizesAndPlays()
    {
        var tts = OkTts();
        var player = OkPlayer();
        var pipeline = CreatePipeline(tts, player);

        var result = await pipeline.RunAsync("Hello world");

        result.IsSuccess.Should().BeTrue();
        result.Mode.Should().Be("read_selection");
        result.Text.Should().NotBeEmpty();
        tts.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        player.Verify(
            p => p.PlayAsync(It.IsAny<byte[]>(), 24_000, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_HappyPath_SetsVolumeFromSettings()
    {
        var player = OkPlayer();
        var pipeline = CreatePipeline(player: player, settings: MockSettings(volumePercent: 60));

        await pipeline.RunAsync("test text");

        player.VerifySet(p => p.Volume = 0.6f, Times.Once());
    }

    [Fact]
    public async Task RunAsync_HappyPath_RawTranscriptContainsOriginalText()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.RunAsync("**Bold** text with markdown");

        result.RawTranscript.Should().Be("**Bold** text with markdown");
        // TextCleaner strips markdown
        result.Text.Should().NotContain("**");
    }

    [Fact]
    public async Task RunAsync_HappyPath_PassesVoiceIdFromSettings()
    {
        var tts = OkTts();
        var pipeline = CreatePipeline(tts: tts, settings: MockSettings(voiceId: "af_nicole"));

        await pipeline.RunAsync("test");

        tts.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), "af_nicole", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_EmptyVoiceId_PassesNull()
    {
        var tts = OkTts();
        var pipeline = CreatePipeline(tts: tts, settings: MockSettings(voiceId: ""));

        await pipeline.RunAsync("test");

        tts.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // No selection / empty input
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_NullText_ReturnsFailure()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.RunAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No text selected");
    }

    [Fact]
    public async Task RunAsync_EmptyText_ReturnsFailure()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.RunAsync("   ");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No text selected");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Synthesis failure
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_SynthesisReturnsEmpty_ReturnsFailure()
    {
        var pipeline = CreatePipeline(tts: EmptyTts());

        var result = await pipeline.RunAsync("test text");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no audio");
    }

    [Fact]
    public async Task RunAsync_SynthesisThrows_ReturnsFailure()
    {
        var pipeline = CreatePipeline(tts: ThrowingTts("model missing"));

        var result = await pipeline.RunAsync("test text");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("model missing");
    }

    [Fact]
    public async Task RunAsync_SynthesisThrows_StopsPlayer()
    {
        var player = OkPlayer();
        var pipeline = CreatePipeline(tts: ThrowingTts(), player: player);

        await pipeline.RunAsync("test text");

        player.Verify(p => p.Stop(), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Cancellation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_Cancelled_ReturnsFailure()
    {
        var player = OkPlayer();
        var pipeline = CreatePipeline(tts: CancellingTts(), player: player);

        var result = await pipeline.RunAsync("test text");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Cancelled");
        player.Verify(p => p.Stop(), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // State transitions
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_HappyPath_EmitsCorrectStateSequence()
    {
        var states = new List<PipelineState>();
        var pipeline = CreatePipeline();
        pipeline.StateChanged += (_, s) => states.Add(s);

        await pipeline.RunAsync("test text");

        states.Should().ContainInOrder(
            PipelineState.Processing,
            PipelineState.Speaking,
            PipelineState.Idle);
    }

    [Fact]
    public async Task RunAsync_EmptyText_EmitsIdle()
    {
        var states = new List<PipelineState>();
        var pipeline = CreatePipeline();
        pipeline.StateChanged += (_, s) => states.Add(s);

        await pipeline.RunAsync("");

        states.Should().Contain(PipelineState.Idle);
    }

    [Fact]
    public async Task RunAsync_SynthesisThrows_EmitsError()
    {
        var states = new List<PipelineState>();
        var pipeline = CreatePipeline(tts: ThrowingTts());
        pipeline.StateChanged += (_, s) => states.Add(s);

        await pipeline.RunAsync("test");

        states.Should().Contain(PipelineState.Error);
    }

    [Fact]
    public async Task RunAsync_Cancelled_EmitsIdle()
    {
        var states = new List<PipelineState>();
        var pipeline = CreatePipeline(tts: CancellingTts());
        pipeline.StateChanged += (_, s) => states.Add(s);

        await pipeline.RunAsync("test");

        states.Should().Contain(PipelineState.Idle);
        states.Should().NotContain(PipelineState.Error);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Completed event
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_HappyPath_FiresCompletedEvent()
    {
        PipelineResult? fired = null;
        var pipeline = CreatePipeline();
        pipeline.Completed += (_, r) => fired = r;

        await pipeline.RunAsync("test text");

        fired.Should().NotBeNull();
        fired!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_Failure_FiresCompletedEvent()
    {
        PipelineResult? fired = null;
        var pipeline = CreatePipeline(tts: ThrowingTts());
        pipeline.Completed += (_, r) => fired = r;

        await pipeline.RunAsync("test");

        fired.Should().NotBeNull();
        fired!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_EmptyInput_FiresCompletedEvent()
    {
        PipelineResult? fired = null;
        var pipeline = CreatePipeline();
        pipeline.Completed += (_, r) => fired = r;

        await pipeline.RunAsync("");

        fired.Should().NotBeNull();
        fired!.IsSuccess.Should().BeFalse();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TextCleaner integration
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_CleansMarkdownBeforeSynthesis()
    {
        var tts = OkTts();
        var pipeline = CreatePipeline(tts: tts);

        await pipeline.RunAsync("## Header\n**bold** text");

        tts.Verify(
            p => p.SynthesizeAsync(
                It.Is<string>(t => !t.Contains("##") && !t.Contains("**")),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Privacy
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_GhostPrivacy_DoesNotThrow()
    {
        var pipeline = CreatePipeline(settings: MockSettings(privacy: PrivacyLevel.Ghost));

        var result = await pipeline.RunAsync("secret text");

        result.IsSuccess.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Timing
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_HappyPath_ReportsTimings()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.RunAsync("test");

        result.ProcessingMs.Should().BeGreaterThanOrEqualTo(0);
        result.TotalMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
