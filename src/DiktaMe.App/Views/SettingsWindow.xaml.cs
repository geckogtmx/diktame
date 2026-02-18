
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

        // Select first tab on load
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
            "aiengine" => typeof(Settings.AIEngineSettingsPage),
            "modes" => typeof(Settings.ModesSettingsPage),
            "audio" => typeof(Settings.AudioSettingsPage),
            "privacy" => typeof(Settings.PrivacySettingsPage),
            "apikeys" => typeof(Settings.ApiKeysSettingsPage),
            "ollama" => typeof(Settings.OllamaSettingsPage),
            "snippets" => typeof(Settings.SnippetsSettingsPage),
            "controlpanel" => typeof(Settings.ControlPanelConfigPage),
            "about" => typeof(Settings.AboutPage),
            _ => typeof(Settings.GeneralSettingsPage),
        };

        ContentFrame.Navigate(pageType);
    }
}
