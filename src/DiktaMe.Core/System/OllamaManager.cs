namespace DiktaMe.Core.SystemManagement;

using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

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
            Log.Information("OllamaManager: version changed since last run (new={Version})", version);

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
                return v.GetString();

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
                return NormaliseTag(candidate);
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
        if (_manifest is not null) return _manifest;

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
            tag = tag[..^7];
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
                return Array.Empty<string>();

            var tags = new List<string>();
            foreach (var model in models.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var name))
                {
                    string? tag = name.GetString();
                    if (tag is not null) tags.Add(tag);
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
        if (_disposed) return;
        _disposed = true;
        if (_ownsClient) _http.Dispose();
    }
}
