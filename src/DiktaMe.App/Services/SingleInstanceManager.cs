
using System.IO.Pipes;
using Serilog;

namespace DiktaMe.App.Services;
/// <summary>
/// Enforces single-instance behavior using a named mutex and forwards deeplink URIs
/// from secondary instances to the primary via a named pipe.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = "DiktaMe.V2.SingleInstance";
    private const string PipeName = "DiktaMe.V2.DeepLink";

    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    /// <summary>
    /// Raised on the primary instance when a secondary sends a deeplink URI.
    /// </summary>
    public event Action<string>? DeepLinkReceived;

    /// <summary>
    /// Attempts to acquire the single-instance mutex.
    /// Returns true if this is the primary instance, false if another instance is already running.
    /// </summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance owns the mutex — we are secondary
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        Log.Information("SingleInstanceManager: acquired single-instance mutex");
        return true;
    }

    /// <summary>
    /// Starts a named pipe server to listen for deeplink URIs from secondary instances.
    /// Must only be called on the primary instance (after <see cref="TryAcquire"/> returns true).
    /// </summary>
    public void StartListening()
    {
        _cts = new CancellationTokenSource();
        _listenerTask = ListenLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Sends a deeplink URI to the primary instance via named pipe. Called by secondary instances.
    /// </summary>
    public static async Task SendDeepLinkAsync(string uri)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            await client.ConnectAsync(3000).ConfigureAwait(false); // 3s timeout

            using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync(uri).ConfigureAwait(false);

            Log.Information("SingleInstanceManager: sent deeplink to primary instance");
        }
        catch (TimeoutException)
        {
            Log.Warning("SingleInstanceManager: timed out connecting to primary instance pipe");
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "SingleInstanceManager: failed to send deeplink to primary instance");
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        Log.Information("SingleInstanceManager: pipe listener started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                string? uri = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(uri))
                {
                    Log.Information("SingleInstanceManager: received deeplink via pipe: {Uri}", uri);
                    DeepLinkReceived?.Invoke(uri);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "SingleInstanceManager: pipe error — restarting listener");
            }
        }

        Log.Information("SingleInstanceManager: pipe listener stopped");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
