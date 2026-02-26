using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

public sealed class DictationModeDefaultsTests
{
    // ── Built-in Dictation Modes ───────────────────────────────────────────

    [Fact]
    public void CreateBuiltInModes_ReturnsFourModes()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        modes.Should().HaveCount(4);
    }

    [Fact]
    public void CreateBuiltInModes_AllModesAreBuiltIn()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        modes.Should().OnlyContain(m => m.IsBuiltIn);
    }

    [Fact]
    public void CreateBuiltInModes_AllModesHaveUniqueIds()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        modes.Select(m => m.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CreateBuiltInModes_AllModesHaveNonEmptyTitles()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        modes.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Title));
    }

    [Fact]
    public void CreateBuiltInModes_HasCorrectSortOrder()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        modes.Select(m => m.SortOrder).Should().BeInAscendingOrder();
        modes.Select(m => m.SortOrder).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void CreateBuiltInModes_StandardMode_HasCorrectConfiguration()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        var standard = modes.Should().ContainSingle(m => m.Id == "dictate-standard").Which;
        standard.Title.Should().Be("Standard");
        standard.SortOrder.Should().Be(0);
        standard.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        standard.CloudProfile.UseLlm.Should().BeTrue();
        standard.CloudProfile.ModelName.Should().Be("gpt-4o-mini");
        standard.CloudProfile.Hotkey.Should().Be("Ctrl+Alt+D");
        standard.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        standard.LocalProfile.UseLlm.Should().BeTrue();
        standard.LocalProfile.ModelName.Should().BeNull();
        standard.LocalProfile.Hotkey.Should().Be("Ctrl+Alt+D");
    }

    [Fact]
    public void CreateBuiltInModes_PromptMode_HasCorrectConfiguration()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        var prompt = modes.Should().ContainSingle(m => m.Id == "dictate-prompt").Which;
        prompt.Title.Should().Be("Prompt");
        prompt.SortOrder.Should().Be(1);
        prompt.CloudProfile.SystemPrompt.Should().Contain("custom instruction");
        prompt.CloudProfile.UseLlm.Should().BeTrue();
        prompt.CloudProfile.ModelName.Should().Be("gpt-4o");
        prompt.CloudProfile.Hotkey.Should().Be("Ctrl+Alt+P");
    }

    [Fact]
    public void CreateBuiltInModes_ProfessionalMode_HasCorrectConfiguration()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        var professional = modes.Should().ContainSingle(m => m.Id == "dictate-professional").Which;
        professional.Title.Should().Be("Professional");
        professional.SortOrder.Should().Be(2);
        professional.CloudProfile.SystemPrompt.Should().Contain("professional business writing");
        professional.CloudProfile.UseLlm.Should().BeTrue();
        professional.CloudProfile.ModelName.Should().Be("claude-sonnet-4");
        professional.CloudProfile.Hotkey.Should().Be("Ctrl+Alt+Shift+D");
    }

    [Fact]
    public void CreateBuiltInModes_RawMode_HasCorrectConfiguration()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        var raw = modes.Should().ContainSingle(m => m.Id == "dictate-raw").Which;
        raw.Title.Should().Be("RAW");
        raw.SortOrder.Should().Be(3);
        raw.CloudProfile.SystemPrompt.Should().BeNull();
        raw.CloudProfile.UseLlm.Should().BeFalse();
        raw.CloudProfile.ModelName.Should().BeNull();
        raw.CloudProfile.Hotkey.Should().Be("Ctrl+Alt+R");
        raw.LocalProfile.SystemPrompt.Should().BeNull();
        raw.LocalProfile.UseLlm.Should().BeFalse();
    }

    [Fact]
    public void CreateBuiltInModes_AllNonRawModesUsePromptDefaults()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        var standard = modes.First(m => string.Equals(m.Id, "dictate-standard", StringComparison.Ordinal));
        standard.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
        standard.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Dictate);
    }

    [Fact]
    public void CreateBuiltInModes_CloudProfilesHaveModels_LocalProfilesDoNot()
    {
        var modes = DictationModeDefaults.CreateBuiltInModes();

        var nonRawModes = modes.Where(m => m.CloudProfile.UseLlm).ToList();

        nonRawModes.Should().OnlyContain(m => !string.IsNullOrEmpty(m.CloudProfile.ModelName));
        // Cannot use "is null" in expression tree - use null check instead
        foreach (var mode in nonRawModes)
        {
            mode.LocalProfile.ModelName.Should().BeNull();
        }
    }

    // ── Built-in Utility Pipelines ──────────────────────────────────────────

    [Fact]
    public void CreateBuiltInUtilityPipelines_ReturnsFivePipelines()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        pipelines.Should().HaveCount(5);
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_HasAllExpectedTypes()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var types = pipelines.Select(p => p.PipelineType).ToList();
        types.Should().Contain("ask");
        types.Should().Contain("refine");
        types.Should().Contain("translate");
        types.Should().Contain("note");
        types.Should().Contain("chat");
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_AllHaveHotkeys()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        pipelines.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Hotkey));
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
    public void CreateBuiltInUtilityPipelines_RefinePipeline_HasCorrectConfiguration()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var refine = pipelines.Should().ContainSingle(p => p.PipelineType == "refine").Which;
        refine.Hotkey.Should().Be("Ctrl+Alt+F");
        refine.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Refine);
        refine.CloudProfile.ModelName.Should().Be("gpt-4o-mini");
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
        // Cannot use "is null" in expression tree - use null check instead
        foreach (var pipeline in pipelines)
        {
            pipeline.LocalProfile.ModelName.Should().BeNull();
        }
    }

    [Fact]
    public void CreateBuiltInUtilityPipelines_AllUsePromptsFromPromptDefaults()
    {
        var pipelines = DictationModeDefaults.CreateBuiltInUtilityPipelines();

        var ask = pipelines.First(p => string.Equals(p.PipelineType, "ask", StringComparison.Ordinal));
        ask.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Ask);
        ask.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Ask);

        var refine = pipelines.First(p => string.Equals(p.PipelineType, "refine", StringComparison.Ordinal));
        refine.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Refine);
        refine.LocalProfile.SystemPrompt.Should().Be(PromptDefaults.Refine);

        var translate = pipelines.First(p => string.Equals(p.PipelineType, "translate", StringComparison.Ordinal));
        translate.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Translate);

        var note = pipelines.First(p => string.Equals(p.PipelineType, "note", StringComparison.Ordinal));
        note.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Note);

        var chat = pipelines.First(p => string.Equals(p.PipelineType, "chat", StringComparison.Ordinal));
        chat.CloudProfile.SystemPrompt.Should().Be(PromptDefaults.Chat);
    }

    // ── Idempotency ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateBuiltInModes_IsIdempotent()
    {
        var modes1 = DictationModeDefaults.CreateBuiltInModes();
        var modes2 = DictationModeDefaults.CreateBuiltInModes();

        modes1.Should().HaveCount(modes2.Count);

        for (int i = 0; i < modes1.Count; i++)
        {
            modes1[i].Id.Should().Be(modes2[i].Id);
            modes1[i].Title.Should().Be(modes2[i].Title);
            modes1[i].SortOrder.Should().Be(modes2[i].SortOrder);
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
