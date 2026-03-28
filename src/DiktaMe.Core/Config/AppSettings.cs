
using System.Text.Json.Serialization;

namespace DiktaMe.Core.Config;
/// <summary>
/// Privacy level for history and logging. Mirrors V1's 4-tier privacy system.
/// </summary>
public enum PrivacyLevel
{
    /// <summary>No logging, no history, no metrics. Leaves no traces.</summary>
    Ghost = 0,

    /// <summary>Aggregate metrics only (word counts, latency). No text stored.</summary>
    Stats = 1,

    /// <summary>History stored with PII scrubbing applied.</summary>
    Balanced = 2,

    /// <summary>Full history including potentially sensitive text.</summary>
    Full = 3,
}

/// <summary>
/// Output mode for Ask pipeline results.
/// </summary>
public enum AskOutputMode
{
    /// <summary>Show result in toast notification only.</summary>
    ToastOnly = 0,

    /// <summary>Copy result to clipboard only (no notification).</summary>
    ClipboardOnly = 1,

    /// <summary>Inject result as text at cursor position.</summary>
    InjectOnly = 2,

    /// <summary>Copy to clipboard AND show toast notification.</summary>
    ClipboardAndToast = 3,
}

/// <summary>
/// LLM provider state for the control panel quick toggle.
/// Replaces the separate IsLocalLlm + RawModeOverride booleans.
/// </summary>
public enum LlmMode
{
    /// <summary>Local LLM via Ollama.</summary>
    Local = 0,

    /// <summary>Cloud LLM via Gemini.</summary>
    Cloud = 1,

    /// <summary>LLM disabled (raw mode — no text processing).</summary>
    Off = 2,
}

/// <summary>
/// TTS provider state for the control panel quick toggle.
/// </summary>
public enum TtsMode
{
    /// <summary>Local TTS via Kokoro.</summary>
    Local = 0,

    /// <summary>Cloud TTS via Deepgram/OpenAI.</summary>
    Cloud = 1,

    /// <summary>TTS disabled.</summary>
    Off = 2,
}

/// <summary>
/// Per-mode settings for a single profile slot (provider + prompt selection).
/// 8 modes × 2 profiles = 16 configuration slots.
/// </summary>
public sealed record ModeSettings
{
    /// <summary>STT provider name for this mode (e.g. "deepgram", "whisper", "gemini-audio").</summary>
    public string SttProvider { get; init; } = "deepgram";

    /// <summary>LLM provider name for this mode (e.g. "gemini", "openai", "ollama", "none").</summary>
    public string LlmProvider { get; init; } = "gemini";

    /// <summary>
    /// LLM model override for this mode. Empty = use provider default.
    /// </summary>
    public string LlmModel { get; init; } = string.Empty;

    /// <summary>Index into the 16-slot custom prompt array (0-15), or -1 to use provider default.</summary>
    public int PromptSlot { get; init; } = -1;

    /// <summary>Whether to run the LLM at all in this mode (false = raw STT output).</summary>
    public bool UseLlm { get; init; } = true;
}

/// <summary>
/// General application settings.
/// </summary>
public sealed record GeneralSettings
{
    public string Language { get; init; } = "en";

    /// <summary>UI display language code (e.g. "en", "es-MX"). Requires restart to take effect.</summary>
    public string UiLanguage { get; init; } = "en";

    public bool AutoStart { get; init; } = false;
    public bool SoundFeedback { get; init; } = true;

    /// <summary>Key to press after text injection (e.g. "Enter", "Tab", or "" for none).</summary>
    public string AdditionalKey { get; init; } = string.Empty;

    /// <summary>Whether to add a trailing space after injected text.</summary>
    public bool TrailingSpace { get; init; } = true;

    /// <summary>Output mode for Ask pipeline results (toast, clipboard, inject, or both).</summary>
    public AskOutputMode AskOutput { get; init; } = AskOutputMode.ClipboardAndToast;

    /// <summary>Global RAW mode override — skips LLM processing for all dictation modes.</summary>
    public bool RawModeOverride { get; init; } = false;

