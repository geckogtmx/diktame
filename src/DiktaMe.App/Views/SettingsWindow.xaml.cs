
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views;
/// <summary>
/// Tabbed settings window with NavigationView sidebar.
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

        // Wire PaneFooter click → navigate to Account page
        UserFooter.NavigateToAccountRequested += () =>
        {
            NavView.SelectedItem = NavView.MenuItems[0]; // Account is the first item
        };

        // Select General (second item) on load
        NavView.SelectedItem = NavView.MenuItems[1];
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
            "account" => typeof(Settings.AccountSettingsPage),
            "general" => typeof(Settings.GeneralSettingsPage),
            "aiengine" => typeof(Settings.AIEngineSettingsPage),
            "modes" => typeof(Settings.ModesSettingsPage),
            "dictationmodes" => typeof(Settings.DictationModesSettingsPage),
            "audio" => typeof(Settings.AudioSettingsPage),
            "hotkeys" => typeof(Settings.HotkeysSettingsPage),
            "privacy" => typeof(Settings.PrivacySettingsPage),
            "apikeys" => typeof(Settings.ApiKeysSettingsPage),
            "ollama" => typeof(Settings.OllamaSettingsPage),
            "snippets" => typeof(Settings.SnippetsSettingsPage),
            "controlpanel" => typeof(Settings.ControlPanelConfigPage),
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
