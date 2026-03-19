
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
        AppWindow.Title = "dIKta.me — Settings";

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
        ApplyGlassmorphicGradient(ThemeService.GetPalette(_themeService.CurrentTheme));
        ApplyGradientBackground(ThemeService.GetPalette(_themeService.CurrentTheme));
        _themeService.ThemeChanged += (_, themeName) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var palette = ThemeService.GetPalette(themeName);
                ApplyGlassmorphicGradient(palette);
                ApplyGradientBackground(palette);
                ApplyNavItemColors();
            });
        };

        // Inject per-item local ThemeResource brush overrides for nav text contrast.
        // Each NavigationViewItem gets its own brush instances in its Resources dict,
        // so WinUI's VisualStateManager resolves them locally (no global bleed).
        var initPalette = ThemeService.GetPalette(_themeService.CurrentTheme);
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

        // The glow uses 0x35 (~21%), 0x18 (~9%), and 0x00 opacity steps
        GlowStop1.Color = Color.FromArgb(0x35, palette.GlowBase.R, palette.GlowBase.G, palette.GlowBase.B);
        GlowStop2.Color = Color.FromArgb(0x18, palette.GlowBase.R, palette.GlowBase.G, palette.GlowBase.B);
        GlowStop3.Color = Color.FromArgb(0x00, palette.GlowBase.R, palette.GlowBase.G, palette.GlowBase.B);
    }

    private void ApplyGlassmorphicGradient(ThemePalette palette)
    {
        if (palette.IsDark)
        {
            // Dark themes: purple/magenta sweep
            GlassStop1.Color = Color.FromArgb(0x00, 0x16, 0x15, 0x30);
            GlassStop2.Color = Color.FromArgb(0x30, 0x66, 0x33, 0x99);
            GlassStop3.Color = Color.FromArgb(0x20, 0x99, 0x33, 0x66);
            GlassStop4.Color = Color.FromArgb(0x00, 0x16, 0x15, 0x30);
        }
        else
        {
            // Light themes: subtle blue/lavender sweep
            GlassStop1.Color = Color.FromArgb(0x00, 0xC0, 0xD0, 0xFF);
            GlassStop2.Color = Color.FromArgb(0x18, 0x80, 0x90, 0xE0);
            GlassStop3.Color = Color.FromArgb(0x10, 0xA0, 0x80, 0xD0);
            GlassStop4.Color = Color.FromArgb(0x00, 0xC0, 0xD0, 0xFF);
        }
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
        if (isSelected)
        {
            // Selected item: dark text on blue/accent background — all states dark
            var dark = palette.Background;
            return new NavItemBrushes(
                Foreground: new SolidColorBrush(dark),
                ForegroundPointerOver: new SolidColorBrush(dark),
                ForegroundPressed: new SolidColorBrush(dark),
                ForegroundSelected: new SolidColorBrush(dark),
                ForegroundSelectedPointerOver: new SolidColorBrush(dark),
                ForegroundSelectedPressed: new SolidColorBrush(dark));
        }

        // Non-selected item: 70% text normal, accent on hover, full text on press
        var normal = Color.FromArgb(0xB3, palette.Text.R, palette.Text.G, palette.Text.B);
        return new NavItemBrushes(
            Foreground: new SolidColorBrush(normal),
            ForegroundPointerOver: new SolidColorBrush(palette.NavActive),
            ForegroundPressed: new SolidColorBrush(palette.Text),
            ForegroundSelected: new SolidColorBrush(palette.Background),
            ForegroundSelectedPointerOver: new SolidColorBrush(palette.Background),
            ForegroundSelectedPressed: new SolidColorBrush(palette.Background));
    }

    /// <summary>
    /// Mutates existing local brush colors in-place based on current selection state.
    /// Called on selection change and theme change.
    /// </summary>
    private void ApplyNavItemColors()
    {
        var palette = ThemeService.GetPalette(_themeService.CurrentTheme);
        var dark = palette.Background;
        var normal = Color.FromArgb(0xB3, palette.Text.R, palette.Text.G, palette.Text.B);

        foreach (var menuItem in NavView.MenuItems)
        {
            if (menuItem is NavigationViewItem navItem && _navBrushes.TryGetValue(navItem, out var brushes))
            {
                bool isSelected = ReferenceEquals(navItem, NavView.SelectedItem);

                if (isSelected)
                {
                    brushes.Foreground.Color = dark;
                    brushes.ForegroundPointerOver.Color = dark;
                    brushes.ForegroundPressed.Color = dark;
                    brushes.ForegroundSelected.Color = dark;
                    brushes.ForegroundSelectedPointerOver.Color = dark;
                    brushes.ForegroundSelectedPressed.Color = dark;
                }
                else
                {
                    brushes.Foreground.Color = normal;
                    brushes.ForegroundPointerOver.Color = palette.NavActive;
                    brushes.ForegroundPressed.Color = palette.Text;
                    brushes.ForegroundSelected.Color = dark;
                    brushes.ForegroundSelectedPointerOver.Color = dark;
                    brushes.ForegroundSelectedPressed.Color = dark;
                }
            }
        }
    }
}
