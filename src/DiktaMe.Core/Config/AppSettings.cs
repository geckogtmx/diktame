
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
    public bool AutoStart { get; init; } = false;
    public bool SoundFeedback { get; init; } = true;

    /// <summary>Key to press after text injection (e.g. "Enter", "Tab", or "" for none).</summary>
    public string AdditionalKey { get; init; } = string.Empty;

    /// <summary>Whether to add a trailing space after injected text.</summary>
    public bool TrailingSpace { get; init; } = true;
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
}

/// <summary>
/// Root settings model for dIKta.me V2.
/// Strongly-typed (not a dictionary) for trim-safety and compile-time correctness.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Schema version — incremented on breaking changes for migration support.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Whether the first-run wizard has been completed.</summary>
    public bool WizardCompleted { get; init; } = false;

    public GeneralSettings General { get; init; } = new();
    public AudioSettings Audio { get; init; } = new();
    public AudioDuckingSettings AudioDucking { get; init; } = new();
    public PrivacySettings Privacy { get; init; } = new();
    public HotkeySettings Hotkeys { get; init; } = new();
    public ControlPanelSettings ControlPanel { get; init; } = new();

    /// <summary>
    /// Per-mode settings for 2 profiles.
    /// Key format: "{mode}_{profile}" e.g. "dictate_0", "ask_1".
    /// 8 modes × 2 profiles = up to 16 entries.
    /// </summary>
    public Dictionary<string, ModeSettings> ModeProfiles { get; init; } = new();

    /// <summary>
    /// 16 custom prompt slots (indices 0-15). Null = use built-in default.
    /// </summary>
    public string?[] CustomPrompts { get; init; } = new string?[16];

    /// <summary>
    /// Currently active profile index (0 or 1). Maps to the ModeProfiles key suffix.
    /// </summary>
    public int ActiveProfile { get; init; } = 0;

    /// <summary>Notes file path for the Note pipeline.</summary>
    public string NotesFilePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "diktame-notes.md");

    /// <summary>Ollama model to use as the default local LLM.</summary>
    public string OllamaModel { get; init; } = "llama3.2";
}

/// <summary>
/// Source-generated JSON serialization context for <see cref="AppSettings"/>.
/// Required for IL-trim compatibility (PublishTrimmed).
/// </summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(GeneralSettings))]
[JsonSerializable(typeof(AudioSettings))]
[JsonSerializable(typeof(AudioDuckingSettings))]
[JsonSerializable(typeof(PrivacySettings))]
[JsonSerializable(typeof(HotkeySettings))]
[JsonSerializable(typeof(ControlPanelSettings))]
[JsonSerializable(typeof(ModeSettings))]
[JsonSerializable(typeof(Dictionary<string, ModeSettings>))]
public partial class AppSettingsContext : JsonSerializerContext { }
