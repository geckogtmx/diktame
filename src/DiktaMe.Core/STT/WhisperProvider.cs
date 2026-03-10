
using System.Diagnostics;
using System.Net.Http;
using Serilog;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace DiktaMe.Core.STT;
/// <summary>
/// STT provider using Whisper.net (ONNX/GGML-based, fully local, no Python required).
/// Models are stored in <c>%APPDATA%\DiktaMe\models\</c> and loaded lazily on first use.
/// Supports CPU inference out-of-the-box; CUDA GPU acceleration when available.
/// Port of V1's Transcriber class from python/core/transcriber.py.
/// </summary>
public sealed class WhisperProvider : ISTTProvider, IDisposable
{
    /// <summary>Models directory under %APPDATA%\DiktaMe\models\</summary>
    public static readonly string ModelsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "models");

    // Mapping from friendly name to Whisper.net GgmlType enum and file name.
    // "turbo" maps to large-v3-turbo — the recommended default (V1 equivalent).
    private static readonly IReadOnlyDictionary<string, (GgmlType Type, string FileName)> ModelMap =
        new Dictionary<string, (GgmlType, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["tiny"] = (GgmlType.Tiny, "ggml-tiny.bin"),
            ["base"] = (GgmlType.Base, "ggml-base.bin"),
            ["small"] = (GgmlType.Small, "ggml-small.bin"),
            ["medium"] = (GgmlType.Medium, "ggml-medium.bin"),
            ["large"] = (GgmlType.LargeV3, "ggml-large-v3.bin"),
            ["turbo"] = (GgmlType.LargeV3Turbo, "ggml-large-v3-turbo.bin"),
        };

    private readonly string _modelSize;
    private readonly string _modelPath;
    private WhisperFactory? _factory;
    private bool _runtimeLogged;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => $"Whisper ({_modelSize})";

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised during model download to report progress (0–100).
    /// </summary>
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="modelSize">
    /// Model size name: <c>tiny</c>, <c>base</c>, <c>small</c>, <c>medium</c>,
    /// <c>large</c>, or <c>turbo</c> (default, recommended).
    /// </param>
    /// <param name="modelsDirectory">
    /// Override the directory where model files are stored (mainly for tests).
    /// Defaults to <see cref="ModelsDirectory"/>.
    /// </param>
    public WhisperProvider(string modelSize = "turbo", string? modelsDirectory = null)
    {
        if (!ModelMap.ContainsKey(modelSize))
        {
            throw new ArgumentException(
                $"Unknown model size '{modelSize}'. Valid: {string.Join(", ", ModelMap.Keys)}",
                nameof(modelSize));
        }

        _modelSize = modelSize;
        string dir = modelsDirectory ?? ModelsDirectory;
        _modelPath = Path.Combine(dir, ModelMap[modelSize].FileName);
    }

    // ── ISTTProvider ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <c>true</c> if the model file exists on disk.
    /// A <c>false</c> result means the model needs to be downloaded first.
    /// </remarks>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(_modelPath));

    /// <inheritdoc/>
    /// <remarks>
    /// Loads the model on first call (lazy initialisation).
    /// If the model file is not present, throws <see cref="FileNotFoundException"/>.
    /// Call <see cref="DownloadModelAsync"/> first to ensure the model is present.
    /// </remarks>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException(
                $"Whisper model '{_modelSize}' not found at '{_modelPath}'. " +
                "Call DownloadModelAsync() first.", _modelPath);
        }

        // Lazy-load the factory (expensive — only once)
        _factory ??= WhisperFactory.FromPath(_modelPath);

        // Log which runtime backend was selected (once)
        if (!_runtimeLogged)
        {
            _runtimeLogged = true;
            try
            {
                var loadedLib = RuntimeOptions.LoadedLibrary;
                Log.Information("WhisperProvider: loaded runtime={Runtime}", loadedLib);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WhisperProvider: failed to read loaded runtime");
            }
            try
            {
                var info = WhisperFactory.GetRuntimeInfo();
                Log.Information("WhisperProvider: runtimeInfo={Info}", info);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WhisperProvider: failed to get runtime info");
            }
        }

        var sw = Stopwatch.StartNew();

        bool autoDetect = language.Equals("auto", StringComparison.OrdinalIgnoreCase);

        var builder = _factory.CreateBuilder();
        if (autoDetect)
        {
            builder = builder.WithLanguageDetection();
        }
        else
        {
            builder = builder.WithLanguage(language);
        }

        using var processor = builder.Build();

        var segments = new List<SegmentData>();
        await using var audioStream = File.OpenRead(audioFilePath);
        await foreach (var segment in processor.ProcessAsync(audioStream))
        {
            segments.Add(segment);
        }

        sw.Stop();

        string transcript = string.Join(" ", segments.Select(s => s.Text.Trim()))
                                  .Trim();

        string? detectedLang = segments.Count > 0 ? segments[0].Language : null;

        // Compute audio duration from WAV file size (16kHz, 16-bit mono = 32000 bytes/sec)
        const int WavHeaderSize = 44;
        const int BytesPerSecond = 32000; // 16000 Hz * 2 bytes/sample * 1 channel
        double? audioDurationSec = null;
        try
        {
            long fileSize = new FileInfo(audioFilePath).Length;
            if (fileSize > WavHeaderSize)
            {
                audioDurationSec = (double)(fileSize - WavHeaderSize) / BytesPerSecond;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "WhisperProvider: could not compute audio duration");
        }

        // Log with performance ratio and GPU/CPU assessment
        if (audioDurationSec.HasValue && audioDurationSec.Value > 0)
        {
            double ratio = sw.ElapsedMilliseconds / (audioDurationSec.Value * 1000);
            string tag = ratio < 0.1 ? "GPU" : ratio < 0.2 ? "BORDERLINE" : "CPU";
            Log.Information(
                "WhisperProvider ({Model}): {Ms}ms for {Dur:F1}s audio (ratio={Ratio:F2}x, likely {Tag}) — \"{Preview}\"",
                _modelSize, sw.ElapsedMilliseconds, audioDurationSec.Value, ratio, tag,
                transcript.Length > 80 ? transcript[..80] + "…" : transcript);
        }
        else
        {
            Log.Information("WhisperProvider ({Model}): transcribed in {Ms}ms — \"{Preview}\"",
                _modelSize, sw.ElapsedMilliseconds,
                transcript.Length > 80 ? transcript[..80] + "…" : transcript);
        }

        return new TranscriptionResult
        {
            Text = transcript,
            DetectedLanguage = detectedLang,
            LatencyMs = sw.ElapsedMilliseconds,
            Provider = ProviderName,
            AudioDurationSec = audioDurationSec,
        };
    }

    // ── Model download ────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the model file to <see cref="ModelsDirectory"/> if not already present.
    /// Reports progress via <see cref="DownloadProgress"/> (0–100).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the downloaded model file.</returns>
    public async Task<string> DownloadModelAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (File.Exists(_modelPath))
        {
            Log.Information("WhisperProvider: model already present at '{Path}'", _modelPath);
            return _modelPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);

        Log.Information("WhisperProvider: downloading model '{Model}' to '{Path}'",
            _modelSize, _modelPath);

        (GgmlType ggmlType, _) = ModelMap[_modelSize];

        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
            ggmlType, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        long? totalBytes = modelStream.CanSeek ? modelStream.Length : null;
        long bytesRead = 0;
        int lastPercent = -1;

        await using var fileStream = new FileStream(
            _modelPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81_920, useAsync: true);

        var buffer = new byte[81_920];
        int read;

        while ((read = await modelStream.ReadAsync(buffer, cancellationToken)
                                         .ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                            .ConfigureAwait(false);

            bytesRead += read;

            if (totalBytes.HasValue)
            {
                int percent = (int)(bytesRead * 100L / totalBytes.Value);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(percent, bytesRead, totalBytes.Value));
                }
            }
        }

        Log.Information("WhisperProvider: download complete — {Bytes:N0} bytes", bytesRead);
        DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(100, bytesRead, bytesRead));

        return _modelPath;
    }

    /// <summary>
    /// Returns <c>true</c> if the model file is already downloaded.
    /// </summary>
    public bool IsModelDownloaded => File.Exists(_modelPath);

    /// <summary>
    /// Returns the path where the model file will be stored.
    /// </summary>
    public string ModelPath => _modelPath;

    // ── Dispose ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _factory?.Dispose();
        _factory = null;
    }
}

/// <summary>Event args for Whisper model download progress.</summary>
public sealed class DownloadProgressEventArgs(int percent, long bytesReceived, long totalBytes)
    : EventArgs
{
    /// <summary>Progress percentage (0–100).</summary>
    public int Percent { get; } = percent;

    /// <summary>Bytes received so far.</summary>
    public long BytesReceived { get; } = bytesReceived;

    /// <summary>Total expected bytes.</summary>
    public long TotalBytes { get; } = totalBytes;
}
