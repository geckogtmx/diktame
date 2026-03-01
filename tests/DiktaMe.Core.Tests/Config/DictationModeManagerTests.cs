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
        // Initialize with a single default preset (like first-run)
        var defaultPreset = DictationModeDefaults.CreateDefaultPreset();
        var initial = _settings.Current with
        {
            DictationModes = [defaultPreset],
            ActiveDictationModeId = defaultPreset.Id,
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
    public void GetAllModes_ReturnsDefaultPreset()
    {
        var modes = _manager.GetAllModes();

        modes.Should().HaveCount(1);
        modes[0].Title.Should().Be("Standard");
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
        var modes = _manager.GetAllModes();
        var mode = _manager.GetModeById(modes[0].Id);

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
        var updated = _settings.Current with { ActiveProfileName = "Cloud" };
        await _settings.UpdateAsync(updated);

        var modes = _manager.GetAllModes();
        var profile = _manager.GetActiveProfile(modes[0].Id);

        profile.Should().NotBeNull();
        profile.ModelName.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task GetActiveProfile_LocalProfile_ReturnsLocalProfile()
    {
        var updated = _settings.Current with { ActiveProfileName = "Local" };
        await _settings.UpdateAsync(updated);

        var modes = _manager.GetAllModes();
        var profile = _manager.GetActiveProfile(modes[0].Id);

        profile.Should().NotBeNull();
        profile.ModelName.Should().BeNull();
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
        newMode.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(newMode.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateModeAsync_ValidInput_AssignsNextSortOrder()
    {
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var newMode = await _manager.CreateModeAsync("Test Mode", cloudProfile, localProfile);

        // Default preset has SortOrder 0, new one should be 1
        newMode.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task CreateModeAsync_ValidInput_PersistsToSettings()
    {
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var newMode = await _manager.CreateModeAsync("Test Mode", cloudProfile, localProfile);

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
    public async Task UpdateModeAsync_ExistingPreset_UpdatesSuccessfully()
    {
        var modes = _manager.GetAllModes();
        var presetId = modes[0].Id;

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

        await _manager.UpdateModeAsync(presetId, "Modified Standard", cloudProfile, localProfile);

        var updated = _manager.GetModeById(presetId);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Modified Standard");
        updated.CloudProfile.SystemPrompt.Should().Be("Modified prompt");
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
        var modes = _manager.GetAllModes();
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };

        var act = async () => await _manager.UpdateModeAsync(modes[0].Id, "", cloudProfile, localProfile);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── DeleteModeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteModeAsync_ExistingPreset_RemovesFromList()
    {
        // Create a second preset so we can delete one
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var created = await _manager.CreateModeAsync("To Delete", cloudProfile, localProfile);

        await _manager.DeleteModeAsync(created.Id);

        var deleted = _manager.GetModeById(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteModeAsync_AnyPreset_CanBeDeleted()
    {
        // All presets are user-owned and deletable
        var modes = _manager.GetAllModes();
        var presetId = modes[0].Id;

        await _manager.DeleteModeAsync(presetId);

        var deleted = _manager.GetModeById(presetId);
        deleted.Should().BeNull();
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
        // Create a second preset
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        await _manager.CreateModeAsync("Second", cloudProfile, localProfile);

        var modes = _manager.GetAllModes();
        var currentIds = modes.Select(m => m.Id).ToList();

        // Reverse the order
        var reversedIds = currentIds.AsEnumerable().Reverse().ToList();

        await _manager.ReorderModesAsync(reversedIds);

        var reordered = _manager.GetAllModes();
        reordered.Select(m => m.Id).Should().Equal(reversedIds);
        reordered.Select(m => m.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public async Task ReorderModesAsync_MissingIds_ThrowsArgumentException()
    {
        // Create a second preset
        var cloudProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        var localProfile = new DictationProfile { SystemPrompt = "Test", UseLlm = true };
        await _manager.CreateModeAsync("Second", cloudProfile, localProfile);

        var modes = _manager.GetAllModes();
        var incompleteIds = modes.Take(1).Select(m => m.Id).ToList();

        var act = async () => await _manager.ReorderModesAsync(incompleteIds);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Ordered ID list does not match existing modes*");
    }

    // ── Integration ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUpdateDelete_FullLifecycle_Works()
    {
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

        // Create
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
