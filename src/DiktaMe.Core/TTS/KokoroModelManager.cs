using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Manages downloading and locating Kokoro ONNX TTS model files.
/// Models are stored in <c>%APPDATA%/DiktaMe/models/tts/</c>.
/// Follows the same pattern as <see cref="STT.WhisperProvider"/> model management.
/// </summary>
public sealed class KokoroModelManager
{
    /// <summary>Directory where TTS models are stored.</summary>
    public static readonly string ModelsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "models", "tts");

    private const string BaseDownloadUrl =
        "https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/";

    private static readonly IReadOnlyDictionary<string, (string FileName, long ApproxSizeMb)> ModelMap =
        new Dictionary<string, (string, long)>(StringComparer.OrdinalIgnoreCase)
        {
            ["int8"] = ("kokoro-quant-convinteger.onnx", 88),
            ["fp16"] = ("kokoro-quant.onnx", 169),
            ["fp32"] = ("kokoro.onnx", 310),
        };

    private readonly string _variant;
    private readonly string _modelPath;

    /// <summary>Progress event for model download (0–100 percent).</summary>
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;

    /// <summary>Full path to the model file.</summary>
    public string ModelPath => _modelPath;

    /// <summary>Whether the model file exists on disk.</summary>
    public bool IsModelDownloaded => File.Exists(_modelPath);

    public KokoroModelManager(string variant = "int8")
    {
        if (!ModelMap.ContainsKey(variant))
            throw new ArgumentException($"Unknown Kokoro model variant: '{variant}'. Valid: int8, fp16, fp32.", nameof(variant));

        _variant = variant;
        var (fileName, _) = ModelMap[variant];
        _modelPath = Path.Combine(ModelsDirectory, fileName);
    }

    /// <summary>
    /// Downloads the model if not already present. Returns the model file path.
    /// </summary>
    public async Task<string> DownloadModelAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_modelPath))
        {
            Log.Debug("KokoroModelManager: model already downloaded at {Path}", _modelPath);
            return _modelPath;
        }

        var (fileName, _) = ModelMap[_variant];
        var url = BaseDownloadUrl + fileName;

        Log.Information("KokoroModelManager: downloading {Variant} model from {Url}", _variant, url);
        Directory.CreateDirectory(ModelsDirectory);

        var tempPath = _modelPath + ".tmp";

        // Clean up stale .tmp from a previous cancelled download
        try { if (File.Exists(tempPath)) File.Delete(tempPath); }
        catch (IOException ex) { Log.Debug(ex, "KokoroModelManager: could not delete stale .tmp file"); }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            long bytesRead = 0;
            var buffer = new byte[81_920];

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81_920, true);

            int read;
            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    int percent = (int)(bytesRead * 100L / totalBytes.Value);
                    DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(percent, bytesRead, totalBytes.Value));
                }
            }

            // Atomic rename — may fail if model is loaded by KokoroTtsProvider (ONNX runtime holds file open)
            try
            {
                File.Move(tempPath, _modelPath, overwrite: true);
            }
            catch (IOException)
            {
                throw new IOException(
                    "Model file is in use — restart the app and try again.");
            }

            Log.Information("KokoroModelManager: model downloaded to {Path} ({Bytes} bytes)", _modelPath, bytesRead);
            return _modelPath;
        }
        catch
        {
            // Clean up partial download
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Gets the approximate download size in megabytes for a model variant.
    /// </summary>
    public static long GetApproxSizeMb(string variant)
    {
        return ModelMap.TryGetValue(variant, out var info) ? info.ApproxSizeMb : 0;
    }
}

/// <summary>
/// Progress information for model downloads.
/// </summary>
public sealed class DownloadProgressEventArgs : EventArgs
{
    public int Percent { get; }
    public long BytesRead { get; }
    public long TotalBytes { get; }

    public DownloadProgressEventArgs(int percent, long bytesRead, long totalBytes)
    {
        Percent = percent;
        BytesRead = bytesRead;
        TotalBytes = totalBytes;
    }
}
