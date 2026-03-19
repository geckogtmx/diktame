
using DiktaMe.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        var themeService = App.Current.Services.GetRequiredService<ThemeService>();
        ApplyGlassmorphicGradient(ThemeService.GetPalette(themeService.CurrentTheme));
        ApplyGradientBackground(ThemeService.GetPalette(themeService.CurrentTheme));
        themeService.ThemeChanged += (_, themeName) =>
        {
            DispatcherQueue.TryEnqueue(() => 
            {
                var palette = ThemeService.GetPalette(themeName);
                ApplyGlassmorphicGradient(palette);
                ApplyGradientBackground(palette);
            });
        };

        // Select General (first item) on load
        NavView.SelectedItem = NavView.MenuItems[0];
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
}
