namespace DiktaMe.Core.Config;

/// <summary>
/// Manages the dual-profile system. Each of the 8 workflow modes has
/// independent settings per profile (provider, model, prompt).
/// Profile 0 = primary, Profile 1 = secondary (e.g. "local mode").
/// </summary>
public sealed class ProfileManager
{
    private readonly SettingsManager _settings;

    public ProfileManager(SettingsManager settings)
    {
        _settings = settings;
    }

    /// <summary>Index of the currently active profile (0 or 1).</summary>
    public int ActiveProfile => _settings.Current.ActiveProfile;

    /// <summary>
    /// Returns the <see cref="ModeSettings"/> for the given mode in the active profile.
    /// Falls back to default <see cref="ModeSettings"/> if not configured.
    /// </summary>
    /// <param name="mode">Mode name: "dictate", "refine", "ask", "translate", "note", "chat".</param>
    public ModeSettings GetModeSettings(string mode)
    {
        string key = $"{mode}_{ActiveProfile}";
        return _settings.Current.ModeProfiles.TryGetValue(key, out var ms)
            ? ms
            : new ModeSettings();
    }

    /// <summary>
    /// Returns the <see cref="ModeSettings"/> for the given mode in a specific profile.
    /// </summary>
    public ModeSettings GetModeSettings(string mode, int profile)
    {
        string key = $"{mode}_{profile}";
        return _settings.Current.ModeProfiles.TryGetValue(key, out var ms)
            ? ms
            : new ModeSettings();
    }

    /// <summary>
    /// Switches the active profile and saves settings.
    /// </summary>
    public async Task SwitchProfileAsync(int profile, CancellationToken cancellationToken = default)
    {
        if (profile is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(profile), "Profile must be 0 or 1.");

        var updated = _settings.Current with { ActiveProfile = profile };
        await _settings.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the mode settings for a specific mode/profile combination and saves.
    /// </summary>
    public async Task SetModeSettingsAsync(
        string mode,
        int profile,
        ModeSettings modeSettings,
        CancellationToken cancellationToken = default)
    {
        string key = $"{mode}_{profile}";
        var updatedProfiles = new Dictionary<string, ModeSettings>(_settings.Current.ModeProfiles)
        {
            [key] = modeSettings,
        };

        var updated = _settings.Current with { ModeProfiles = updatedProfiles };
        await _settings.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
    }
}
