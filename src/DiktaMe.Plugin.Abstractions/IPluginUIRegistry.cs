namespace DiktaMe.Plugin;

public interface IPluginUIRegistry
{
    void AddSettingsPage(PluginSettingsPageInfo page);
    void RemoveSettingsPage(string pluginId);
    void AddTrayMenuItems(string pluginId, IReadOnlyList<PluginTrayMenuItem> items);
    void RemoveTrayMenuItems(string pluginId);
    void AddControlPanelWidget(PluginWidgetInfo widget);
    void RemoveControlPanelWidget(string pluginId);

    IReadOnlyList<PluginSettingsPageInfo> SettingsPages { get; }
    IReadOnlyList<PluginTrayMenuItem> TrayMenuItems { get; }
    IReadOnlyList<PluginWidgetInfo> Widgets { get; }

    event EventHandler? ContributionsChanged;
}
