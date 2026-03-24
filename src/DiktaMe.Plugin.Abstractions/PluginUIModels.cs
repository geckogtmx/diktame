namespace DiktaMe.Plugin;

public sealed record PluginSettingsPageInfo(
    string PluginId,
    string NavigationTag,
    string DisplayName,
    string IconGlyph,
    Func<object> PageFactory);

public sealed record PluginTrayMenuItem(
    string Label,
    Action OnClick,
    bool IsSeparatorBefore = false);

public sealed record PluginWidgetInfo(
    string PluginId,
    string Title,
    Func<object> WidgetFactory);