    /// <summary>
    /// When true and the STT provider supports streaming (Deepgram), dictation uses
    /// real-time WebSocket streaming instead of batch record-then-transcribe.
    /// Streaming always operates in raw mode (no LLM). Default: false.
    /// </summary>
    public bool StreamingEnabled { get; init; } = false;

    /// <summary>When true, Refine uses voice instruction mode; when false, uses auto (text selection only).</summary>
    public bool RefineVoiceMode { get; init; } = false;

    /// <summary>App color theme: "Midnight" (cool dark), "Ember" (warm dark), or "Frost" (light).</summary>
    public string ThemeName { get; init; } = "Midnight";
}

/// <summary>
/// Audio recording settings.
/// </summary>
public sealed record AudioSettings
{
    /// <summary>Friendly name of the recording device (empty = default device).</summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>Maximum recording duration in seconds.</summary>
    public int MaxDurationSeconds { get; init; } = 60;
}

/// <summary>
/// Audio ducking settings — lowers other apps' volume while recording.
/// </summary>
public sealed record AudioDuckingSettings
{
    /// <summary>Whether audio ducking is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Volume level to duck other sessions to (0–100, inclusive).
    /// Stored as integer percentage; converted to 0.0–1.0 float when applied.
    /// </summary>
    public int DuckLevelPercent { get; init; } = 20;

    /// <summary>
    /// Duration in milliseconds to ramp volume down when ducking starts (0 = instant).
    /// </summary>
    public int RampDownMs { get; init; } = 500;
}

/// <summary>
/// Privacy and data retention settings.
/// </summary>
public sealed record PrivacySettings
{
    public PrivacyLevel Level { get; init; } = PrivacyLevel.Balanced;
    public bool PiiScrubEnabled { get; init; } = true;
    public int HistoryRetentionDays { get; init; } = 90;
}

/// <summary>
/// Control Panel HUD visibility toggles (SPEC_043).
/// </summary>
public sealed record ControlPanelSettings
{
    public bool ShowModesRow { get; init; } = true;
    public bool ShowActionsRow { get; init; } = true;
    public bool ShowSessionStats { get; init; } = true;
    public bool ShowPerformanceStats { get; init; } = true;
    public bool AlwaysOnTop { get; init; } = false;
    public bool IsExpanded { get; init; } = true;

    /// <summary>Expand direction: "Down" (header at top) or "Up" (header at bottom).</summary>
    public string ExpandDirection { get; init; } = "Down";

    /// <summary>Enable reactive background visual effects (glow, shimmer).</summary>
    public bool VisualEffectsEnabled { get; init; } = true;

    /// <summary>Scope of visual effects: "WholeApp" or "TopBarOnly".</summary>
    public string VisualEffectsScope { get; init; } = "WholeApp";

    /// <summary>Intensity of voice-level glow effects (0.0 = subtle, 1.0 = hardcore).</summary>
    public double VisualEffectsIntensity { get; init; } = 0.5;

    /// <summary>Auto-collapse: shrink bar width after idle (bar mode only).</summary>
    public bool AutoCollapseEnabled { get; init; } = false;

    /// <summary>Seconds of idle before bar collapses. Must be ≤ AutoHideDelaySeconds when both are enabled.</summary>
    public int AutoCollapseDelaySeconds { get; init; } = 10;

    /// <summary>Waveform style: "Off", "Wave", or "Bars".</summary>
    public string WaveformStyle { get; init; } = "Wave";

    /// <summary>Screen snap position: TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight.</summary>
    public string BarPosition { get; init; } = "TopRight";

    /// <summary>Auto-hide: fade the control panel after idle.</summary>
    public bool AutoHideEnabled { get; init; } = false;

    /// <summary>Seconds of idle before fade starts. 0 = Never.</summary>
    public int AutoHideDelaySeconds { get; init; } = 30;

    /// <summary>Idle branding roll: cycles status → logo+clock → weather when collapsed and idle.</summary>
    public bool IdleRollEnabled { get; init; } = true;

    /// <summary>Show clock layer in idle roll.</summary>
    public bool IdleRollShowClock { get; init; } = true;

    /// <summary>Show weather layer in idle roll.</summary>
    public bool IdleRollShowWeather { get; init; } = true;

