using DiktaMe.Core.Pipeline;
using Serilog;

namespace DiktaMe.Plugin;

public sealed class PluginContext : IPluginContext
{
    public required IServiceProvider Services { get; init; }
    public required IPipelineEventBus PipelineEvents { get; init; }
    public required IPluginSettingsStore Settings { get; init; }
    public required IPluginUIRegistry UI { get; init; }
    public required Action<Action> Dispatcher { get; init; }
    public required ILogger Logger { get; init; }
}
