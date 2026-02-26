
using Serilog;

namespace DiktaMe.Core.Config;

/// <summary>
/// Manages configuration for fixed utility pipelines (Ask, Refine, Translate, Note, Chat).
/// Allows updating profiles and hotkeys but NOT creating/deleting pipelines (they are fixed).
/// </summary>
public sealed class PipelineConfigManager
{
    private readonly SettingsManager _settings;

    public PipelineConfigManager(SettingsManager settings)
    {
        _settings = settings;
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all utility pipeline configurations.
    /// </summary>
    public List<PipelineConfig> GetAllPipelines()
    {
        return _settings.Current.UtilityPipelines.ToList();
    }

    /// <summary>
    /// Finds a pipeline configuration by its type (e.g., "ask", "refine").
    /// </summary>
    /// <param name="pipelineType">The pipeline type identifier.</param>
    /// <returns>The matching pipeline config, or null if not found.</returns>
    public PipelineConfig? GetPipelineByType(string pipelineType)
    {
        return _settings.Current.UtilityPipelines
            .FirstOrDefault(p => string.Equals(p.PipelineType, pipelineType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the appropriate profile (Cloud or Local) for a given pipeline based on
    /// the active profile setting.
    /// </summary>
    /// <param name="pipelineType">The pipeline type (e.g., "ask").</param>
    /// <returns>The active profile (CloudProfile or LocalProfile).</returns>
    /// <exception cref="InvalidOperationException">Thrown if pipeline is not found.</exception>
    public UtilityProfile GetActiveProfile(string pipelineType)
    {
        var pipeline = GetPipelineByType(pipelineType);
        if (pipeline is null)
        {
            throw new InvalidOperationException($"Pipeline '{pipelineType}' not found");
        }

        bool isCloud = string.Equals(_settings.Current.ActiveProfileName, "Cloud", StringComparison.OrdinalIgnoreCase);
        return isCloud ? pipeline.CloudProfile : pipeline.LocalProfile;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates a pipeline's profiles and hotkey.
    /// </summary>
    /// <param name="pipelineType">The pipeline type to update.</param>
    /// <param name="cloudProfile">Updated cloud profile.</param>
    /// <param name="localProfile">Updated local profile.</param>
    /// <param name="hotkey">Updated hotkey (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if pipeline is not found.</exception>
    public async Task UpdatePipelineAsync(
        string pipelineType,
        UtilityProfile cloudProfile,
        UtilityProfile localProfile,
        string? hotkey = null,
        CancellationToken cancellationToken = default)
    {
        var existing = GetPipelineByType(pipelineType);
        if (existing is null)
        {
            throw new InvalidOperationException($"Pipeline '{pipelineType}' not found");
        }

        var updated = existing with
        {
            CloudProfile = cloudProfile,
            LocalProfile = localProfile,
            Hotkey = hotkey ?? existing.Hotkey, // preserve existing if not provided
        };

        var newPipelines = _settings.Current.UtilityPipelines
            .Select(p => string.Equals(p.PipelineType, pipelineType, StringComparison.OrdinalIgnoreCase) ? updated : p)
            .ToList();

        var newSettings = _settings.Current with { UtilityPipelines = newPipelines };
        await _settings.UpdateAsync(newSettings, cancellationToken).ConfigureAwait(false);

        Log.Information("PipelineConfigManager: Updated pipeline '{Type}'", pipelineType);
    }
}
