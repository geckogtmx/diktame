using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Security;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Account;

/// <summary>
/// Streaming STT provider for Wallet users that proxies audio to the Gemini Live API
/// via the wallet-stream Supabase Edge Function.
///
/// Architecture — Buffer-and-Flush:
/// ConnectAsync() immediately returns (and sets IsConnected = true) while the WebSocket
/// handshake runs asynchronously in the background. Audio chunks arriving via SendAudioAsync()
/// during the handshake are buffered in memory. Once the Edge Function sends {"type":"ready"},
/// the buffer is flushed and we transition to live streaming.
///
/// This guarantees that pressing the dictation hotkey instantly plays the start sound and
/// begins capturing audio, with zero perceived latency — even on a cold Edge Function start.
///
/// On ANY connection failure, a WalletStreamingFallbackException is thrown so the caller
/// (LoadingViewModel) can fall back silently to the batch pipeline.
/// </summary>
public sealed class WalletStreamingSTTProxy : IStreamingSTTProvider
{
    private const string TokenKey = "trial_token";
    private const string DefaultStreamUrl =
        "wss://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/wallet-stream";

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly WalletManager _wallet;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _sessionCts;
    private Task? _receiveLoop;

    // Buffer for audio arriving before the WS is ready
    private readonly ConcurrentQueue<byte[]> _audioBuffer = new();
    private bool _wsReady;               // set when edge fn sends {"type":"ready"}
    private bool _audioEnded;            // set when CloseAsync is called
    private bool _disposed;

    // Completion: resolves when the edge fn sends {"type":"final"} or an error/fallback
    private readonly TaskCompletionSource<FinalMessage?> _finalTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Track accumulated text for partial display
    private readonly StringBuilder _accumulatedText = new();

    // IStreamingSTTProvider ──────────────────────────────────────────────────

    public string ProviderName => "Wallet Streaming (Gemini Live)";

    /// <summary>
    /// Returns true as soon as ConnectAsync is called — we accept audio immediately
    /// and buffer it until the WebSocket is ready.
    /// </summary>
    public bool IsConnected { get; private set; }

    public event EventHandler<StreamingTranscriptEventArgs>? PartialTranscriptReceived;
    public event EventHandler<StreamingTranscriptEventArgs>? FinalTranscriptReceived;
    public event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;

    /// <summary>Raised when the proxy returns an updated wallet balance.</summary>
    public event Action<long>? BalanceUpdated;