    /// <summary>Clock format preset for idle roll: "ddd M/d HH:mm", "HH:mm:ss", "hh:mm tt", "ddd HH:mm".</summary>
    public string IdleRollClockFormat { get; init; } = "ddd M/d HH:mm";

    /// <summary>Seconds each layer is held during idle roll (5–20).</summary>
    public int IdleRollHoldSeconds { get; init; } = 5;
}

/// <summary>
/// Vision module settings (SPEC_002).
/// </summary>
public sealed record VisionSettings
{
    public bool Enabled { get; init; } = true;
    public string DefaultQuery { get; init; } = "Describe what you see and extract any visible text.";
    public int MaxImageDimensionPx { get; init; } = 2048;
    public bool AutoRecordQuery { get; init; } = true;
    public int QueryTimeoutSeconds { get; init; } = 10;
    public int MaxResponseTokens { get; init; } = 4096;
    public double Temperature { get; init; } = 0.3;
    public int OllamaKeepAliveSeconds { get; init; } = 300;
    public string OutputMode { get; init; } = "inject"; // Legacy — kept for backwards compat

    // Inject-at-cursor toggles (AI text always goes to clipboard; these control whether it also injects)
    public bool ClipInjectAtCursor { get; init; } = true;
    public bool OcrInjectAtCursor { get; init; } = true;
    public bool ColorPickerInjectAtCursor { get; init; } = true;
    public bool VideoAiInjectAtCursor { get; init; } = true;

    // Cloud vision settings
    public string CloudVisionProvider { get; init; } = "gemini";
    public string CloudVisionModelId { get; init; } = "gemini-2.5-flash";

    // Local vision settings (Ollama)
    public string LocalVisionModelId { get; init; } = "minicpm-v";

    // Resolved provider/model — PipelineFactory reads these.
    // Kept for backwards compat with existing settings.json files.
    public string VisionModelId { get; init; } = "minicpm-v";
    public string VisionProvider { get; init; } = "ollama";

    // Screen recording settings
    public string VideoQuality { get; init; } = "medium";       // "low" (1.5Mbps/24fps), "medium" (5Mbps/30fps), "high" (8Mbps/60fps)
    public bool EnableWebcam { get; init; } = true;
    public int WebcamSize { get; init; } = 200;                  // px width (height auto-calculated at 16:9)
    public string WebcamPosition { get; init; } = "bottom-right"; // "bottom-right", "bottom-left", "top-right", "top-left"
    public bool EnableMicAudio { get; init; } = true;
    public bool EnableSystemAudio { get; init; } = true;
}

/// <summary>
/// Global hotkey configuration. Default values match V1.
/// </summary>
public sealed record HotkeySettings
{
    public string Dictate { get; init; } = "Ctrl+Alt+D";
    public string Refine { get; init; } = "Ctrl+Alt+R";
    public string Ask { get; init; } = "Ctrl+Alt+A";
    public string Translate { get; init; } = "Ctrl+Alt+T";
    public string Oops { get; init; } = "Ctrl+Alt+V";
    public string Note { get; init; } = "Ctrl+Alt+N";
    public string Chat { get; init; } = "Ctrl+Alt+C";
    public string ReadSelection { get; init; } = "Ctrl+Alt+Q";
    public string Vision { get; init; } = "Ctrl+Alt+S";
}

/// <summary>
/// Note pipeline configuration (voice-to-note with timestamp and LLM processing).
/// </summary>
public sealed record NoteSettings
{
    /// <summary>File path where notes are saved (.md or .txt).</summary>
    public string FilePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "diktame-notes.md");

    /// <summary>Whether to use LLM to clean up transcription into professional notes.</summary>
    public bool UseLlmProcessing { get; init; } = true;

    /// <summary>
    /// Timestamp format string (C# DateTime format).
    /// Default: "%Y-%m-%d %H:%M:%S" from V1.
    /// </summary>
    public string TimestampFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
}

/// <summary>
/// Sound feedback configuration — per-pipeline sound selection.
/// Sound stems resolve to WAV files in Assets\Sounds\ (e.g. "a" → "a.wav").
/// </summary>
public sealed record SoundSettings
{
    /// <summary>Sound played when dictation recording starts.</summary>
    public string StartSound { get; init; } = "a";

