using Serilog;

namespace DiktaMe.Core.Pipeline;

public sealed class PipelineEventBus : IPipelineEventBus
{
    private readonly object _lock = new();
    private readonly List<Action<PipelineResult>> _completedHandlers = [];
    private readonly List<Action<BeforeLlmContext>> _beforeLlmHandlers = [];
    private readonly List<Action<AfterTranscriptionContext>> _afterTranscriptionHandlers = [];
    private readonly List<Action<PipelineState>> _stateChangedHandlers = [];
    private readonly ILogger _logger = Log.ForContext<PipelineEventBus>();

    public IDisposable OnCompleted(Action<PipelineResult> handler)
    {
        lock (_lock) _completedHandlers.Add(handler);
        return new Subscription(() => { lock (_lock) _completedHandlers.Remove(handler); });
    }

    public IDisposable OnBeforeLlmProcessing(Action<BeforeLlmContext> handler)
    {
        lock (_lock) _beforeLlmHandlers.Add(handler);
        return new Subscription(() => { lock (_lock) _beforeLlmHandlers.Remove(handler); });
    }

    public IDisposable OnAfterTranscription(Action<AfterTranscriptionContext> handler)
    {
        lock (_lock) _afterTranscriptionHandlers.Add(handler);
        return new Subscription(() => { lock (_lock) _afterTranscriptionHandlers.Remove(handler); });
    }

    public IDisposable OnStateChanged(Action<PipelineState> handler)
    {
        lock (_lock) _stateChangedHandlers.Add(handler);
        return new Subscription(() => { lock (_lock) _stateChangedHandlers.Remove(handler); });
    }

    public void PublishCompleted(PipelineResult result)
    {
        List<Action<PipelineResult>> snapshot;
        lock (_lock) snapshot = [.. _completedHandlers];
        foreach (var handler in snapshot)
        {
            try { handler(result); }
            catch (Exception ex) { _logger.Error(ex, "Event handler failed in OnCompleted"); }
        }
    }

    public void PublishBeforeLlm(BeforeLlmContext context)
    {
        List<Action<BeforeLlmContext>> snapshot;
        lock (_lock) snapshot = [.. _beforeLlmHandlers];
        foreach (var handler in snapshot)
        {
            try { handler(context); }
            catch (Exception ex) { _logger.Error(ex, "Event handler failed in OnBeforeLlmProcessing"); }
        }
    }

    public void PublishAfterTranscription(AfterTranscriptionContext context)
    {
        List<Action<AfterTranscriptionContext>> snapshot;
        lock (_lock) snapshot = [.. _afterTranscriptionHandlers];
        foreach (var handler in snapshot)
        {
            try { handler(context); }
            catch (Exception ex) { _logger.Error(ex, "Event handler failed in OnAfterTranscription"); }
        }
    }

    public void PublishStateChanged(PipelineState state)
    {
        List<Action<PipelineState>> snapshot;
        lock (_lock) snapshot = [.. _stateChangedHandlers];
        foreach (var handler in snapshot)
        {
            try { handler(state); }
            catch (Exception ex) { _logger.Error(ex, "Event handler failed in OnStateChanged"); }
        }
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
