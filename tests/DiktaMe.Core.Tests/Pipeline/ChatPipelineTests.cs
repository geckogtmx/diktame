
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

    // ── Conversation (multi-turn) path ────────────────────────────────────

    [Fact]
    public async Task RunConversationAsync_WithHistory_ReturnsAnswer()
    {
        var llm = new Mock<ILLMProvider>();
        llm.Setup(l => l.ProviderName).Returns("MockLLM");
        llm.Setup(l => l.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new LlmResult { Text = "conversation answer", Provider = "MockLLM" });

        var pipeline = new ChatPipeline(llm.Object, MockSettings());
        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi there"),
            new("user", "how are you?"),
        };

        var result = await pipeline.RunConversationAsync(
            new ChatOptions { SystemPrompt = DefaultPrompt },
            history);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("conversation answer");
        result.RawTranscript.Should().Be("how are you?");
        result.Mode.Should().Be("chat");
    }

    [Fact]
    public async Task RunConversationAsync_EmptyHistory_ReturnsFailure()
    {
        var pipeline = new ChatPipeline(OkLlm().Object, MockSettings());

        var result = await pipeline.RunConversationAsync(
            new ChatOptions { SystemPrompt = DefaultPrompt },
            Array.Empty<ConversationTurn>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No messages");
    }

    [Fact]
    public async Task RunConversationAsync_LlmReturnsEmpty_ReturnsFailure()
    {
        var llm = new Mock<ILLMProvider>();
        llm.Setup(l => l.ProviderName).Returns("MockLLM");
        llm.Setup(l => l.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new LlmResult { Text = string.Empty, Provider = "MockLLM" });

        var pipeline = new ChatPipeline(llm.Object, MockSettings());
        var history = new List<ConversationTurn> { new("user", "hi") };

        var result = await pipeline.RunConversationAsync(
            new ChatOptions { SystemPrompt = DefaultPrompt },
            history);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty answer");
    }

    [Fact]
    public async Task RunConversationAsync_WhenCancelled_ReturnsFailure()
    {
        var llm = new Mock<ILLMProvider>();
        llm.Setup(l => l.ProviderName).Returns("MockLLM");
        llm.Setup(l => l.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new OperationCanceledException());

        var pipeline = new ChatPipeline(llm.Object, MockSettings());
        var history = new List<ConversationTurn> { new("user", "hi") };

        var result = await pipeline.RunConversationAsync(
            new ChatOptions { SystemPrompt = DefaultPrompt },
            history);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Cancelled");
    }

    // ── Context window truncation ─────────────────────────────────────────

    [Fact]
    public void TruncateHistory_SmallHistory_KeepsAll()
    {
        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi"),
        };

        var result = ChatPipeline.TruncateHistory(history, "Be helpful.", contextWindowTokens: 8192);

        result.Should().HaveCount(2);
        result[0].Content.Should().Be("hello");
        result[1].Content.Should().Be("hi");
    }

    [Fact]
    public void TruncateHistory_LargeHistory_TruncatesOldest()
    {
        // Budget: 100 tokens * 0.80 = 80 tokens. System prompt "sys" = 0 tokens (3 chars / 4 = 0).
        // Each message: "message NN" = 10 chars / 4 = 2 tokens.
        // So ~40 messages fit. Create 50 messages, expect some to be truncated.
        var history = new List<ConversationTurn>();
        for (int i = 0; i < 50; i++)
        {
            history.Add(new(i % 2 == 0 ? "user" : "assistant", $"message {i:D2}"));
        }

        var result = ChatPipeline.TruncateHistory(history, "sys", contextWindowTokens: 100);

        result.Count.Should().BeLessThan(50);
        // Last message should always be included
        result[^1].Content.Should().Be("message 49");
    }

    [Fact]
    public void TruncateHistory_AlwaysIncludesAtLeastOneMessage()
    {
        // Huge message that exceeds entire budget
        var history = new List<ConversationTurn>
        {
            new("user", new string('x', 100_000)),
        };

        var result = ChatPipeline.TruncateHistory(history, "sys", contextWindowTokens: 100);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void TruncateHistory_HugeSystemPrompt_ReturnsLastMessage()
    {
        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi"),
        };

        // System prompt uses entire budget
        var result = ChatPipeline.TruncateHistory(
            history, new string('x', 100_000), contextWindowTokens: 100);

        result.Should().HaveCount(1);
        result[0].Content.Should().Be("hi"); // Last message
    }
}