    /// <summary>Sound played when dictation recording stops.</summary>
    public string StopSound { get; init; } = "a";

    /// <summary>Sound played when utility pipelines (ask/refine/translate/note) start and stop.</summary>
    public string UtilitySound { get; init; } = "c";
}

/// <summary>
/// Deepgram STT provider configuration.
/// Controls model selection and transcription features sent as URL parameters.
/// </summary>
public sealed record DeepgramSettings
{
    /// <summary>Deepgram model to use ("nova-3", "nova-2").</summary>
    public string Model { get; init; } = "nova-3";

    /// <summary>Auto-add punctuation and capitalization to STT output.</summary>
    public bool Punctuate { get; init; } = true;

    /// <summary>
    /// Convert spoken punctuation commands ("comma", "period", "question mark") to symbols.
    /// English only. Requires <see cref="Punctuate"/> to be enabled.
    /// </summary>
    public bool Dictation { get; init; } = true;

    /// <summary>
    /// Auto-format dates, currency, phones, emails, URLs.
    /// Superset of <see cref="Punctuate"/> (includes punctuation automatically).
    /// Adds ~3s buffer in streaming mode while waiting for complete entities.
    /// </summary>
    public bool SmartFormat { get; init; } = false;

    /// <summary>
    /// Server-side find-and-replace pairs applied before LLM cleanup.
    /// Format: "find:replace" per entry. Find terms must be lowercase. Max 200.
    /// Complements client-side <see cref="SnippetManager"/> (which runs after LLM).
    /// </summary>
    public List<string> Replacements { get; init; } = [];
}

/// <summary>
/// Chat overlay UI configuration.
/// </summary>
public sealed record ChatSettings
{
    /// <summary>Font size for chat messages (in points).</summary>
    public int FontSize { get; init; } = 14;

    /// <summary>Whether to clear chat history when closing the overlay.</summary>
    public bool ForgetOnClose { get; init; } = false;

    /// <summary>Maximum number of messages to keep in history (0 = unlimited).</summary>
    public int MaxHistoryMessages { get; init; } = 50;

    /// <summary>Chat window opacity (0.0-1.0).</summary>
    public double WindowOpacity { get; init; } = 0.95;

    /// <summary>Whether to show timestamps next to each message.</summary>
    public bool ShowTimestamps { get; init; } = true;

    /// <summary>Enable markdown rendering in chat messages.</summary>
    public bool EnableMarkdown { get; init; } = true;

    /// <summary>
    /// Default LLM model ID for new conversations (e.g., "gpt-4o-mini", "gemini-2.5-flash").
    /// When null, uses the profile's default model.
    /// </summary>
    public string? DefaultModelId { get; init; }

    /// <summary>Chat window width in device-independent pixels.</summary>
    public double WindowWidth { get; init; } = 600;

    /// <summary>Chat window height in device-independent pixels.</summary>
    public double WindowHeight { get; init; } = 600;

    /// <summary>Whether the chat window stays on top of other windows.</summary>
    public bool AlwaysOnTop { get; init; } = true;

    /// <summary>
    /// Default system prompt for new conversations.
    /// When null, uses the built-in default.
    /// </summary>
    public string? DefaultSystemPrompt { get; init; }

    /// <summary>
    /// When true, Gemini models use Google Search grounding to provide
    /// up-to-date answers with web sources. Only affects Gemini provider.
    /// </summary>
    public bool WebSearchEnabled { get; init; } = false;
}

/// <summary>
/// Cloud LLM model management settings.
/// Controls which cloud models are visible in pipeline/chat model dropdowns.
/// </summary>
public sealed record CloudLlmSettings
{
    /// <summary>
    /// Model IDs that the user has disabled. Blacklist approach — new models are auto-enabled.
    /// </summary>
    public List<string> DisabledModelIds { get; init; } = [];
}