    public WalletStreamingSTTProxy(
        SecureStorage secureStorage,
        SettingsManager settings,
        WalletManager wallet)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _wallet = wallet;
    }

    /// <summary>
    /// Starts the WebSocket connection asynchronously in the background and returns immediately.
    /// IsConnected is set to true right away so the pipeline starts buffering audio.
    /// The caller does NOT need to wait for the network handshake.
    /// </summary>
    public Task ConnectAsync(string language, CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            throw new WalletStreamingFallbackException("No wallet JWT token available.");
        }

        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        string streamUrl = BuildStreamUrl(token, language);
        Log.Information("WalletStreaming: starting background connection to {Url}",
            streamUrl.Split('?')[0]);

        // Mark as "accepting audio" immediately — buffer mode
        IsConnected = true;

        // Fire-and-forget: connect + receive loop runs in background
        _receiveLoop = Task.Run(
            () => RunConnectionAsync(streamUrl, language, _sessionCts.Token),
            _sessionCts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a PCM audio chunk. If the WebSocket is not yet ready, the chunk is
    /// buffered and will be flushed once the connection is established.
    /// </summary>
    public async Task SendAudioAsync(ReadOnlyMemory<byte> pcmData, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _disposed)
        {
            return;
        }

        byte[] copy = pcmData.ToArray();

        if (!_wsReady)
        {
            // Connection still in progress — buffer the chunk
            _audioBuffer.Enqueue(copy);
            return;
        }

        // WS is ready — send directly
        await SendPcmChunkAsync(copy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Signals end of audio, then waits for the edge function to deliver the final transcript.
    /// The final transcript is emitted via FinalTranscriptReceived before this returns.
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _audioEnded = true;
        Log.Information("WalletStreaming: audio ended, signaling end to edge function");

        if (_wsReady && _ws?.State == WebSocketState.Open)
        {
            await SendJsonAsync("{\"type\":\"end_audio\"}", cancellationToken).ConfigureAwait(false);
        }

        // Wait for the final message (with timeout)
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            FinalMessage? final = await _finalTcs.Task
                .WaitAsync(linked.Token)
                .ConfigureAwait(false);

            if (final is not null)
            {
                Log.Information("WalletStreaming: final text received ({Chars} chars, cost={Cost}µ$)",
                    final.Text.Length, final.Cost);

                // Record cost locally
                if (final.Cost > 0)
                {
                    await _wallet.InsertTransactionAsync(
                        -final.Cost,
                        WalletTransactionType.Usage,
                        "{\"service\":\"gemini-live\"}",
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                if (final.Balance > 0)
                {
                    BalanceUpdated?.Invoke(final.Balance);
                }

                // Emit final transcript
                if (!string.IsNullOrWhiteSpace(final.Text))
                {
                    FinalTranscriptReceived?.Invoke(this,
                        new StreamingTranscriptEventArgs(
                            transcript: final.Text,
                            isFinal: true,
                            confidence: 1.0,
                            duration: TimeSpan.Zero));
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("WalletStreaming: timed out waiting for final transcript");
        }
    }

    // ── IAsyncDisposable ────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsConnected = false;

        _sessionCts?.Cancel();

        try
        {
            if (_ws?.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch { /* ignore close errors during dispose */ }

        try
        {
            if (_receiveLoop is not null)
            {
                await _receiveLoop.ConfigureAwait(false);
            }
        }
        catch { /* ignore task errors during dispose */ }

        _ws?.Dispose();
        _sessionCts?.Dispose();
    }

    // ── Connection + Receive Loop ────────────────────────────────────────────

    private async Task RunConnectionAsync(
        string streamUrl, string language, CancellationToken cancellationToken)
    {
        try
        {
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

            Log.Information("WalletStreaming: connecting WebSocket...");
            await _ws.ConnectAsync(new Uri(streamUrl), cancellationToken)
                .ConfigureAwait(false);

            Log.Information("WalletStreaming: WebSocket connected, sending session config");

            // Send session config as the first text message
            string configJson = $"{{\"language\":\"{JsonEscape(language)}\",\"mode\":\"dictate\",\"systemPrompt\":null}}";
            await SendJsonAsync(configJson, cancellationToken).ConfigureAwait(false);

            // Start receive loop
            await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (WalletStreamingFallbackException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Log.Information("WalletStreaming: connection cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WalletStreaming: connection failed");
            _finalTcs.TrySetException(new WalletStreamingFallbackException(
                $"Connection failed: {ex.Message}", ex));

            ErrorOccurred?.Invoke(this,
                new StreamingErrorEventArgs($"Connection failed: {ex.Message}", ex));
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024]; // 64KB receive buffer

        while (!cancellationToken.IsCancellationRequested
               && _ws?.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            // Read a complete message (may arrive in multiple frames)
            do
            {
                result = await _ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Log.Information("WalletStreaming: received WS close frame");
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue; // ignore binary frames (shouldn't occur)
            }

            string text = Encoding.UTF8.GetString(ms.ToArray());
            await HandleEdgeMessageAsync(text, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleEdgeMessageAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
            {
                return;
            }

            string msgType = typeProp.GetString() ?? "";

            switch (msgType)
            {
                case "ready":
                    await OnReadyAsync(cancellationToken).ConfigureAwait(false);
                    break;

                case "transcript":
                    OnPartialTranscript(root);
                    break;

                case "final":
                    OnFinal(root);
                    break;

                case "error":
                    OnError(root);
                    break;

                case "fallback":
                    OnFallback(root);
                    break;

                default:
                    Log.Debug("WalletStreaming: unknown message type '{Type}'", msgType);
                    break;
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "WalletStreaming: failed to parse edge function message");
        }
    }

    private async Task OnReadyAsync(CancellationToken cancellationToken)
    {
        Log.Information("WalletStreaming: edge function ready — flushing {Count} buffered chunks",
            _audioBuffer.Count);

        _wsReady = true;

        // Flush buffered audio
        while (_audioBuffer.TryDequeue(out byte[]? chunk))
        {
            await SendPcmChunkAsync(chunk, cancellationToken).ConfigureAwait(false);
        }

        // If end_audio arrived before ready, signal it now
        if (_audioEnded)
        {
            await SendJsonAsync("{\"type\":\"end_audio\"}", cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnPartialTranscript(JsonElement root)
    {
        string text = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Log.Debug("WalletStreaming: partial transcript: \"{Text}\"",
            text.Length > 50 ? string.Concat(text.AsSpan(0, 50), "...") : text);

        PartialTranscriptReceived?.Invoke(this,
            new StreamingTranscriptEventArgs(
                transcript: text,
                isFinal: false,
                confidence: 0.0,
                duration: TimeSpan.Zero));
    }

    private void OnFinal(JsonElement root)
    {
        string text = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        long cost = root.TryGetProperty("cost", out var c) ? c.GetInt64() : 0;
        long balance = root.TryGetProperty("balance", out var b) ? b.GetInt64() : 0;

        Log.Information("WalletStreaming: final message received — cost={Cost}µ$, balance={Balance}µ$",
            cost, balance);

        _finalTcs.TrySetResult(new FinalMessage(text, cost, balance));
    }

    private void OnError(JsonElement root)
    {
        string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown error" : "Unknown error";
        int code = root.TryGetProperty("code", out var c) ? c.GetInt32() : 0;

        Log.Error("WalletStreaming: error from edge function: {Message} (code={Code})", message, code);

        // 402 = insufficient balance — surface specifically
        string userMessage = code == 402
            ? "Insufficient wallet balance. Please top up."
            : $"Streaming error: {message}";

        _finalTcs.TrySetException(new WalletStreamingFallbackException(userMessage));
        ErrorOccurred?.Invoke(this, new StreamingErrorEventArgs(userMessage));
    }

    private void OnFallback(JsonElement root)
    {
        string reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
        Log.Information("WalletStreaming: fallback signal from edge function, reason={Reason}", reason);
        _finalTcs.TrySetException(new WalletStreamingFallbackException($"Fallback: {reason}"));
    }

    // ── Send helpers ─────────────────────────────────────────────────────────

    private async Task SendPcmChunkAsync(byte[] pcm, CancellationToken cancellationToken)
    {
        if (_ws?.State != WebSocketState.Open)
        {
            return;
        }

        // Send as binary frame — edge function receives it as ArrayBuffer
        try
        {
            await _ws.SendAsync(
                new ArraySegment<byte>(pcm),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "WalletStreaming: failed to send audio chunk");
        }
    }

    private async Task SendJsonAsync(string json, CancellationToken cancellationToken)
    {
        if (_ws?.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "WalletStreaming: failed to send JSON message");
        }
    }

    // ── URL builder ──────────────────────────────────────────────────────────

    private string BuildStreamUrl(string token, string language)
    {
        // Derive stream URL from proxy URL: replace "wallet-proxy" → "wallet-stream"
        string baseUrl = _settings.Current.Account.WalletProxyUrl;
        string streamBase = baseUrl.Contains("wallet-proxy", StringComparison.OrdinalIgnoreCase)
            ? baseUrl.Replace("wallet-proxy", "wallet-stream", StringComparison.OrdinalIgnoreCase)
            : DefaultStreamUrl;

        // Convert https → wss
        if (streamBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            streamBase = "wss://" + streamBase[8..];
        }
        else if (streamBase.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            streamBase = "ws://" + streamBase[7..];
        }

        return $"{streamBase}?jwt={Uri.EscapeDataString(token)}&lang={Uri.EscapeDataString(language)}";
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private sealed record FinalMessage(string Text, long Cost, long Balance);

    private static string JsonEscape(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

/// <summary>
/// Thrown when the wallet streaming connection fails and the caller should
/// transparently fall back to the batch pipeline.
/// </summary>
public sealed class WalletStreamingFallbackException : Exception
{
    public WalletStreamingFallbackException(string message) : base(message) { }
    public WalletStreamingFallbackException(string message, Exception inner) : base(message, inner) { }
}
