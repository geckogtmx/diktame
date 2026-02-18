
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using DiktaMe.Core.Tests;
using Moq;
using Xunit;

namespace DiktaMe.Core.Tests.Config;
[Trait("Category", "Unit")]
[Collection(nameof(SettingsWriterCollection))]
public sealed class PipelineFactoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mock<ISTTProvider> MakeStt()
    {
        var m = new Mock<ISTTProvider>();
        m.Setup(s => s.ProviderName).Returns("MockSTT");
        return m;
    }

    private static Mock<ILLMProvider> MakeLlm()
    {
        var m = new Mock<ILLMProvider>();
        m.Setup(l => l.ProviderName).Returns("MockLLM");
        return m;
    }

    private static (PipelineFactory Factory, Mock<ISTTProviderFactory> SttFactory, Mock<ILLMProviderFactory> LlmFactory)
        MakeFactory(AppSettings? settings = null)
    {
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_pf_{Guid.NewGuid()}.json"));
        if (settings is not null)
        {
            sm.UpdateAsync(settings).GetAwaiter().GetResult();
        }

        var profiles = new ProfileManager(sm);
        var sttFactory = new Mock<ISTTProviderFactory>();
        var llmFactory = new Mock<ILLMProviderFactory>();
        var injector = new TextInjector();

        sttFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeStt().Object);
        llmFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeLlm().Object);

        var factory = new PipelineFactory(profiles, sttFactory.Object, llmFactory.Object, injector);
        return (factory, sttFactory, llmFactory);
    }

    // ── Factory method tests ──────────────────────────────────────────────────

    [Fact]
    public void CreateDictationPipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateDictationPipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void CreateRefinePipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateRefinePipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void CreateAskPipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateAskPipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void CreateTranslatePipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateTranslatePipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void CreateNotePipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateNotePipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void CreateChatPipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateChatPipeline();
        Assert.NotNull(pipeline);
    }

    // ── Provider selection from mode settings ─────────────────────────────────

    [Fact]
    public void CreateDictationPipeline_UsesConfiguredSttProvider()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { SttProvider = "whisper", UseLlm = true },
            },
        };
        var (factory, sttFactory, _) = MakeFactory(settings);

        factory.CreateDictationPipeline();

        sttFactory.Verify(f => f.CreateProvider("whisper", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CreateDictationPipeline_UsesConfiguredLlmProvider()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "ollama", UseLlm = true },
            },
        };
        var (factory, _, llmFactory) = MakeFactory(settings);

        factory.CreateDictationPipeline();

        llmFactory.Verify(f => f.CreateProvider("ollama", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void GetProviders_UseLlmFalse_DoesNotCreateLlmProvider()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { UseLlm = false },
            },
        };
        var (factory, _, llmFactory) = MakeFactory(settings);

        factory.CreateDictationPipeline();

        llmFactory.Verify(f => f.CreateProvider(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── Mode override ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateDictationPipeline_WithModeOverride_UsesOverrideSettings()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { SttProvider = "deepgram" },
                ["refine_0"] = new ModeSettings { SttProvider = "gemini-audio" },
            },
        };
        var (factory, sttFactory, _) = MakeFactory(settings);

        factory.CreateDictationPipeline(modeOverride: "refine");

        sttFactory.Verify(f => f.CreateProvider("gemini-audio", It.IsAny<string>()), Times.Once);
    }

    // ── Profile switching ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDictationPipeline_AfterProfileSwitch_UsesProfile1Settings()
    {
        var settings = new AppSettings
        {
            ActiveProfile = 0,
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { SttProvider = "deepgram" },
                ["dictate_1"] = new ModeSettings { SttProvider = "whisper" },
            },
        };
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_pf_{Guid.NewGuid()}.json"));
        await sm.UpdateAsync(settings);
        var profiles = new ProfileManager(sm);

        var sttFactory = new Mock<ISTTProviderFactory>();
        var llmFactory = new Mock<ILLMProviderFactory>();
        sttFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeStt().Object);
        llmFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeLlm().Object);

        var factory = new PipelineFactory(profiles, sttFactory.Object, llmFactory.Object, new TextInjector());

        await profiles.SwitchProfileAsync(1);
        factory.CreateDictationPipeline();

        sttFactory.Verify(f => f.CreateProvider("whisper", It.IsAny<string>()), Times.Once);
    }
}
