
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Serilog;

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

        // Select General (first item) on load
        NavView.SelectedItem = NavView.MenuItems[0];
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
