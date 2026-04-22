
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.Config;
/// <summary>
/// Persists and loads <see cref="AppSettings"/> as JSON at
/// <c>%APPDATA%\DiktaMe\settings.json</c> (or a custom path for tests).
/// Provides defaults for missing fields, schema migration from V1,
/// and observable change notification for MVVM.
/// </summary>
public sealed class SettingsManager
{
    /// <summary>Default path to the settings file.</summary>
    public static readonly string DefaultSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "settings.json");

    /// <summary>Path to the settings file used by this instance.</summary>
    public string SettingsFilePath { get; }

    /// <summary>Initializes a new instance. Pass a custom path in tests to avoid shared-file collisions.</summary>
    public SettingsManager(string? filePath = null)
    {
        SettingsFilePath = filePath ?? DefaultSettingsFilePath;
    }

    private AppSettings _current = new();

    /// <summary>The currently loaded settings.</summary>
    public AppSettings Current
    {
        get => _current;
        private set
        {
            _current = value;
            try
            {
                SettingsChanged?.Invoke(this, value);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SettingsManager: SettingsChanged subscriber threw — settings loaded, notification partially failed");
            }
        }
    }

    /// <summary>Raised whenever settings are reloaded or saved.</summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads settings from disk. Creates the file with defaults if it does not exist.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsFilePath))
        {
            Log.Information("SettingsManager: no settings file found — creating defaults with Standard/Prompt/Professional presets");
            var defaultPresets = DictationModeDefaults.CreateDefaultPresets();
            Current = new AppSettings
            {
                DictationModes = defaultPresets,
                ActiveDictationModeId = defaultPresets[0].Id,
                UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
            };
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken)
                .ConfigureAwait(false);

            var loaded = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);
            if (loaded is null)
            {
                Log.Warning("SettingsManager: deserialized null — using defaults");
                Current = new AppSettings();
                return;
            }

            Current = SanitizeNulls(MigrateIfNeeded(loaded));
            Log.Information("SettingsManager: loaded settings (schema v{V})", Current.SchemaVersion);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "SettingsManager: JSON parse error — using defaults");
            Current = new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SettingsManager: failed to load settings — using defaults");
            Current = new AppSettings();
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current settings to disk atomically (write-then-rename).
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        string? dir = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(Current, AppSettingsContext.Default.AppSettings);

        string tmpPath = SettingsFilePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tmpPath, SettingsFilePath, overwrite: true);

        Log.Debug("SettingsManager: saved settings to '{Path}'", SettingsFilePath);
    }

    /// <summary>
    /// Updates the current settings and saves to disk.
    /// </summary>
    public async Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Current = settings;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    // ── Migration ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to import settings from the V1 dIKtate electron-store JSON.
    /// V1 settings are at <c>%APPDATA%\dIKtate\settings.json</c>.
    /// No-op if the V1 file does not exist.
    /// </summary>
    public async Task TryMigrateFromV1Async(CancellationToken cancellationToken = default)
    {
        string v1Path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dIKtate", "settings.json");

        if (!File.Exists(v1Path))
        {
            Log.Debug("SettingsManager: no V1 settings file at '{Path}' — skipping migration", v1Path);
            return;
        }

        try
        {
            string v1Json = await File.ReadAllTextAsync(v1Path, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(v1Json);
            var root = doc.RootElement;

            // Map known V1 keys to V2 settings
            var migrated = new AppSettings
            {
                WizardCompleted = true,  // V1 users don't need the wizard
                General = new GeneralSettings
                {
                    Language = GetString(root, "language", "en"),
                    AutoStart = GetBool(root, "autoStart", false),
                    SoundFeedback = GetBool(root, "soundFeedback", true),
                    AdditionalKey = GetString(root, "additionalKey", string.Empty),
                    TrailingSpace = GetBool(root, "trailingSpace", true),
                },
                Audio = new AudioSettings
                {
                    DeviceName = GetString(root, "audioDevice", string.Empty),
                    MaxDurationSeconds = GetInt(root, "maxDuration", 60),
                },
                OllamaModel = GetString(root, "ollamaModel", "llama3.2"),
                // Preload Standard/Prompt/Professional presets for V1 users
                DictationModes = DictationModeDefaults.CreateDefaultPresets(),
                UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
                ActiveProfileName = "Cloud", // V1 default
            };

            Current = migrated;
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("SettingsManager: migrated settings from V1 at '{Path}'", v1Path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SettingsManager: V1 migration failed — keeping defaults");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces null sub-objects with defaults after deserialization.
    /// JSON "Tts":null (or any other null sub-object) overrides the <c>= new()</c> initializer,
    /// causing NullReferenceException at runtime.
    /// </summary>
    private static AppSettings SanitizeNulls(AppSettings s)
    {
        s = s with
        {
            General = s.General ?? new(),
            Audio = s.Audio ?? new(),
            AudioDucking = s.AudioDucking ?? new(),
            Privacy = s.Privacy ?? new(),
            Hotkeys = s.Hotkeys ?? new(),
            ControlPanel = s.ControlPanel ?? new(),
            Note = s.Note ?? new(),
            Chat = s.Chat ?? new(),
            Sound = s.Sound ?? new(),
            Deepgram = s.Deepgram ?? new(),
            Tts = s.Tts ?? new(),
            CloudLlm = s.CloudLlm ?? new(),
            Vision = s.Vision ?? new(),
            Account = s.Account ?? new(),
        };

        // Protect individual hotkey strings from JSON null override.
        // JSON "ReadSelection":null overwrites the = "Ctrl+Alt+Q" init default with null,
        // causing HotkeyParser.TryParse(null) → "Hotkey Conflict" toast at startup.
        var h = s.Hotkeys;
        var defaults = new HotkeySettings();
        s = s with
        {
            Hotkeys = h with
            {
                Dictate = h.Dictate ?? defaults.Dictate,
                Refine = h.Refine ?? defaults.Refine,
                Ask = h.Ask ?? defaults.Ask,
                Translate = h.Translate ?? defaults.Translate,
                Oops = h.Oops ?? defaults.Oops,
                Note = h.Note ?? defaults.Note,
                Chat = h.Chat ?? defaults.Chat,
                ReadSelection = h.ReadSelection ?? defaults.ReadSelection,
                Vision = h.Vision ?? defaults.Vision,
            },
        };

        // Protect AccountSettings.WalletProxyUrl from JSON null override and enforce HTTPS.
        var acctDefaults = new AccountSettings();
        bool needsProxyReset = string.IsNullOrEmpty(s.Account.WalletProxyUrl)
            || (!s.Account.WalletProxyUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !s.Account.WalletProxyUrl.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                && !s.Account.WalletProxyUrl.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase));

        if (needsProxyReset)
        {
            if (!string.IsNullOrEmpty(s.Account.WalletProxyUrl))
            {
                Log.Warning("SettingsManager: WalletProxyUrl rejected (must be HTTPS or localhost), resetting to default");
            }

            s = s with
            {
                Account = s.Account with { WalletProxyUrl = acctDefaults.WalletProxyUrl },
            };
        }

        return s;
    }

    private static AppSettings MigrateIfNeeded(AppSettings loaded)
    {
        var migrated = loaded;

        // Populate utility pipelines if empty (Modes — ask/refine/translate/note/chat)
        if (migrated.UtilityPipelines.Count == 0)
        {
            migrated = migrated with
            {
                UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
            };
            Log.Information("SettingsManager: populated {Count} built-in utility pipelines", migrated.UtilityPipelines.Count);
        }

        // Migration 2: Convert old ActiveProfile (0/1) to ActiveProfileName ("Cloud"/"Local")
        if (string.IsNullOrEmpty(migrated.ActiveProfileName) && migrated.ActiveProfile >= 0)
        {
            migrated = migrated with
            {
                ActiveProfileName = migrated.ActiveProfile == 0 ? "Cloud" : "Local",
            };
            Log.Information("SettingsManager: migrated ActiveProfile={Index} to ActiveProfileName={Name}",
                loaded.ActiveProfile, migrated.ActiveProfileName);
        }

        // Migration 3: Add refine_auto and refine_instruction pipelines if missing (Session 9 — Modes page consolidation)
        bool hasRefineAuto = migrated.UtilityPipelines.Any(p => string.Equals(p.PipelineType, "refine_auto", StringComparison.Ordinal));
        bool hasRefineInstruction = migrated.UtilityPipelines.Any(p => string.Equals(p.PipelineType, "refine_instruction", StringComparison.Ordinal));

        if (!hasRefineAuto || !hasRefineInstruction)
        {
            var builtInPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();
            var updatedPipelines = new List<PipelineConfig>(migrated.UtilityPipelines);

            if (!hasRefineAuto)
            {
                var refineAuto = builtInPipelines.First(p => string.Equals(p.PipelineType, "refine_auto", StringComparison.Ordinal));
                updatedPipelines.Add(refineAuto);
                Log.Information("SettingsManager: added missing 'refine_auto' pipeline");
            }

            if (!hasRefineInstruction)
            {
                var refineInstruction = builtInPipelines.First(p => string.Equals(p.PipelineType, "refine_instruction", StringComparison.Ordinal));
                updatedPipelines.Add(refineInstruction);
                Log.Information("SettingsManager: added missing 'refine_instruction' pipeline");
            }

            migrated = migrated with
            {
                UtilityPipelines = updatedPipelines,
            };
        }

        // Migration 4: (removed — Trial system replaced by Wallet in K.8)

        // Migration 5: Ensure Account settings are never null
        if (migrated.Account is null)
        {
            migrated = migrated with { Account = new AccountSettings() };
            Log.Information("SettingsManager: initialized null Account settings to defaults");
        }

        // Migration 6: Ensure Deepgram settings are never null (pre-Phase1 files)
        if (migrated.Deepgram is null)
        {
            migrated = migrated with { Deepgram = new DeepgramSettings() };
            Log.Information("SettingsManager: initialized null Deepgram settings to defaults");
        }

        // Migration 7: Populate DictationModes if empty (pre-Stream-J or wizard-created files missing presets)
        if (migrated.DictationModes.Count == 0)
        {
            var defaultPresets = DictationModeDefaults.CreateDefaultPresets();
            migrated = migrated with
            {
                DictationModes = defaultPresets,
                ActiveDictationModeId = defaultPresets[0].Id,
            };
            Log.Information("SettingsManager: populated {Count} default dictation presets", defaultPresets.Count);
        }

        // Migration 9: Fix refine_instruction prompt missing {instruction} placeholder.
        // Earlier settings had a custom prompt without the placeholder, causing spoken
        // instructions ("translate this", "add bullets") to be silently dropped.
        var refineInstr = migrated.UtilityPipelines
            .FirstOrDefault(p => string.Equals(p.PipelineType, "refine_instruction", StringComparison.Ordinal));
        if (refineInstr is not null
            && refineInstr.CloudProfile.SystemPrompt is not null
            && !refineInstr.CloudProfile.SystemPrompt.Contains("{instruction}", StringComparison.Ordinal))
        {
            var builtIn = DictationModeDefaults.CreateBuiltInUtilityPipelines()
                .First(p => string.Equals(p.PipelineType, "refine_instruction", StringComparison.Ordinal));

            var updatedPipelines = migrated.UtilityPipelines
                .Select(p => string.Equals(p.PipelineType, "refine_instruction", StringComparison.Ordinal) ? builtIn : p)
                .ToList();

            migrated = migrated with { UtilityPipelines = updatedPipelines };
            Log.Information("SettingsManager: migrated 'refine_instruction' prompt to include {{instruction}} placeholder");
        }

        // Migration 10: Heal UseLlm=false on non-dictate profile slots. The old CP pill
        // incorrectly cascaded "Off" to all 6 modes, leaving Chat/Ask/Refine/Translate/Note
        // with UseLlm=false even after the pill went back to Cloud/Local. That state causes
        // PipelineFactory.GetProviders to return null LLM for non-dictate pipelines, NREing
        // at first use. The pill now only writes dictate slots; fix the stale state here.
        var modesToHeal = new[] { "refine", "ask", "translate", "note", "chat" };
        bool anyHealed = false;
        var healedProfiles = new Dictionary<string, ModeSettings>(migrated.ModeProfiles, StringComparer.Ordinal);
        foreach (var mode in modesToHeal)
        {
            for (int p = 0; p < 2; p++)
            {
                string key = $"{mode}_{p}";
                if (healedProfiles.TryGetValue(key, out var ms) && !ms.UseLlm)
                {
                    healedProfiles[key] = ms with { UseLlm = true };
                    anyHealed = true;
                }
            }
        }
        if (anyHealed)
        {
            migrated = migrated with { ModeProfiles = healedProfiles };
            Log.Information("SettingsManager: healed UseLlm=false on non-dictate profile slots (Migration 10)");
        }

        // Migration 8: Auto-complete wizard when a settings file already exists on disk.
        // If MigrateIfNeeded is called, the file was loaded from disk — the user has been
        // using the app. WizardCompleted=false in an existing file means the flag wasn't
        // persisted (e.g. wizard completed but save failed, or file predates wizard feature).
        // A truly first-run user has no file at all (handled by LoadAsync's !File.Exists branch).
        if (!migrated.WizardCompleted)
        {
            migrated = migrated with { WizardCompleted = true };
            Log.Information("SettingsManager: auto-completed wizard for existing settings file");
        }

        return migrated;
    }

    private static string GetString(JsonElement root, string key, string defaultValue)
        => root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? defaultValue
            : defaultValue;

    private static bool GetBool(JsonElement root, string key, bool defaultValue)
        => root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.True ||
           (root.TryGetProperty(key, out el) && el.ValueKind != JsonValueKind.False && defaultValue);

    private static int GetInt(JsonElement root, string key, int defaultValue)
        => root.TryGetProperty(key, out var el) && el.TryGetInt32(out int v) ? v : defaultValue;
}
