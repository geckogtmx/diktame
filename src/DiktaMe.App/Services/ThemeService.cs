
using System;
using System.Collections.Generic;
using DiktaMe.Core.Config;
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
    private string _currentTheme = string.Empty;

    /// <summary>Raised after a theme has been applied. Subscribers (e.g. ControlPanelPage)
    /// can re-derive animation colors from the new palette.</summary>
    public event EventHandler<string>? ThemeChanged;

    // ── Palette definitions ──────────────────────────────────────────────
    private static readonly Dictionary<string, ThemePalette> Palettes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Midnight"] = new ThemePalette(
            Background: ColorFrom(0x0A, 0x09, 0x18),
            Surface: ColorFrom(0x16, 0x15, 0x2E),
            Surface2: ColorFrom(0x1E, 0x1D, 0x38),
            Border: ColorFrom(0xFF, 0xFF, 0xFF, 0x14),
            NavActive: ColorFrom(0x3B, 0x82, 0xF6),
            Accent: ColorFrom(0x5D, 0xEB, 0xFF),
            Text: ColorFrom(0xFF, 0xFF, 0xFF),
            TextDim: ColorFrom(0xFF, 0xFF, 0xFF, 0x99),
            PerfGreen: ColorFrom(0x7A, 0xFF, 0x9E),
            IsDark: true),

        ["Ember"] = new ThemePalette(
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
            Background: ColorFrom(0xF0, 0xF4, 0xFF),
            Surface: ColorFrom(0xFF, 0xFF, 0xFF),
            Surface2: ColorFrom(0xF2, 0xF4, 0xFF),
            Border: ColorFrom(0x00, 0x00, 0x00, 0x14),
            NavActive: ColorFrom(0x38, 0xBD, 0xF8),
            Accent: ColorFrom(0x0E, 0xA5, 0xE9),
            Text: ColorFrom(0x1A, 0x1A, 0x2E),
            TextDim: ColorFrom(0x1A, 0x1A, 0x2E, 0x99),
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
        ("SliderTrackFill", p => p.Surface2),
        ("SliderTrackFillPointerOver", p => p.Surface2),
        ("SliderTrackFillPressed", p => p.Surface2),
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
        ("ComboBoxBackground", p => p.Surface2),
        ("ComboBoxBackgroundPointerOver", p => p.Surface2),
        ("ComboBoxBackgroundPressed", p => p.Surface2),
        ("ComboBoxBorderBrush", p => p.Border),
        ("ComboBoxBorderBrushPointerOver", p => p.TextDim),
        ("ComboBoxBorderBrushPressed", p => p.Accent),
        ("ComboBoxForeground", p => p.Text),
        ("ComboBoxDropDownBackground", p => p.Surface2),
        ("ComboBoxDropDownBorderBrush", p => p.Border),
        ("ComboBoxDropDownForeground", p => p.Text),

        // ListView overrides (sub-nav selected item in settings pages)
        ("ListViewItemBackgroundSelected", p => p.Border),
        ("ListViewItemBackgroundSelectedPointerOver", p => p.Border),
        ("ListViewItemBackgroundSelectedPressed", p => p.Border),
        ("ListViewItemForegroundSelected", p => p.Text),
        ("ListViewItemSelectionIndicatorBrush", p => p.NavActive),

        // NavigationView overrides
        ("NavigationViewDefaultPaneBackground", p => p.Background),
        ("NavigationViewItemForeground", p => p.TextDim),
        ("NavigationViewItemForegroundPointerOver", p => p.Text),
        ("NavigationViewItemForegroundPressed", p => p.Text),
        ("NavigationViewItemBackgroundSelected", p => p.NavActive),
        ("NavigationViewItemBackgroundSelectedPointerOver", p => p.NavActive),
        ("NavigationViewItemBackgroundSelectedPressed", p => p.NavActive),
        ("NavigationViewItemForegroundSelected", p => p.Text),
        ("NavigationViewItemForegroundSelectedPointerOver", p => p.Text),
        ("NavigationViewItemForegroundSelectedPressed", p => p.Text),

        // ToggleSwitch overrides
        ("ToggleSwitchFillOff", p => p.Surface2),
        ("ToggleSwitchFillOffPointerOver", p => p.Surface2),
        ("ToggleSwitchFillOffPressed", p => p.Surface2),
        ("ToggleSwitchFillOn", p => p.NavActive),
        ("ToggleSwitchFillOnPointerOver", p => p.NavActive),
        ("ToggleSwitchFillOnPressed", p => p.NavActive),
        ("ToggleSwitchStrokeOff", p => p.Border),
        ("ToggleSwitchStrokeOffPointerOver", p => p.TextDim),
        ("ToggleSwitchStrokeOffPressed", p => p.TextDim),
        ("ToggleSwitchStrokeOn", p => p.NavActive),
        ("ToggleSwitchStrokeOnPointerOver", p => p.NavActive),
        ("ToggleSwitchStrokeOnPressed", p => p.NavActive),
        ("ToggleSwitchKnobFillOff", p => p.TextDim),
        ("ToggleSwitchKnobFillOffPointerOver", p => p.TextDim),
        ("ToggleSwitchKnobFillOffPressed", p => p.TextDim),
        ("ToggleSwitchKnobFillOn", p => p.IsDark ? ColorFrom(0xFF, 0xFF, 0xFF) : ColorFrom(0x1A, 0x1A, 0x2E)),
        ("ToggleSwitchKnobFillOnPointerOver", p => p.IsDark ? ColorFrom(0xFF, 0xFF, 0xFF) : ColorFrom(0x1A, 0x1A, 0x2E)),
        ("ToggleSwitchKnobFillOnPressed", p => p.IsDark ? ColorFrom(0xE0, 0xE0, 0xE0) : ColorFrom(0x33, 0x33, 0x44)),

        // ToggleSwitch content (On/Off label) foreground
        ("ToggleSwitchOnContentForeground", p => p.TextDim),
        ("ToggleSwitchOffContentForeground", p => p.TextDim),
    ];

    public ThemeService(SettingsManager settings)
    {
        _settings = settings;

        // Re-apply theme when settings are loaded from disk (e.g. after LoadAsync in LoadingViewModel).
        // This handles the case where the user's saved theme differs from the default "Midnight".
        _settings.SettingsChanged += (_, newSettings) =>
        {
            var savedTheme = newSettings.General.ThemeName;
            if (!string.Equals(_currentTheme, savedTheme, StringComparison.OrdinalIgnoreCase))
            {
                ApplyTheme(savedTheme);
            }
        };
    }

    /// <summary>The currently active theme name.</summary>
    public string CurrentTheme => _currentTheme;

    /// <summary>Available theme names for UI selectors.</summary>
    public static string[] AvailableThemes => ["Midnight", "Ember", "Frost"];

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

        // Mutate each brush's Color in-place so existing StaticResource bindings update.
        // Brushes live in ThemeDictionaries (for ThemeResource resolution) AND at the
        // flat level (for our custom App*Brush StaticResource bindings). Search both.
        foreach (var (key, accessor) in BrushKeys)
        {
            var found = false;

            // 1. Search flat resources (App*Brush keys from SharedResources.xaml)
            if (resources.TryGetValue(key, out var obj) && obj is SolidColorBrush brush)
            {
                brush.Color = accessor(palette);
                found = true;
            }

            // 2. Search ThemeDictionaries (WinUI control overrides in App.xaml)
            if (resources.ThemeDictionaries is { } themeDicts)
            {
                foreach (var entry in themeDicts)
                {
                    if (entry.Value is ResourceDictionary themeDict
                        && themeDict.TryGetValue(key, out var tObj) && tObj is SolidColorBrush tBrush)
                    {
                        tBrush.Color = accessor(palette);
                        found = true;
                    }
                }
            }

            // 3. Also search MergedDictionaries' ThemeDictionaries
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
                        }
                    }
                }
            }

            if (!found)
            {
                Log.Warning("Theme brush '{Key}' not found in Application.Resources or ThemeDictionaries", key);
            }
        }

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

        // Set RequestedTheme on all active windows for correct system control rendering
        var elementTheme = palette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
        foreach (var window in App.Current.ActiveWindows)
        {
            if (window.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = elementTheme;
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

    private static Color ColorFrom(byte r, byte g, byte b, byte a = 0xFF)
        => Color.FromArgb(a, r, g, b);
}

/// <summary>
/// Immutable palette definition for a single theme.
/// </summary>
public sealed record ThemePalette(
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
