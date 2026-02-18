namespace DiktaMe.Core.Tests.Config;

using DiktaMe.Core.Config;
using DiktaMe.Core.Tests;
using Xunit;

[Trait("Category", "Unit")]
[Collection(nameof(SettingsWriterCollection))]
public sealed class PromptRepositoryTests
{
    private static (SettingsManager Settings, PromptRepository Repo) Make()
    {
        var sm = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_prompt_{Guid.NewGuid()}.json"));
        return (sm, new PromptRepository(sm));
    }

    // ── GetPrompt ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetPrompt_DefaultSlots_AllNull()
    {
        var (_, repo) = Make();

        for (int i = 0; i < PromptRepository.SlotCount; i++)
        {
            Assert.Null(repo.GetPrompt(i));
        }
    }

    [Fact]
    public void GetPrompt_InvalidSlot_Throws()
    {
        var (_, repo) = Make();

        Assert.Throws<ArgumentOutOfRangeException>(() => repo.GetPrompt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => repo.GetPrompt(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => repo.GetPrompt(100));
    }

    // ── SetPrompt ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetPromptAsync_StoresAndRetrieves()
    {
        var (_, repo) = Make();

        await repo.SetPromptAsync(0, "You are a helpful assistant.");
        Assert.Equal("You are a helpful assistant.", repo.GetPrompt(0));
    }

    [Fact]
    public async Task SetPromptAsync_LastSlot_Works()
    {
        var (_, repo) = Make();
        int last = PromptRepository.SlotCount - 1;

        await repo.SetPromptAsync(last, "Custom prompt for slot 15.");
        Assert.Equal("Custom prompt for slot 15.", repo.GetPrompt(last));
    }

    [Fact]
    public async Task SetPromptAsync_MultipleSlots_Independent()
    {
        var (_, repo) = Make();

        await repo.SetPromptAsync(0, "Prompt A");
        await repo.SetPromptAsync(5, "Prompt B");
        await repo.SetPromptAsync(10, "Prompt C");

        Assert.Equal("Prompt A", repo.GetPrompt(0));
        Assert.Equal("Prompt B", repo.GetPrompt(5));
        Assert.Equal("Prompt C", repo.GetPrompt(10));
        Assert.Null(repo.GetPrompt(1));
    }

    [Fact]
    public async Task SetPromptAsync_OverwritesExisting()
    {
        var (_, repo) = Make();

        await repo.SetPromptAsync(3, "Original prompt");
        await repo.SetPromptAsync(3, "Updated prompt");

        Assert.Equal("Updated prompt", repo.GetPrompt(3));
    }

    [Fact]
    public async Task SetPromptAsync_InvalidSlot_Throws()
    {
        var (_, repo) = Make();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.SetPromptAsync(-1, "x"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.SetPromptAsync(16, "x"));
    }

    // ── ResetToDefault ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetToDefaultAsync_ClearsSlot()
    {
        var (_, repo) = Make();

        await repo.SetPromptAsync(2, "Some prompt");
        await repo.ResetToDefaultAsync(2);

        Assert.Null(repo.GetPrompt(2));
    }

    [Fact]
    public async Task ResetToDefaultAsync_OtherSlotsUnaffected()
    {
        var (_, repo) = Make();

        await repo.SetPromptAsync(0, "Keep this");
        await repo.SetPromptAsync(1, "Reset this");
        await repo.ResetToDefaultAsync(1);

        Assert.Equal("Keep this", repo.GetPrompt(0));
        Assert.Null(repo.GetPrompt(1));
    }

    [Fact]
    public async Task ResetToDefaultAsync_InvalidSlot_Throws()
    {
        var (_, repo) = Make();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ResetToDefaultAsync(16));
    }

    // ── GetAllPrompts ─────────────────────────────────────────────────────────

    [Fact]
    public void GetAllPrompts_Returns16Slots()
    {
        var (_, repo) = Make();
        var all = repo.GetAllPrompts();
        Assert.Equal(16, all.Length);
    }

    [Fact]
    public async Task GetAllPrompts_ReflectsStoredValues()
    {
        var (_, repo) = Make();

        await repo.SetPromptAsync(7, "Lucky prompt");

        var all = repo.GetAllPrompts();
        Assert.Equal("Lucky prompt", all[7]);
        Assert.Null(all[0]);
    }

    [Fact]
    public void GetAllPrompts_ReturnsClone_MutationDoesNotAffectStorage()
    {
        var (_, repo) = Make();

        var all = repo.GetAllPrompts();
        all[0] = "tampered";

        Assert.Null(repo.GetPrompt(0)); // original unchanged
    }

    // ── SlotCount constant ────────────────────────────────────────────────────

    [Fact]
    public void SlotCount_Is16()
    {
        Assert.Equal(16, PromptRepository.SlotCount);
    }
}
