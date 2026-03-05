using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.Core.STT;

/// <summary>
/// Real-time streaming STT provider backed by Deepgram's WebSocket Listen API.
/// Sends raw PCM audio frames and receives JSON transcript events.
/// </summary>
public sealed class DeepgramStreamingProvider : IStreamingSTTProvider
{
    // ── Constants ────────────────────────────────────────────────────────────
    private const string BaseWsUrl = "wss://api.deepgram.com/v1/listen";
    private const int ReceiveBufferSize = 8192; // 8KB — sufficient for JSON transcript frames
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    // Pre-encoded JSON messages for zero-alloc sends
    private static readonly byte[] KeepAliveMessage =
        Encoding.UTF8.GetBytes("""{"type":"KeepAlive"}""");

    private static readonly byte[] CloseStreamMessage =
        Encoding.UTF8.GetBytes("""{"type":"CloseStream"}""");

    // ── Dependencies ────────────────────────────────────────────────────────
    private readonly string _apiKey;
    private readonly DeepgramSettings _settings;
    private readonly IWebSocketClient _webSocket;

    // ── State ────────────────────────────────────────────────────────────────
    private Task? _receiveLoopTask;
    private PeriodicTimer? _keepAliveTimer;
    private Task? _keepAliveTask;
    private CancellationTokenSource? _receiveCts;
    private TaskCompletionSource? _closeTcs;
    private bool _disposed;

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the streaming provider.
    /// </summary>
    /// <param name="apiKey">Deepgram API key.</param>
    /// <param name="settings">Deepgram feature settings. Uses defaults if null.</param>
    /// <param name="webSocketClient">
    /// Optional WebSocket client (for testing). If null, creates a <see cref="SystemWebSocketClient"/>.
    /// </param>
    public DeepgramStreamingProvider(
        string apiKey,
        DeepgramSettings? settings = null,
        IWebSocketClient? webSocketClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Deepgram API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _settings = settings ?? new DeepgramSettings();
        _webSocket = webSocketClient ?? new SystemWebSocketClient();
    }

    // ── Properties & Events ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public string ProviderName => $"Deepgram {_settings.Model} Streaming";

    /// <inheritdoc/>
    public bool IsConnected => !_disposed && _webSocket.State == WebSocketState.Open;

    /// <inheritdoc/>
    public event EventHandler<StreamingTranscriptEventArgs>? PartialTranscriptReceived;

    /// <inheritdoc/>
    public event EventHandler<StreamingTranscriptEventArgs>? FinalTranscriptReceived;

    /// <inheritdoc/>
    public event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ConnectAsync(string language, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State == WebSocketState.Open)
        {
            throw new InvalidOperationException("Already connected.");
        }

        string url = BuildStreamingUrl(language);
        _webSocket.SetRequestHeader("Authorization", $"Token {_apiKey}");

        Log.Information("DeepgramStreaming: connecting to {Url}", url);
        await _webSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        Log.Information("DeepgramStreaming: connected");

