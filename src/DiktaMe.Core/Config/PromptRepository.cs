namespace DiktaMe.Core.Config;

/// <summary>
/// CRUD access to the 16 custom prompt slots stored in <see cref="AppSettings.CustomPrompts"/>.
/// Slot indices 0-15. Null means "use built-in provider default for this mode".
/// </summary>
public sealed class PromptRepository
{
    public const int SlotCount = 16;

    private readonly SettingsManager _settings;

    public PromptRepository(SettingsManager settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Returns the prompt at the given slot, or null if the slot is empty.
    /// </summary>
    public string? GetPrompt(int slot)
    {
        ValidateSlot(slot);
        var prompts = _settings.Current.CustomPrompts;
        return slot < prompts.Length ? prompts[slot] : null;
    }

    /// <summary>
    /// Stores a custom prompt at the given slot and persists settings.
    /// </summary>
    public async Task SetPromptAsync(int slot, string prompt, CancellationToken cancellationToken = default)
    {
        ValidateSlot(slot);

        var prompts = (string?[])_settings.Current.CustomPrompts.Clone();
        prompts[slot] = prompt;

        var updated = _settings.Current with { CustomPrompts = prompts };
        await _settings.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the prompt at the given slot (sets to null) and persists settings.
    /// </summary>
    public async Task ResetToDefaultAsync(int slot, CancellationToken cancellationToken = default)
    {
        ValidateSlot(slot);

        var prompts = (string?[])_settings.Current.CustomPrompts.Clone();
        prompts[slot] = null;

        var updated = _settings.Current with { CustomPrompts = prompts };
        await _settings.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns all 16 prompt slots as an array (null = use default).
    /// </summary>
    public string?[] GetAllPrompts() => (string?[])_settings.Current.CustomPrompts.Clone();

    private static void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot must be 0-{SlotCount - 1}.");
        }
    }
}
