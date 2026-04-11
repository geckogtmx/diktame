
using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

[Trait("Category", "Unit")]
[Collection(nameof(SettingsWriterCollection))]
public sealed class PipelineConfigManagerTests
{
    private static (PipelineConfigManager Manager, SettingsManager Settings) Make(AppSettings? settings = null)
    {
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_pcm_{Guid.NewGuid()}.json"));
        var initial = settings ?? new AppSettings
        {
            UtilityPipelines =
            [
                new PipelineConfig
                {
                    PipelineType = "ask",
                    CloudProfile = new UtilityProfile { SystemPrompt = "Cloud ask prompt", ModelName = "gpt-4o" },
                    LocalProfile = new UtilityProfile { SystemPrompt = "Local ask prompt" },
                    Hotkey = "Ctrl+Alt+A",
                },
                new PipelineConfig
                {
                    PipelineType = "refine",
                    CloudProfile = new UtilityProfile { SystemPrompt = "Cloud refine prompt" },
                    LocalProfile = new UtilityProfile { SystemPrompt = "Local refine prompt" },
                },
            ],
            ActiveProfileName = "Cloud",
        };
        sm.UpdateAsync(initial).GetAwaiter().GetResult();
        return (new PipelineConfigManager(sm), sm);
    }

    [Fact]
    public void GetAllPipelines_ReturnsConfiguredPipelines()
    {
        var (mgr, _) = Make();

        var all = mgr.GetAllPipelines();

        all.Should().HaveCount(2);
        all.Select(p => p.PipelineType).Should().Contain(["ask", "refine"]);
    }

    [Fact]
    public void GetPipelineByType_Found_ReturnsPipeline()
    {
        var (mgr, _) = Make();

        var result = mgr.GetPipelineByType("ask");

        result.Should().NotBeNull();
        result!.PipelineType.Should().Be("ask");
        result.CloudProfile.SystemPrompt.Should().Be("Cloud ask prompt");
    }

    [Fact]
    public void GetPipelineByType_CaseInsensitive()
    {
        var (mgr, _) = Make();

        var result = mgr.GetPipelineByType("ASK");

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetPipelineByType_NotFound_ReturnsNull()
    {
        var (mgr, _) = Make();

        var result = mgr.GetPipelineByType("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void GetActiveProfile_Cloud_ReturnsCloudProfile()
    {
        var (mgr, _) = Make();

        var profile = mgr.GetActiveProfile("ask");

        profile.SystemPrompt.Should().Be("Cloud ask prompt");
        profile.ModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public void GetActiveProfile_Local_ReturnsLocalProfile()
    {
        var settings = new AppSettings
        {
            UtilityPipelines =
            [
                new PipelineConfig
                {
                    PipelineType = "ask",
                    CloudProfile = new UtilityProfile { SystemPrompt = "Cloud" },
                    LocalProfile = new UtilityProfile { SystemPrompt = "Local" },
                },
            ],
            ActiveProfileName = "Local",
        };
        var (mgr, _) = Make(settings);

        var profile = mgr.GetActiveProfile("ask");

        profile.SystemPrompt.Should().Be("Local");
    }

    [Fact]
    public void GetActiveProfile_NotFound_ThrowsInvalidOperation()
    {
        var (mgr, _) = Make();

        var act = () => mgr.GetActiveProfile("nonexistent");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdatePipelineAsync_PersistsChanges()
    {
        var (mgr, sm) = Make();
        var newCloud = new UtilityProfile { SystemPrompt = "Updated cloud prompt", ModelName = "gpt-5" };
        var newLocal = new UtilityProfile { SystemPrompt = "Updated local prompt" };

        await mgr.UpdatePipelineAsync("ask", newCloud, newLocal, hotkey: "Ctrl+Shift+A");

        var reloaded = mgr.GetPipelineByType("ask");
        reloaded.Should().NotBeNull();
        reloaded!.CloudProfile.SystemPrompt.Should().Be("Updated cloud prompt");
        reloaded.CloudProfile.ModelName.Should().Be("gpt-5");
        reloaded.LocalProfile.SystemPrompt.Should().Be("Updated local prompt");
        reloaded.Hotkey.Should().Be("Ctrl+Shift+A");
    }

    [Fact]
    public async Task UpdatePipelineAsync_NotFound_ThrowsInvalidOperation()
    {
        var (mgr, _) = Make();
        var profile = new UtilityProfile { SystemPrompt = "Test" };

        var act = () => mgr.UpdatePipelineAsync("nonexistent", profile, profile);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
