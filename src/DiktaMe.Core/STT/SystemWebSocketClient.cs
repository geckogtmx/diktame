using System.Net.WebSockets;

namespace DiktaMe.Core.STT;

/// <summary>
/// Production implementation of <see cref="IWebSocketClient"/> wrapping
/// <see cref="ClientWebSocket"/>.
/// </summary>
public sealed class SystemWebSocketClient : IWebSocketClient
{
    private readonly ClientWebSocket _ws = new();

    /// <inheritdoc/>
    public WebSocketState State => _ws.State;

    /// <inheritdoc/>
    public void SetRequestHeader(string name, string value)
        => _ws.Options.SetRequestHeader(name, value);

    /// <inheritdoc/>
    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        => _ws.ConnectAsync(uri, cancellationToken);

    /// <inheritdoc/>
    public async Task SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        // ClientWebSocket.SendAsync(ReadOnlyMemory<byte>) returns ValueTask — await to bridge to Task
        await _ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<WebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        // ClientWebSocket.ReceiveAsync(Memory<byte>) returns ValueTask<ValueWebSocketReceiveResult>.
        // Convert to WebSocketReceiveResult for interface compatibility.
        ValueWebSocketReceiveResult result = await _ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        return new WebSocketReceiveResult(
            result.Count,
            result.MessageType,
            result.EndOfMessage);
    }

    /// <inheritdoc/>
    public Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
        => _ws.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _ws.Dispose();
}
