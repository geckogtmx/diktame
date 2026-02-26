using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

/// <summary>
/// Tests for DictationModeManager CRUD operations.
/// Uses real SettingsManager with temp file path to avoid collisions.
/// </summary>
[Collection(nameof(SettingsWriterCollection))]
public sealed class DictationModeManagerTests : IAsyncLifetime
{
    private readonly string _tempSettingsPath;
    private readonly SettingsManager _settings;
    private readonly DictationModeManager _manager;

    public DictationModeManagerTests()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"diktame-test-{Guid.NewGuid()}.json");
        _settings = new SettingsManager(_tempSettingsPath);
        _manager = new DictationModeManager(_settings);
    }

    public async Task InitializeAsync()
    {
        // Initialize with built-in modes
        var initial = _settings.Current with
        {
            DictationModes = DictationModeDefaults.CreateBuiltInModes(),
        };
        await _settings.UpdateAsync(initial);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_tempSettingsPath))
        {
            File.Delete(_tempSettingsPath);
        }
        return Task.CompletedTask;
    }

    // ── GetAllModes ───────────────────────────────────────────────────────────

    [Fact]
    public void GetAllModes_ReturnsBuiltInModes()
    {
        var modes = _manager.GetAllModes();

        modes.Should().HaveCount(4);
        modes.Should().OnlyContain(m => m.IsBuiltIn);
    }

    [Fact]
    public void GetAllModes_ReturnsSortedBySortOrder()
    {
        var modes = _manager.GetAllModes();

        modes.Select(m => m.SortOrder).Should().BeInAscendingOrder();
    }

    // ── GetModeById ───────────────────────────────────────────────────────────

    [Fact]
    public void GetModeById_ExistingMode_ReturnsMode()
    {
        var mode = _manager.GetModeById("dictate-standard");

        mode.Should().NotBeNull();
        mode!.Title.Should().Be("Standard");
    }

    [Fact]
    public void GetModeById_NonExistentMode_ReturnsNull()
    {
        var mode = _manager.GetModeById("nonexistent");

        mode.Should().BeNull();
    }

    // ── GetActiveProfile ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveProfile_CloudProfile_ReturnsCloudProfile()
    {
        // Ensure ActiveProfileName = "Cloud"
        var updated = _settings.Current with { ActiveProfileName = "Cloud" };
        await _settings.UpdateAsync(updated);

        var profile = _manager.GetActiveProfile("dictate-standard");

        profile.Should().NotBeNull();
        profile.ModelName.Should().Be("gpt-4o-mini"); // Cloud profile has model
    }

    [Fact]
    public async Task GetActiveProfile_LocalProfile_ReturnsLocalProfile()
    {
        // Set ActiveProfileName = "Local"
        var updated = _settings.Current with { ActiveProfileName = "Local" };
        await _settings.UpdateAsync(updated);

        var profile = _manager.GetActiveProfile("dictate-standard");

        profile.Should().NotBeNull();
        profile.ModelName.Should().BeNull(); // Local profile has no model
    }

    [Fact]
    public void GetActiveProfile_NonExistentMode_ThrowsInvalidOperationException()
    {
        var act = () => _manager.GetActiveProfile("nonexistent");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Mode 'nonexistent' not found");
    }

    // ── CreateModeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateModeAsync_ValidInput_CreatesMode()
    {
        var cloudProfile = new DictationProfile
        {
            SystemPrompt = "Test cloud prompt",
            UseLlm = true,
            ModelName = "gpt-4",
            Hotkey = "Ctrl+Alt+T",
        };

        var localProfile = new DictationProfile
        {
            SystemPrompt = "Test local prompt",
            UseLlm = true,
            ModelName = null,
            Hotkey = "Ctrl+Alt+T",
        };

        var newMode = await _manager.CreateModeAsync("Test Mode", cloudProfile, localProfile);

        newMode.Should().NotBeNull();
        newMode.Title.Should().Be("Test Mode");
        newMode.IsBuiltIn.Should().BeFalse();
        newMode.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(newMode.Id, out _).Should().BeTrue(); // ID is a GUID
    }

    [Fact]
    public async Task CreateModeAsync_ValidInput_AssignsNextSortOrder()
    {
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var newMode = await _manager.CreateModeAsync("Test Mode", cloudProfile, localProfile);

        // Built-in modes have SortOrder 0-3, custom mode should be 4
        newMode.SortOrder.Should().Be(4);
    }

    [Fact]
    public async Task CreateModeAsync_ValidInput_PersistsToSettings()
    {
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var newMode = await _manager.CreateModeAsync("Test Mode", cloudProfile, localProfile);

        // Reload settings from disk
        await _settings.LoadAsync();

        var reloaded = _manager.GetModeById(newMode.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Title.Should().Be("Test Mode");
    }

    [Fact]
    public async Task CreateModeAsync_EmptyTitle_ThrowsArgumentException()
    {
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var act = async () => await _manager.CreateModeAsync("", cloudProfile, localProfile);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── UpdateModeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateModeAsync_CustomMode_UpdatesSuccessfully()
    {
        // Create a custom mode first
        var cloudProfile = new DictationProfile { SystemPrompt = "Original", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Original", UseLlm = true };
        var created = await _manager.CreateModeAsync("Original Title", cloudProfile, localProfile);

        // Update it
        var updatedCloud = cloudProfile with { SystemPrompt = "Updated" };
        var updatedLocal = localProfile with { SystemPrompt = "Updated" };
        await _manager.UpdateModeAsync(created.Id, "Updated Title", updatedCloud, updatedLocal);

        // Verify changes
        var updated = _manager.GetModeById(created.Id);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Title");
        updated.CloudProfile.SystemPrompt.Should().Be("Updated");
        updated.LocalProfile.SystemPrompt.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateModeAsync_BuiltInMode_UpdatesSuccessfully()
    {
        var cloudProfile = new DictationProfile
        {
            SystemPrompt = "Modified prompt",
            UseLlm = true,
            ModelName = "gpt-4o",
            Hotkey = "Ctrl+Alt+D",
        };
        var localProfile = new DictationProfile
        {
            SystemPrompt = "Modified prompt",
            UseLlm = true,
            ModelName = null,
            Hotkey = "Ctrl+Alt+D",
        };

        await _manager.UpdateModeAsync("dictate-standard", "Modified Standard", cloudProfile, localProfile);

        var updated = _manager.GetModeById("dictate-standard");
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Modified Standard");
        updated.CloudProfile.SystemPrompt.Should().Be("Modified prompt");
        updated.IsBuiltIn.Should().BeTrue(); // Still marked as built-in
    }

    [Fact]
    public async Task UpdateModeAsync_NonExistentMode_ThrowsInvalidOperationException()
    {
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var act = async () => await _manager.UpdateModeAsync("nonexistent", "Title", cloudProfile, localProfile);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Mode 'nonexistent' not found");
    }

    [Fact]
    public async Task UpdateModeAsync_EmptyTitle_ThrowsArgumentException()
    {
        // Create a custom mode first
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var created = await _manager.CreateModeAsync("Original", cloudProfile, localProfile);

        var act = async () => await _manager.UpdateModeAsync(created.Id, "", cloudProfile, localProfile);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── DeleteModeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteModeAsync_CustomMode_RemovesFromList()
    {
        // Create a custom mode
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var created = await _manager.CreateModeAsync("To Delete", cloudProfile, localProfile);

        // Delete it
        await _manager.DeleteModeAsync(created.Id);

        // Verify it's gone
        var deleted = _manager.GetModeById(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteModeAsync_BuiltInMode_ThrowsInvalidOperationException()
    {
        var act = async () => await _manager.DeleteModeAsync("dictate-standard");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot delete built-in mode 'dictate-standard'");
    }

    [Fact]
    public async Task DeleteModeAsync_NonExistentMode_ThrowsInvalidOperationException()
    {
        var act = async () => await _manager.DeleteModeAsync("nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Mode 'nonexistent' not found");
    }

    // ── ReorderModesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderModesAsync_ValidOrder_UpdatesSortOrder()
    {
        var modes = _manager.GetAllModes();
        var currentIds = modes.Select(m => m.Id).ToList();

        // Reverse the order
        var reversedIds = currentIds.AsEnumerable().Reverse().ToList();

        await _manager.ReorderModesAsync(reversedIds);

        // Verify new order
        var reordered = _manager.GetAllModes();
        reordered.Select(m => m.Id).Should().Equal(reversedIds);
        reordered.Select(m => m.SortOrder).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public async Task ReorderModesAsync_MissingIds_ThrowsArgumentException()
    {
        var modes = _manager.GetAllModes();
        var incompleteIds = modes.Take(2).Select(m => m.Id).ToList();

        var act = async () => await _manager.ReorderModesAsync(incompleteIds);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Ordered ID list does not match existing modes*");
    }

    [Fact]
    public async Task ReorderModesAsync_ExtraIds_ThrowsArgumentException()
    {
        var modes = _manager.GetAllModes();
        var currentIds = modes.Select(m => m.Id).ToList();
        var extraIds = currentIds.Concat(["extra-id"]).ToList();

        var act = async () => await _manager.ReorderModesAsync(extraIds);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Ordered ID list does not match existing modes*");
    }

    [Fact]
    public async Task ReorderModesAsync_DuplicateIds_ThrowsArgumentException()
    {
        var modes = _manager.GetAllModes();
        var currentIds = modes.Select(m => m.Id).ToList();
        var duplicateIds = currentIds.Take(3).Concat([currentIds[0]]).ToList(); // Duplicate first ID

        var act = async () => await _manager.ReorderModesAsync(duplicateIds);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Ordered ID list does not match existing modes*");
    }

    // ── Integration ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUpdateDelete_FullLifecycle_Works()
    {
        // Create
        var cloudProfile = new DictationProfile
        {
            SystemPrompt = "Original prompt",
            UseLlm = true,
            ModelName = "gpt-4",
            Hotkey = "Ctrl+Alt+X",
        };
        var localProfile = new DictationProfile
        {
            SystemPrompt = "Original prompt",
            UseLlm = true,
            ModelName = null,
            Hotkey = "Ctrl+Alt+X",
        };

        var created = await _manager.CreateModeAsync("Lifecycle Test", cloudProfile, localProfile);
        created.Title.Should().Be("Lifecycle Test");

        // Update
        var updatedCloud = cloudProfile with { SystemPrompt = "Updated prompt" };
        var updatedLocal = localProfile with { SystemPrompt = "Updated prompt" };
        await _manager.UpdateModeAsync(created.Id, "Lifecycle Updated", updatedCloud, updatedLocal);

        var updated = _manager.GetModeById(created.Id);
        updated!.Title.Should().Be("Lifecycle Updated");
        updated.CloudProfile.SystemPrompt.Should().Be("Updated prompt");

        // Delete
        await _manager.DeleteModeAsync(created.Id);
        var deleted = _manager.GetModeById(created.Id);
        deleted.Should().BeNull();
    }
}