        // Start background receive loop
        _receiveCts = new CancellationTokenSource();
        _closeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), CancellationToken.None);

        // Start keepalive timer
        StartKeepAlive();
    }

    /// <inheritdoc/>
    public async Task SendAudioAsync(ReadOnlyMemory<byte> pcmData, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Not connected.");
        }

        await _webSocket.SendAsync(
            pcmData,
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State != WebSocketState.Open)
        {
            return;
        }

        Log.Information("DeepgramStreaming: closing connection");
        StopKeepAlive();

        try
        {
            // Send CloseStream message
            await _webSocket.SendAsync(
                CloseStreamMessage,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);

            // Wait for remaining finals (server will close after flushing)
            if (_closeTcs is not null)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(CloseTimeout);

                try
                {
                    await _closeTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("DeepgramStreaming: close timed out after {Timeout}s",
                        CloseTimeout.TotalSeconds);
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "DeepgramStreaming: error during close");
        }
        finally
        {
            // Cancel the receive loop
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = null;

            // Close the WebSocket if still open
            if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await _webSocket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Done",
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (WebSocketException) { /* best-effort */ }
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopKeepAlive();
        _receiveCts?.Cancel();

        // Wait for background tasks to finish
        if (_receiveLoopTask is not null)
        {
            try { await _receiveLoopTask.ConfigureAwait(false); }
            catch { /* best-effort */ }
        }

        if (_keepAliveTask is not null)
        {
            try { await _keepAliveTask.ConfigureAwait(false); }
            catch { /* best-effort */ }
        }

        _receiveCts?.Dispose();
        _closeTcs?.TrySetResult();
        _webSocket.Dispose();
    }

    // ── URL builder ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the WebSocket streaming URL from settings and language.
    /// Appends audio encoding params required for streaming (not needed for batch).
    /// </summary>
    internal string BuildStreamingUrl(string language)
    {
        string sharedParams = DeepgramProvider.BuildQueryParameters(_settings, language);
        return $"{BaseWsUrl}?{sharedParams}&encoding=linear16&sample_rate=16000&channels=1";
    }

    // ── Receive loop ────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested
                   && _webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(
                        buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    Log.Error(ex, "DeepgramStreaming: WebSocket receive error");
                    OnError("WebSocket receive error", ex);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log.Information("DeepgramStreaming: server closed connection");
                    _closeTcs?.TrySetResult();
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    if (!result.EndOfMessage)
                    {
                        Log.Warning("DeepgramStreaming: received fragmented message ({Count} bytes), skipping",
                            result.Count);
                        continue;
                    }

                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessTranscriptMessage(json);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "DeepgramStreaming: receive loop failed");
            OnError("Receive loop failed", ex);
        }
        finally
        {
            _closeTcs?.TrySetResult();
        }
    }

    // ── JSON parsing ────────────────────────────────────────────────────────

    private void ProcessTranscriptMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check message type
            if (root.TryGetProperty("type", out var typeEl))
            {
                string? type = typeEl.GetString();

                // Error messages from Deepgram
                if (string.Equals(type, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    string errorMsg = root.TryGetProperty("description", out var desc)
                        ? desc.GetString() ?? "Unknown error"
                        : "Unknown error";
                    Log.Warning("DeepgramStreaming: server error — {Error}", errorMsg);
                    OnError($"Deepgram server error: {errorMsg}", null);
                    return;
                }

                // Skip non-Results messages (Metadata, etc.)
                if (!string.Equals(type, "Results", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // Parse transcript: channel.alternatives[0].transcript
            if (!root.TryGetProperty("channel", out var channel))
            {
                return;
            }

            if (!channel.TryGetProperty("alternatives", out var alts))
            {
                return;
            }

            if (alts.GetArrayLength() == 0)
            {
                return;
            }

            var firstAlt = alts[0];
            string transcript = firstAlt.TryGetProperty("transcript", out var t)
                ? t.GetString() ?? string.Empty
                : string.Empty;

            double confidence = firstAlt.TryGetProperty("confidence", out var c)
                ? c.GetDouble()
                : 0.0;

            bool isFinal = root.TryGetProperty("is_final", out var isF) && isF.GetBoolean();

            double duration = root.TryGetProperty("duration", out var dur)
                ? dur.GetDouble()
                : 0.0;

            var args = new StreamingTranscriptEventArgs(
                transcript,
                isFinal: isFinal,
                confidence: confidence,
                duration: TimeSpan.FromSeconds(duration));

            if (isFinal)
            {
                Log.Debug("DeepgramStreaming: final — \"{Transcript}\" (conf={Confidence:F2})",
                    transcript.Length > 60 ? string.Concat(transcript.AsSpan(0, 60), "...") : transcript,
                    confidence);
                FinalTranscriptReceived?.Invoke(this, args);
            }
            else
            {
                PartialTranscriptReceived?.Invoke(this, args);
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "DeepgramStreaming: failed to parse transcript JSON");
        }
    }

    // ── KeepAlive ───────────────────────────────────────────────────────────

    private void StartKeepAlive()
    {
        _keepAliveTimer = new PeriodicTimer(KeepAliveInterval);
        _keepAliveTask = Task.Run(async () =>
        {
            try
            {
                while (await _keepAliveTimer.WaitForNextTickAsync().ConfigureAwait(false))
                {
                    if (_webSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    await _webSocket.SendAsync(
                        KeepAliveMessage,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { /* timer disposed */ }
            catch (ObjectDisposedException) { /* timer disposed */ }
            catch (Exception ex)
            {
                Log.Warning(ex, "DeepgramStreaming: KeepAlive send failed");
            }
        });
    }

    private void StopKeepAlive()
    {
        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OnError(string message, Exception? ex)
    {
        ErrorOccurred?.Invoke(this, new StreamingErrorEventArgs(message, ex));
    }
}
