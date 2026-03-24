using System.IO.Pipes;
using System.Text.Json;
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.Pipeline;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.Services;

/// <summary>
/// Named pipe IPC server for external tool integration (Stream Deck, automation scripts).
/// Hosts a bidirectional named pipe that accepts JSON commands and broadcasts state/settings events.
/// Protocol: newline-delimited JSON, one object per line.
/// </summary>
public sealed class LocalApiServer : IDisposable
{
    private const string PipeName = "DiktaMe.V2.Api";

    private readonly LoadingViewModel _loadingVm;
    private readonly ControlPanelViewModel _controlPanel;
    private readonly SettingsManager _settings;
    private readonly DictationModeManager _dictationModes;

    private DispatcherQueue? _uiDispatcher;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    private readonly List<ConnectedClient> _connectedClients = [];
    private readonly object _clientsLock = new();
    private long _lastTriggerTicks;

    public LocalApiServer(
        LoadingViewModel loadingVm,
        ControlPanelViewModel controlPanel,
        SettingsManager settings,
        DictationModeManager dictationModes)
    {
        _loadingVm = loadingVm;
        _controlPanel = controlPanel;
        _settings = settings;
        _dictationModes = dictationModes;
    }

    /// <summary>
    /// Starts the pipe listener and subscribes to app events.
    /// Must be called from the UI thread (captures DispatcherQueue).
    /// </summary>
    public void Start()
    {
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
        _cts = new CancellationTokenSource();

        _controlPanel.ExternalStateChanged += OnPipelineStateChanged;
        _settings.SettingsChanged += OnSettingsChanged;

        _listenerTask = AcceptLoopAsync(_cts.Token);
        Log.Information("LocalApiServer: started on pipe '{Pipe}'", PipeName);
    }

    public void Dispose()
    {
        _controlPanel.ExternalStateChanged -= OnPipelineStateChanged;
        _settings.SettingsChanged -= OnSettingsChanged;

        _cts?.Cancel();
        _cts?.Dispose();

        lock (_clientsLock)
        {
            _connectedClients.Clear();
        }

        Log.Information("LocalApiServer: stopped");
    }

    // ── Accept loop ─────────────────────────────────────────────────────────

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                Log.Information("LocalApiServer: client connected");

