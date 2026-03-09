namespace DiktaMe.Core.Config;

/// <summary>
/// Reads per-mode provider settings (STT provider, LLM provider, model) from <see cref="ModeSettings"/>.
/// Always reads from profile 0, which is the canonical copy written by the wizard and Settings UI.
/// Cloud/Local switching is handled by <see cref="DictationModeManager"/> and <see cref="PipelineConfigManager"/>
/// via <see cref="AppSettings.ActiveProfileName"/> — this class only provides provider names.
/// </summary>
public sealed class ProfileManager
{
    private readonly SettingsManager _settings;

    public ProfileManager(SettingsManager settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Returns the <see cref="ModeSettings"/> for the given mode.
    /// Always reads profile 0 (the canonical copy written by wizard and Settings UI).
    /// Falls back to default <see cref="ModeSettings"/> if not configured.
    /// </summary>
    /// <param name="mode">Mode name: "dictate", "refine", "ask", "translate", "note", "chat".</param>
    public ModeSettings GetModeSettings(string mode)
    {
        string key = $"{mode}_0";
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
