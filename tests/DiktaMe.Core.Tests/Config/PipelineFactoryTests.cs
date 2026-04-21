
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using DiktaMe.Core.Tests;
using DiktaMe.Core.TTS;
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

    private static Mock<ITTSProvider> MakeTts()
    {
        var m = new Mock<ITTSProvider>();
        m.Setup(t => t.ProviderName).Returns("MockTTS");
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
        var ttsFactory = new Mock<ITTSProviderFactory>();
        var ttsPlayer = new Mock<ITtsPlayerService>();
        var injector = new TextInjector();
        var snippets = new SnippetManager();

        sttFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeStt().Object);
        llmFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeLlm().Object);
        ttsFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string?>()))
                  .Returns(MakeTts().Object);

        var factory = new PipelineFactory(profiles, sttFactory.Object, llmFactory.Object,
            ttsFactory.Object, ttsPlayer.Object, injector, sm, snippets);
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

    [Fact]
    public void CreateReadSelectionPipeline_ReturnsNonNull()
    {
        var (factory, _, _) = MakeFactory();
        var pipeline = factory.CreateReadSelectionPipeline();
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

    // ── Warmup tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WarmUpLocalProviders_OllamaMode_CallsCreateProvider()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "ollama", UseLlm = true, LlmModel = "gemma3:4b" },
            },
        };
        var (factory, _, llmFactory) = MakeFactory(settings);

        // Default mock returns ILLMProvider (not OllamaProvider), so WarmUpAsync won't
        // be called — but we can verify CreateProvider IS called with the right args.
        await factory.WarmUpLocalProvidersAsync("dictate");

        llmFactory.Verify(f => f.CreateProvider("ollama", It.IsAny<string>(), "gemma3:4b"), Times.Once);
    }

    [Fact]
    public async Task WarmUpLocalProviders_CloudMode_SkipsWarmUp()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "gemini", UseLlm = true },
            },
        };
        var (factory, _, llmFactory) = MakeFactory(settings);

        await factory.WarmUpLocalProvidersAsync("dictate");

        llmFactory.Verify(f => f.CreateProvider(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WarmUpLocalProviders_WalletMode_SkipsWarmUp()
    {
        var settings = new AppSettings
        {
            AuthMode = AuthMode.Wallet,
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "ollama", UseLlm = true },
            },
        };
        var (factory, _, llmFactory) = MakeFactory(settings);

        await factory.WarmUpLocalProvidersAsync("dictate");

        llmFactory.Verify(f => f.CreateProvider(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WarmUpLocalProviders_LlmDisabled_SkipsWarmUp()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "ollama", UseLlm = false },
            },
        };
        var (factory, _, llmFactory) = MakeFactory(settings);

        await factory.WarmUpLocalProvidersAsync("dictate");

        llmFactory.Verify(f => f.CreateProvider(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── BUG-030: Router injection ────────────────────────────────────────────
    // When an LLMRouter is injected, PipelineFactory must hand it to pipelines
    // rather than building a concrete provider from ModeProfiles.LlmProvider.
    // Pipelines then call router.ProcessWithModelAsync(options.ModelName), which
    // resolves the provider dynamically from DictationProfile.ModelName via
    // ModelListService.ResolveProviderFromModelId — the user's per-mode model
    // selection is honoured end-to-end.

    [Fact]
    public async Task CreateDictationPipeline_WithRouter_SkipsConcreteProviderConstruction()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "gemini", UseLlm = true },
            },
        };
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_pf_{Guid.NewGuid()}.json"));
        await sm.UpdateAsync(settings);

        var profiles = new ProfileManager(sm);
        var sttFactory = new Mock<ISTTProviderFactory>();
        var llmFactory = new Mock<ILLMProviderFactory>();
        var ttsFactory = new Mock<ITTSProviderFactory>();
        var ttsPlayer = new Mock<ITtsPlayerService>();
        var injector = new TextInjector();
        var snippets = new SnippetManager();

        sttFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeStt().Object);
        var routerMock = MakeLlm();
        routerMock.Setup(l => l.ProviderName).Returns("LLMRouter");

        var factory = new PipelineFactory(profiles, sttFactory.Object, llmFactory.Object,
            ttsFactory.Object, ttsPlayer.Object, injector, sm, snippets,
            llmRouter: routerMock.Object);

        factory.CreateDictationPipeline();

        // When router is injected, the factory must NOT construct an LLM via the factory.
        llmFactory.Verify(f => f.CreateProvider(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateDictationPipeline_WithRouterAndUseLlmFalse_PassesNullLlm()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { LlmProvider = "gemini", UseLlm = false },
            },
        };
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_pf_{Guid.NewGuid()}.json"));
        await sm.UpdateAsync(settings);

        var profiles = new ProfileManager(sm);
        var sttFactory = new Mock<ISTTProviderFactory>();
        var llmFactory = new Mock<ILLMProviderFactory>();
        var ttsFactory = new Mock<ITTSProviderFactory>();
        var ttsPlayer = new Mock<ITtsPlayerService>();
        var injector = new TextInjector();
        var snippets = new SnippetManager();

        sttFactory.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(MakeStt().Object);

        var factory = new PipelineFactory(profiles, sttFactory.Object, llmFactory.Object,
            ttsFactory.Object, ttsPlayer.Object, injector, sm, snippets,
            llmRouter: MakeLlm().Object);

        // Should not throw — UseLlm=false means null LLM even with router injected.
        factory.CreateDictationPipeline();

        llmFactory.Verify(f => f.CreateProvider(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
