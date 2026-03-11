
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace DiktaMe.Core.SystemManagement;
// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Result of an <see cref="OllamaManager"/> preflight check.</summary>
public enum OllamaStatus
{
    /// <summary>Ollama is running and the selected model is ready.</summary>
    Ready,

    /// <summary>Ollama is not running or unreachable.</summary>
    Offline,

    /// <summary>Ollama is running but the installed version is too old for the selected model.</summary>
    VersionTooOld,

    /// <summary>Ollama is running but the selected model is not pulled yet.</summary>
    ModelNotPulled,
}

/// <summary>Progress data from an Ollama model pull operation.</summary>
public sealed record OllamaPullProgress(string Status, long Completed, long Total);

/// <summary>Result of an Ollama installation attempt.</summary>
public enum OllamaInstallResult
{
    /// <summary>Ollama was installed successfully.</summary>
    Success,

    /// <summary>winget is not available on this system.</summary>
    WingetNotFound,

    /// <summary>Installation failed (see error message).</summary>
    Failed,
}

/// <summary>Result returned by <see cref="OllamaManager.CheckAsync"/>.</summary>
public sealed record OllamaCheckResult
{
    public OllamaStatus Status { get; init; }

    /// <summary>Detected Ollama version string (e.g. "0.6.1"). Null if Ollama is offline.</summary>
    public string? OllamaVersion { get; init; }

    /// <summary>Selected model tag.</summary>
    public string ModelTag { get; init; } = string.Empty;

    /// <summary>
    /// If <see cref="Status"/> is <see cref="OllamaStatus.VersionTooOld"/>,
    /// the minimum version needed for the selected model.
    /// </summary>
    public string? RequiredVersion { get; init; }

    /// <summary>
    /// Fallback model tag suggested when the primary is incompatible.
    /// Non-null only when Status is VersionTooOld or ModelNotPulled.
    /// </summary>
    public string? FallbackModel { get; init; }

    /// <summary>Whether this run detected an Ollama version change since last check.</summary>
    public bool VersionChanged { get; init; }
}

/// <summary>
/// A single entry in the models.json compatibility manifest.
/// </summary>
public sealed class ModelEntry
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("minOllamaVersion")]
    public string MinOllamaVersion { get; set; } = "0.0.0";

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("recommended")]
    public bool Recommended { get; set; }
}

internal sealed class ModelsManifest
{
    [JsonPropertyName("models")]
    public List<ModelEntry> Models { get; set; } = new();

    [JsonPropertyName("fallbackModel")]
    public string FallbackModel { get; set; } = "gemma";

    [JsonPropertyName("fallbackMinOllamaVersion")]
    public string FallbackMinOllamaVersion { get; set; } = "0.1.20";
}

// ── Source-generated JSON contexts ───────────────────────────────────────────

[JsonSerializable(typeof(ModelsManifest))]
[JsonSerializable(typeof(ModelEntry))]
[JsonSerializable(typeof(List<ModelEntry>))]
internal partial class OllamaJsonContext : JsonSerializerContext { }

// ── OllamaManager ─────────────────────────────────────────────────────────────

