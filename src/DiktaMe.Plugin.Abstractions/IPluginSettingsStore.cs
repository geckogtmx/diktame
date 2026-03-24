namespace DiktaMe.Plugin;

public interface IPluginSettingsStore
{
    Task<T> LoadAsync<T>() where T : new();
    Task SaveAsync<T>(T settings);
    event EventHandler? SettingsChanged;
}
