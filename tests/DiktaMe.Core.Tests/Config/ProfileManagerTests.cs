
using DiktaMe.Core.Config;
using DiktaMe.Core.Tests;
using Xunit;

namespace DiktaMe.Core.Tests.Config;
[Trait("Category", "Unit")]
[Collection(nameof(SettingsWriterCollection))]
public sealed class ProfileManagerTests
{
    private static (SettingsManager Settings, ProfileManager Manager) Make(AppSettings? initial = null)
    {
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_profile_{Guid.NewGuid()}.json"));
        if (initial is not null)
        {
            sm.UpdateAsync(initial).GetAwaiter().GetResult();
        }
        return (sm, new ProfileManager(sm));
    }

    // ── GetModeSettings ───────────────────────────────────────────────────────

    [Fact]
    public void GetModeSettings_MissingKey_ReturnsDefault()
    {
        var (_, manager) = Make();
        var ms = manager.GetModeSettings("dictate");
        // Default ModeSettings
        Assert.Equal("deepgram", ms.SttProvider);
        Assert.Equal("gemini", ms.LlmProvider);
        Assert.True(ms.UseLlm);
    }

    [Fact]
    public void GetModeSettings_ConfiguredKey_ReturnsConfigured()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["dictate_0"] = new ModeSettings { SttProvider = "whisper", LlmProvider = "ollama" },
            },
        };
        var (_, manager) = Make(settings);

        var ms = manager.GetModeSettings("dictate");
        Assert.Equal("whisper", ms.SttProvider);
        Assert.Equal("ollama", ms.LlmProvider);
    }

    [Fact]
    public void GetModeSettings_WithExplicitProfile_ReturnsCorrect()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["refine_0"] = new ModeSettings { SttProvider = "deepgram" },
                ["refine_1"] = new ModeSettings { SttProvider = "gemini-audio" },
            },
        };
        var (_, manager) = Make(settings);

        Assert.Equal("deepgram", manager.GetModeSettings("refine", 0).SttProvider);
        Assert.Equal("gemini-audio", manager.GetModeSettings("refine", 1).SttProvider);
    }

    // ── SetModeSettings ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetModeSettingsAsync_PersistsNewSettings()
    {
        var (_, manager) = Make();
        var newMs = new ModeSettings { SttProvider = "whisper", LlmProvider = "none", UseLlm = false };

        await manager.SetModeSettingsAsync("chat", 0, newMs);

        var retrieved = manager.GetModeSettings("chat", 0);
        Assert.Equal("whisper", retrieved.SttProvider);
        Assert.False(retrieved.UseLlm);
    }

    [Fact]
    public async Task SetModeSettingsAsync_OverwritesExisting()
    {
        var settings = new AppSettings
        {
            ModeProfiles = new Dictionary<string, ModeSettings>
            {
                ["ask_0"] = new ModeSettings { SttProvider = "deepgram" },
            },
        };
        var (_, manager) = Make(settings);

        await manager.SetModeSettingsAsync("ask", 0, new ModeSettings { SttProvider = "gemini-audio" });

        Assert.Equal("gemini-audio", manager.GetModeSettings("ask", 0).SttProvider);
    }

    [Fact]
    public async Task SetModeSettingsAsync_Profile1_DoesNotAffectProfile0()
    {
        var (_, manager) = Make();

        await manager.SetModeSettingsAsync("translate", 0, new ModeSettings { SttProvider = "deepgram" });
        await manager.SetModeSettingsAsync("translate", 1, new ModeSettings { SttProvider = "whisper" });

        Assert.Equal("deepgram", manager.GetModeSettings("translate", 0).SttProvider);
        Assert.Equal("whisper", manager.GetModeSettings("translate", 1).SttProvider);
    }
}
