namespace DiktaMe.Core.Config;

using System.Text.Json;
using Serilog;

/// <summary>
/// Persists and loads <see cref="AppSettings"/> as JSON at
/// <c>%APPDATA%\DiktaMe\settings.json</c>.
/// Provides defaults for missing fields, schema migration from V1,
/// and observable change notification for MVVM.
/// </summary>
public sealed class SettingsManager
{
    /// <summary>Path to the settings file.</summary>
    public static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "settings.json");

    private AppSettings _current = new();

    /// <summary>The currently loaded settings.</summary>
    public AppSettings Current
    {
        get => _current;
        private set
        {
            _current = value;
            SettingsChanged?.Invoke(this, value);
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
            Log.Information("SettingsManager: no settings file found — creating defaults");
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

            Current = MigrateIfNeeded(loaded);
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
            Directory.CreateDirectory(dir);

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

    private static AppSettings MigrateIfNeeded(AppSettings loaded)
    {
        // Currently only schema version 1 exists — nothing to migrate.
        return loaded;
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
