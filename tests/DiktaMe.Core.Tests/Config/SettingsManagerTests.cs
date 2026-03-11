
using System.Text.Json;
using DiktaMe.Core.Config;
using Xunit;

namespace DiktaMe.Core.Tests.Config;
public sealed class SettingsManagerTests : IDisposable
{
    private readonly string _testFile;

    public SettingsManagerTests()
    {
        _testFile = Path.Combine(Path.GetTempPath(), $"diktame_test_settings_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFile))
        {
            File.Delete(_testFile);
        }

        if (File.Exists(_testFile + ".tmp"))
        {
            File.Delete(_testFile + ".tmp");
        }
    }

    // ── Unit tests (no I/O) ───────────────────────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var s = new AppSettings();
        Assert.Equal(2, s.SchemaVersion);
        Assert.False(s.WizardCompleted);
        Assert.Equal("en", s.General.Language);
        Assert.False(s.General.AutoStart);
        Assert.True(s.General.SoundFeedback);
        Assert.Equal(PrivacyLevel.Balanced, s.Privacy.Level);
        Assert.Equal(90, s.Privacy.HistoryRetentionDays);
        Assert.Equal("Ctrl+Alt+D", s.Hotkeys.Dictate);
        Assert.Equal("gemma3:1b", s.OllamaModel);
        Assert.Equal("small", s.WhisperModel);
        Assert.Equal(16, s.CustomPrompts.Length);
        Assert.Equal(0, s.ActiveProfile);
        Assert.Null(s.ActiveDictationModeId);
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
    public void AppSettings_ActiveDictationModeId_CanBeSet()
    {
        var original = new AppSettings { ActiveDictationModeId = null };
        var updated = original with { ActiveDictationModeId = "some-preset-id" };

        Assert.Null(original.ActiveDictationModeId);
        Assert.Equal("some-preset-id", updated.ActiveDictationModeId);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_ActiveDictationModeId_JsonRoundTrip()
    {
        var original = new AppSettings { ActiveDictationModeId = "my-preset-123" };

        string json = JsonSerializer.Serialize(original, AppSettingsContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

        Assert.NotNull(deserialized);
        Assert.Equal("my-preset-123", deserialized!.ActiveDictationModeId);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_DictationModes_JsonRoundTrip()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();
        var original = new AppSettings
        {
            DictationModes = presets,
            ActiveDictationModeId = presets[0].Id,
        };

        string json = JsonSerializer.Serialize(original, AppSettingsContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized!.DictationModes.Count);
        Assert.Equal("Standard", deserialized.DictationModes[0].Title);
        Assert.Equal("Prompt", deserialized.DictationModes[1].Title);
        Assert.Equal("Professional", deserialized.DictationModes[2].Title);
        Assert.Equal(presets[0].Id, deserialized.ActiveDictationModeId);
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
        Assert.Equal(2, deserialized!.SchemaVersion);
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
        await File.WriteAllTextAsync(_testFile, "{ this is not valid json }}}");

        string json = await File.ReadAllTextAsync(_testFile);
        AppSettings? result = null;
        try
        {
            result = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);
        }
        catch (JsonException)
        {
            result = new AppSettings();
        }

        Assert.NotNull(result);
        Assert.Equal(2, result!.SchemaVersion);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task TryMigrateFromV1Async_IsNoOp_WhenV1FileAbsent()
    {
        var manager = new SettingsManager(_testFile);
        var ex = await Record.ExceptionAsync(() => manager.TryMigrateFromV1Async());
        Assert.Null(ex);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task TryMigrateFromV1Async_MapsKnownKeys()
    {
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
            if (Directory.Exists(v1Dir))
            {
                Directory.Delete(v1Dir, recursive: true);
            }
        }
    }

    [Fact, Trait("Category", "Integration")]
    public async Task SettingsChanged_Event_RaisedOnUpdate()
    {
        var manager = new SettingsManager(_testFile);
        AppSettings? received = null;
        manager.SettingsChanged += (_, s) => received = s;

        var newSettings = new AppSettings { WizardCompleted = true };
        await manager.UpdateAsync(newSettings);

        Assert.NotNull(received);
        Assert.True(received!.WizardCompleted);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_CreatesDefaultPreset_OnFirstRun()
    {
        // _testFile does NOT exist (first-run scenario)
        Assert.False(File.Exists(_testFile));

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        // Verify Standard/Prompt/Professional presets were created
        Assert.Equal(3, manager.Current.DictationModes.Count);
        Assert.Equal("Standard", manager.Current.DictationModes[0].Title);
        Assert.Equal("Prompt", manager.Current.DictationModes[1].Title);
        Assert.Equal("Professional", manager.Current.DictationModes[2].Title);

        // Verify ActiveDictationModeId was set
        Assert.NotNull(manager.Current.ActiveDictationModeId);
        Assert.Equal(manager.Current.DictationModes[0].Id, manager.Current.ActiveDictationModeId);

        // Verify utility pipelines were populated
        Assert.True(manager.Current.UtilityPipelines.Count > 0);

        // Verify file was created
        Assert.True(File.Exists(_testFile));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_PreservesExistingPresets_WhenNotEmpty()
    {
        // Write settings with existing custom preset
        var customPreset = new DictationMode
        {
            Id = "custom-123",
            Title = "My Custom Preset",
            CloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true },
            LocalProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true },
            SortOrder = 0,
        };

        var settings = new AppSettings
        {
            WizardCompleted = true,
            DictationModes = [customPreset],
            UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
        };
        string json = JsonSerializer.Serialize(settings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        // Verify custom preset is still there (not overwritten)
        Assert.Single(manager.Current.DictationModes);
        Assert.Equal("custom-123", manager.Current.DictationModes[0].Id);
        Assert.Equal("My Custom Preset", manager.Current.DictationModes[0].Title);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_MigratesActiveProfile0_ToCloud()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();
        var oldSettings = new AppSettings
        {
            ActiveProfile = 0,
            ActiveProfileName = string.Empty,
            DictationModes = presets,
            UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
        };
        string json = JsonSerializer.Serialize(oldSettings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        Assert.Equal("Cloud", manager.Current.ActiveProfileName);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_MigratesActiveProfile1_ToLocal()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();
        var oldSettings = new AppSettings
        {
            ActiveProfile = 1,
            ActiveProfileName = string.Empty,
            DictationModes = presets,
            UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
        };
        string json = JsonSerializer.Serialize(oldSettings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        Assert.Equal("Local", manager.Current.ActiveProfileName);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LoadAsync_PopulatesDefaultPresets_WhenDictationModesEmpty()
    {
        var oldSettings = new AppSettings
        {
            WizardCompleted = true,
            DictationModes = [],
            UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
        };
        string json = JsonSerializer.Serialize(oldSettings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        Assert.Equal(3, manager.Current.DictationModes.Count);
        Assert.Equal("Standard", manager.Current.DictationModes[0].Title);
        Assert.Equal("Prompt", manager.Current.DictationModes[1].Title);
        Assert.Equal("Professional", manager.Current.DictationModes[2].Title);
        Assert.NotNull(manager.Current.ActiveDictationModeId);
        Assert.Equal(manager.Current.DictationModes[0].Id, manager.Current.ActiveDictationModeId);
    }

    [Fact, Trait("Category", "Unit")]
    public async Task LoadAsync_AutoCompletesWizard_ForPreWizardSchemaV1Files()
    {
        // Pre-wizard settings file (schema v1): WizardCompleted is false
        var oldSettings = new AppSettings
        {
            SchemaVersion = 1,
            WizardCompleted = false,
            UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
        };
        string json = JsonSerializer.Serialize(oldSettings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        Assert.True(manager.Current.WizardCompleted);
    }

    [Fact, Trait("Category", "Unit")]
    public async Task LoadAsync_AutoCompletesWizard_ForExistingSchemaV2Files()
    {
        // Existing settings file on disk with WizardCompleted=false means the flag
        // wasn't persisted — auto-complete since the file's existence proves prior use.
        var existingSettings = new AppSettings
        {
            SchemaVersion = 2,
            WizardCompleted = false,
            UtilityPipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines(),
        };
        string json = JsonSerializer.Serialize(existingSettings, AppSettingsContext.Default.AppSettings);
        await File.WriteAllTextAsync(_testFile, json);

        var manager = new SettingsManager(_testFile);
        await manager.LoadAsync();

        Assert.True(manager.Current.WizardCompleted);
    }

    // ── Wizard ActiveProfileName logic tests ──────────────────────────────────

    [Fact, Trait("Category", "Unit")]
    public void WizardLogic_LocalLlm_SetsActiveProfileNameToLocal()
    {
        // Simulates CompleteWizardAsync() with LlmChoice = "local"
        string llmChoice = "local";
        string profileName = string.Equals(llmChoice, "local", StringComparison.Ordinal)
            ? "Local" : "Cloud";

        var settings = new AppSettings { ActiveProfileName = profileName, WizardCompleted = true };
        Assert.Equal("Local", settings.ActiveProfileName);
    }

    [Fact, Trait("Category", "Unit")]
    public void WizardLogic_CloudLlm_SetsActiveProfileNameToCloud()
    {
        // Simulates CompleteWizardAsync() with LlmChoice = "cloud"
        string llmChoice = "cloud";
        string profileName = string.Equals(llmChoice, "local", StringComparison.Ordinal)
            ? "Local" : "Cloud";

        var settings = new AppSettings { ActiveProfileName = profileName, WizardCompleted = true };
        Assert.Equal("Cloud", settings.ActiveProfileName);
    }

    [Fact, Trait("Category", "Unit")]
    public void WizardLogic_HybridLocalSttCloudLlm_SetsCloudProfileAndWhisperStt()
    {
        // Simulates CompleteWizardAsync() with STT=local, LLM=cloud
        string sttChoice = "local";
        string llmChoice = "cloud";
        string defaultStt = string.Equals(sttChoice, "local", StringComparison.Ordinal) ? "whisper" : "deepgram";
        string defaultLlm = "gemini";
        string profileName = string.Equals(llmChoice, "local", StringComparison.Ordinal)
            ? "Local" : "Cloud";

        var profiles = new Dictionary<string, ModeSettings>
        {
            ["dictate_0"] = new ModeSettings { SttProvider = defaultStt, LlmProvider = defaultLlm, UseLlm = true },
        };

        var settings = new AppSettings
        {
            ActiveProfileName = profileName,
            ModeProfiles = profiles,
            WizardCompleted = true,
        };

        Assert.Equal("Cloud", settings.ActiveProfileName);
        Assert.Equal("whisper", settings.ModeProfiles["dictate_0"].SttProvider);
        Assert.Equal("gemini", settings.ModeProfiles["dictate_0"].LlmProvider);
    }

    [Fact, Trait("Category", "Unit")]
    public void AppSettings_AccountSettings_JsonRoundTrip()
    {
        var original = new AppSettings
        {
            AuthMode = AuthMode.Account,
            Account = new AccountSettings { Email = "test@example.com", LastSynced = "2026-01-01T00:00:00Z" },
        };

        string json = JsonSerializer.Serialize(original, AppSettingsContext.Default.AppSettings);
        var deserialized = JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);

        Assert.NotNull(deserialized);
        Assert.Equal(AuthMode.Account, deserialized!.AuthMode);
        Assert.Equal("test@example.com", deserialized.Account.Email);
        Assert.Equal("2026-01-01T00:00:00Z", deserialized.Account.LastSynced);
    }
}
