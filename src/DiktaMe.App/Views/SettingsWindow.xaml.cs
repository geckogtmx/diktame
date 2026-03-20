
using System.Collections.Generic;
using DiktaMe.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Serilog;
using Windows.UI;

namespace DiktaMe.App.Views;
/// <summary>
/// Tabbed settings window with NavigationView sidebar (9 items).
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private bool _suppressNextSelection;
    private object? _selectionBeforeClick; // the item that was selected before the current click
    private object? _currentSelection;     // the currently selected item
    private readonly ThemeService _themeService;
    private readonly Dictionary<NavigationViewItem, NavItemBrushes> _navBrushes = new();

    /// <summary>Holds per-item local brush refs injected into NavigationViewItem.Resources.</summary>
    private sealed record NavItemBrushes(
        SolidColorBrush Foreground,
        SolidColorBrush ForegroundPointerOver,
        SolidColorBrush ForegroundPressed,
        SolidColorBrush ForegroundSelected,
        SolidColorBrush ForegroundSelectedPointerOver,
        SolidColorBrush ForegroundSelectedPressed);

    public SettingsWindow()
    {
        this.InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 700));
        AppWindow.Title = "dIKta.me — Settings [v20]";

        // Extend content into title bar (caption buttons float over content)
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(new Grid { Height = 32, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) });

        // Set window icon
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        // Wire PaneFooter click → sync nav selection to Account item
        UserFooter.NavigateToAccountRequested += () =>
        {
            NavView.SelectedItem = NavAccountItem;
        };

        // Apply theme-appropriate glassmorphic gradient on theme change
        _themeService = App.Current.Services.GetRequiredService<ThemeService>();
        var initPalette = ThemeService.GetPalette(_themeService.CurrentTheme);
        Log.Information("SettingsWindow: init theme={Theme} IsDark={IsDark}",
            _themeService.CurrentTheme, initPalette.IsDark);

        // Set RequestedTheme so WinUI controls resolve {ThemeResource} from the correct
        // ThemeDictionary (Default for dark, Light for light). Without this, SettingsWindow
        // defaults to Dark because ApplyTheme() ran before this window existed.
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = initPalette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
            Log.Information("SettingsWindow: RequestedTheme set to {Theme}",
                initPalette.IsDark ? "Dark" : "Light");
        }

        ApplyGlassmorphicGradient(initPalette);
        ApplyGradientBackground(initPalette);

        // Inject WinUI control ThemeResource brushes at NavView scope (for nav pane controls)
        // and into each navigated page (for ComboBox/TextBox/Slider inside content pages).
        // This bypasses the ThemeDictionary brush-identity mismatch bug.
        InjectControlBrushes(initPalette);

        // Inject brushes into each page as it navigates into the ContentFrame.
        // Page.Resources is checked by controls' {ThemeResource} resolution before
        // walking up to NavView or Application.Resources.
        ContentFrame.Navigated += (_, args) =>
        {
            if (args.Content is FrameworkElement page)
            {
                var pal = ThemeService.GetPalette(_themeService.CurrentTheme);
                InjectPageBrushes(page, pal);
            }
        };

        _themeService.ThemeChanged += (_, themeName) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var palette = ThemeService.GetPalette(themeName);
                Log.Information("SettingsWindow: ThemeChanged → {Theme} IsDark={IsDark}",
                    themeName, palette.IsDark);

                // Update RequestedTheme so {ThemeResource} resolves from correct dictionary
                if (Content is FrameworkElement root)
                {
                    root.RequestedTheme = palette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
                }

                InjectControlBrushes(palette);
                if (ContentFrame.Content is FrameworkElement page)
                {
                    InjectPageBrushes(page, palette);
                }
                ApplyGlassmorphicGradient(palette);
                ApplyGradientBackground(palette);
                ApplyNavItemColors();
            });
        };

        // Inject per-item local ThemeResource brush overrides for nav text contrast.
        // Each NavigationViewItem gets its own brush instances in its Resources dict,
        // so WinUI's VisualStateManager resolves them locally (no global bleed).
        foreach (var menuItem in NavView.MenuItems)
        {
            if (menuItem is NavigationViewItem navItem)
            {
                _navBrushes[navItem] = InjectNavItemBrushes(navItem, initPalette, isSelected: false);
            }
        }

        // Select General (first item) on load
        NavView.SelectedItem = NavView.MenuItems[0];
        ApplyNavItemColors();
    }

    private void ApplyGradientBackground(ThemePalette palette)
    {
        BgGradStart.Color = palette.GradientStart;
        BgGradMid.Color = palette.GradientMid;
        BgGradEnd.Color = palette.GradientEnd;

        // Dark themes get atmospheric glow (21%/9%); light themes get zero glow for clean white
        byte glowA1 = palette.IsDark ? (byte)0x35 : (byte)0x00;
        byte glowA2 = palette.IsDark ? (byte)0x18 : (byte)0x00;
        GlowStop1.Color = Color.FromArgb(glowA1, palette.GlowBase.R, palette.GlowBase.G, palette.GlowBase.B);
        GlowStop2.Color = Color.FromArgb(glowA2, palette.GlowBase.R, palette.GlowBase.G, palette.GlowBase.B);
        GlowStop3.Color = Color.FromArgb(0x00, palette.GlowBase.R, palette.GlowBase.G, palette.GlowBase.B);
    }

    private void ApplyGlassmorphicGradient(ThemePalette palette)
    {
        var (s1, s2, s3, s4) = ThemeService.ComputeGlassStops(palette);
        GlassStop1.Color = s1;
        GlassStop2.Color = s2;
        GlassStop3.Color = s3;
        GlassStop4.Color = s4;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNextSelection)
        {
            _suppressNextSelection = false;
            _currentSelection = args.SelectedItem;
            return;
        }

        // Track the previous selection so DoubleTapped can revert cross-item double-clicks
        _selectionBeforeClick = _currentSelection;
        _currentSelection = args.SelectedItem;

        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        string tag = item.Tag?.ToString() ?? "general";
        Type? pageType = tag switch
        {
            "general" => typeof(Settings.GeneralSettingsPage),
            "hardware" => typeof(Settings.HardwareSettingsPage),
            "aiengine" => typeof(Settings.AIEngineSettingsPage),
            "workflows" => typeof(Settings.WorkflowsSettingsPage),
            "presets" => typeof(Settings.DictationPresetsSettingsPage),
            "snippets" => typeof(Settings.SnippetsSettingsPage),
            "privacy" => typeof(Settings.PrivacySettingsPage),
            "account" => typeof(Settings.AccountSettingsPage),
            "about" => typeof(Settings.AboutPage),
            _ => typeof(Settings.GeneralSettingsPage),
        };

        try
        {
            ContentFrame.Navigate(pageType);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SettingsWindow: CRASH navigating to {Page}", pageType?.Name);
        }

        // Update nav item foreground colors (selected = dark on blue bg, deselected = dim)
        ApplyNavItemColors();
    }

    private void NavView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Walk up from the tapped element to see if it's inside a NavigationViewItem
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not NavigationViewItem and not NavigationView)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is not NavigationViewItem)
        {
            return;
        }

        NavView.IsPaneOpen = !NavView.IsPaneOpen;

        // If the first click of the double-click changed selection, revert it
        if (_selectionBeforeClick is not null && NavView.SelectedItem != _selectionBeforeClick)
        {
            _suppressNextSelection = true;
            NavView.SelectedItem = _selectionBeforeClick;
        }

        e.Handled = true;
    }

    // ── Nav item foreground colors (per-item local ThemeResource overrides) ──

    /// <summary>
    /// Creates 6 SolidColorBrush instances and injects them into the NavigationViewItem's
    /// Resources dictionary. WinUI's VisualStateManager resolves {ThemeResource} keys from
    /// the nearest scope — these local brushes take priority over App.xaml globals.
    /// </summary>
    private static NavItemBrushes InjectNavItemBrushes(NavigationViewItem navItem, ThemePalette palette, bool isSelected)
    {
        var brushes = CreateBrushSet(palette, isSelected);

        navItem.Resources["NavigationViewItemForeground"] = brushes.Foreground;
        navItem.Resources["NavigationViewItemForegroundPointerOver"] = brushes.ForegroundPointerOver;
        navItem.Resources["NavigationViewItemForegroundPressed"] = brushes.ForegroundPressed;
        navItem.Resources["NavigationViewItemForegroundSelected"] = brushes.ForegroundSelected;
        navItem.Resources["NavigationViewItemForegroundSelectedPointerOver"] = brushes.ForegroundSelectedPointerOver;
        navItem.Resources["NavigationViewItemForegroundSelectedPressed"] = brushes.ForegroundSelectedPressed;

        return brushes;
    }

    private static NavItemBrushes CreateBrushSet(ThemePalette palette, bool isSelected)
    {
        // Dark themes: palette.Background (dark text on blue/purple bg) — matches Midnight/Ember design
        // Light themes: white (bright text on sky blue bg) — user preference for Frost
        var selectedFg = palette.IsDark ? palette.Background : Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        if (isSelected)
        {
            return new NavItemBrushes(
                Foreground: new SolidColorBrush(selectedFg),
                ForegroundPointerOver: new SolidColorBrush(selectedFg),
                ForegroundPressed: new SolidColorBrush(selectedFg),
                ForegroundSelected: new SolidColorBrush(selectedFg),
                ForegroundSelectedPointerOver: new SolidColorBrush(selectedFg),
                ForegroundSelectedPressed: new SolidColorBrush(selectedFg));
        }

        // Non-selected item: 70% text normal, accent on hover, full text on press
        var normal = Color.FromArgb(0xB3, palette.Text.R, palette.Text.G, palette.Text.B);
        return new NavItemBrushes(
            Foreground: new SolidColorBrush(normal),
            ForegroundPointerOver: new SolidColorBrush(palette.NavActive),
            ForegroundPressed: new SolidColorBrush(palette.Text),
            ForegroundSelected: new SolidColorBrush(selectedFg),
            ForegroundSelectedPointerOver: new SolidColorBrush(selectedFg),
            ForegroundSelectedPressed: new SolidColorBrush(selectedFg));
    }

    /// <summary>
    /// Injects WinUI control ThemeResource brushes into NavView.Resources (for nav pane controls).
    /// </summary>
    private void InjectControlBrushes(ThemePalette palette)
    {
        int count = 0;
        foreach (var (key, color) in ThemeService.GetControlBrushValues(palette))
        {
            NavView.Resources[key] = new SolidColorBrush(color);
            count++;
        }
        Log.Debug("SettingsWindow: InjectControlBrushes — {Count} keys into NavView.Resources", count);
    }

    /// <summary>
    /// Injects WinUI control ThemeResource brushes into a page's Resources.
    /// Page.Resources is checked by controls' {ThemeResource} resolution before
    /// walking up to NavView/Application scope — fixes ComboBox/TextBox/Slider theming.
    /// </summary>
    private static void InjectPageBrushes(FrameworkElement page, ThemePalette palette)
    {
        int count = 0;
        foreach (var (key, color) in ThemeService.GetControlBrushValues(palette))
        {
            page.Resources[key] = new SolidColorBrush(color);
            count++;
        }
        Log.Debug("SettingsWindow: InjectPageBrushes — {Count} keys into {Page}.Resources",
            count, page.GetType().Name);
    }

    /// <summary>
    /// Mutates existing local brush colors in-place based on current selection state.
    /// Called on selection change and theme change.
    /// </summary>
    private void ApplyNavItemColors()
    {
        var palette = ThemeService.GetPalette(_themeService.CurrentTheme);
        var selectedFg = palette.IsDark ? palette.Background : Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        var normal = Color.FromArgb(0xB3, palette.Text.R, palette.Text.G, palette.Text.B);

        foreach (var menuItem in NavView.MenuItems)
        {
            if (menuItem is NavigationViewItem navItem && _navBrushes.TryGetValue(navItem, out var brushes))
            {
                bool isSelected = ReferenceEquals(navItem, NavView.SelectedItem);

                if (isSelected)
                {
                    brushes.Foreground.Color = selectedFg;
                    brushes.ForegroundPointerOver.Color = selectedFg;
                    brushes.ForegroundPressed.Color = selectedFg;
                    brushes.ForegroundSelected.Color = selectedFg;
                    brushes.ForegroundSelectedPointerOver.Color = selectedFg;
                    brushes.ForegroundSelectedPressed.Color = selectedFg;
                }
                else
                {
                    brushes.Foreground.Color = normal;
                    brushes.ForegroundPointerOver.Color = palette.NavActive;
                    brushes.ForegroundPressed.Color = palette.Text;
                    brushes.ForegroundSelected.Color = selectedFg;
                    brushes.ForegroundSelectedPointerOver.Color = selectedFg;
                    brushes.ForegroundSelectedPressed.Color = selectedFg;
                }
            }
        }
    }
}
