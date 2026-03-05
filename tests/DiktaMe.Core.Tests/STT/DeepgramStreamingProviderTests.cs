using System.Net.WebSockets;
using DiktaMe.Core.Config;
using DiktaMe.Core.STT;
using FluentAssertions;

namespace DiktaMe.Core.Tests.STT;

/// <summary>
/// Tests for <see cref="DeepgramStreamingProvider"/>.
/// </summary>
public sealed class DeepgramStreamingProviderTests
{
    // ── Sample JSON responses (Deepgram streaming format) ────────────────────

    private const string FinalTranscriptJson = """
        {
            "type": "Results",
            "channel_index": [0, 1],
            "duration": 1.52,
            "start": 0.0,
            "is_final": true,
            "speech_final": true,
            "channel": {
                "alternatives": [{
                    "transcript": "hello world",
                    "confidence": 0.99
                }]
            }
        }
        """;

    private const string PartialTranscriptJson = """
        {
            "type": "Results",
            "channel_index": [0, 1],
            "duration": 0.8,
            "start": 0.0,
            "is_final": false,
            "speech_final": false,
            "channel": {
                "alternatives": [{
                    "transcript": "hello",
                    "confidence": 0.85
                }]
            }
        }
        """;

    private const string EmptyTranscriptJson = """
        {
            "type": "Results",
            "channel_index": [0, 1],
            "duration": 0.0,
            "start": 0.0,
            "is_final": true,
            "speech_final": false,
            "channel": {
                "alternatives": [{
                    "transcript": "",
                    "confidence": 0.0
                }]
            }
        }
        """;

    private const string MetadataJson = """
        {
            "type": "Metadata",
            "transaction_key": "abc123",
            "request_id": "req-456",
            "sha256": "deadbeef",
            "created": "2026-03-05T12:00:00Z"
        }
        """;

    private const string ErrorJson = """
        {
            "type": "Error",
            "description": "Rate limit exceeded",
            "message": "Too many requests"
        }
        """;

    // ── Constructor tests ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        var act = () => new DeepgramStreamingProvider("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullApiKey_Throws()
    {
        var act = () => new DeepgramStreamingProvider(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ValidKey_SetsProviderName()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.ProviderName.Should().Be("Deepgram nova-3 Streaming");
    }

    // ── ConnectAsync tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Connect_SetsAuthorizationHeader()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();
        await using var provider = new DeepgramStreamingProvider("my_key", webSocketClient: ws);

        await provider.ConnectAsync("en");
        await Task.Delay(50); // let receive loop start

        ws.LastHeaderName.Should().Be("Authorization");
        ws.LastHeaderValue.Should().Be("Token my_key");
    }

    [Fact]
    public async Task Connect_BuildsCorrectUrl()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        await provider.ConnectAsync("en");
        await Task.Delay(50);

