using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using DiktaMe.Core.Vision;
using FluentAssertions;
using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using Moq;

namespace DiktaMe.Core.Tests.Vision;

public sealed class VisionPipelineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly byte[] FakeScreenshot = [0x89, 0x50, 0x4E, 0x47];
    private const string PngMime = "image/png";

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

    private static VisionOptions DefaultOptions(VisionOutputMode outputMode = VisionOutputMode.Inject) => new()
    {
        SystemPrompt = "You are a vision assistant.",
        OutputMode = outputMode,
    };

    private static Mock<ILLMProvider> OkVisionLlm(string text = "I see a cat")
    {
        var m = new Mock<ILLMProvider>();
        m.SetupGet(l => l.ProviderName).Returns("MockVisionLLM");
        m.Setup(l => l.ProcessWithImageAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult { Text = text, Provider = "MockVisionLLM" });
        return m;
    }

    private static Mock<ILLMProvider> EmptyVisionLlm()
    {
        var m = new Mock<ILLMProvider>();
        m.SetupGet(l => l.ProviderName).Returns("MockVisionLLM");
        m.Setup(l => l.ProcessWithImageAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResult { Text = string.Empty, Provider = "MockVisionLLM" });
        return m;
    }

    private static Mock<ILLMProvider> NotSupportedLlm()
    {
        var m = new Mock<ILLMProvider>();
        m.SetupGet(l => l.ProviderName).Returns("MockTextOnlyLLM");
        m.Setup(l => l.ProcessWithImageAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("Vision not supported"));
        return m;
    }

    private static Mock<ISTTProvider> OkStt(string text = "what is this")
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult { Text = text, Provider = "MockSTT" });
        return m;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithScreenshotOnly_UsesDefaultQuery()
    {
        var llm = OkVisionLlm();
        var sut = new VisionPipeline(llm.Object, NullInjector());

        var result = await sut.RunAsync(FakeScreenshot, PngMime, audioFilePath: null, DefaultOptions());

        result.IsSuccess.Should().BeTrue();
        llm.Verify(l => l.ProcessWithImageAsync(
            FakeScreenshot, PngMime,
            "Describe what you see and extract any visible text.",
            It.IsAny<string>(), "vision", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithVoiceQuery_UsesTranscribedText()
    {
        var llm = OkVisionLlm();
        var stt = OkStt("what is this");
        var sut = new VisionPipeline(llm.Object, NullInjector(), stt.Object);

        var result = await sut.RunAsync(FakeScreenshot, PngMime, "audio.wav", DefaultOptions());

        result.IsSuccess.Should().BeTrue();
        llm.Verify(l => l.ProcessWithImageAsync(
            FakeScreenshot, PngMime,
            "what is this",
            It.IsAny<string>(), "vision", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LlmSuccess_InjectsText()
    {
        var llm = OkVisionLlm("The screen shows a dashboard");
        var sut = new VisionPipeline(llm.Object, NullInjector());

        var result = await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions());

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("The screen shows a dashboard");
        result.Mode.Should().Be("vision");
    }

    [Fact]
    public async Task RunAsync_LlmFailure_ReturnsFailedResult()
    {
        var llm = EmptyVisionLlm();
        var sut = new VisionPipeline(llm.Object, NullInjector());

        var result = await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ProviderNotSupported_ReturnsFailedResult()
    {
        var llm = NotSupportedLlm();
        var sut = new VisionPipeline(llm.Object, NullInjector());

        var result = await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not support vision", Exactly.Once());
    }

    [Fact]
    public async Task RunAsync_Cancelled_ReturnsFailedResult()
    {
        var llm = new Mock<ILLMProvider>();
        llm.SetupGet(l => l.ProviderName).Returns("MockLLM");
        llm.Setup(l => l.ProcessWithImageAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new VisionPipeline(llm.Object, NullInjector());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions(), cts.Token);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled", Exactly.Once());
    }

    [Fact]
    public async Task RunAsync_ClipboardMode_DoesNotInject()
    {
        var llm = OkVisionLlm("clipboard text");
        var sut = new VisionPipeline(llm.Object, NullInjector());

        var result = await sut.RunAsync(FakeScreenshot, PngMime, null,
            DefaultOptions(VisionOutputMode.Clipboard));

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("clipboard text");
        result.InjectionMs.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_BeforeLlmHook_AppliesToPrompt()
    {
        var llm = OkVisionLlm();
        var eventBus = new PipelineEventBus();
        eventBus.OnBeforeLlmProcessing(ctx => ctx.AdditionalSystemContext = "Extra context here");
        var sut = new VisionPipeline(llm.Object, NullInjector(), eventBus: eventBus);

        await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions());

        llm.Verify(l => l.ProcessWithImageAsync(
            FakeScreenshot, PngMime,
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Extra context here", StringComparison.Ordinal)),
            "vision", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Completed_Event_Fires_OnSuccess()
    {
        var llm = OkVisionLlm();
        var sut = new VisionPipeline(llm.Object, NullInjector());
        PipelineResult? captured = null;
        sut.Completed += (_, r) => captured = r;

        await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions());

        captured.Should().NotBeNull();
        captured!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StateChanged_Event_Fires_InOrder()
    {
        var llm = OkVisionLlm();
        var sut = new VisionPipeline(llm.Object, NullInjector());
        var states = new List<PipelineState>();
        sut.StateChanged += (_, s) => states.Add(s);

        await sut.RunAsync(FakeScreenshot, PngMime, null, DefaultOptions());

        states.Should().Contain(PipelineState.Processing);
        states.Should().Contain(PipelineState.Injecting);
        states.Should().Contain(PipelineState.Idle);
    }
}
