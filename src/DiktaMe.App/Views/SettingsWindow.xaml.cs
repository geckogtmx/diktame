
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views;
/// <summary>
/// Tabbed settings window with NavigationView sidebar (9 items).
/// </summary>
public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        this.InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 700));
        AppWindow.Title = "dIKta.me — Settings";

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
}