        string url = ws.ConnectedUri!.ToString();
        url.Should().StartWith("wss://api.deepgram.com/v1/listen?");
        url.Should().Contain("encoding=linear16");
        url.Should().Contain("sample_rate=16000");
        url.Should().Contain("channels=1");
        url.Should().Contain("model=nova-3");
        url.Should().Contain("language=en");
    }

    [Fact]
    public async Task Connect_SetsIsConnectedTrue()
    {
        var ws = new FakeWebSocket();
        // No close enqueued — connection stays open
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        await provider.ConnectAsync("en");

        provider.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_WhenAlreadyConnected_Throws()
    {
        var ws = new FakeWebSocket();
        // No close enqueued — connection stays open
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.ConnectAsync("en");

        var act = () => provider.ConnectAsync("en");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── BuildStreamingUrl tests ─────────────────────────────────────────────

    [Fact]
    public void BuildStreamingUrl_DefaultSettings_ContainsExpectedParams()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        string url = provider.BuildStreamingUrl("en");

        url.Should().Contain("model=nova-3");
        url.Should().Contain("punctuate=true");
        url.Should().Contain("dictation=true");
        url.Should().Contain("encoding=linear16");
        url.Should().Contain("sample_rate=16000");
        url.Should().Contain("channels=1");
        url.Should().Contain("language=en");
    }

    [Fact]
    public void BuildStreamingUrl_AutoLanguage_ContainsDetectLanguage()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        string url = provider.BuildStreamingUrl("auto");

        url.Should().Contain("detect_language=true");
        url.Should().NotContain("&language=");
    }

    [Fact]
    public void BuildStreamingUrl_SpecificLanguage_ContainsLanguage()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        string url = provider.BuildStreamingUrl("es");

        url.Should().Contain("language=es");
    }

    [Fact]
    public void BuildStreamingUrl_SmartFormatOn_OmitsPunctuate()
    {
        var settings = new DeepgramSettings { SmartFormat = true };
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", settings, ws);

        string url = provider.BuildStreamingUrl("en");

        url.Should().Contain("smart_format=true");
        url.Should().NotContain("punctuate=true");
    }

    [Fact]
    public void BuildStreamingUrl_Replacements_AppendedCorrectly()
    {
        var settings = new DeepgramSettings { Replacements = ["monika:Monica", "zen desk:Zendesk"] };
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", settings, ws);

        string url = provider.BuildStreamingUrl("en");

        url.Should().Contain("replace=monika%3AMonica");
        url.Should().Contain("replace=zen%20desk%3AZendesk");
    }

    // ── SendAudioAsync tests ────────────────────────────────────────────────

    [Fact]
    public async Task SendAudio_WhenConnected_SendsBinaryFrame()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.ConnectAsync("en");

        byte[] pcm = [1, 2, 3, 4];
        await provider.SendAudioAsync(pcm);

        ws.SentBinaryFrames.Should().HaveCount(1);
        ws.SentBinaryFrames[0].Should().BeEquivalentTo(pcm);
    }

    [Fact]
    public async Task SendAudio_WhenNotConnected_Throws()
    {
        var ws = new FakeWebSocket();
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        var act = () => provider.SendAudioAsync(new byte[] { 1, 2, 3 });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAudio_AfterDispose_Throws()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.DisposeAsync();

        var act = () => provider.SendAudioAsync(new byte[] { 1, 2, 3 });

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ── Receive / Transcript event tests ────────────────────────────────────

    [Fact]
    public async Task ReceiveLoop_FinalTranscript_RaisesFinalEvent()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueResponse(FinalTranscriptJson);
        ws.EnqueueClose();

        StreamingTranscriptEventArgs? received = null;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.FinalTranscriptReceived += (_, e) => received = e;

        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100); // let event handler fire

        received.Should().NotBeNull();
        received!.Transcript.Should().Be("hello world");
        received.IsFinal.Should().BeTrue();
        received.Confidence.Should().BeApproximately(0.99, 0.01);
        received.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(1.52), TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task ReceiveLoop_PartialTranscript_RaisesPartialEvent()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueResponse(PartialTranscriptJson);
        ws.EnqueueClose();

        StreamingTranscriptEventArgs? received = null;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.PartialTranscriptReceived += (_, e) => received = e;

        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        received.Should().NotBeNull();
        received!.Transcript.Should().Be("hello");
        received.IsFinal.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveLoop_EmptyTranscript_StillRaisesEvent()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueResponse(EmptyTranscriptJson);
        ws.EnqueueClose();

        StreamingTranscriptEventArgs? received = null;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.FinalTranscriptReceived += (_, e) => received = e;

        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        received.Should().NotBeNull();
        received!.Transcript.Should().BeEmpty();
    }

    [Fact]
    public async Task ReceiveLoop_MalformedJson_DoesNotThrow()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueResponse("not valid json {{{");
        ws.EnqueueClose();

        bool anyEventFired = false;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.FinalTranscriptReceived += (_, _) => anyEventFired = true;
        provider.PartialTranscriptReceived += (_, _) => anyEventFired = true;

        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        anyEventFired.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveLoop_MetadataMessage_Ignored()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueResponse(MetadataJson);
        ws.EnqueueClose();

        bool anyTranscriptFired = false;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.FinalTranscriptReceived += (_, _) => anyTranscriptFired = true;
        provider.PartialTranscriptReceived += (_, _) => anyTranscriptFired = true;

        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        anyTranscriptFired.Should().BeFalse();
    }

    [Fact]
    public async Task ReceiveLoop_ErrorMessage_RaisesErrorEvent()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueResponse(ErrorJson);
        ws.EnqueueClose();

        StreamingErrorEventArgs? received = null;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.ErrorOccurred += (_, e) => received = e;

        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        received.Should().NotBeNull();
        received!.Message.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task ReceiveLoop_CloseFrame_CompletesGracefully()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();

        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.ConnectAsync("en");
        await ws.WaitForAllConsumedAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        // Provider should still be usable (not crashed)
        // The receive loop exited normally
    }

    // ── KeepAlive tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task KeepAlive_AfterConnect_SendsKeepAliveMessages()
    {
        var ws = new FakeWebSocket();
        // Don't enqueue close — let keepalive run for a bit
        // But we need something in the queue to prevent ReceiveAsync from blocking forever after cancel
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.ConnectAsync("en");

        // Wait long enough for at least one keepalive (8s interval + margin)
        await Task.Delay(9000);

        // Enqueue close to let receive loop end
        ws.EnqueueClose();
        await Task.Delay(200);

        ws.SentTextMessages.Should().Contain("""{"type":"KeepAlive"}""");
    }

    // ── CloseAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task Close_SendsCloseStreamMessage()
    {
        var ws = new FakeWebSocket();
        // No pre-enqueued close — connection stays open for receive loop
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.ConnectAsync("en");

        // Simulate server closing after we send CloseStream
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            ws.EnqueueClose();
        });

        await provider.CloseAsync();

        ws.SentTextMessages.Should().Contain("""{"type":"CloseStream"}""");
    }

    [Fact]
    public async Task Close_WaitsForRemainingFinals()
    {
        var ws = new FakeWebSocket();

        StreamingTranscriptEventArgs? received = null;
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        provider.FinalTranscriptReceived += (_, e) => received = e;
        await provider.ConnectAsync("en");

        // Simulate server sending a final transcript then closing after CloseStream
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            ws.EnqueueResponse(FinalTranscriptJson);
            ws.EnqueueClose();
        });

        await provider.CloseAsync();

        received.Should().NotBeNull();
        received!.Transcript.Should().Be("hello world");
    }

    [Fact]
    public async Task Close_WhenNotConnected_DoesNotThrow()
    {
        var ws = new FakeWebSocket();
        await using var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        // Should not throw — just returns silently
        await provider.CloseAsync();
    }

    // ── DisposeAsync tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_WhenConnected_CleansUp()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);
        await provider.ConnectAsync("en");
        await Task.Delay(50);

        await provider.DisposeAsync();

        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_WhenNotConnected_DoesNotThrow()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        await provider.DisposeAsync();
        // No exception = pass
    }

    [Fact]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        var ws = new FakeWebSocket();
        var provider = new DeepgramStreamingProvider("key", webSocketClient: ws);

        await provider.DisposeAsync();
        await provider.DisposeAsync();
        // No exception = pass
    }
}
