namespace DiktaMe.Core.Pipeline;

public interface IPipelineEventBus
{
    IDisposable OnCompleted(Action<PipelineResult> handler);
    IDisposable OnBeforeLlmProcessing(Action<BeforeLlmContext> handler);
    IDisposable OnAfterTranscription(Action<AfterTranscriptionContext> handler);
    IDisposable OnStateChanged(Action<PipelineState> handler);
}
