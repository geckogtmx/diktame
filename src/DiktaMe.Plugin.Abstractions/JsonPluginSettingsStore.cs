using System.Text.Json;

namespace DiktaMe.Plugin;

public sealed class JsonPluginSettingsStore : IPluginSettingsStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonPluginSettingsStore(string pluginId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DiktaMe", "plugins");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, $"{pluginId}-settings.json");
    }

    public event EventHandler? SettingsChanged;

    public async Task<T> LoadAsync<T>() where T : new()
    {
        if (!File.Exists(_filePath)) return new T();
        var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
    }

    public async Task SaveAsync<T>(T settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tmpPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
        File.Move(tmpPath, _filePath, overwrite: true);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
