
using System;
using System.Collections.Generic;
using DiktaMe.Core.Config;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Serilog;
using Windows.UI;

namespace DiktaMe.App.Services;

/// <summary>
/// Manages live theme switching by mutating brush Color properties in-place.
/// WinUI 3 does NOT support DynamicResource, so StaticResource bindings resolve
/// at XAML parse time. In-place Color mutation on the same brush objects ensures
/// already-rendered UI updates immediately without re-parsing XAML.
/// </summary>
public sealed class ThemeService
{
    private readonly SettingsManager _settings;
    private readonly DispatcherQueue _dispatcherQueue;
    private string _currentTheme = string.Empty;

    /// <summary>Raised after a theme has been applied. Subscribers (e.g. ControlPanelPage)
    /// can re-derive animation colors from the new palette.</summary>
    public event EventHandler<string>? ThemeChanged;

    // ── Palette definitions ──────────────────────────────────────────────
    private static readonly Dictionary<string, ThemePalette> Palettes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Midnight"] = new ThemePalette(
            GradientStart: ColorFrom(0x12, 0x13, 0x21),
            GradientMid: ColorFrom(0x2A, 0x25, 0x50),
            GradientEnd: ColorFrom(0x4A, 0x3B, 0x75),
            GlowBase: ColorFrom(0x5A, 0x3D, 0x8F),
            GlassColor1: ColorFrom(0x66, 0x33, 0x99),
            GlassColor2: ColorFrom(0x99, 0x33, 0x66),
            BackgroundTranslucent: ColorFrom(0x0A, 0x09, 0x18, 0xD0),
            SurfaceTranslucent: ColorFrom(0x16, 0x15, 0x2E, 0xA0),
            Surface2Translucent: ColorFrom(0x1E, 0x1D, 0x38, 0x70),
            Background: ColorFrom(0x0A, 0x09, 0x18),
            Surface: ColorFrom(0x16, 0x15, 0x2E),
            Surface2: ColorFrom(0x1E, 0x1D, 0x38),
            Border: ColorFrom(0xFF, 0xFF, 0xFF, 0x14),
            NavActive: ColorFrom(0x3B, 0x82, 0xF6),
            Accent: ColorFrom(0x70, 0xE0, 0xFF),
            Text: ColorFrom(0xFF, 0xFF, 0xFF),
            TextDim: ColorFrom(0xFF, 0xFF, 0xFF, 0x99),
            PerfGreen: ColorFrom(0x7A, 0xFF, 0x9E),
            IsDark: true),

        ["Ember"] = new ThemePalette(
            GradientStart: ColorFrom(0x12, 0x08, 0x08),
            GradientMid: ColorFrom(0x2A, 0x15, 0x18),
            GradientEnd: ColorFrom(0x5A, 0x20, 0x3A),
            GlowBase: ColorFrom(0x9A, 0x30, 0x60),
            GlassColor1: ColorFrom(0x80, 0x20, 0x40),
            GlassColor2: ColorFrom(0x99, 0x44, 0x22),
            BackgroundTranslucent: ColorFrom(0x0D, 0x08, 0x06, 0xD0),
            SurfaceTranslucent: ColorFrom(0x1C, 0x1C, 0x1C, 0xA0),
            Surface2Translucent: ColorFrom(0x25, 0x25, 0x25, 0x70),
            Background: ColorFrom(0x0D, 0x08, 0x06),
            Surface: ColorFrom(0x1C, 0x1C, 0x1C),
            Surface2: ColorFrom(0x25, 0x25, 0x25),
            Border: ColorFrom(0xFF, 0xFF, 0xFF, 0x14),
            NavActive: ColorFrom(0x7C, 0x3A, 0xED),
            Accent: ColorFrom(0xF5, 0x9E, 0x0B),
            Text: ColorFrom(0xFF, 0xFF, 0xFF),
            TextDim: ColorFrom(0xFF, 0xFF, 0xFF, 0x99),
            PerfGreen: ColorFrom(0x7A, 0xFF, 0x9E),
            IsDark: true),

        ["Frost"] = new ThemePalette(
            GradientStart: ColorFrom(0xFA, 0xFB, 0xFF),
            GradientMid: ColorFrom(0xFA, 0xFB, 0xFF),
            GradientEnd: ColorFrom(0xFA, 0xFB, 0xFF),
            GlowBase: ColorFrom(0xC0, 0xD0, 0xFF),
            GlassColor1: ColorFrom(0xFF, 0xFF, 0xFF),
            GlassColor2: ColorFrom(0xFF, 0xFF, 0xFF),
            BackgroundTranslucent: ColorFrom(0xFA, 0xFB, 0xFF, 0xFF),
            SurfaceTranslucent: ColorFrom(0xFF, 0xFF, 0xFF, 0xFF),
            Surface2Translucent: ColorFrom(0xF0, 0xF2, 0xF8, 0xFF),
            Background: ColorFrom(0xFA, 0xFB, 0xFF),
            Surface: ColorFrom(0xFF, 0xFF, 0xFF),
            Surface2: ColorFrom(0xF0, 0xF2, 0xF8),
            Border: ColorFrom(0xE2, 0xE5, 0xEB),
            NavActive: ColorFrom(0x38, 0xBD, 0xF8),
            Accent: ColorFrom(0x0E, 0xA5, 0xE9),
            Text: ColorFrom(0x1A, 0x1A, 0x2E),
            TextDim: ColorFrom(0x6B, 0x72, 0x80),
            PerfGreen: ColorFrom(0x05, 0x96, 0x69),
            IsDark: false),
    };

    // ── Brush key → palette accessor mapping ─────────────────────────────
    private static readonly (string Key, Func<ThemePalette, Color> Accessor)[] BrushKeys =
    [
        // Core app brushes
        ("AppBackgroundBrush", p => p.Background),
        ("AppSurfaceBrush", p => p.Surface),
        ("AppSurface2Brush", p => p.Surface2),
        ("AppBackgroundTranslucentBrush", p => p.BackgroundTranslucent),
        ("AppSurfaceTranslucentBrush", p => p.SurfaceTranslucent),
        ("AppSurface2TranslucentBrush", p => p.Surface2Translucent),
        ("AppBorderBrush", p => p.Border),
        ("AppNavActiveBrush", p => p.NavActive),
        ("AppAccentBrush", p => p.Accent),
        ("AppTextBrush", p => p.Text),
        ("AppTextDimBrush", p => p.TextDim),
        ("AppPerfGreenBrush", p => p.PerfGreen),

        // Slider overrides
        ("SliderTrackValueFill", p => p.Accent),
        ("SliderTrackValueFillPointerOver", p => p.Accent),
        ("SliderTrackValueFillPressed", p => p.Accent),
        ("SliderTrackFill", p => p.Border),
        ("SliderTrackFillPointerOver", p => p.Border),
        ("SliderTrackFillPressed", p => p.Border),
        ("SliderThumbBackground", p => p.IsDark ? ColorFrom(0xFF, 0xFF, 0xFF) : ColorFrom(0x1A, 0x1A, 0x2E)),
        ("SliderThumbBackgroundPointerOver", p => p.IsDark ? ColorFrom(0xFF, 0xFF, 0xFF) : ColorFrom(0x1A, 0x1A, 0x2E)),
        ("SliderThumbBackgroundPressed", p => p.IsDark ? ColorFrom(0xE0, 0xE0, 0xE0) : ColorFrom(0x33, 0x33, 0x44)),

        // TextBox overrides
        ("TextControlBackground", p => p.Surface2),
        ("TextControlBackgroundPointerOver", p => p.Surface2),
        ("TextControlBackgroundFocused", p => p.Surface2),
        ("TextControlBorderBrush", p => p.Border),
        ("TextControlBorderBrushPointerOver", p => p.TextDim),
        ("TextControlBorderBrushFocused", p => p.Accent),
        ("TextControlForeground", p => p.Text),
        ("TextControlPlaceholderForeground", p => p.TextDim),

        // ComboBox overrides
        ("ComboBoxBackground", p => p.IsDark ? p.Border : p.Surface2),
        ("ComboBoxBackgroundPointerOver", p => p.IsDark
            ? ColorFrom(0xFF, 0xFF, 0xFF, 0x1A) : ColorFrom(0x00, 0x00, 0x00, 0x06)),
        ("ComboBoxBackgroundPressed", p => p.IsDark
            ? ColorFrom(0xFF, 0xFF, 0xFF, 0x22) : ColorFrom(0x00, 0x00, 0x00, 0x0A)),
        ("ComboBoxBorderBrush", p => p.Border),
        ("ComboBoxBorderBrushPointerOver", p => p.TextDim),
        ("ComboBoxBorderBrushPressed", p => p.Accent),
        ("ComboBoxForeground", p => p.Text),
        ("ComboBoxDropDownBackground", p => p.Surface2Translucent),
        ("ComboBoxDropDownBorderBrush", p => p.Border),
        ("ComboBoxDropDownForeground", p => p.Text),

        // ListView overrides (sub-nav selected item in settings pages)
        ("ListViewItemBackgroundSelected", p => p.Border),
        ("ListViewItemBackgroundSelectedPointerOver", p => p.Border),
        ("ListViewItemBackgroundSelectedPressed", p => p.Border),
        ("ListViewItemForegroundSelected", p => p.Text),
        ("ListViewItemSelectionIndicatorBrush", p => p.Accent),

        // Sub-nav (settings pages) selected item
        ("SubNavItemBackgroundSelected", p => p.Accent),
        ("SubNavItemForegroundSelected", p => p.Background),

        // NavigationView overrides
        ("NavigationViewDefaultPaneBackground", p => p.BackgroundTranslucent),
        ("NavigationViewItemForeground", p => Color.FromArgb(0xB3, p.Text.R, p.Text.G, p.Text.B)),
        ("NavigationViewItemForegroundPointerOver", p => p.Text),
        ("NavigationViewItemForegroundPressed", p => p.Text),
        ("NavigationViewItemBackgroundSelected", p => p.Accent),
        ("NavigationViewItemBackgroundSelectedPointerOver", p => p.Accent),
        ("NavigationViewItemBackgroundSelectedPressed", p => p.Accent),
        ("NavigationViewItemForegroundSelected", p => p.Text),
        ("NavigationViewItemForegroundSelectedPointerOver", p => p.Text),
        ("NavigationViewItemForegroundSelectedPressed", p => p.Text),

        // ToggleSwitch overrides
        ("ToggleSwitchFillOff", p => Color.FromArgb(0x00, 0, 0, 0)),
        ("ToggleSwitchFillOffPointerOver", p => Color.FromArgb(0x00, 0, 0, 0)),
        ("ToggleSwitchFillOffPressed", p => Color.FromArgb(0x00, 0, 0, 0)),
        ("ToggleSwitchFillOn", p => p.NavActive),
        ("ToggleSwitchFillOnPointerOver", p => p.NavActive),
        ("ToggleSwitchFillOnPressed", p => p.NavActive),
        ("ToggleSwitchStrokeOff", p => p.Border),
        ("ToggleSwitchStrokeOffPointerOver", p => p.TextDim),
        ("ToggleSwitchStrokeOffPressed", p => p.TextDim),
        ("ToggleSwitchStrokeOn", p => p.Accent),
        ("ToggleSwitchStrokeOnPointerOver", p => p.Accent),
        ("ToggleSwitchStrokeOnPressed", p => p.Accent),
        ("ToggleSwitchKnobFillOff", p => p.TextDim),
        ("ToggleSwitchKnobFillOffPointerOver", p => p.TextDim),
        ("ToggleSwitchKnobFillOffPressed", p => p.TextDim),
        ("ToggleSwitchKnobFillOn", p => p.Background),
        ("ToggleSwitchKnobFillOnPointerOver", p => p.Background),
        ("ToggleSwitchKnobFillOnPressed", p => p.Background),

        // ToggleSwitch content (On/Off label) foreground
        ("ToggleSwitchOnContentForeground", p => p.TextDim),
        ("ToggleSwitchOffContentForeground", p => p.TextDim),
    ];

    public ThemeService(SettingsManager settings)
    {
        _settings = settings;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Re-apply theme when settings are loaded from disk (e.g. after LoadAsync in LoadingViewModel).
        // This handles the case where the user's saved theme differs from the default "Midnight".
        // SettingsChanged may fire from a background thread (LoadAsync uses ConfigureAwait(false)),
        // so we must marshal ApplyTheme to the UI thread — it accesses Application.Current.Resources.
        _settings.SettingsChanged += (_, newSettings) =>
        {
            var savedTheme = newSettings.General.ThemeName;
            if (!string.Equals(_currentTheme, savedTheme, StringComparison.OrdinalIgnoreCase))
            {
                if (_dispatcherQueue.HasThreadAccess)
                {
                    ApplyTheme(savedTheme);
                }
                else
                {
                    _dispatcherQueue.TryEnqueue(() => ApplyTheme(savedTheme));
                }
            }
        };
    }

    /// <summary>The currently active theme name.</summary>
    public string CurrentTheme => _currentTheme;

    /// <summary>Available theme names for UI selectors. Derived from Palettes dictionary.</summary>
    public static string[] AvailableThemes => [.. Palettes.Keys];

    /// <summary>Gets the palette for the given theme name. Returns Midnight if not found.</summary>
    public static ThemePalette GetPalette(string? themeName)
    {
        return !string.IsNullOrEmpty(themeName) && Palettes.TryGetValue(themeName, out var p) ? p : Palettes["Midnight"];
    }

    /// <summary>
    /// Applies the named theme by mutating all App*Brush Colors in-place
    /// and setting RequestedTheme on all open windows.
    /// </summary>
    public void ApplyTheme(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName) || !Palettes.TryGetValue(themeName, out var palette))
        {
            Log.Warning("Unknown theme '{Theme}', falling back to Midnight", themeName);
            themeName = "Midnight";
            palette = Palettes[themeName];
        }

        var resources = Application.Current.Resources;
        Log.Debug("████ THEME APPLYING: {Theme} (IsDark={IsDark}) MergedDicts={Count}",
            themeName, palette.IsDark, resources.MergedDictionaries.Count);

        // Set RequestedTheme FIRST so WinUI re-resolves {ThemeResource} bindings to the
        // correct ThemeDictionary (Default or Light) brush instances BEFORE we mutate them.
        // If we mutate first then switch theme, WinUI replaces our mutated brushes with
        // fresh un-mutated instances from the newly-active dictionary.
        var elementTheme = palette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        foreach (var window in App.Current.ActiveWindows)
        {
            if (window.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = elementTheme;
            }
        }

        var (foundFlat, foundTheme, foundMergedTheme, foundMergedFlat, notFound, crashed) =
            MutateBrushes(resources, palette);

        Log.Debug("████ THEME BRUSH SUMMARY: flat={Flat} themeDicts={Theme} mergedTheme={MT} mergedFlat={MF} notFound={NF} crashed={Crashed} total={Total}",
            foundFlat, foundTheme, foundMergedTheme, foundMergedFlat, notFound, crashed, BrushKeys.Length);

        // Override SystemAccentColor and variants in ThemeDictionaries so WinUI controls
        // use our accent, not the Windows system accent (which may be yellow/gold).
        var accentKeys = new[] { "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2",
            "SystemAccentColorLight3", "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3" };

        foreach (var accentKey in accentKeys)
        {
            // Set at flat level
            resources[accentKey] = palette.Accent;

            // Set in each ThemeDictionary
            if (resources.ThemeDictionaries is { } themeDicts2)
            {
                foreach (var entry in themeDicts2)
                {
                    if (entry.Value is ResourceDictionary themeDict && themeDict.ContainsKey(accentKey))
                    {
                        themeDict[accentKey] = palette.Accent;
                    }
                }
            }
        }

        _currentTheme = themeName;
        Log.Information("Theme applied: {Theme} (RequestedTheme={ElementTheme})", themeName, elementTheme);

        ThemeChanged?.Invoke(this, themeName);
    }

    /// <summary>
    /// Applies the theme from settings and persists the choice.
    /// Called from GeneralSettingsViewModel when the user changes theme.
    /// </summary>
    public async Task ApplyAndSaveAsync(string themeName)
    {
        ApplyTheme(themeName);

        var updated = _settings.Current with
        {
            General = _settings.Current.General with { ThemeName = themeName },
        };
        await _settings.UpdateAsync(updated).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the theme from current settings. Called once at app startup.
    /// </summary>
    public void ApplyFromSettings()
    {
        var themeName = _settings.Current.General.ThemeName;
        ApplyTheme(themeName);
    }

    /// <summary>
    /// Mutates each brush's Color in-place across all 4 resource scopes.
    /// Each key is wrapped in try-catch because mutating .Color on a brush from a
    /// ThemeDictionary that became inactive after RequestedTheme change can cause
    /// a silent native WinUI crash (observed: Frost crashes at TextControlForeground).
    /// </summary>
    private static (int Flat, int Theme, int MergedTheme, int MergedFlat, int NotFound, int Crashed)
        MutateBrushes(ResourceDictionary resources, ThemePalette palette)
    {
        int foundFlat = 0, foundTheme = 0, foundMergedTheme = 0, foundMergedFlat = 0, notFound = 0, crashed = 0;

        foreach (var (key, accessor) in BrushKeys)
        {
            try
            {
                var found = false;
                bool inFlat = false, inTheme = false, inMergedTheme = false, inMergedFlat = false;

                // 1. Flat resources (App*Brush keys)
                if (resources.TryGetValue(key, out var obj) && obj is SolidColorBrush brush)
                {
                    brush.Color = accessor(palette);
                    found = true;
                    inFlat = true;
                }

                // 2. ThemeDictionaries (WinUI control overrides in App.xaml)
                if (resources.ThemeDictionaries is { } themeDicts)
                {
                    foreach (var entry in themeDicts)
                    {
                        if (entry.Value is ResourceDictionary themeDict
                            && themeDict.TryGetValue(key, out var tObj) && tObj is SolidColorBrush tBrush)
                        {
                            tBrush.Color = accessor(palette);
                            found = true;
                            inTheme = true;
                        }
                    }
                }

                // 3. MergedDictionaries' ThemeDictionaries
                foreach (var merged in resources.MergedDictionaries)
                {
                    if (merged.ThemeDictionaries is { } mergedThemeDicts)
                    {
                        foreach (var entry in mergedThemeDicts)
                        {
                            if (entry.Value is ResourceDictionary themeDict
                                && themeDict.TryGetValue(key, out var mObj) && mObj is SolidColorBrush mBrush)
                            {
                                mBrush.Color = accessor(palette);
                                found = true;
                                inMergedTheme = true;
                            }
                        }
                    }
                }

                // 4. MergedDictionaries' flat resources
                foreach (var merged in resources.MergedDictionaries)
                {
                    if (merged.TryGetValue(key, out var mFlatObj) && mFlatObj is SolidColorBrush mFlatBrush)
                    {
                        mFlatBrush.Color = accessor(palette);
                        found = true;
                        inMergedFlat = true;
                    }
                }

                if (inFlat) { foundFlat++; }
                if (inTheme) { foundTheme++; }
                if (inMergedTheme) { foundMergedTheme++; }
                if (inMergedFlat) { foundMergedFlat++; }
                if (!found)
                {
                    notFound++;
                    Log.Warning("Theme brush '{Key}' NOT FOUND anywhere", key);
                }
                else
                {
                    var c = accessor(palette);
                    Log.Debug("  {Key} → #{A:X2}{R:X2}{G:X2}{B:X2} [flat={F} theme={T} mTheme={MT} mFlat={MF}]",
                        key, c.A, c.R, c.G, c.B, inFlat, inTheme, inMergedTheme, inMergedFlat);
                }
            }
            catch (Exception ex)
            {
                crashed++;
                Log.Warning("Theme brush '{Key}' CRASHED during mutation: {Error}", key, ex.Message);
            }
        }

        return (foundFlat, foundTheme, foundMergedTheme, foundMergedFlat, notFound, crashed);
    }

    /// <summary>
    /// Returns all WinUI control ThemeResource key→color pairs for the given palette.
    /// Used by window code-behind to inject brushes at the visual-tree level,
    /// bypassing the ThemeDictionary brush-identity mismatch bug.
    /// Excludes App*Brush keys (those are flat resources mutated in-place by ApplyTheme).
    /// </summary>
    public static IEnumerable<(string Key, Color Color)> GetControlBrushValues(ThemePalette palette)
    {
        foreach (var (key, accessor) in BrushKeys)
        {
            if (!key.StartsWith("App", StringComparison.Ordinal))
            {
                yield return (key, accessor(palette));
            }
        }
    }

    /// <summary>
    /// Computes glassmorphic gradient stop colors from the palette.
    /// Dark themes get a visible colored sweep; light themes get fully transparent stops.
    /// Used by SettingsWindow and ControlPanelPage to eliminate hardcoded glass colors.
    /// </summary>
    public static (Color S1, Color S2, Color S3, Color S4) ComputeGlassStops(ThemePalette palette)
    {
        byte a2 = palette.IsDark ? (byte)0x30 : (byte)0x00;
        byte a3 = palette.IsDark ? (byte)0x20 : (byte)0x00;
        return (
            Color.FromArgb(0x00, palette.Surface.R, palette.Surface.G, palette.Surface.B),
            Color.FromArgb(a2, palette.GlassColor1.R, palette.GlassColor1.G, palette.GlassColor1.B),
            Color.FromArgb(a3, palette.GlassColor2.R, palette.GlassColor2.G, palette.GlassColor2.B),
            Color.FromArgb(0x00, palette.Surface.R, palette.Surface.G, palette.Surface.B));
    }

    private static Color ColorFrom(byte r, byte g, byte b, byte a = 0xFF)
        => Color.FromArgb(a, r, g, b);
}

/// <summary>
/// Immutable palette definition for a single theme.
/// This is the SINGLE SOURCE OF TRUTH for all theme colors.
/// Adding a new theme = adding one entry to <see cref="ThemeService"/> Palettes dictionary.
/// </summary>
public sealed record ThemePalette(
    Color GradientStart,
    Color GradientMid,
    Color GradientEnd,
    Color GlowBase,
    Color GlassColor1,
    Color GlassColor2,
    Color BackgroundTranslucent,
    Color SurfaceTranslucent,
    Color Surface2Translucent,
    Color Background,
    Color Surface,
    Color Surface2,
    Color Border,
    Color NavActive,
    Color Accent,
    Color Text,
    Color TextDim,
    Color PerfGreen,
    bool IsDark);
