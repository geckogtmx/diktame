using DiktaMe.Plugin;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Plugin;

public sealed record TestPluginSettings
{
    public string Name { get; init; } = "default";
    public int Count { get; init; } = 0;
}

public sealed class JsonPluginSettingsStoreTests : IDisposable
{
    private readonly string _pluginId = $"test-{Guid.NewGuid():N}";
    private readonly string _settingsDir;

    public JsonPluginSettingsStoreTests()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsDir = Path.Combine(appData, "DiktaMe", "plugins");
    }

    private JsonPluginSettingsStore CreateStore() => new(_pluginId);

    private string SettingsFilePath => Path.Combine(_settingsDir, $"{_pluginId}-settings.json");

    [Fact]
    public async Task Load_returns_default_when_file_missing()
    {
        var store = CreateStore();

        var settings = await store.LoadAsync<TestPluginSettings>();

        settings.Should().NotBeNull();
        settings.Name.Should().Be("default");
        settings.Count.Should().Be(0);
    }

    [Fact]
    public async Task Save_then_load_roundtrips()
    {
        var store = CreateStore();
        var original = new TestPluginSettings { Name = "custom", Count = 42 };

        await store.SaveAsync(original);
        var loaded = await store.LoadAsync<TestPluginSettings>();

        loaded.Name.Should().Be("custom");
        loaded.Count.Should().Be(42);
    }

    [Fact]
    public async Task Save_fires_settings_changed_event()
    {
        var store = CreateStore();
        var fired = false;
        store.SettingsChanged += (_, _) => fired = true;

        await store.SaveAsync(new TestPluginSettings { Name = "changed" });

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task Save_atomic_write()
    {
        var store = CreateStore();

        await store.SaveAsync(new TestPluginSettings { Name = "persisted" });

        // The settings file should exist (no leftover .tmp)
        File.Exists(SettingsFilePath).Should().BeTrue();
        var tmpFiles = Directory.GetFiles(_settingsDir, $"{_pluginId}*.tmp");
        tmpFiles.Should().BeEmpty();
    }

    public void Dispose()
    {
        try { File.Delete(SettingsFilePath); } catch { /* best-effort */ }
    }
}
