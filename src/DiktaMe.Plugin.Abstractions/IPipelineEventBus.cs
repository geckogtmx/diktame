using DiktaMe.Core.Pipeline;

namespace DiktaMe.Plugin;

public interface IPipelineEventBus
{
    IDisposable OnCompleted(Action<PipelineResult> handler);
    IDisposable OnBeforeLlmProcessing(Action<BeforeLlmContext> handler);
    IDisposable OnAfterTranscription(Action<AfterTranscriptionContext> handler);
    IDisposable OnStateChanged(Action<PipelineState> handler);
}
