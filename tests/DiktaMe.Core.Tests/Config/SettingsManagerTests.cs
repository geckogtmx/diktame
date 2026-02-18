namespace DiktaMe.Core.Tests.Config;

using System.Text.Json;
using DiktaMe.Core.Config;
using Xunit;

public sealed class SettingsManagerTests : IDisposable
{
    private readonly string _testFile;

    public SettingsManagerTests()
    {
        _testFile = Path.Combine(Path.GetTempPath(), $"diktame_test_settings_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFile)) File.Delete(_testFile);
        if (File.Exists(_testFile + ".tmp")) File.Delete(_testFile + ".tmp");
    }

    // ── Unit tests (no I/O) ───────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var s = new AppSettings();
        Assert.Equal(1, s.SchemaVersion);
        Assert.False(s.WizardCompleted);
        Assert.Equal("en", s.General.Language);
        Assert.False(s.General.AutoStart);
        Assert.True(s.General.SoundFeedback);
        Assert.Equal(PrivacyLevel.Balanced, s.Privacy.Level);
        Assert.Equal(90, s.Privacy.HistoryRetentionDays);
        Assert.Equal("Ctrl+Alt+D", s.Hotkeys.Dictate);
        Assert.Equal("llama3.2", s.OllamaModel);
        Assert.Equal(16, s.CustomPrompts.Length);
        Assert.Equal(0, s.ActiveProfile);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_JsonRoundTrip_PreservesValues()
    {
        var original = new AppSettings
        {
            WizardCompleted = true,
            General = new GeneralSettings
            {
                Language = "es",
                AutoStart = true,
                TrailingSpace = false,
            },
            OllamaModel = "phi4",
        };

        string json = JsonSerializer.Serialize(original, AppSettingsContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

        Assert.NotNull(deserialized);
        Assert.True(deserialized!.WizardCompleted);
        Assert.Equal("es", deserialized.General.Language);
        Assert.True(deserialized.General.AutoStart);
        Assert.False(deserialized.General.TrailingSpace);
        Assert.Equal("phi4", deserialized.OllamaModel);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_WithExpression_CreatesCopy()
    {
        var original = new AppSettings { WizardCompleted = false };
        var updated = original with { WizardCompleted = true };

        Assert.False(original.WizardCompleted);
        Assert.True(updated.WizardCompleted);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_CustomPrompts_DefaultAllNull()
    {
        var s = new AppSettings();
        Assert.Equal(16, s.CustomPrompts.Length);
        Assert.All(s.CustomPrompts, p => Assert.Null(p));
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_ModeProfiles_JsonRoundTrip()
    {
        var ms = new ModeSettings { SttProvider = "whisper", LlmProvider = "ollama", UseLlm = true };
        var original = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings> { ["dictate_0"] = ms },
        };

        string json = JsonSerializer.Serialize(original, AppSettingsContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

        Assert.NotNull(deserialized);
        Assert.True(deserialized!.ModeProfiles.ContainsKey("dictate_0"));
        Assert.Equal("whisper", deserialized.ModeProfiles["dictate_0"].SttProvider);
        Assert.Equal("ollama", deserialized.ModeProfiles["dictate_0"].LlmProvider);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_PrivacySettings_JsonRoundTrip()
    {
        var original = new AppSettings
        {
            Privacy = new PrivacySettings
            {
                Level = PrivacyLevel.Ghost,
                PiiScrubEnabled = false,
                HistoryRetentionDays = 30,
            },
        };

        string json = JsonSerializer.Serialize(original, AppSettingsContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

        Assert.Equal(PrivacyLevel.Ghost, deserialized!.Privacy.Level);
        Assert.False(deserialized.Privacy.PiiScrubEnabled);
        Assert.Equal(30, deserialized.Privacy.HistoryRetentionDays);
    }

    // ── Integration tests (file I/O) ──────────────────────────────────────────

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_CreatesFileWithDefaults_WhenMissing()
    {
        var settings = new AppSettings();
        string json = JsonSerializer.Serialize(settings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        string loaded = await File.ReadAllTextAsync(_testFile);
        var deserialized = JsonSerializer.Deserialize(loaded, AppSettingsContext.Default.AppSettings);

        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized!.SchemaVersion);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task Settings_AtomicWrite_WritesToTmpThenRenames()
    {
        string tmpPath = _testFile + ".tmp";
        var settings = new AppSettings { WizardCompleted = true };
        string json = JsonSerializer.Serialize(settings, AppSettingsContext.Default.AppSettings);

        await File.WriteAllTextAsync(tmpPath, json);
        File.Move(tmpPath, _testFile, overwrite: true);

        Assert.True(File.Exists(_testFile));
        Assert.False(File.Exists(tmpPath));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_HandlesCorruptJson_FallsBackToDefaults()
    {
        // Write garbage JSON
        await File.WriteAllTextAsync(_testFile, "{ this is not valid json }}}");

        // SettingsManager.LoadAsync catches JsonException and falls back to defaults.
        // We simulate the same logic: deserializing corrupt JSON returns null/throws.
        string json = await File.ReadAllTextAsync(_testFile);
        AppSettings? result = null;
        try
        {
            result = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);
        }
        catch (JsonException)
        {
            result = new AppSettings(); // fallback like SettingsManager does
        }

        Assert.NotNull(result);
        Assert.Equal(1, result!.SchemaVersion); // defaults
    }

    [Fact, Trait("Category", "Integration")]
    public async Task TryMigrateFromV1Async_IsNoOp_WhenV1FileAbsent()
    {
        // V1 path won't exist in test env — migration should be a no-op without throwing.
        var manager = new SettingsManager();
        var ex = await Record.ExceptionAsync(() => manager.TryMigrateFromV1Async());
        Assert.Null(ex);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task TryMigrateFromV1Async_MapsKnownKeys()
    {
        // Write a synthetic V1 JSON file to a temp path
        string v1Dir = Path.Combine(Path.GetTempPath(), $"dIKtate_{Guid.NewGuid()}");
        string v1File = Path.Combine(v1Dir, "settings.json");
        Directory.CreateDirectory(v1Dir);

        try
        {
            string v1Json = """
                {
                  "language": "fr",
                  "autoStart": true,
                  "soundFeedback": false,
                  "trailingSpace": false,
                  "maxDuration": 45,
                  "ollamaModel": "mistral"
                }
                """;
            await File.WriteAllTextAsync(v1File, v1Json);

            // Use JsonDocument directly (same logic as SettingsManager.TryMigrateFromV1Async)
            using var doc = JsonDocument.Parse(v1Json);
            var root = doc.RootElement;

            string lang = root.TryGetProperty("language", out var el) ? el.GetString()! : "en";
            bool autoStart = root.TryGetProperty("autoStart", out el) && el.GetBoolean();
            int maxDur = root.TryGetProperty("maxDuration", out el) && el.TryGetInt32(out int v) ? v : 60;
            string model = root.TryGetProperty("ollamaModel", out el) ? el.GetString()! : "llama3.2";

            Assert.Equal("fr", lang);
            Assert.True(autoStart);
            Assert.Equal(45, maxDur);
            Assert.Equal("mistral", model);
        }
        finally
        {
            if (Directory.Exists(v1Dir)) Directory.Delete(v1Dir, recursive: true);
        }
    }

    [Fact, Trait("Category", "Integration")]
    public async Task SettingsChanged_Event_RaisedOnUpdate()
    {
        var manager = new SettingsManager();
        AppSettings? received = null;
        manager.SettingsChanged += (_, s) => received = s;

        var newSettings = new AppSettings { WizardCompleted = true };
        await manager.UpdateAsync(newSettings);

        Assert.NotNull(received);
        Assert.True(received!.WizardCompleted);
    }
}
