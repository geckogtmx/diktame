
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using DiktaMe.Core.TTS;

namespace DiktaMe.Core.Config;
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
    private readonly ITTSProviderFactory _ttsFactory;
    private readonly ITtsPlayerService _ttsPlayer;
    private readonly TextInjector _injector;
    private readonly SettingsManager _settings;
    private readonly SnippetManager _snippets;
    private readonly ISTTProvider? _walletStt;
    private readonly ILLMProvider? _walletLlm;

    public PipelineFactory(
        ProfileManager profiles,
        ISTTProviderFactory sttFactory,
        ILLMProviderFactory llmFactory,
        ITTSProviderFactory ttsFactory,
        ITtsPlayerService ttsPlayer,
        TextInjector injector,
        SettingsManager settings,
        SnippetManager snippets,
        ISTTProvider? walletStt = null,
        ILLMProvider? walletLlm = null)
    {
        _profiles = profiles;
        _sttFactory = sttFactory;
        _llmFactory = llmFactory;
        _ttsFactory = ttsFactory;
        _ttsPlayer = ttsPlayer;
        _injector = injector;
        _settings = settings;
        _snippets = snippets;
        _walletStt = walletStt;
        _walletLlm = walletLlm;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    public DictationPipeline CreateDictationPipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("dictate", modeOverride);
        return new DictationPipeline(stt, llm, _injector, _snippets);
    }

    public RefinePipeline CreateRefinePipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("refine", modeOverride);
        return new RefinePipeline(llm!, _injector, _settings, stt, _snippets);
    }

    /// <summary>
    /// Creates a RefinePipeline in autopilot mode (LLM only, no STT).
    /// Used when the Control Panel REFINE toggle is set to Auto.
    /// </summary>
    public RefinePipeline CreateRefineAutoPipeline(string? modeOverride = null)
    {
        var (_, llm) = GetProviders("refine", modeOverride);
        return new RefinePipeline(llm!, _injector, _settings, stt: null, _snippets);
    }

    public AskPipeline CreateAskPipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("ask", modeOverride);
        return new AskPipeline(stt, llm!, _settings);
    }

    public TranslatePipeline CreateTranslatePipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("translate", modeOverride);
        return new TranslatePipeline(stt, llm!, _injector, _snippets);
    }

    public NotePipeline CreateNotePipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("note", modeOverride);
        return new NotePipeline(stt, llm, _snippets);
    }

    public ChatPipeline CreateChatPipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("chat", modeOverride);
        return new ChatPipeline(llm!, _settings, stt);
    }

    /// <summary>
    /// Creates a ReadSelectionPipeline using the configured TTS provider.
    /// Does not require STT or LLM — operates on pre-captured text only.
    /// </summary>
    public ReadSelectionPipeline CreateReadSelectionPipeline()
    {
        var tts = _settings.Current.Tts;
        string? variant = string.Equals(tts.Provider, "kokoro", StringComparison.OrdinalIgnoreCase)
            ? tts.KokoroModelVariant : null;
        ITTSProvider provider = _ttsFactory.CreateProvider(tts.Provider, variant);
        return new ReadSelectionPipeline(provider, _ttsPlayer, _settings);
    }

    /// <summary>
    /// Returns true if the active dictation profile uses a streaming-capable
    /// STT provider (currently only Deepgram).
    /// </summary>
    public bool CanStreamDictation(string? modeOverride = null)
    {
        ModeSettings ms = _profiles.GetModeSettings(modeOverride ?? "dictate");
        return _sttFactory.SupportsStreaming(ms.SttProvider);
    }

    /// <summary>
    /// Creates a streaming dictation pipeline, or returns null if the active
    /// STT provider does not support streaming.
    /// </summary>
    public StreamingDictationPipeline? CreateStreamingDictationPipeline(string? modeOverride = null)
    {
        ModeSettings ms = _profiles.GetModeSettings(modeOverride ?? "dictate");
        IStreamingSTTProvider? streaming = _sttFactory.CreateStreamingProvider(ms.SttProvider);
        if (streaming is null)
        {
            return null;
        }

        return new StreamingDictationPipeline(streaming, _injector, _snippets);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private (ISTTProvider Stt, ILLMProvider? Llm) GetProviders(string mode, string? modeOverride)
    {
        // Wallet mode — override all provider selection with wallet proxies.
        if (_settings.Current.AuthMode == AuthMode.Wallet && _walletStt is not null && _walletLlm is not null)
        {
            return (_walletStt, _walletLlm);
        }

        string effectiveMode = modeOverride ?? mode;
        ModeSettings ms = _profiles.GetModeSettings(effectiveMode);

        ISTTProvider stt = _sttFactory.CreateProvider(ms.SttProvider);

        ILLMProvider? llm = ms.UseLlm
            ? _llmFactory.CreateProvider(ms.LlmProvider, model: ms.LlmModel)
            : null;

        return (stt, llm);
    }
}
