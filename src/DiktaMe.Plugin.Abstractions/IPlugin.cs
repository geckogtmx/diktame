namespace DiktaMe.Plugin;

public interface IPlugin : IAsyncDisposable
{
    string Id { get; }
    string DisplayName { get; }
    PluginState State { get; }
    Task InitializeAsync(IPluginContext context);
    Task EnableAsync();
    Task DisableAsync();
}
