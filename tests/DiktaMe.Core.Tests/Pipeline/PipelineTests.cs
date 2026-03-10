
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using FluentAssertions;
using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using Moq;

namespace DiktaMe.Core.Tests.Pipeline;
/// <summary>
/// Unit tests for all Work Stream D pipelines.
/// Uses Moq mocks for ISTTProvider and ILLMProvider, and a
/// NullInputSimulator to avoid real Win32 calls.
/// </summary>
public sealed class PipelineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a TextInjector whose IInputSimulator is a Moq stub.
    /// All keyboard calls are no-ops; no real Win32 calls are made.
    /// CaptureSelection returns null because the clipboard never changes.
    /// </summary>
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

    private static Mock<ISTTProvider> OkStt(string text = "hello world", string lang = "en")
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TranscriptionResult
         {
             Text = text,
             Provider = "MockSTT",
             DetectedLanguage = lang,
         });
        return m;
    }

    private static Mock<ISTTProvider> EmptyStt()
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new TranscriptionResult { Text = string.Empty, Provider = "MockSTT" });
        return m;
    }

    private static Mock<ISTTProvider> ThrowingStt()
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(new HttpRequestException("network error"));
        return m;
    }

    private static Mock<ILLMProvider> OkLlm(string text = "cleaned text")
    {
        var m = new Mock<ILLMProvider>();
        m.SetupGet(l => l.ProviderName).Returns("MockLLM");
        m.Setup(l => l.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new LlmResult { Text = text, Provider = "MockLLM" });
        return m;
    }

    private static SettingsManager MockSettings(PrivacyLevel level = PrivacyLevel.Full, bool piiScrubEnabled = false)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"diktame_pipeline_test_{Guid.NewGuid()}.json");
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

    private static Mock<ILLMProvider> EmptyLlm()
    {
        var m = new Mock<ILLMProvider>();
        m.SetupGet(l => l.ProviderName).Returns("MockLLM");
        m.Setup(l => l.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new LlmResult { Text = string.Empty, Provider = "MockLLM" });
        return m;
    }

    private static Mock<ILLMProvider> ThrowingLlm()
    {
        var m = new Mock<ILLMProvider>();
        m.SetupGet(l => l.ProviderName).Returns("MockLLM");
        m.Setup(l => l.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(new InvalidOperationException("LLM error"));
        return m;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DictationPipeline
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Dictation_FullFlow_ReturnsCleanedText()
    {
        var stt = OkStt("raw transcript");
        var llm = OkLlm("cleaned transcript");
        var pipeline = new DictationPipeline(stt.Object, llm.Object, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean this.", Language = "en", RecordingDurationMs = 1500 };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("cleaned transcript");
        result.RawTranscript.Should().Be("raw transcript");
        result.Mode.Should().Be("dictate");
        result.TranscriptionMs.Should().BeGreaterThanOrEqualTo(0);
        result.RecordingMs.Should().Be(1500); // NEW: verify RecordingMs populated
        result.TotalMs.Should().BeGreaterThan(0); // NEW: verify TotalMs exists
        result.LlmProvider.Should().Be("MockLLM");
    }

    [Fact]
    public async Task Dictation_RawMode_SkipsLlm()
    {
        var stt = OkStt("raw words");
        var llm = new Mock<ILLMProvider>();
        var pipeline = new DictationPipeline(stt.Object, llm.Object, NullInjector());
        var opts = new DictationOptions { RawMode = true };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("raw words");
        // LLM should never be called
        llm.Verify(l => l.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Dictation_NullLlm_ActsAsRaw()
    {
        var stt = OkStt("some words");
        var pipeline = new DictationPipeline(stt.Object, null, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("some words");
        result.LlmProvider.Should().BeNull();
    }

    [Fact]
    public async Task Dictation_EmptyTranscription_SuccessWithEmptyText()
    {
        var pipeline = new DictationPipeline(EmptyStt().Object, OkLlm().Object, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        // Empty transcription is "success" (user simply didn't speak)
        result.IsSuccess.Should().BeTrue();
        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task Dictation_RecordingDurationMs_IsPropagatedToPipelineResult()
    {
        var stt = OkStt("hello");
        var llm = OkLlm("Hello!");
        var pipeline = new DictationPipeline(stt.Object, llm.Object, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean.", RecordingDurationMs = 3500 };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.RecordingMs.Should().Be(3500, "RecordingDurationMs from options should be copied to RecordingMs in result");
    }

    [Fact]
    public async Task Dictation_LlmReturnsEmpty_FallsBackToRaw()
    {
        var stt = OkStt("raw fallback");
        var pipeline = new DictationPipeline(stt.Object, EmptyLlm().Object, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("raw fallback");
    }

    [Fact]
    public async Task Dictation_SttThrows_ReturnFailure()
    {
        var pipeline = new DictationPipeline(ThrowingStt().Object, null, NullInjector());
        var opts = new DictationOptions();

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("network error");
    }

    [Fact]
    public async Task Dictation_LlmThrows_FallsBackToRawTranscript()
    {
        var pipeline = new DictationPipeline(OkStt().Object, ThrowingLlm().Object, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hello world");
        result.WarningMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Dictation_FiresStateChangedEvents()
    {
        var states = new List<PipelineState>();
        var pipeline = new DictationPipeline(OkStt().Object, OkLlm().Object, NullInjector());
        pipeline.StateChanged += (_, s) => states.Add(s);
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        await pipeline.RunAsync("audio.wav", opts);

        states.Should().ContainInOrder(
            PipelineState.Transcribing,
            PipelineState.Processing,
            PipelineState.Injecting,
            PipelineState.Idle);
    }

    [Fact]
    public async Task Dictation_FiresCompletedEvent()
    {
        PipelineResult? fired = null;
        var pipeline = new DictationPipeline(OkStt().Object, OkLlm().Object, NullInjector());
        pipeline.Completed += (_, r) => fired = r;
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        await pipeline.RunAsync("audio.wav", opts);

        fired.Should().NotBeNull();
        fired!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Dictation_WordCount_IsCorrect()
    {
        var pipeline = new DictationPipeline(OkStt().Object, OkLlm("one two three").Object, NullInjector());
        var opts = new DictationOptions { SystemPrompt = "Clean." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.WordCount.Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AskPipeline
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Ask_FullFlow_ReturnsAnswer()
    {
        var pipeline = new AskPipeline(OkStt("what is AI?").Object, OkLlm("AI is cool").Object, MockSettings());
        var opts = new AskOptions { SystemPrompt = "Answer questions.", Language = "en", RecordingDurationMs = 2000 };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("AI is cool");
        result.RawTranscript.Should().Be("what is AI?");
        result.Mode.Should().Be("ask");
        result.RecordingMs.Should().Be(2000);
        result.TotalMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Ask_EmptyTranscription_ReturnsFailure()
    {
        var pipeline = new AskPipeline(EmptyStt().Object, OkLlm().Object, MockSettings());
        var opts = new AskOptions { SystemPrompt = "Answer." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No question");
    }

    [Fact]
    public async Task Ask_LlmReturnsEmpty_ReturnsFailure()
    {
        var pipeline = new AskPipeline(OkStt().Object, EmptyLlm().Object, MockSettings());
        var opts = new AskOptions { SystemPrompt = "Answer." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Ask_SttThrows_ReturnsFailure()
    {
        var pipeline = new AskPipeline(ThrowingStt().Object, OkLlm().Object, MockSettings());
        var opts = new AskOptions { SystemPrompt = "Answer." };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TranslatePipeline
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Translate_FullFlow_ReturnsTranslatedText()
    {
        var stt = OkStt("bonjour monde", "fr");
        var llm = OkLlm("hello world");
        var pipeline = new TranslatePipeline(stt.Object, llm.Object, NullInjector());
        var opts = new TranslateOptions { SystemPrompt = "Translate to English.", Language = "auto" };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hello world");
        result.RawTranscript.Should().Be("bonjour monde");
        result.Mode.Should().Be("translate");
    }

    [Fact]
    public async Task Translate_LlmFails_FallsBackToRaw()
    {
        var stt = OkStt("hola");
        var pipeline = new TranslatePipeline(stt.Object, EmptyLlm().Object, NullInjector());
        var opts = new TranslateOptions { SystemPrompt = "Translate.", Language = "auto" };

        var result = await pipeline.RunAsync("audio.wav", opts);

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("hola");   // raw fallback
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NotePipeline
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Note_FullFlow_WritesToFile()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"diktame_note_test_{Guid.NewGuid():N}.md");
        try
        {
            var llm = OkLlm("formatted note");
            var pipeline = new NotePipeline(OkStt("raw note text").Object, llm.Object);
            var opts = new NoteOptions
            {
                SystemPrompt = "Format as note.",
                NotesFilePath = tmpFile,
            };

            var result = await pipeline.RunAsync("audio.wav", opts);

            result.IsSuccess.Should().BeTrue();
            result.Text.Should().Be("formatted note");
            result.Mode.Should().Be("note");

            File.Exists(tmpFile).Should().BeTrue();
            string content = await File.ReadAllTextAsync(tmpFile);
            content.Should().Contain("formatted note");
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
    public async Task Note_NoLlm_SavesRawTranscript()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"diktame_note_test_{Guid.NewGuid():N}.md");
        try
        {
            var pipeline = new NotePipeline(OkStt("raw note").Object, llm: null);
            var opts = new NoteOptions { NotesFilePath = tmpFile };

            var result = await pipeline.RunAsync("audio.wav", opts);

            result.IsSuccess.Should().BeTrue();
            result.Text.Should().Be("raw note");
            string content = await File.ReadAllTextAsync(tmpFile);
            content.Should().Contain("raw note");
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
    public async Task Note_EmptyTranscription_ReturnsFailure()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"diktame_note_test_{Guid.NewGuid():N}.md");
        try
        {
            var pipeline = new NotePipeline(EmptyStt().Object);
            var opts = new NoteOptions { NotesFilePath = tmpFile };

            var result = await pipeline.RunAsync("audio.wav", opts);

            result.IsSuccess.Should().BeFalse();
            File.Exists(tmpFile).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RefinePipeline (autopilot — no audio, selection already captured)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Category", "Hardware")] // Uses real Win32 clipboard via CaptureSelection()
    public async Task Refine_Autopilot_NoSelection_ReturnsFailure()
    {
        // NullInputSimulator returns empty string from Ctrl+C — so CaptureSelection returns null
        var pipeline = new RefinePipeline(OkLlm().Object, NullInjector(), MockSettings(), stt: null);
        var opts = new RefineOptions { SystemPrompt = "Clean this." };

        var result = await pipeline.RunAsync(null, opts);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No text selected");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // D.4: Oops re-inject (TextInjector.LastInjectedText)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TextInjector_LastInjectedText_IsNullInitially()
    {
        var injector = NullInjector();
        injector.LastInjectedText.Should().BeNull();
    }

    [Fact]
    public void TextInjector_InjectText_StoresLastText()
    {
        var injector = NullInjector();
        injector.InjectText("hello pipeline");
        injector.LastInjectedText.Should().Be("hello pipeline");
    }

    [Fact]
    public void TextInjector_InjectText_UpdatesOnSecondCall()
    {
        var injector = NullInjector();
        injector.InjectText("first");
        injector.InjectText("second");
        injector.LastInjectedText.Should().Be("second");
    }

    [Fact]
    public void TextInjector_ReInjectLast_NoOpWhenEmpty()
    {
        var injector = NullInjector();
        // Should not throw
        injector.ReInjectLast();
        injector.LastInjectedText.Should().BeNull();
    }

    [Fact]
    public void TextInjector_ReInjectLast_ReInjectsStoredText()
    {
        var injector = NullInjector();
        injector.InjectText("remember me");
        // Call ReInjectLast — should not throw and last text should remain the same
        injector.ReInjectLast();
        injector.LastInjectedText.Should().Be("remember me");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PipelineResult helpers
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void PipelineResult_Failure_HasCorrectShape()
    {
        var r = PipelineResult.Failure("dictate", "oh no");
        r.IsSuccess.Should().BeFalse();
        r.ErrorMessage.Should().Be("oh no");
        r.Mode.Should().Be("dictate");
        r.Text.Should().BeEmpty();
        r.WordCount.Should().Be(0);
    }

    [Fact]
    public void PipelineResult_WordCount_CountsWords()
    {
        var r = new PipelineResult
        {
            Text = "one two three four",
            Mode = "dictate",
            IsSuccess = true,
        };
        r.WordCount.Should().Be(4);
    }
}

