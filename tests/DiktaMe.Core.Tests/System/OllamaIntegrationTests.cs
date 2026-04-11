
using DiktaMe.Core.SystemManagement;
using FluentAssertions;

namespace DiktaMe.Core.Tests.SystemManagement;

/// <summary>
/// Integration tests for <see cref="OllamaManager"/> against a real local Ollama instance.
/// All tests are guarded: if Ollama is not running, they pass silently (no-op).
/// Run with: <c>dotnet test --filter "Category=Integration"</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OllamaIntegrationTests : IDisposable
{
    private readonly OllamaManager _manager;
    private readonly bool _ollamaAvailable;

    public OllamaIntegrationTests()
    {
        _manager = new OllamaManager();

        try
        {
            var version = _manager.GetVersionAsync(null).GetAwaiter().GetResult();
            _ollamaAvailable = version is not null;
        }
        catch
        {
            _ollamaAvailable = false;
        }
    }

    public void Dispose() => _manager.Dispose();

    // ── CheckAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_WithInstalledModel_ReturnsReady()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var tags = await _manager.GetInstalledModelTagsAsync();
        if (tags.Count == 0)
        {
            return;
        }

        var result = await _manager.CheckAsync(tags[0]);

        result.Status.Should().Be(OllamaStatus.Ready);
        result.OllamaVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckAsync_NonExistentModel_ReturnsModelNotPulled()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var result = await _manager.CheckAsync("nonexistent-model-xyz-99999");

        result.Status.Should().Be(OllamaStatus.ModelNotPulled);
    }

    // ── GetVersionAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetVersionAsync_ReturnsNonNullVersionString()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var version = await _manager.GetVersionAsync(null);

        version.Should().NotBeNullOrEmpty();
        version.Should().MatchRegex(@"\d+\.\d+");
    }

    // ── GetInstalledModelsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetInstalledModelsAsync_ReturnsModelDetails()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var models = await _manager.GetInstalledModelsAsync();

        models.Should().NotBeNull();
        if (models.Count > 0)
        {
            var first = models[0];
            first.Name.Should().NotBeNullOrEmpty();
            first.Size.Should().BeGreaterThan(0);
        }
    }

    // ── GetInstalledModelTagsAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetInstalledModelTagsAsync_ReturnsStrings()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var tags = await _manager.GetInstalledModelTagsAsync();

        tags.Should().NotBeNull();
        foreach (string tag in tags)
        {
            tag.Should().NotBeNullOrEmpty();
        }
    }

    // ── GetRunningModelsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetRunningModelsAsync_ReturnsList()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var running = await _manager.GetRunningModelsAsync();

        running.Should().NotBeNull();
    }

    // ── GetKnownModelsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetKnownModelsAsync_ReturnsEmbeddedManifest()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var models = await _manager.GetKnownModelsAsync();

        models.Should().NotBeEmpty();
        models.Should().Contain(m => string.Equals(m.Tag, "llama3.2", StringComparison.Ordinal));
    }

    // ── Version not too old ──────────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_VersionIsNotTooOld()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var tags = await _manager.GetInstalledModelTagsAsync();
        if (tags.Count == 0)
        {
            return;
        }

        var result = await _manager.CheckAsync(tags[0]);

        result.Status.Should().NotBe(OllamaStatus.VersionTooOld);
        result.OllamaVersion.Should().NotBeNullOrEmpty();
    }

    // ── GetModelInfoAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetModelInfoAsync_ForInstalledModel_ReturnsValidInfo()
    {
        if (!_ollamaAvailable)
        {
            return;
        }

        var tags = await _manager.GetInstalledModelTagsAsync();
        if (tags.Count == 0)
        {
            return;
        }

        var models = await _manager.GetInstalledModelsAsync();
        var first = models[0];

        first.Name.Should().NotBeNullOrEmpty();
        first.Family.Should().NotBeNullOrEmpty();
    }
}
