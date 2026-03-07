using System.Globalization;
using WinUI3Localizer;

namespace DiktaMe.App.Services;

/// <summary>
/// Centralized service for loading localized strings from .resw resources.
/// Registered as singleton in DI. Delegates to <see cref="WinUI3Localizer.Localizer"/>
/// which reads .resw files directly from disk (works in unpackaged apps).
/// </summary>
public sealed class LocalizationService
{
    /// <summary>Gets a localized string by key name (without .Property suffix).</summary>
    public string GetString(string key) => Localizer.Get().GetLocalizedString(key);

    /// <summary>Gets a localized format string and fills in arguments using CurrentCulture.</summary>
    public string GetFormatted(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, Localizer.Get().GetLocalizedString(key), args);
}
