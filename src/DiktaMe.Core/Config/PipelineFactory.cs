namespace DiktaMe.Core.Config;

using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;

/// <summary>
/// Constructs pipeline instances with mode-aware provider selection based on
/// the active profile in <see cref="SettingsManager"/>.
/// Replaces direct <c>new DictationPipeline(stt, llm, injector)</c> calls in hotkey handlers.
/// </summary>
public sealed class PipelineFactory
{
    private readonly ProfileManager _profiles;
    private readonly ISTTProviderFactory _sttFactory;
    private readonly ILLMProviderFactory _llmFactory;
    private readonly TextInjector _injector;

    public PipelineFactory(
        ProfileManager profiles,
        ISTTProviderFactory sttFactory,
        ILLMProviderFactory llmFactory,
        TextInjector injector)
    {
        _profiles = profiles;
        _sttFactory = sttFactory;
        _llmFactory = llmFactory;
        _injector = injector;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    public DictationPipeline CreateDictationPipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("dictate", modeOverride);
        return new DictationPipeline(stt, llm, _injector);
    }

    public RefinePipeline CreateRefinePipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("refine", modeOverride);
        return new RefinePipeline(llm!, _injector, stt);
    }

    public AskPipeline CreateAskPipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("ask", modeOverride);
        return new AskPipeline(stt, llm!);
    }

    public TranslatePipeline CreateTranslatePipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("translate", modeOverride);
        return new TranslatePipeline(stt, llm!, _injector);
    }

    public NotePipeline CreateNotePipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("note", modeOverride);
        return new NotePipeline(stt, llm);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private (ISTTProvider Stt, ILLMProvider? Llm) GetProviders(string mode, string? modeOverride)
    {
        string effectiveMode = modeOverride ?? mode;
        ModeSettings ms = _profiles.GetModeSettings(effectiveMode);

        ISTTProvider stt = _sttFactory.CreateProvider(ms.SttProvider);

        ILLMProvider? llm = ms.UseLlm
            ? _llmFactory.CreateProvider(ms.LlmProvider, model: ms.LlmModel)
            : null;

        return (stt, llm);
    }
}
