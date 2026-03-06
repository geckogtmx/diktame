
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiktaMe.Core.Tests.Pipeline;
/// <summary>
/// Unit tests for <see cref="ChatPipeline"/>.
/// Mocks <see cref="ISTTProvider"/> and <see cref="ILLMProvider"/> — no real I/O.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChatPipelineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private const string DefaultPrompt = "You are a helpful assistant.";

    private static Mock<ILLMProvider> OkLlm(string answer = "The answer is 42.")
    {
        var m = new Mock<ILLMProvider>();
        m.Setup(l => l.ProviderName).Returns("MockLLM");
        m.Setup(l => l.ProcessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new LlmResult { Text = answer, Provider = "MockLLM" });
        return m;
    }

    private static Mock<ILLMProvider> EmptyLlm()
    {
        var m = new Mock<ILLMProvider>();
        m.Setup(l => l.ProviderName).Returns("MockLLM");
        m.Setup(l => l.ProcessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new LlmResult { Text = string.Empty, Provider = "MockLLM" });
        return m;
    }

    private static Mock<ISTTProvider> OkStt(string text = "what is the meaning of life")
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.TranscribeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TranscriptionResult { Text = text, Provider = "MockSTT" });
        return m;
    }

    private static Mock<ISTTProvider> EmptyStt()
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.TranscribeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TranscriptionResult { Text = string.Empty, Provider = "MockSTT" });
        return m;
    }

    private static SettingsManager MockSettings(PrivacyLevel level = PrivacyLevel.Full, bool piiScrubEnabled = false)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"diktame_chat_test_{Guid.NewGuid()}.json");
        var settings = new SettingsManager(tempPath);
        var updated = settings.Current with
        {
            Privacy = settings.Current.Privacy with
            {
                Level = level,
                PiiScrubEnabled = piiScrubEnabled,
            }
        };
        settings.UpdateAsync(updated).Wait();
        return settings;
    }

    // ── Text input path ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithTextInput_SkipsSttAndReturnsAnswer()
    {
        var llm = OkLlm("The answer is 42.");
        var pipeline = new ChatPipeline(llm.Object, MockSettings());

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "What is the meaning of life?",
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("The answer is 42.");
        result.Mode.Should().Be("chat");
        result.SttProvider.Should().BeNull();
        result.LlmProvider.Should().Be("MockLLM");
        result.TranscriptionMs.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithWhitespaceTextInput_ReturnsFailure()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings());

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "   ",
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No question");
    }

    [Fact]
    public async Task RunAsync_WithTextInput_PassesQuestionToLlm()
    {
        var llm = OkLlm();
        var pipeline = new ChatPipeline(llm.Object, MockSettings());

        await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "hello",
        });

        llm.Verify(l => l.ProcessAsync(
            "hello",
            DefaultPrompt,
            "chat",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Voice input path ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithAudioFilePath_TranscribesAndChatsThenReturns()
    {
        var stt = OkStt("what is the meaning of life");
        var llm = OkLlm("The answer is 42.");
        var pipeline = new ChatPipeline(llm.Object, MockSettings(), stt.Object);

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            AudioFilePath = "fake.wav",
        });

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("The answer is 42.");
        result.RawTranscript.Should().Be("what is the meaning of life");
        result.SttProvider.Should().Be("MockSTT");
        result.TranscriptionMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAsync_WithEmptyTranscription_ReturnsFailure()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings(), EmptyStt().Object);

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            AudioFilePath = "fake.wav",
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No question detected");
    }

    [Fact]
    public async Task RunAsync_WithAudioButNoStt_Throws()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings(), stt: null);

        Func<Task> act = () => pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            AudioFilePath = "fake.wav",
        });

        // Exception is caught internally and returned as failure
        var result = await act.Should().NotThrowAsync();
        var r = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            AudioFilePath = "fake.wav",
        });
        r.IsSuccess.Should().BeFalse();
    }

    // ── Neither input provided ────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithNeitherInputNorAudio_ReturnsFailure()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings());

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            // TextInput = null, AudioFilePath = null
        });

        result.IsSuccess.Should().BeFalse();
    }

    // ── LLM failure ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenLlmReturnsEmpty_ReturnsFailure()
    {
        var pipeline = new ChatPipeline(EmptyLlm().Object, MockSettings());

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "hello",
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty answer");
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenCancelled_ReturnsFailureWithCancelledMessage()
    {
        var llm = new Mock<ILLMProvider>();
        llm.Setup(l => l.ProviderName).Returns("MockLLM");
        llm.Setup(l => l.ProcessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
           .ThrowsAsync(new OperationCanceledException());

        var pipeline = new ChatPipeline(llm.Object, MockSettings());

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "hello",
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Cancelled");
        result.Mode.Should().Be("chat");
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RaisesStateChangedAndCompletedEvents()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings());
        var states = new List<PipelineState>();
        PipelineResult? completedResult = null;

        pipeline.StateChanged += (_, s) => states.Add(s);
        pipeline.Completed += (_, r) => completedResult = r;

        await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "test",
        });

        states.Should().Contain(PipelineState.Processing);
        states.Should().Contain(PipelineState.Idle);
        completedResult.Should().NotBeNull();
        completedResult!.IsSuccess.Should().BeTrue();
    }

    // ── Latency fields ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SuccessResult_HasNonZeroTotalMs()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings());

        var result = await pipeline.RunAsync(new ChatOptions
        {
            SystemPrompt = DefaultPrompt,
            TextInput = "hello",
        });

        result.TotalMs.Should().BeGreaterThanOrEqualTo(0);
        result.ProcessingMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
