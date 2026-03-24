using DiktaMe.Plugin;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.Plugin;

public sealed class PluginManagerTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "diktame-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    [Fact]
    public async Task Empty_plugins_directory_discovers_nothing()
    {
        var dir = CreateTempDir();
        var context = new Mock<IPluginContext>();
        var manager = new PluginManager();

        await manager.DiscoverAndLoadAsync(dir, context.Object);

        manager.Plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_plugins_directory_creates_it()
    {
        var dir = Path.Combine(Path.GetTempPath(), "diktame-test-" + Guid.NewGuid().ToString("N"));
        _tempDirs.Add(dir);
        var context = new Mock<IPluginContext>();
        var manager = new PluginManager();

        await manager.DiscoverAndLoadAsync(dir, context.Object);

        Directory.Exists(dir).Should().BeTrue();
        manager.Plugins.Should().BeEmpty();
    }

    [Fact]
    public void Plugins_list_initially_empty()
    {
        var manager = new PluginManager();

        manager.Plugins.Should().BeEmpty();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
