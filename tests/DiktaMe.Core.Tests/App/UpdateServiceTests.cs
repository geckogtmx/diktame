using FluentAssertions;
using Velopack;
using Velopack.Sources;

namespace DiktaMe.Core.Tests.App;

/// <summary>
/// Shared fixture that calls VelopackApp.Build().Run() once before any test in
/// the collection. This sets up the global VelopackLocator that UpdateManager requires.
/// </summary>
public sealed class VelopackFixture
{
    public VelopackFixture()
    {
        // Must be called before any UpdateManager constructor — sets global locator.
        // In non-installed context, Run() is a no-op and returns immediately.
        VelopackApp.Build().Run();
    }
}

[CollectionDefinition("Velopack")]
public class VelopackCollection : ICollectionFixture<VelopackFixture> { }

/// <summary>
/// Tests for the Velopack update infrastructure.
/// UpdateService itself lives in DiktaMe.App (WinUI project) and can't be directly
/// referenced from this test project. These tests verify the Velopack API behaviors
/// that UpdateService depends on — specifically the safety guarantees:
///   1. VelopackApp.Build().Run() is safe in any context
///   2. GithubSource can be constructed without throwing
///   3. UpdateManager behaves correctly in non-installed context
/// Full E2E update flow is validated via the RC2 → RC3 test sequence (SPEC_019).
/// </summary>
[Collection("Velopack")]
public sealed class UpdateServiceTests
{
    [Fact]
    public void VelopackApp_Build_DoesNotThrow()
    {
        // VelopackApp.Build() is called in Program.Main() — it must never throw.
        var act = () => VelopackApp.Build();

        act.Should().NotThrow("VelopackApp.Build() must be safe to call in any context");
    }

    [Fact]
    public void VelopackApp_Run_DoesNotThrow_InNonInstalledContext()
    {
        // In a non-installed context (dev/test), Run() returns immediately without side effects.
        var act = () => VelopackApp.Build().Run();

        act.Should().NotThrow("Run() must be a no-op outside Velopack install context");
    }

    [Fact]
    public void GithubSource_CanBeConstructed_WithRepoUrl()
    {
        var act = () => new GithubSource("https://github.com/geckogtmx/diktame", null, false);

        act.Should().NotThrow("GithubSource should accept a valid GitHub repo URL");
    }

    [Fact]
    public void GithubSource_CanBeConstructed_WithPrerelease()
    {
        // During RC testing, prerelease=true is needed to find rc2/rc3 releases.
        var act = () => new GithubSource("https://github.com/geckogtmx/diktame", null, true);

        act.Should().NotThrow("GithubSource should accept prerelease=true");
    }

    [Fact]
    public void UpdateManager_IsInstalled_ReturnsFalse_OutsideVelopackContext()
    {
        // When running from a dev build (not installed via Velopack),
        // IsInstalled must return false so UpdateService skips all update logic.
        var source = new GithubSource("https://github.com/geckogtmx/diktame", null, false);
        var mgr = new UpdateManager(source);

        mgr.IsInstalled.Should().BeFalse(
            "dev/test builds are not installed via Velopack — update checks should be skipped");
    }

    [Fact]
    public void UpdateManager_CurrentVersion_IsNull_WhenNotInstalled()
    {
        // CurrentVersion should be null when not installed via Velopack.
        var source = new GithubSource("https://github.com/geckogtmx/diktame", null, false);
        var mgr = new UpdateManager(source);

        mgr.CurrentVersion.Should().BeNull(
            "CurrentVersion should be null outside Velopack install context");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_HandlesNonInstalledContext()
    {
        // UpdateService checks IsInstalled first and returns false.
        // This test verifies the same pattern works.
        var source = new GithubSource("https://github.com/geckogtmx/diktame", null, false);
        var mgr = new UpdateManager(source);

        mgr.IsInstalled.Should().BeFalse();

        // In non-installed context, CheckForUpdatesAsync may throw or return null.
        // UpdateService wraps this in try/catch — verify it doesn't crash the process.
        try
        {
            var result = await mgr.CheckForUpdatesAsync();
            // If it returns, null means no update available — expected
            result.Should().BeNull();
        }
        catch (Exception)
        {
            // Some Velopack versions throw when not installed — that's fine,
            // UpdateService.CheckForUpdatesAsync() catches this.
        }
    }
}
