using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

public sealed class DictationModeDefaultsTests
{
    // ── Default Preset ────────────────────────────────────────────────────

    [Fact]
    public void CreateDefaultPreset_ReturnsSingleStandardPreset()
    {
        var preset = DictationModeDefaults.CreateDefaultPreset();

        preset.Should().NotBeNull();
        preset.Title.Should().Be("Standard");
        preset.Id.Should().NotBeNullOrWhiteSpace();
        preset.SortOrder.Should().Be(0);
    }

    [Fact]
    public void CreateDefaultPreset_HasUniqueIdPerCall()
    {
        var preset1 = DictationModeDefaults.CreateDefaultPreset();
        var preset2 = DictationModeDefaults.CreateDefaultPreset();

        preset1.Id.Should().NotBe(preset2.Id, "each call should generate a unique GUID");
    }

    [Fact]
    public void CreateDefaultPreset_CloudProfile_UsesDefaultPrompt()
    {
        var preset = DictationModeDefaults.CreateDefaultPreset();

        preset.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        preset.CloudProfile.UseLlm.Should().BeTrue();
        preset.CloudProfile.ModelName.Should().Be("gpt-4o-mini");
        preset.CloudProfile.Hotkey.Should().Be("Ctrl+Alt+D");
    }

    [Fact]
    public void CreateDefaultPreset_LocalProfile_UsesDefaultPrompt()
    {
        var preset = DictationModeDefaults.CreateDefaultPreset();

        preset.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        preset.LocalProfile.UseLlm.Should().BeTrue();
        preset.LocalProfile.ModelName.Should().BeNull("Local profile uses global Ollama model");
        preset.LocalProfile.Hotkey.Should().Be("Ctrl+Alt+D");
    }

    // ── Utility Pipelines (Modes) ─────────────────────────────────────────

    [Fact]
    public void CreateBuiltInUtilityPipelines_ReturnsExpectedCount()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        pipelines.Should().HaveCount(7);
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_HasAllExpectedTypes()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var types = pipelines.Select(p => p.PipelineType).ToList();
        types.Should().Contain("ask");
        types.Should().Contain("refine");
        types.Should().Contain("refine_auto");
        types.Should().Contain("refine_instruction");
        types.Should().Contain("translate");
        types.Should().Contain("note");
        types.Should().Contain("chat");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_AskPipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var ask = pipelines.Should().ContainSingle(p => p.PipelineType == "ask").Which;
        ask.Hotkey.Should().Be("Ctrl+Alt+A");
        ask.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Ask);
        ask.CloudProfile.ModelName.Should().Be("gpt-4o-mini");
        ask.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Ask);
        ask.LocalProfile.ModelName.Should().BeNull();
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_TranslatePipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var translate = pipelines.Should().ContainSingle(p => p.PipelineType == "translate").Which;
        translate.Hotkey.Should().Be("Ctrl+Alt+T");
        translate.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Translate);
        translate.CloudProfile.ModelName.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_NotePipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var note = pipelines.Should().ContainSingle(p => p.PipelineType == "note").Which;
        note.Hotkey.Should().Be("Ctrl+Alt+N");
        note.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Note);
        note.CloudProfile.ModelName.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_ChatPipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var chat = pipelines.Should().ContainSingle(p => p.PipelineType == "chat").Which;
        chat.Hotkey.Should().Be("Ctrl+Alt+C");
        chat.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Chat);
        chat.CloudProfile.ModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_CloudProfilesHaveModels_LocalProfilesDoNot()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        pipelines.Should().OnlyContain(p => !string.IsNullOrEmpty(p.CloudProfile.ModelName));
        foreach (var pipeline in pipelines)
        {
            pipeline.LocalProfile.ModelName.Should().BeNull();
        }
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_IsIdempotent()
    {
        var pipelines1 = DictationModeDefaults.CreateBuiltInUtilityPipelines();
        var pipelines2 = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        pipelines1.Should().HaveCount(pipelines2.Count);

        for (int i = 0; i < pipelines1.Count; i++)
        {
            pipelines1[i].PipelineType.Should().Be(pipelines2[i].PipelineType);
            pipelines1[i].Hotkey.Should().Be(pipelines2[i].Hotkey);
        }
    }
}
