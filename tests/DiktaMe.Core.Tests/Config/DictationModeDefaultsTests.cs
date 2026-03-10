using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

public sealed class DictationModeDefaultsTests
{
    // ── Default Presets ───────────────────────────────────────────────────

    [Fact]
    public void CreateDefaultPresets_ReturnsThreePresets()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();

        presets.Should().HaveCount(3);
        presets[0].Title.Should().Be("Standard");
        presets[1].Title.Should().Be("Prompt");
        presets[2].Title.Should().Be("Professional");
    }

    [Fact]
    public void CreateDefaultPresets_HasCorrectSortOrder()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();

        presets[0].SortOrder.Should().Be(0);
        presets[1].SortOrder.Should().Be(1);
        presets[2].SortOrder.Should().Be(2);
    }

    [Fact]
    public void CreateDefaultPresets_HasUniqueIdsPerCall()
    {
        var presets1 = DictationModeDefaults.CreateDefaultPresets();
        var presets2 = DictationModeDefaults.CreateDefaultPresets();

        foreach (var p1 in presets1)
        {
            foreach (var p2 in presets2)
            {
                p1.Id.Should().NotBe(p2.Id, "each call should generate unique GUIDs");
            }
        }
    }

    [Fact]
    public void CreateDefaultPresets_StandardPreset_UsesCorrectPrompt()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();
        var standard = presets[0];

        standard.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        standard.CloudProfile.UseLlm.Should().BeTrue();
        standard.CloudProfile.ModelName.Should().Be("gemini-2.5-flash");
        standard.CloudProfile.Hotkey.Should().Be("Ctrl+Alt+D");

        standard.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        standard.LocalProfile.UseLlm.Should().BeTrue();
        standard.LocalProfile.ModelName.Should().BeNull("Local profile uses global Ollama model");
        standard.LocalProfile.Hotkey.Should().Be("Ctrl+Alt+D");
    }

    [Fact]
    public void CreateDefaultPresets_PromptPreset_UsesCorrectPrompt()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();
        var prompt = presets[1];

        prompt.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.DictatePrompt);
        prompt.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.DictatePrompt);
    }

    [Fact]
    public void CreateDefaultPresets_ProfessionalPreset_UsesCorrectPrompt()
    {
        var presets = DictationModeDefaults.CreateDefaultPresets();
        var professional = presets[2];

        professional.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.DictateProfessional);
        professional.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.DictateProfessional);
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
        ask.CloudProfile.ModelName.Should().Be("gemini-2.5-flash");
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
        translate.CloudProfile.ModelName.Should().Be("gemini-2.5-flash");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_NotePipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var note = pipelines.Should().ContainSingle(p => p.PipelineType == "note").Which;
        note.Hotkey.Should().Be("Ctrl+Alt+N");
        note.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Note);
        note.CloudProfile.ModelName.Should().Be("gemini-2.5-flash");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_ChatPipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var chat = pipelines.Should().ContainSingle(p => p.PipelineType == "chat").Which;
        chat.Hotkey.Should().Be("Ctrl+Alt+C");
        chat.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Chat);
        chat.CloudProfile.ModelName.Should().Be("gemini-2.5-flash");
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
