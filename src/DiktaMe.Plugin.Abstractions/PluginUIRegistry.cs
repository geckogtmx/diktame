namespace DiktaMe.Plugin;

public sealed class PluginUIRegistry : IPluginUIRegistry
{
    private readonly object _lock = new();
    private readonly List<PluginSettingsPageInfo> _settingsPages = [];
    private readonly Dictionary<string, List<PluginTrayMenuItem>> _trayMenuItemsByPlugin = [];
    private readonly List<PluginWidgetInfo> _widgets = [];

    public IReadOnlyList<PluginSettingsPageInfo> SettingsPages
    {
        get { lock (_lock) return [.. _settingsPages]; }
    }

    public IReadOnlyList<PluginTrayMenuItem> TrayMenuItems
    {
        get { lock (_lock) return [.. _trayMenuItemsByPlugin.Values.SelectMany(v => v)]; }
    }

    public IReadOnlyList<PluginWidgetInfo> Widgets
    {
        get { lock (_lock) return [.. _widgets]; }
    }

    public event EventHandler? ContributionsChanged;

    public void AddSettingsPage(PluginSettingsPageInfo page)
    {
        lock (_lock) _settingsPages.Add(page);
        ContributionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveSettingsPage(string pluginId)
    {
        lock (_lock) _settingsPages.RemoveAll(p => string.Equals(p.PluginId, pluginId, StringComparison.Ordinal));
        ContributionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddTrayMenuItems(string pluginId, IReadOnlyList<PluginTrayMenuItem> items)
    {
        lock (_lock) _trayMenuItemsByPlugin[pluginId] = [.. items];
        ContributionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveTrayMenuItems(string pluginId)
    {
        lock (_lock) _trayMenuItemsByPlugin.Remove(pluginId);
        ContributionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddControlPanelWidget(PluginWidgetInfo widget)
    {
        lock (_lock) _widgets.Add(widget);
        ContributionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveControlPanelWidget(string pluginId)
    {
        lock (_lock) _widgets.RemoveAll(w => string.Equals(w.PluginId, pluginId, StringComparison.Ordinal));
        ContributionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
