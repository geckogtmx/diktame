
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using DiktaMe.Core.TTS;
using Serilog;

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
    private readonly PipelineEventBus? _eventBus;

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
        ILLMProvider? walletLlm = null,
        PipelineEventBus? eventBus = null)
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
        _eventBus = eventBus;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    public DictationPipeline CreateDictationPipeline(string? modeOverride = null)
    {
        var (stt, llm) = GetProviders("dictate", modeOverride);
        return new DictationPipeline(stt, llm, _injector, _snippets, _eventBus);
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
        return new AskPipeline(stt, llm!, _settings, _eventBus);
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
        return new ChatPipeline(llm!, _settings, stt, _eventBus);
    }

    public VisionPipeline CreateVisionPipeline(string? modeOverride = null)
    {
        // Wallet mode — override all provider selection with wallet proxies.
        if (_settings.Current.AuthMode == AuthMode.Wallet && _walletStt is not null && _walletLlm is not null)
        {
            return new VisionPipeline(_walletLlm, _injector, _walletStt, _eventBus);
        }

        // Use Vision-specific provider/model from VisionSettings (set by AI Engine > Vision)
        var vision = _settings.Current.Vision;
        string visionProvider = string.IsNullOrWhiteSpace(vision.VisionProvider) ? "ollama" : vision.VisionProvider;
        string visionModel = string.IsNullOrWhiteSpace(vision.VisionModelId) ? "moondream" : vision.VisionModelId;
        var llm = _llmFactory.CreateProvider(visionProvider, model: visionModel);

        // STT still comes from the normal profile (for voice query transcription)
        string effectiveMode = modeOverride ?? "vision";
        ModeSettings ms = _profiles.GetModeSettings(effectiveMode);
        var stt = _sttFactory.CreateProvider(ms.SttProvider);

        return new VisionPipeline(llm, _injector, stt, _eventBus);
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

    // ── Warmup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fire-and-forget warmup for local providers. Call at recording start to
    /// pre-load Ollama model into VRAM while the user is still speaking.
    /// </summary>
    public async Task WarmUpLocalProvidersAsync(string mode)
    {
        try
        {
            if (_settings.Current.AuthMode == AuthMode.Wallet)
            {
                return;
            }

            ModeSettings ms = _profiles.GetModeSettings(mode);

            if (ms.UseLlm && string.Equals(ms.LlmProvider, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                var provider = _llmFactory.CreateProvider(ms.LlmProvider, model: ms.LlmModel);
                if (provider is OllamaProvider ollama)
                {
                    await ollama.WarmUpAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Recording warmup failed (non-fatal)");
        }
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
