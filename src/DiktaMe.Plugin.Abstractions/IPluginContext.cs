using DiktaMe.Core.Pipeline;
using Serilog;

namespace DiktaMe.Plugin;

public interface IPluginContext
{
    IServiceProvider Services { get; }
    IPipelineEventBus PipelineEvents { get; }
    IPluginSettingsStore Settings { get; }
    IPluginUIRegistry UI { get; }
    Action<Action> Dispatcher { get; }
    ILogger Logger { get; }
}
