using System.Net.WebSockets;
using System.Text;
using DiktaMe.Core.STT;

namespace DiktaMe.Core.Tests.STT;

/// <summary>
/// Fake WebSocket for testing <see cref="DeepgramStreamingProvider"/>.
/// Enqueues canned JSON responses that are returned by ReceiveAsync.
/// </summary>
internal sealed class FakeWebSocket : IWebSocketClient
{
    private readonly Queue<(string Json, WebSocketMessageType Type)> _receiveQueue = new();
    private readonly List<byte[]> _sentBinaryFrames = [];
    private readonly List<string> _sentTextMessages = [];
    private readonly TaskCompletionSource _allDequeued = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WebSocketState State { get; set; } = WebSocketState.None;
    public string? LastHeaderName { get; private set; }
    public string? LastHeaderValue { get; private set; }
    public Uri? ConnectedUri { get; private set; }
    public IReadOnlyList<byte[]> SentBinaryFrames => _sentBinaryFrames;
    public IReadOnlyList<string> SentTextMessages => _sentTextMessages;
    public bool CloseOutputCalled { get; private set; }

    /// <summary>Enqueue a JSON text response to be returned by ReceiveAsync.</summary>
    public void EnqueueResponse(string json)
        => _receiveQueue.Enqueue((json, WebSocketMessageType.Text));

    /// <summary>Enqueue a close frame.</summary>
    public void EnqueueClose()
        => _receiveQueue.Enqueue(("", WebSocketMessageType.Close));

    public void SetRequestHeader(string name, string value)
    {
        LastHeaderName = name;
        LastHeaderValue = value;
    }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        ConnectedUri = uri;
        State = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
        bool endOfMessage, CancellationToken cancellationToken)
    {
        if (messageType == WebSocketMessageType.Binary)
        {
            _sentBinaryFrames.Add(buffer.ToArray());
        }
        else if (messageType == WebSocketMessageType.Text)
        {
            _sentTextMessages.Add(Encoding.UTF8.GetString(buffer.Span));
        }

        return Task.CompletedTask;
    }

    public async Task<WebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken)
    {
        // Wait until a message is available or cancellation
        while (_receiveQueue.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        var (json, type) = _receiveQueue.Dequeue();

        if (type == WebSocketMessageType.Close)
        {
            State = WebSocketState.CloseReceived;
            if (_receiveQueue.Count == 0)
            {
                _allDequeued.TrySetResult();
            }

            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                WebSocketCloseStatus.NormalClosure, "");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        bytes.CopyTo(buffer);
        if (_receiveQueue.Count == 0)
        {
            _allDequeued.TrySetResult();
        }

        return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
    }

    public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken)
    {
        CloseOutputCalled = true;
        State = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    /// <summary>Wait until all enqueued messages have been consumed.</summary>
    public Task WaitForAllConsumedAsync(TimeSpan timeout)
        => _allDequeued.Task.WaitAsync(timeout);

    public void Dispose() => State = WebSocketState.Closed;
}
