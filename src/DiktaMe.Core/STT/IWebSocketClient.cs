using System.Net.WebSockets;

namespace DiktaMe.Core.STT;

/// <summary>
/// Abstraction over <see cref="ClientWebSocket"/> for testability.
/// Only exposes the subset needed by <see cref="DeepgramStreamingProvider"/>.
/// </summary>
public interface IWebSocketClient : IDisposable
{
    /// <summary>Current WebSocket state.</summary>
    WebSocketState State { get; }

    /// <summary>Sets a custom request header before connecting.</summary>
    void SetRequestHeader(string name, string value);

    /// <summary>Opens a WebSocket connection.</summary>
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>Sends binary or text data over the WebSocket.</summary>
    Task SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken);

    /// <summary>Receives data from the WebSocket.</summary>
    Task<WebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken);

    /// <summary>Initiates a one-way WebSocket close handshake (sends close frame without waiting for reply).</summary>
    Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken);
}
