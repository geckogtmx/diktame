using DiktaMe.Core.Pipeline;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Plugin;

public sealed class PipelineEventBusTests
{
    private readonly PipelineEventBus _bus = new();

    private static PipelineResult MakeResult(string text = "test") => new()
    {
        Text = text,
        Mode = "dictate",
        IsSuccess = true,
    };

    [Fact]
    public void Subscribe_and_publish_completed_invokes_handler()
    {
        PipelineResult? received = null;
        _bus.OnCompleted(r => received = r);

        var result = MakeResult();
        _bus.PublishCompleted(result);

        received.Should().BeSameAs(result);
    }

    [Fact]
    public void Subscribe_and_publish_before_llm_invokes_handler()
    {
        BeforeLlmContext? received = null;
        _bus.OnBeforeLlmProcessing(ctx => received = ctx);

        var ctx = new BeforeLlmContext { UserText = "hi", SystemPrompt = "sys", Mode = "dictate" };
        _bus.PublishBeforeLlm(ctx);

        received.Should().BeSameAs(ctx);
    }

    [Fact]
    public void Subscribe_and_publish_after_transcription_invokes_handler()
    {
        AfterTranscriptionContext? received = null;
        _bus.OnAfterTranscription(ctx => received = ctx);

        var ctx = new AfterTranscriptionContext { RawTranscript = "raw", Mode = "dictate" };
        _bus.PublishAfterTranscription(ctx);

        received.Should().BeSameAs(ctx);
    }

    [Fact]
    public void Subscribe_and_publish_state_changed_invokes_handler()
    {
        PipelineState? received = null;
        _bus.OnStateChanged(s => received = s);

        _bus.PublishStateChanged(PipelineState.Recording);

        received.Should().Be(PipelineState.Recording);
    }

    [Fact]
    public void Dispose_subscription_removes_handler()
    {
        var callCount = 0;
        var sub = _bus.OnCompleted(_ => callCount++);

        sub.Dispose();
        _bus.PublishCompleted(MakeResult());

        callCount.Should().Be(0);
    }

    [Fact]
    public void Multiple_handlers_all_invoked()
    {
        var calls = new List<int>();
        _bus.OnCompleted(_ => calls.Add(1));
        _bus.OnCompleted(_ => calls.Add(2));
        _bus.OnCompleted(_ => calls.Add(3));

        _bus.PublishCompleted(MakeResult());

        calls.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Handler_exception_does_not_break_other_handlers()
    {
        var called = false;
        _bus.OnCompleted(_ => throw new InvalidOperationException("boom"));
        _bus.OnCompleted(_ => called = true);

        var act = () => _bus.PublishCompleted(MakeResult());

        act.Should().NotThrow();
        called.Should().BeTrue();
    }

    [Fact]
    public void Publish_with_no_subscribers_does_not_throw()
    {
        var act = () => _bus.PublishCompleted(MakeResult());
        act.Should().NotThrow();
    }

    [Fact]
    public void Before_llm_context_allows_mutation()
    {
        var ctx = new BeforeLlmContext { UserText = "hi", SystemPrompt = "sys", Mode = "dictate" };
        _bus.OnBeforeLlmProcessing(c => c.AdditionalSystemContext += " extra");

        ctx.AdditionalSystemContext = "base";
        _bus.PublishBeforeLlm(ctx);

        ctx.AdditionalSystemContext.Should().Be("base extra");
    }

    [Fact]
    public void Dispose_subscription_twice_does_not_throw()
    {
        var sub = _bus.OnCompleted(_ => { });

        var act = () =>
        {
            sub.Dispose();
            sub.Dispose();
        };

        act.Should().NotThrow();
    }
}