/// <summary>
/// Manages Ollama health checking, version compatibility, and graceful fallback.
/// Port of V1 SPEC_031 Ollama update management.
/// </summary>
/// <remarks>
/// UI hooks (412 Rescue dialog, Model Library tab) are wired in Stream F.
/// Core logic lives here so it can be tested independently.
/// </remarks>
public sealed class OllamaManager : IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────────

    private const string DefaultBaseUrl = "http://localhost:11434";
    private const string VersionEndpoint = "/api/version";
    private const string TagsEndpoint = "/api/tags";
    private const string PullEndpoint = "/api/pull";

    private static readonly string LastVersionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "ollama_last_version.txt");

    // ── State ─────────────────────────────────────────────────────────────

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly bool _ownsClient;
    private ModelsManifest? _manifest;
    private bool _disposed;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="OllamaManager"/>.
    /// </summary>
    /// <param name="baseUrl">Ollama base URL (default: http://localhost:11434).</param>
    /// <param name="httpClient">Optional shared <see cref="HttpClient"/>.</param>
    public OllamaManager(
        string baseUrl = DefaultBaseUrl,
        HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a full pre-flight check for the given model tag.
    /// </summary>
    /// <param name="modelTag">Model tag to validate (e.g. "llama3.2").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="OllamaCheckResult"/> describing current Ollama status.
    /// </returns>
    public async Task<OllamaCheckResult> CheckAsync(
        string modelTag,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 1. Version sensing
        string? version = await GetVersionAsync(cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            Log.Warning("OllamaManager: Ollama offline or unreachable");
            return new OllamaCheckResult { Status = OllamaStatus.Offline, ModelTag = modelTag };
        }

        Log.Information("OllamaManager: detected Ollama {Version}", version);

        // 2. Version-change detection
        bool versionChanged = await DetectVersionChangeAsync(version).ConfigureAwait(false);
        if (versionChanged)
        {
            Log.Information("OllamaManager: version changed since last run (new={Version})", version);
        }

        // 3. Compatibility manifest check
        var manifest = await LoadManifestAsync().ConfigureAwait(false);
        var entry = FindEntry(manifest, modelTag);
        string? requiredVersion = entry?.MinOllamaVersion;

        if (requiredVersion is not null && CompareVersions(version, requiredVersion) < 0)
        {
            Log.Warning(
                "OllamaManager: model '{Model}' requires Ollama {Required}, installed {Actual}",
                modelTag, requiredVersion, version);

            return new OllamaCheckResult
            {
                Status = OllamaStatus.VersionTooOld,
                OllamaVersion = version,
                ModelTag = modelTag,
                RequiredVersion = requiredVersion,
                FallbackModel = manifest.FallbackModel,
                VersionChanged = versionChanged,
            };
        }

        // 4. Health check — is the model actually pulled?
        bool modelReady = await IsModelPulledAsync(modelTag, cancellationToken).ConfigureAwait(false);
        if (!modelReady)
        {
            Log.Warning("OllamaManager: model '{Model}' is not pulled", modelTag);
            return new OllamaCheckResult
            {
                Status = OllamaStatus.ModelNotPulled,
                OllamaVersion = version,
                ModelTag = modelTag,
                FallbackModel = await FindBestFallbackAsync(manifest, version, cancellationToken)
                    .ConfigureAwait(false),
                VersionChanged = versionChanged,
            };
        }

        return new OllamaCheckResult
        {
            Status = OllamaStatus.Ready,
            OllamaVersion = version,
            ModelTag = modelTag,
            VersionChanged = versionChanged,
        };
    }

    /// <summary>
    /// Returns models listed in the embedded compatibility manifest.
    /// </summary>
    public async Task<IReadOnlyList<ModelEntry>> GetKnownModelsAsync()
    {
        var manifest = await LoadManifestAsync().ConfigureAwait(false);
        return manifest.Models.AsReadOnly();
    }

    /// <summary>
    /// Returns the list of model tags currently installed in Ollama (/api/tags).
    /// Returns empty list if Ollama is offline.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetInstalledModelTagsAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            string json = await _http
                .GetStringAsync(_baseUrl + TagsEndpoint, cancellationToken)
                .ConfigureAwait(false);

            return ParseModelTags(json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaManager: failed to list installed models");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Pulls (downloads) a model from the Ollama registry with streaming progress.
    /// </summary>
    /// <param name="modelTag">Model tag to pull (e.g. "gemma3").</param>
    /// <param name="progress">Optional progress reporter for download updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="HttpRequestException">Thrown if Ollama is unreachable or returns an error.</exception>
    /// <exception cref="InvalidOperationException">Thrown if pull stream ends without success.</exception>
    public async Task PullModelAsync(
        string modelTag,
        IProgress<OllamaPullProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string requestJson = $$"""{"name":"{{modelTag}}","stream":true}""";
        using var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + PullEndpoint) { Content = content };

        // ResponseHeadersRead: timeout applies only until headers arrive,
        // then streaming reads proceed without timeout pressure.
        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        bool gotSuccess = false;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                string status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                long completed = root.TryGetProperty("completed", out var c) ? c.GetInt64() : 0;
                long total = root.TryGetProperty("total", out var t) ? t.GetInt64() : 0;

                if (string.Equals(status, "success", StringComparison.Ordinal))
                {
                    gotSuccess = true;
                    progress?.Report(new OllamaPullProgress(status, total, total));
                    break;
                }

                // Check for error field (Ollama returns {"error":"..."} on failures)
                if (root.TryGetProperty("error", out var err))
                {
                    string errorMsg = err.GetString() ?? "Unknown pull error";
                    throw new InvalidOperationException($"Ollama pull failed: {errorMsg}");
                }

                progress?.Report(new OllamaPullProgress(status, completed, total));
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "OllamaManager: failed to parse pull progress line: {Line}", line);
            }
        }

        if (!gotSuccess)
        {
            throw new InvalidOperationException($"Ollama pull stream ended without success for model '{modelTag}'");
        }

        Log.Information("OllamaManager: successfully pulled model '{Model}'", modelTag);
    }

    // ── Install helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Checks whether winget (Windows Package Manager) is available.
    /// </summary>
    public static async Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (proc is null)
            {
                return false;
            }

            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Installs Ollama via winget with silent flags.
    /// </summary>
    /// <param name="progress">Optional progress reporter for status messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Install result and optional error message.</returns>
    public static async Task<(OllamaInstallResult Result, string? Error)> InstallViaWingetAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Check winget availability
        progress?.Report("Checking winget...");
        if (!await IsWingetAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return (OllamaInstallResult.WingetNotFound, "winget is not available on this system");
        }

        // 2. Run winget install
        progress?.Report("Downloading Ollama...");
        Log.Information("OllamaManager: starting winget install Ollama.Ollama");

        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "install Ollama.Ollama --silent --accept-package-agreements --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (proc is null)
            {
                return (OllamaInstallResult.Failed, "Failed to start winget process");
            }

            // Read stdout lines for phased progress (winget outputs: Found, Downloading, Installing, Successfully installed)
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutLines = new System.Text.StringBuilder();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (await proc.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                    {
                        stdoutLines.AppendLine(line);
                        string lower = line.ToLowerInvariant();
                        if (lower.Contains("downloading", StringComparison.Ordinal))
                        {
                            progress?.Report("Downloading Ollama...");
                        }
                        else if (lower.Contains("installing", StringComparison.Ordinal))
                        {
                            progress?.Report("Installing Ollama...");
                        }
                        else if (lower.Contains("successfully", StringComparison.Ordinal))
                        {
                            progress?.Report("Starting Ollama...");
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, cancellationToken);

            // Wait with timeout (5 minutes for download + install)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            string stderr = await stderrTask.ConfigureAwait(false);

            if (proc.ExitCode == 0)
            {
                Log.Information("OllamaManager: winget install succeeded");
                progress?.Report("Ollama installed!");
                return (OllamaInstallResult.Success, null);
            }

            string stdout = stdoutLines.ToString();
            string errorDetail = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
            Log.Warning("OllamaManager: winget install failed (exit {Code}): {Error}", proc.ExitCode, errorDetail);
            return (OllamaInstallResult.Failed, $"winget exited with code {proc.ExitCode}: {errorDetail}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaManager: winget install threw");
            return (OllamaInstallResult.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Starts the Ollama service and waits for it to become ready.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if Ollama started and is responding; <c>false</c> otherwise.</returns>
    public async Task<bool> StartOllamaAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Find ollama.exe — check default install path, then PATH
        string? ollamaPath = FindOllamaExe();
        if (ollamaPath is null)
        {
            Log.Warning("OllamaManager: cannot find ollama.exe to start");
            return false;
        }

        Log.Information("OllamaManager: starting ollama serve from '{Path}'", ollamaPath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ollamaPath,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaManager: failed to start ollama serve");
            return false;
        }

        // Wait for API to respond (up to 15 seconds)
        for (int i = 0; i < 15; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

            string? version = await GetVersionAsync(cancellationToken).ConfigureAwait(false);
            if (version is not null)
            {
                Log.Information("OllamaManager: ollama serve is ready (v{Version})", version);
                return true;
            }
        }

        Log.Warning("OllamaManager: ollama serve did not become ready within 15 seconds");
        return false;
    }

    /// <summary>
    /// Finds the ollama.exe path. Checks default install location, then PATH.
    /// </summary>
    private static string? FindOllamaExe()
    {
        // Default winget/installer location
        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Ollama", "ollama.exe");

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        // Check PATH via 'where'
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "ollama",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (proc is not null)
            {
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    // 'where' may return multiple lines; take the first
                    string firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    if (File.Exists(firstLine))
                    {
                        return firstLine;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore — where not available or failed
        }

        return null;
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private async Task<string?> GetVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            string json = await _http
                .GetStringAsync(_baseUrl + VersionEndpoint, cancellationToken)
                .ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("version", out var v))
            {
                return v.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "OllamaManager: version check failed");
            return null;
        }
    }

    private async Task<bool> IsModelPulledAsync(string modelTag, CancellationToken cancellationToken)
    {
        var installed = await GetInstalledModelTagsAsync(cancellationToken).ConfigureAwait(false);
        // Ollama tags may include ":latest" suffix — strip for comparison
        string normalised = NormaliseTag(modelTag);
        return installed.Any(t => string.Equals(NormaliseTag(t), normalised, StringComparison.Ordinal));
    }

    private async Task<string?> FindBestFallbackAsync(
        ModelsManifest manifest,
        string ollamaVersion,
        CancellationToken cancellationToken)
    {
        // Try manifest fallback first; if that's not compatible find the best installed model.
        var installed = await GetInstalledModelTagsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var candidate in installed)
        {
            var entry = FindEntry(manifest, candidate);
            string minVer = entry?.MinOllamaVersion ?? "0.0.0";
            if (CompareVersions(ollamaVersion, minVer) >= 0)
            {
                return NormaliseTag(candidate);
            }
        }

        // Fall back to the manifest default
        return manifest.FallbackModel;
    }

    private async Task<bool> DetectVersionChangeAsync(string currentVersion)
    {
        try
        {
            string dir = Path.GetDirectoryName(LastVersionFilePath)!;
            Directory.CreateDirectory(dir);

            if (!File.Exists(LastVersionFilePath))
            {
                await File.WriteAllTextAsync(LastVersionFilePath, currentVersion).ConfigureAwait(false);
                return false; // first run — no change to detect
            }

            string lastVersion = (await File.ReadAllTextAsync(LastVersionFilePath).ConfigureAwait(false)).Trim();
            if (!string.Equals(lastVersion, currentVersion, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(LastVersionFilePath, currentVersion).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaManager: version-change detection failed");
            return false;
        }
    }

    private async Task<ModelsManifest> LoadManifestAsync()
    {
        if (_manifest is not null)
        {
            return _manifest;
        }

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "DiktaMe.Core.System.models.json";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

            _manifest = await JsonSerializer
                .DeserializeAsync(stream, OllamaJsonContext.Default.ModelsManifest)
                .ConfigureAwait(false)
                ?? new ModelsManifest();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OllamaManager: failed to load models manifest; using empty manifest");
            _manifest = new ModelsManifest();
        }

        return _manifest;
    }

    private static ModelEntry? FindEntry(ModelsManifest manifest, string modelTag)
    {
        string normalised = NormaliseTag(modelTag);
        return manifest.Models.FirstOrDefault(m => string.Equals(NormaliseTag(m.Tag), normalised, StringComparison.Ordinal));
    }

    /// <summary>
    /// Strips ":latest" suffix and trims whitespace for consistent comparison.
    /// </summary>
    private static string NormaliseTag(string tag)
    {
        tag = tag.Trim();
        if (tag.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
        {
            tag = tag[..^7];
        }

        return tag;
    }

    /// <summary>
    /// Compares two semantic version strings (major.minor.patch).
    /// Returns negative if a &lt; b, zero if equal, positive if a &gt; b.
    /// Gracefully handles missing patch component.
    /// </summary>
    internal static int CompareVersions(string a, string b)
    {
        static Version Parse(string s)
        {
            // Normalise to at least major.minor.patch
            var parts = s.TrimStart('v').Split('.');
            string normalised = parts.Length switch
            {
                1 => $"{parts[0]}.0.0",
                2 => $"{parts[0]}.{parts[1]}.0",
                _ => $"{parts[0]}.{parts[1]}.{parts[2]}",
            };
            return Version.TryParse(normalised, out var v) ? v : new Version(0, 0, 0);
        }

        return Parse(a).CompareTo(Parse(b));
    }

    /// <summary>Parses /api/tags response into a list of model name strings.</summary>
    private static IReadOnlyList<string> ParseModelTags(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var models))
            {
                return Array.Empty<string>();
            }

            var tags = new List<string>();
            foreach (var model in models.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var name))
                {
                    string? tag = name.GetString();
                    if (tag is not null)
                    {
                        tags.Add(tag);
                    }
                }
            }
            return tags;
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "OllamaManager: failed to parse /api/tags response");
            return Array.Empty<string>();
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    /// <summary>Disposes the internal <see cref="HttpClient"/> if owned.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
