
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using FluentAssertions;
using Xunit;

namespace DiktaMe.Core.Tests.Data;
[Trait("Category", "Integration")]
public sealed class ConversationManagerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SettingsManager _settings;
    private readonly ConversationManager _manager;

    public ConversationManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"diktame_conv_test_{Guid.NewGuid()}.db");
        _settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_conv_{Guid.NewGuid()}.json"));
        _manager = new ConversationManager(_settings, _dbPath);
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitAsync_CreatesDatabase()
    {
        await _manager.InitAsync();
        File.Exists(_dbPath).Should().BeTrue();
    }

    [Fact]
    public async Task InitAsync_IsIdempotent()
    {
        await _manager.InitAsync();
        var act = () => _manager.InitAsync();
        await act.Should().NotThrowAsync();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ReturnsRecordWithGeneratedId()
    {
        await _manager.InitAsync();

        var record = await _manager.CreateAsync("You are helpful.", "gpt-4o");

        record.Id.Should().NotBeNullOrEmpty();
        record.Title.Should().BeNull();
        record.SystemPrompt.Should().Be("You are helpful.");
        record.ModelId.Should().Be("gpt-4o");
        record.MessageCount.Should().Be(0);
        record.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_PersistsToDatabase()
    {
        await _manager.InitAsync();

        var created = await _manager.CreateAsync("system prompt");
        var loaded = await _manager.GetAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.SystemPrompt.Should().Be("system prompt");
    }

    [Fact]
    public async Task CreateAsync_GhostMode_ReturnsRecordButDoesNotPersist()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Ghost },
        });
        await _manager.InitAsync();

        var record = await _manager.CreateAsync("ghost prompt");

        record.Id.Should().NotBeNullOrEmpty();
        var loaded = await _manager.GetAsync(record.Id);
        loaded.Should().BeNull();
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddMessageAsync_PersistsMessage()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        await _manager.AddMessageAsync(conv.Id, "user", "Hello!", 5);
        await _manager.AddMessageAsync(conv.Id, "assistant", "Hi there!", 8);

        var messages = await _manager.GetMessagesAsync(conv.Id);
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("Hello!");
        messages[0].Tokens.Should().Be(5);
        messages[1].Role.Should().Be("assistant");
        messages[1].Content.Should().Be("Hi there!");
        messages[1].Tokens.Should().Be(8);
    }

    [Fact]
    public async Task AddMessageAsync_UpdatesConversationMetadata()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        await _manager.AddMessageAsync(conv.Id, "user", "Hello!");
        await _manager.AddMessageAsync(conv.Id, "assistant", "Hi!");

        var updated = await _manager.GetAsync(conv.Id);
        updated!.MessageCount.Should().Be(2);
        // updated_at is stored as unix seconds so sub-second precision is lost;
        // just verify it's within the same second or later
        updated.UpdatedAt.ToUnixTimeSeconds().Should().BeGreaterThanOrEqualTo(conv.UpdatedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task AddMessageAsync_GhostMode_DoesNotPersist()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Ghost },
        });
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        await _manager.AddMessageAsync(conv.Id, "user", "secret message");

        var messages = await _manager.GetMessagesAsync(conv.Id);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task AddMessageAsync_StatsLevel_StoresEmptyContent()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Stats },
        });
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        await _manager.AddMessageAsync(conv.Id, "user", "sensitive content", 10);

        var messages = await _manager.GetMessagesAsync(conv.Id);
        messages.Should().HaveCount(1);
        messages[0].Content.Should().BeEmpty();
        messages[0].Tokens.Should().Be(10);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsChronologicalOrder()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        await _manager.AddMessageAsync(conv.Id, "user", "First");
        await _manager.AddMessageAsync(conv.Id, "assistant", "Second");
        await _manager.AddMessageAsync(conv.Id, "user", "Third");

        var messages = await _manager.GetMessagesAsync(conv.Id);
        messages.Select(m => m.Content).Should().ContainInOrder("First", "Second", "Third");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTitleAsync_SetsTitle()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        await _manager.UpdateTitleAsync(conv.Id, "My Chat");

        var loaded = await _manager.GetAsync(conv.Id);
        loaded!.Title.Should().Be("My Chat");
    }

    [Fact]
    public async Task UpdateModelAsync_SetsModelId()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt", "gpt-4o-mini");

        await _manager.UpdateModelAsync(conv.Id, "claude-sonnet-4");

        var loaded = await _manager.GetAsync(conv.Id);
        loaded!.ModelId.Should().Be("claude-sonnet-4");
    }

    // ── Recent ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecentAsync_ReturnsOrderedByUpdatedAt()
    {
        await _manager.InitAsync();

        // Create three conversations — they all have the same created_at (same second)
        var c1 = await _manager.CreateAsync("prompt 1");
        var c2 = await _manager.CreateAsync("prompt 2");
        var c3 = await _manager.CreateAsync("prompt 3");

        // Bump c1's updated_at by adding a message (makes it most recently updated)
        await Task.Delay(1100); // wait for unix second to advance
        await _manager.AddMessageAsync(c1.Id, "user", "bump");

        var recent = await _manager.GetRecentAsync(10);

        recent.Should().HaveCount(3);
        // c1 should be first because its updated_at was bumped
        recent[0].Id.Should().Be(c1.Id);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsLimit()
    {
        await _manager.InitAsync();

        for (int i = 0; i < 5; i++)
        {
            await _manager.CreateAsync($"prompt {i.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var recent = await _manager.GetRecentAsync(3);
        recent.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRecentAsync_GhostMode_ReturnsEmpty()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Ghost },
        });
        await _manager.InitAsync();

        var recent = await _manager.GetRecentAsync();
        recent.Should().BeEmpty();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesConversationAndMessages()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");
        await _manager.AddMessageAsync(conv.Id, "user", "Hello");
        await _manager.AddMessageAsync(conv.Id, "assistant", "Hi");

        await _manager.DeleteAsync(conv.Id);

        var loaded = await _manager.GetAsync(conv.Id);
        loaded.Should().BeNull();

        var messages = await _manager.GetMessagesAsync(conv.Id);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesEverything()
    {
        await _manager.InitAsync();

        await _manager.CreateAsync("prompt 1");
        await _manager.CreateAsync("prompt 2");

        await _manager.DeleteAllAsync();

        var recent = await _manager.GetRecentAsync();
        recent.Should().BeEmpty();
    }

    // ── Edge Cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_NonExistentId_ReturnsNull()
    {
        await _manager.InitAsync();

        var result = await _manager.GetAsync("nonexistent-id");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMessagesAsync_EmptyConversation_ReturnsEmpty()
    {
        await _manager.InitAsync();
        var conv = await _manager.CreateAsync("prompt");

        var messages = await _manager.GetMessagesAsync(conv.Id);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Operations_BeforeInit_DoNotThrow()
    {
        // Manager not initialized — all operations should be safe
        var act1 = () => _manager.GetAsync("id");
        var act2 = () => _manager.GetMessagesAsync("id");
        var act3 = () => _manager.AddMessageAsync("id", "user", "text");
        var act4 = () => _manager.UpdateTitleAsync("id", "title");
        var act5 = () => _manager.DeleteAsync("id");
        var act6 = () => _manager.DeleteAllAsync();

        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
        await act3.Should().NotThrowAsync();
        await act4.Should().NotThrowAsync();
        await act5.Should().NotThrowAsync();
        await act6.Should().NotThrowAsync();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _manager.Dispose();
        await Task.CompletedTask;
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