/// <summary>
/// Text-to-Speech configuration. TTS is off by default (opt-in feature).
/// </summary>
public sealed record TtsSettings
{
    /// <summary>Master TTS toggle (off by default — opt-in feature).</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>TTS provider: "kokoro", "deepgram", "inworld", "openai", "none".</summary>
    public string Provider { get; init; } = "kokoro";

    /// <summary>Voice ID (provider-specific). Empty = provider default.</summary>
    public string VoiceId { get; init; } = string.Empty;

    /// <summary>Speaking rate multiplier (0.5–2.0). 1.0 = normal speed.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Playback volume (0–100). Independent of system volume.</summary>
    public int VolumePercent { get; init; } = 80;

    /// <summary>Max words to speak before truncating. 0 = unlimited.</summary>
    public int MaxSpeechWords { get; init; } = 500;

    /// <summary>Duck other apps during TTS playback.</summary>
    public bool DuckDuringPlayback { get; init; } = true;

    /// <summary>Speak Ask mode responses aloud.</summary>
    public bool SpeakAskResponses { get; init; } = false;

    /// <summary>Speak Quick Chat responses aloud.</summary>
    public bool SpeakChatResponses { get; init; } = false;

    /// <summary>Speak translated text aloud.</summary>
    public bool SpeakTranslations { get; init; } = false;

    /// <summary>Speak app notifications aloud (errors, warnings, status).</summary>
    public bool SpeakNotifications { get; init; } = false;

    /// <summary>
    /// Speech style prompt for Gemini TTS (e.g. "Speak cheerfully and warmly").
    /// Prepended to the text content when using Gemini provider. Empty = no style.
    /// </summary>
    public string SpeechPrompt { get; init; } = string.Empty;

    /// <summary>Kokoro model variant: "gpu", "int8", "fp16", "fp32".</summary>
    public string KokoroModelVariant { get; init; } = "gpu";

    /// <summary>Use DirectML GPU acceleration for Kokoro inference.</summary>
    public bool KokoroUseGpu { get; init; } = true;
}

