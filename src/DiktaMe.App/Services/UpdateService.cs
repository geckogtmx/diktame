using Serilog;
using Velopack;
using Velopack.Sources;

namespace DiktaMe.App.Services;

/// <summary>
/// Checks for app updates via Velopack and GitHub Releases.
/// All operations are non-blocking and failure-safe — update issues
/// must NEVER prevent the app from starting or running.
/// </summary>
public sealed class UpdateService
{
    private const string GitHubOwner = "geckogtmx";
    private const string GitHubRepo = "diktame";
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;

    /// <summary>
    /// True if an update has been downloaded and is ready to apply on next restart.
    /// </summary>
    public bool IsUpdateReady => _pendingUpdate is not null;

    /// <summary>
    /// The version string of the pending update, or null if none.
    /// </summary>
    public string? LatestVersion => _pendingUpdate?.TargetFullRelease?.Version?.ToString();

    /// <summary>
    /// Checks GitHub Releases for a newer version. Returns true if an update is available.
    /// Safe to call from any context — catches all exceptions and returns false on failure.
    /// Skips entirely when running outside a Velopack-installed context (dev/debug).
    /// </summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            _updateManager ??= CreateUpdateManager();
            if (_updateManager is null)
            {
                return false;
            }

            var checkTask = _updateManager.CheckForUpdatesAsync();
            var completed = await Task.WhenAny(checkTask, Task.Delay(CheckTimeout)).ConfigureAwait(false);
            if (completed != checkTask)
            {
                Log.Debug("UpdateService: update check timed out");
                return false;
            }

            var updateInfo = await checkTask.ConfigureAwait(false);

            if (updateInfo is null)
            {
                Log.Debug("UpdateService: no updates available");
                return false;
            }

            Log.Information("UpdateService: update available — v{Version}",
                updateInfo.TargetFullRelease?.Version);
            _pendingUpdate = updateInfo;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpdateService: update check failed");
            return false;
        }
    }

    /// <summary>
    /// Downloads the pending update in the background. Call after <see cref="CheckForUpdatesAsync"/> returns true.
    /// </summary>
    public async Task DownloadUpdateAsync()
    {
        if (_pendingUpdate is null || _updateManager is null)
        {
            return;
        }

        try
        {
            Log.Information("UpdateService: downloading update v{Version}...", LatestVersion);
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate).ConfigureAwait(false);
            Log.Information("UpdateService: update v{Version} downloaded successfully", LatestVersion);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpdateService: download failed");
            _pendingUpdate = null; // Reset — don't try to apply a partial download
        }
    }

    /// <summary>
    /// Applies the downloaded update and restarts the app immediately.
    /// Use when the user explicitly clicks "Restart to update".
    /// </summary>
    public void ApplyUpdateAndRestart()
    {
        if (_pendingUpdate?.TargetFullRelease is null || _updateManager is null)
        {
            return;
        }

        try
        {
            Log.Information("UpdateService: applying update v{Version} and restarting...", LatestVersion);
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UpdateService: failed to apply update and restart");
        }
    }

    /// <summary>
    /// Schedules the update to be applied when the app exits.
    /// The next time the app starts, it will be running the new version.
    /// </summary>
    public void ApplyUpdateOnExit()
    {
        if (_pendingUpdate?.TargetFullRelease is null || _updateManager is null)
        {
            return;
        }

        try
        {
            Log.Information("UpdateService: scheduling update v{Version} for next restart", LatestVersion);
            _updateManager.WaitExitThenApplyUpdates(_pendingUpdate.TargetFullRelease, silent: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpdateService: failed to schedule update on exit");
        }
    }

    /// <summary>
    /// Creates the UpdateManager if the app was installed via Velopack.
    /// Returns null for dev/debug runs (not installed via Velopack).
    /// </summary>
    private static UpdateManager? CreateUpdateManager()
    {
        try
        {
            // prerelease: true during RC testing — GitHub auto-marks tags with hyphens
            // (e.g. v2.0.0-rc2) as pre-release. Flip to false for stable V2.0.0 release.
            var source = new GithubSource($"https://github.com/{GitHubOwner}/{GitHubRepo}", null, prerelease: true);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled)
            {
                Log.Debug("UpdateService: not a Velopack install — skipping updates");
                return null;
            }

            Log.Debug("UpdateService: initialized (current v{Version})", mgr.CurrentVersion);
            return mgr;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpdateService: failed to create UpdateManager");
            return null;
        }
    }
}
