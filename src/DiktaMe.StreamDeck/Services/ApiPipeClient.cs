using System.Globalization;
using System.IO.Pipes;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;

namespace DiktaMe.StreamDeck.Services;

internal sealed class ApiPipeClient : IDisposable
{
    private const string PipeName = "DiktaMe.V2.Api";
    private const int ReconnectDelayMs = 3000;

    public static ApiPipeClient Instance { get; } = new();

    // ── State cache ──────────────────────────────────────────────
    public string CurrentPipelineState { get; private set; } = "Idle";
    public JObject? CurrentSettingsJson { get; private set; }
    public List<Models.ModeInfo>? CurrentModes { get; private set; }
    public bool IsConnected { get; private set; }

    // ── Events ───────────────────────────────────────────────────
    public event Action<string>? StateChanged;
    public event Action<JObject>? SettingsReceived;
    public event Action<JArray>? ModesReceived;
    public event Action? BusyReceived;
    public event Action<string>? ErrorReceived;
    public event Action<bool>? ConnectionChanged;

    // ── Internals ────────────────────────────────────────────────
    private readonly object _startLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;
    private bool _started;

    private ApiPipeClient() { }

    /// <summary>
    /// Thread-safe lazy initialization — starts the background connect loop once.
    /// </summary>
    public void EnsureStarted()
    {
        if (_started)
        {
            return;
        }

        lock (_startLock)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _ = Task.Run(() => ConnectLoopAsync(_cts.Token));
        }
    }

    /// <summary>
    /// Send a JSON command to the DiktaMe app over the pipe.
    /// Thread-safe via SemaphoreSlim. IOExceptions are swallowed
    /// (the read loop handles reconnection).
    /// </summary>
    public async Task SendCommandAsync(string json)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writer is not null)
            {
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
                // No FlushAsync — bufferSize=1 means data goes to the pipe kernel buffer
                // immediately. FlushAsync invokes FlushFileBuffers() which blocks until
                // the server reads — deadlock on bidirectional pipes.
            }
        }
        catch (IOException)
        {
            // Silently ignore — the read loop detects the broken pipe and reconnects.
        }
        catch (ObjectDisposedException)
        {
            // Pipe was disposed during reconnect cycle.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
#pragma warning disable CA1031 // Do not catch general exception types — best-effort cleanup
        try { _cts.Cancel(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        try { _writeLock.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
#pragma warning restore CA1031
    }

    // ── Connection loop ──────────────────────────────────────────

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeClientStream? pipe = null;
            try
            {
                pipe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                Logger.Instance.LogMessage(
                    TracingLevel.INFO,
                    "ApiPipeClient: connecting to pipe...");

                await pipe.ConnectAsync(ReconnectDelayMs, ct).ConfigureAwait(false);

                _pipe = pipe;
                var reader = new StreamReader(pipe);
                // bufferSize=1 disables internal buffering — writes go to the pipe immediately.
                // Do NOT use AutoFlush or FlushAsync — both invoke FlushFileBuffers()
                // which blocks until the server reads, causing deadlock on bidirectional pipes.
                _writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, bufferSize: 1, leaveOpen: true);

                IsConnected = true;
                ConnectionChanged?.Invoke(true);

                Logger.Instance.LogMessage(
                    TracingLevel.INFO,
                    "ApiPipeClient: connected");

                // Request initial state
                Logger.Instance.LogMessage(
                    TracingLevel.INFO,
                    "ApiPipeClient: sending initial queries...");
                await SendCommandAsync("{\"action\":\"query\",\"target\":\"settings\"}")
                    .ConfigureAwait(false);
                await SendCommandAsync("{\"action\":\"query\",\"target\":\"modes\"}")
                    .ConfigureAwait(false);
                Logger.Instance.LogMessage(
                    TracingLevel.INFO,
                    "ApiPipeClient: queries sent, entering read loop");

                // Block on read loop until pipe breaks
                await ReadLoopAsync(reader, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (TimeoutException)
            {
                // ConnectAsync timed out — app not running yet.
            }
#pragma warning disable CA1031 // Do not catch general exception types — reconnect on any failure
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Logger.Instance.LogMessage(
                    TracingLevel.ERROR,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ApiPipeClient: connection error: {0}",
                        ex.Message));
            }
            finally
            {
                SetDisconnected();
                pipe?.Dispose();
            }

            if (!ct.IsCancellationRequested)
            {
                await Task.Delay(ReconnectDelayMs, ct).ConfigureAwait(false);
            }
        }
    }

    // ── Read loop ────────────────────────────────────────────────

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken ct)
    {
        Logger.Instance.LogMessage(
            TracingLevel.INFO,
            "ApiPipeClient: ReadLoopAsync started, waiting for first line...");
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                Logger.Instance.LogMessage(
                    TracingLevel.INFO,
                    "ApiPipeClient: server closed pipe (null read)");
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Logger.Instance.LogMessage(
                TracingLevel.INFO,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ApiPipeClient: received: {0}",
                    line.Length > 200 ? line[..200] + "..." : line));

            try
            {
                var msg = JObject.Parse(line);
                var eventType = msg.Value<string>("event") ?? "";

                DispatchEvent(eventType, msg);
            }
#pragma warning disable CA1031 // Do not catch general exception types — malformed JSON must not crash loop
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Logger.Instance.LogMessage(
                    TracingLevel.WARN,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ApiPipeClient: failed to parse message: {0}",
                        ex.Message));
            }
        }
    }

    private void DispatchEvent(string eventType, JObject msg)
    {
        switch (eventType)
        {
            case "state":
                var state = msg.Value<string>("state") ?? "Idle";
                CurrentPipelineState = state;
                StateChanged?.Invoke(state);
                break;

            case "settings":
                // Settings fields are on the root object (not nested under "data").
                // Wire format: {"event":"settings","RawModeOverride":false,"StreamingEnabled":true,...}
                CurrentSettingsJson = msg;
                SettingsReceived?.Invoke(msg);
                break;

            case "modes":
                var modesArray = msg["modes"] as JArray;
                if (modesArray is not null)
                {
                    var list = modesArray.ToObject<List<Models.ModeInfo>>() ?? [];
                    CurrentModes = list;
                    ModesReceived?.Invoke(modesArray);
                }

                break;

            case "busy":
                BusyReceived?.Invoke();
                break;

            case "error":
                var errorMsg = msg.Value<string>("message") ?? "Unknown error";
                ErrorReceived?.Invoke(errorMsg);
                break;

            default:
                Logger.Instance.LogMessage(
                    TracingLevel.WARN,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ApiPipeClient: unknown event type '{0}'",
                        eventType));
                break;
        }
    }

    private void SetDisconnected()
    {
        if (IsConnected)
        {
            IsConnected = false;
            ConnectionChanged?.Invoke(false);

            Logger.Instance.LogMessage(
                TracingLevel.INFO,
                "ApiPipeClient: disconnected");
        }

        _writer = null;
        _pipe = null;
    }
}