/// <summary>
/// Root settings model for dIKta.me V2.
/// Strongly-typed (not a dictionary) for trim-safety and compile-time correctness.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Schema version — incremented on breaking changes for migration support.</summary>
    public int SchemaVersion { get; init; } = 2;

    /// <summary>Whether the first-run wizard has been completed.</summary>
    public bool WizardCompleted { get; init; } = false;

    public GeneralSettings General { get; init; } = new();
    public AudioSettings Audio { get; init; } = new();
    public AudioDuckingSettings AudioDucking { get; init; } = new();
    public PrivacySettings Privacy { get; init; } = new();
    public HotkeySettings Hotkeys { get; init; } = new();
    public ControlPanelSettings ControlPanel { get; init; } = new();
    public NoteSettings Note { get; init; } = new();
    public ChatSettings Chat { get; init; } = new();
    public SoundSettings Sound { get; init; } = new();
    public DeepgramSettings Deepgram { get; init; } = new();
    public TtsSettings Tts { get; init; } = new();
    public CloudLlmSettings CloudLlm { get; init; } = new();
    public VisionSettings Vision { get; init; } = new();

    /// <summary>
    /// Per-mode settings for 2 profiles.
    /// Key format: "{mode}_{profile}" e.g. "dictate_0", "ask_1".
    /// 8 modes × 2 profiles = up to 16 entries.
    /// </summary>
    public Dictionary<string, ModeSettings> ModeProfiles { get; init; } = new();

    /// <summary>
    /// 16 custom prompt slots (indices 0-15). Null = use built-in default.
    /// DEPRECATED: Replaced by DictationMode.CloudProfile/LocalProfile.SystemPrompt in Stream J.
    /// Kept for backwards compatibility during migration.
    /// </summary>
    public string?[] CustomPrompts { get; init; } = new string?[16];

    /// <summary>
    /// MIGRATION-ONLY: Legacy profile index (0 or 1).
    /// No runtime code reads this. Kept solely so existing JSON settings files
    /// can be deserialized and migrated to <see cref="ActiveProfileName"/> on first load.
    /// See SettingsManager.MigrateActiveProfile() for the migration logic.
    /// </summary>
    public int ActiveProfile { get; init; } = 0;

    /// <summary>Notes file path for the Note pipeline.</summary>
    public string NotesFilePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "diktame-notes.md");

    /// <summary>Ollama model to use as the default local LLM.</summary>
    public string OllamaModel { get; init; } = "gemma3:1b";

    /// <summary>Ollama keep-alive duration (how long to keep model in VRAM after inference).</summary>
    public string OllamaKeepAlive { get; init; } = "10m";

    /// <summary>Custom Ollama endpoint URL (for remote instances or non-default ports).</summary>
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>Pre-load default model into VRAM on startup.</summary>
    public bool OllamaAutoWarmup { get; init; } = false;

    /// <summary>Context window size for Ollama inference (num_ctx parameter).</summary>
    public int OllamaNumCtx { get; init; } = 2048;

    /// <summary>Whisper model size for local STT (tiny, base, small, medium, large, turbo).</summary>
    public string WhisperModel { get; init; } = "small";

    // ── Stream J: CRUD Dictation Modes (NEW) ──────────────────────────────────

    /// <summary>
    /// User's dictation presets (CRUD-capable). Serialized as JSON array.
    /// A single "Standard" preset is created on first run.
    /// </summary>
    public List<DictationMode> DictationModes { get; init; } = [];

    /// <summary>
    /// Fixed utility pipeline configs (Ask, Refine, Translate, Note, Chat).
    /// These cannot be created/deleted by users, only configured.
    /// Populated by DictationModeDefaults.CreateBuiltInUtilityPipelines() on first run.
    /// </summary>
    public List<PipelineConfig> UtilityPipelines { get; init; } = [];

    /// <summary>
    /// Active profile name ("Cloud" or "Local").
    /// Determines which profile (CloudProfile vs LocalProfile) is used for each mode.
    /// </summary>
    public string ActiveProfileName { get; init; } = "Cloud";

    /// <summary>
    /// ID of the currently active dictation preset (selected in Control Panel).
    /// If null or invalid, defaults to first preset in sorted order.
    /// </summary>
    public string? ActiveDictationModeId { get; init; }

    // ── Stream K: OAuth & Wallet ─────────────────────────────────────────────

    /// <summary>
    /// Authentication mode controlling LLM/STT routing.
    /// <see cref="AuthMode.Wallet"/> routes through managed wallet proxy.
    /// </summary>
    public AuthMode AuthMode { get; init; } = AuthMode.None;

    /// <summary>
    /// Non-sensitive account and wallet metadata. JWT token stored separately in SecureStorage.
    /// </summary>
    public AccountSettings Account { get; init; } = new();
}

/// <summary>
/// Source-generated JSON serialization context for <see cref="AppSettings"/>.
/// Required for IL-trim compatibility (PublishTrimmed).
/// </summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(GeneralSettings))]
[JsonSerializable(typeof(VisionSettings))]
[JsonSerializable(typeof(AudioSettings))]
[JsonSerializable(typeof(AudioDuckingSettings))]
[JsonSerializable(typeof(PrivacySettings))]
[JsonSerializable(typeof(HotkeySettings))]
[JsonSerializable(typeof(ControlPanelSettings))]
[JsonSerializable(typeof(SoundSettings))]
[JsonSerializable(typeof(DeepgramSettings))]
[JsonSerializable(typeof(ModeSettings))]
[JsonSerializable(typeof(Dictionary<string, ModeSettings>))]
[JsonSerializable(typeof(DictationMode))]
[JsonSerializable(typeof(DictationProfile))]
[JsonSerializable(typeof(PipelineConfig))]
[JsonSerializable(typeof(UtilityProfile))]
[JsonSerializable(typeof(List<DictationMode>))]
[JsonSerializable(typeof(List<PipelineConfig>))]
[JsonSerializable(typeof(AskOutputMode))]
[JsonSerializable(typeof(LlmMode))]
[JsonSerializable(typeof(TtsMode))]
[JsonSerializable(typeof(AuthMode))]
[JsonSerializable(typeof(AccountSettings))]
[JsonSerializable(typeof(TtsSettings))]
[JsonSerializable(typeof(CloudLlmSettings))]
public partial class AppSettingsContext : JsonSerializerContext { }
