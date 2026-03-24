using System.Reflection;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Plugin;

public sealed class PluginManager
{
    private readonly List<PluginInfo> _plugins = [];
    private readonly ILogger _logger = Log.ForContext<PluginManager>();
    private string _pluginsDir = string.Empty;
    private string _stateFilePath = string.Empty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public IReadOnlyList<PluginInfo> Plugins => _plugins.AsReadOnly();

    public async Task DiscoverAndLoadAsync(string pluginsDirectory, IPluginContext context)
    {
        _pluginsDir = pluginsDirectory;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _stateFilePath = Path.Combine(appData, "DiktaMe", "plugin-states.json");

        if (!Directory.Exists(_pluginsDir))
        {
            Directory.CreateDirectory(_pluginsDir);
            return;
        }

        var enabledStates = await LoadEnabledStatesAsync().ConfigureAwait(false);

        foreach (var dir in Directory.GetDirectories(_pluginsDir))
        {
            foreach (var dll in Directory.GetFiles(dir, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    foreach (var type in assembly.GetTypes())
                    {
                        var attr = type.GetCustomAttribute<PluginEntryAttribute>();
                        if (attr is null || !typeof(IPlugin).IsAssignableFrom(type)) continue;

                        var plugin = (IPlugin)Activator.CreateInstance(type)!;
                        await plugin.InitializeAsync(context).ConfigureAwait(false);

                        var info = new PluginInfo(attr.Id, attr.DisplayName, attr.Version, plugin);
                        _plugins.Add(info);

                        if (enabledStates.GetValueOrDefault(attr.Id, defaultValue: false))
                        {
                            await plugin.EnableAsync().ConfigureAwait(false);
                        }

                        _logger.Information("Loaded plugin {Id} v{Version}", attr.Id, attr.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load plugin from {Dll}", dll);
                }
            }
        }
    }

    public async Task EnablePluginAsync(string pluginId)
    {
        var info = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.Ordinal));
        if (info?.Plugin is { State: PluginState.Initialized or PluginState.Disabled } plugin)
        {
            await plugin.EnableAsync().ConfigureAwait(false);
            await SaveEnabledStatesAsync().ConfigureAwait(false);
        }
    }

    public async Task DisablePluginAsync(string pluginId)
    {
        var info = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.Ordinal));
        if (info?.Plugin is { State: PluginState.Enabled } plugin)
        {
            await plugin.DisableAsync().ConfigureAwait(false);
            await SaveEnabledStatesAsync().ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, bool>> LoadEnabledStatesAsync()
    {
        if (!File.Exists(_stateFilePath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load plugin states from {Path}", _stateFilePath);
            return [];
        }
    }

    private async Task SaveEnabledStatesAsync()
    {
        var states = new Dictionary<string, bool>();
        foreach (var info in _plugins)
        {
            states[info.Id] = info.Plugin.State == PluginState.Enabled;
        }

        var dir = Path.GetDirectoryName(_stateFilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(states, JsonOptions);
        var tmpPath = _stateFilePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
        File.Move(tmpPath, _stateFilePath, overwrite: true);
    }
}

public sealed record PluginInfo(string Id, string DisplayName, string Version, IPlugin Plugin);