                _ = HandleClientAsync(server, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "LocalApiServer: pipe accept error — restarting listener");
            }
        }
    }

    // ── Per-client handler ──────────────────────────────────────────────────

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        // bufferSize=1 disables internal buffering — each WriteLine goes to the pipe immediately.
        // Do NOT use AutoFlush or call writer.Flush() — both invoke FlushFileBuffers()
        // which blocks until the client reads, causing deadlock when both sides write before reading.
        var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, bufferSize: 1, leaveOpen: true);
        var reader = new StreamReader(pipe);
        var client = new ConnectedClient(writer);

        lock (_clientsLock) { _connectedClients.Add(client); }

        try
        {
            // Send initial state snapshot so the client knows current state immediately.
            // Uses WriteSafe (synchronous + locked) to prevent race with BroadcastJson.
            Log.Debug("LocalApiServer: sending initial snapshot to client...");
            client.WriteSafe(WriteStateEvent(_controlPanel.CurrentState));
            client.WriteSafe(WriteSettingsEvent(_settings.Current));
            client.WriteSafe(WriteModesEvent(_dictationModes.GetAllModes()));
            Log.Debug("LocalApiServer: initial snapshot sent, entering read loop");

            while (!ct.IsCancellationRequested && pipe.IsConnected)
            {
                string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    Log.Debug("LocalApiServer: client sent null (disconnected)");
                    break; // client disconnected
                }

                Log.Debug("LocalApiServer: received command: {Command}", line);
                ProcessCommand(line, client);
            }
        }
        catch (OperationCanceledException)
        {
            // App shutting down
        }
        catch (IOException)
        {
            // Client disconnected
        }
        finally
        {
            lock (_clientsLock) { _connectedClients.Remove(client); }
            try { pipe.Dispose(); }
            catch { /* best-effort cleanup */ }
            Log.Information("LocalApiServer: client disconnected");
        }
    }

    // ── Command processing ──────────────────────────────────────────────────

    private void ProcessCommand(string json, ConnectedClient client)
    {
        var cmd = ApiCommandParser.TryParse(json);
        if (cmd is null)
        {
            Log.Warning("LocalApiServer: invalid command JSON: {Json}", json);
            client.WriteSafe(WriteErrorEvent("Invalid JSON command"));
            return;
        }

        try
        {
            switch (cmd.Action)
            {
                case "trigger":
                    HandleTrigger(cmd);
                    break;

                case "toggle":
                    HandleToggle(cmd);
                    break;

                case "query":
                    HandleQuery(cmd, client);
                    break;

                default:
                    Log.Warning("LocalApiServer: unknown action '{Action}'", cmd.Action);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LocalApiServer: command processing failed for action '{Action}'", cmd.Action);
            client.WriteSafe(WriteErrorEvent(ex.Message));
        }
    }

    private void HandleTrigger(ApiCommand cmd)
    {
        string pipeline = cmd.Pipeline ?? "";
        if (string.IsNullOrEmpty(pipeline))
        {
            Log.Warning("LocalApiServer: trigger command missing 'pipeline' field");
            return;
        }

        // Capture foreground window NOW, on the pipe reader thread,
        // before dispatching to UI thread (same pattern as OnHotkeyPressed).
        IntPtr sourceWindow = TextInjector.GetCurrentForegroundWindow();
        Log.Information("LocalApiServer: trigger {Pipeline} (modeId={ModeId}, hwnd=0x{Hwnd:X})",
            pipeline, cmd.ModeId, sourceWindow);

        var currentState = _controlPanel.CurrentState;

        // If busy (not Idle and not Recording), reject with busy event
        if (currentState != PipelineState.Idle && currentState != PipelineState.Recording)
        {
            BroadcastJson("""{"event":"busy"}""");
            return;
        }

        // Debounce: pipe message queuing can deliver multiple triggers for one physical press.
        // Allow toggle-stop (Recording → stop) to always pass through.
        const int debounceMs = 300;
        long now = Environment.TickCount64;
        long prev = Interlocked.Exchange(ref _lastTriggerTicks, now);
        if (now - prev < debounceMs && currentState == PipelineState.Idle)
        {
            Log.Debug("LocalApiServer: trigger debounced ({Elapsed}ms < {Threshold}ms)",
                now - prev, debounceMs);
            return;
        }

        if (_uiDispatcher is null)
        {
            Log.Error("LocalApiServer: UI dispatcher not available — cannot dispatch trigger");
            return;
        }

        string? modeId = cmd.ModeId;
        _uiDispatcher.TryEnqueue(() =>
        {
            _loadingVm.TriggerPipeline(pipeline, modeId, sourceWindow);
        });
    }

    private void HandleToggle(ApiCommand cmd)
    {
        string setting = cmd.Setting ?? "";
        if (string.IsNullOrEmpty(setting))
        {
            Log.Warning("LocalApiServer: toggle command missing 'setting' field");
            return;
        }

        Log.Information("LocalApiServer: toggle {Setting}", setting);

        var current = _settings.Current;
        AppSettings updated = setting switch
        {
            "RawModeOverride" => current with
            {
                General = current.General with { RawModeOverride = !current.General.RawModeOverride },
            },
            "StreamingEnabled" => current with
            {
                General = current.General with { StreamingEnabled = !current.General.StreamingEnabled },
            },
            "AudioDucking" => current with
            {
                AudioDucking = current.AudioDucking with { Enabled = !current.AudioDucking.Enabled },
            },
            "Engine" => current with
            {
                ActiveProfileName = string.Equals(current.ActiveProfileName, "Cloud", StringComparison.Ordinal)
                    ? "Local" : "Cloud",
            },
            _ => throw new ArgumentException($"Unknown toggle setting: {setting}", nameof(cmd)),
        };

        // Fire-and-forget: UpdateAsync fires SettingsChanged → OnSettingsChanged → broadcasts to clients
        _ = _settings.UpdateAsync(updated);
    }

    private void HandleQuery(ApiCommand cmd, ConnectedClient client)
    {
        string target = cmd.Target ?? "";
        switch (target)
        {
            case "modes":
                client.WriteSafe(WriteModesEvent(_dictationModes.GetAllModes()));
                break;
            case "settings":
                client.WriteSafe(WriteSettingsEvent(_settings.Current));
                break;
            default:
                Log.Warning("LocalApiServer: unknown query target '{Target}'", target);
                break;
        }
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void OnPipelineStateChanged(object? sender, PipelineState state)
    {
        // Offload to threadpool — BroadcastJson takes per-client write locks
        // that must never block the UI dispatcher thread.
        _ = Task.Run(() =>
        {
            try
            {
                BroadcastJson(WriteStateEvent(state));
            }
#pragma warning disable CA1031 // Must not let exceptions escape into the threadpool callback
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.Warning(ex, "LocalApiServer: broadcast failed for state event");
            }
        });
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        // Offload to threadpool — BroadcastJson takes per-client write locks
        // that must never block the UI dispatcher thread.
        _ = Task.Run(() =>
        {
            try
            {
                BroadcastJson(WriteSettingsEvent(settings));
            }
#pragma warning disable CA1031 // Must not let exceptions escape into the threadpool callback
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.Warning(ex, "LocalApiServer: broadcast failed for settings event");
            }
        });
    }

    // ── Broadcasting ────────────────────────────────────────────────────────

    private void BroadcastJson(string json)
    {
        ConnectedClient[] clients;
        lock (_clientsLock) { clients = [.. _connectedClients]; }

        foreach (var client in clients)
        {
            client.WriteSafe(json);
        }
    }

    // ── JSON serialization (Utf8JsonWriter for trim-safety) ─────────────────

    private static string WriteStateEvent(PipelineState state)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("event", "state");
            w.WriteString("state", state.ToString());
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string WriteSettingsEvent(AppSettings settings)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("event", "settings");
            w.WriteBoolean("RawModeOverride", settings.General.RawModeOverride);
            w.WriteBoolean("StreamingEnabled", settings.General.StreamingEnabled);
            w.WriteBoolean("AudioDucking", settings.AudioDucking.Enabled);
            w.WriteString("ActiveProfile", settings.ActiveProfileName);
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string WriteModesEvent(List<DictationMode> modes)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("event", "modes");
            w.WriteStartArray("modes");
            foreach (var mode in modes)
            {
                w.WriteStartObject();
                w.WriteString("id", mode.Id);
                w.WriteString("title", mode.Title);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string WriteErrorEvent(string message)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("event", "error");
            w.WriteString("message", message);
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    // ── Per-client write wrapper ────────────────────────────────────────────

    /// <summary>
    /// Wraps a StreamWriter with a lock to prevent concurrent writes.
    /// The async initial snapshot and synchronous BroadcastJson were racing
    /// on the same StreamWriter, causing InvalidOperationException.
    /// </summary>
    private sealed class ConnectedClient(StreamWriter writer)
    {
        private readonly object _writeLock = new();

        public void WriteSafe(string json)
        {
            try
            {
                lock (_writeLock)
                {
                    writer.WriteLine(json);
                    // No Flush() — bufferSize=1 means data goes to the pipe kernel buffer
                    // immediately. Calling Flush() would invoke FlushFileBuffers() which
                    // blocks until the client reads — deadlock on bidirectional pipes.
                }
            }
#pragma warning disable CA1031 // Pipe write must never crash the app
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
#pragma warning restore CA1031
            {
                Log.Warning("LocalApiServer: failed to write to client — {Message}", ex.Message);
            }
        }
    }
}
