
using Serilog;

namespace DiktaMe.Core.Config;

/// <summary>
/// Manages CRUD operations for user-defined dictation modes and built-in modes.
/// Persists changes through SettingsManager. Built-in modes can be edited but not deleted.
/// </summary>
public sealed class DictationModeManager
{
    private readonly SettingsManager _settings;

    public DictationModeManager(SettingsManager settings)
    {
        _settings = settings;
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all dictation modes (built-in + custom), sorted by SortOrder.
    /// </summary>
    public List<DictationMode> GetAllModes()
    {
        return _settings.Current.DictationModes
            .OrderBy(m => m.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Finds a dictation mode by its unique ID.
    /// </summary>
    /// <param name="id">The mode ID (e.g., "dictate-standard" or a GUID).</param>
    /// <returns>The matching mode, or null if not found.</returns>
    public DictationMode? GetModeById(string id)
    {
        return _settings.Current.DictationModes
            .FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns the appropriate profile (Cloud or Local) for a given mode based on
    /// the active profile setting.
    /// </summary>
    /// <param name="modeId">The mode ID.</param>
    /// <returns>The active profile (CloudProfile or LocalProfile).</returns>
    /// <exception cref="InvalidOperationException">Thrown if mode is not found.</exception>
    public DictationProfile GetActiveProfile(string modeId)
    {
        var mode = GetModeById(modeId);
        if (mode is null)
        {
            throw new InvalidOperationException($"Mode '{modeId}' not found");
        }

        bool isCloud = string.Equals(_settings.Current.ActiveProfileName, "Cloud", StringComparison.OrdinalIgnoreCase);
        return isCloud ? mode.CloudProfile : mode.LocalProfile;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new custom dictation mode with a unique GUID-based ID.
    /// </summary>
    /// <param name="title">The display title for the mode.</param>
    /// <param name="cloudProfile">Cloud profile settings.</param>
    /// <param name="localProfile">Local profile settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created mode.</returns>
    public async Task<DictationMode> CreateModeAsync(
        string title,
        DictationProfile cloudProfile,
        DictationProfile localProfile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        // Generate unique ID (GUID for custom modes)
        string id = Guid.NewGuid().ToString();

        // Assign next available SortOrder
        int maxSort = _settings.Current.DictationModes.Count > 0
            ? _settings.Current.DictationModes.Max(m => m.SortOrder)
            : -1;

        var newMode = new DictationMode
        {
            Id = id,
            Title = title,
            CloudProfile = cloudProfile,
            LocalProfile = localProfile,
            IsBuiltIn = false,
            SortOrder = maxSort + 1,
        };

        var updated = _settings.Current with
        {
            DictationModes = [.. _settings.Current.DictationModes, newMode]
        };

        await _settings.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

        Log.Information("DictationModeManager: Created mode '{Title}' (ID={Id})", title, id);
        return newMode;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates an existing dictation mode's title and profiles.
    /// Both built-in and custom modes can be edited (only deletion is restricted for built-ins).
    /// </summary>
    /// <param name="id">The mode ID to update.</param>
    /// <param name="title">New title.</param>
    /// <param name="cloudProfile">Updated cloud profile.</param>
    /// <param name="localProfile">Updated local profile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if mode is not found.</exception>
    public async Task UpdateModeAsync(
        string id,
        string title,
        DictationProfile cloudProfile,
        DictationProfile localProfile,
        CancellationToken cancellationToken = default)
    {
        var existing = GetModeById(id);
        if (existing is null)
        {
            throw new InvalidOperationException($"Mode '{id}' not found");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var updated = existing with
        {
            Title = title,
            CloudProfile = cloudProfile,
            LocalProfile = localProfile,
        };

        var newModes = _settings.Current.DictationModes
            .Select(m => string.Equals(m.Id, id, StringComparison.Ordinal) ? updated : m)
            .ToList();

        var newSettings = _settings.Current with { DictationModes = newModes };
        await _settings.UpdateAsync(newSettings, cancellationToken).ConfigureAwait(false);

        Log.Information("DictationModeManager: Updated mode '{Title}' (ID={Id})", title, id);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes a custom dictation mode.
    /// </summary>
    /// <param name="id">The mode ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if mode is built-in or not found.</exception>
    public async Task DeleteModeAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = GetModeById(id);
        if (existing is null)
        {
            throw new InvalidOperationException($"Mode '{id}' not found");
        }

        if (existing.IsBuiltIn)
        {
            throw new InvalidOperationException($"Cannot delete built-in mode '{id}'");
        }

        var newModes = _settings.Current.DictationModes
            .Where(m => !string.Equals(m.Id, id, StringComparison.Ordinal))
            .ToList();

        var newSettings = _settings.Current with { DictationModes = newModes };
        await _settings.UpdateAsync(newSettings, cancellationToken).ConfigureAwait(false);

        Log.Information("DictationModeManager: Deleted mode '{Title}' (ID={Id})", existing.Title, id);
    }

    // ── Reorder ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the SortOrder of all modes based on the provided ordered list of IDs.
    /// </summary>
    /// <param name="orderedIds">List of mode IDs in the desired display order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown if ID list doesn't match existing modes.</exception>
    public async Task ReorderModesAsync(List<string> orderedIds, CancellationToken cancellationToken = default)
    {
        var currentModes = _settings.Current.DictationModes;

        // Validate: all current mode IDs must be present in orderedIds
        var currentIds = currentModes.Select(m => m.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var providedIds = orderedIds.OrderBy(x => x, StringComparer.Ordinal).ToList();

        if (!currentIds.SequenceEqual(providedIds, StringComparer.Ordinal))
        {
            throw new ArgumentException("Ordered ID list does not match existing modes", nameof(orderedIds));
        }

        // Build dictionary for fast lookup
        var modeDict = currentModes.ToDictionary(m => m.Id, StringComparer.Ordinal);

        // Rebuild list with new SortOrder
        var reordered = orderedIds
            .Select((id, index) => modeDict[id] with { SortOrder = index })
            .ToList();

        var newSettings = _settings.Current with { DictationModes = reordered };
        await _settings.UpdateAsync(newSettings, cancellationToken).ConfigureAwait(false);

        Log.Information("DictationModeManager: Reordered {Count} modes", reordered.Count);
    }
}
