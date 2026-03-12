
using System.Collections.Concurrent;
using System.Net.Http;
using HtmlAgilityPack;
using Serilog;

namespace DiktaMe.Core.SystemManagement;

/// <summary>Search result from ollama.com/search.</summary>
public sealed record OllamaSearchResult(
    string Name,
    string Description,
    string[] SizeLabels,
    string[] Capabilities,
    string PullCount,
    int TagCount,
    string LastUpdated,
    string Url,
    bool IsInstalled);

/// <summary>
/// Queries the Ollama model registry (ollama.com/search) for real-time model discovery.
/// Results are cross-referenced with locally installed models.
/// </summary>
public sealed class OllamaSearchService : IDisposable
{
    private const string SearchUrl = "https://ollama.com/search";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly OllamaManager _ollamaManager;
    private readonly bool _ownsClient;
    private readonly ConcurrentDictionary<string, (DateTime Timestamp, IReadOnlyList<OllamaSearchResult> Results)> _cache = new();
    private bool _disposed;

    public OllamaSearchService(OllamaManager ollamaManager, HttpClient? httpClient = null)
    {
        _ollamaManager = ollamaManager;
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = HttpTimeout };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "DiktaMe/2.0");
    }

    /// <summary>
    /// Searches the Ollama model registry. Empty query returns popular/featured models.
    /// Results are cached for 5 minutes.
    /// </summary>
    public async Task<IReadOnlyList<OllamaSearchResult>> SearchModelsAsync(
        string query = "",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string cacheKey = query.Trim().ToLowerInvariant();

        // Check cache
        if (_cache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.Timestamp < CacheTtl)
        {
            return cached.Results;
        }

        try
        {
            string url = string.IsNullOrWhiteSpace(query)
                ? SearchUrl
                : $"{SearchUrl}?q={Uri.EscapeDataString(query.Trim())}";

            string html = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            // Get installed model tags for cross-referencing
            var installed = await _ollamaManager.GetInstalledModelTagsAsync(cancellationToken)
                .ConfigureAwait(false);
            var installedSet = new HashSet<string>(
                installed.Select(NormaliseTag), StringComparer.OrdinalIgnoreCase);

            var results = ParseSearchResults(html, installedSet);

            // Cache results
            _cache[cacheKey] = (DateTime.UtcNow, results);

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "OllamaSearchService: search failed for query '{Query}'", query);
            return Array.Empty<OllamaSearchResult>();
        }
    }

    /// <summary>Returns popular/trending models (empty query search).</summary>
    public Task<IReadOnlyList<OllamaSearchResult>> GetPopularModelsAsync(
        CancellationToken cancellationToken = default)
        => SearchModelsAsync("", cancellationToken);

    /// <summary>Clears the search result cache.</summary>
    public void ClearCache() => _cache.Clear();

    // ── HTML Parsing ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses ollama.com/search HTML into structured search results.
    /// Uses x-test-* attributes for reliable extraction.
    /// </summary>
    internal static IReadOnlyList<OllamaSearchResult> ParseSearchResults(
        string html, HashSet<string>? installedSet = null)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<OllamaSearchResult>();

        // Each model card is an <li x-test-model>
        var modelNodes = doc.DocumentNode.SelectNodes("//li[@x-test-model]");
        if (modelNodes is null)
        {
            return results;
        }

        foreach (var li in modelNodes)
        {
            try
            {
                // Name from <span x-test-search-response-title>
                var titleNode = li.SelectSingleNode(".//span[@x-test-search-response-title]");
                string name = titleNode?.InnerText.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // Link from <a href="/library/...">
                var linkNode = li.SelectSingleNode(".//a[@href]");
                string href = linkNode?.GetAttributeValue("href", "") ?? "";
                string url = href.StartsWith("/", StringComparison.Ordinal)
                    ? $"https://ollama.com{href}"
                    : href;

                // Description from <p class="...text-neutral-800...">
                var descNode = li.SelectSingleNode(".//p[contains(@class, 'text-neutral-800')]");
                string description = descNode?.InnerText.Trim() ?? "";

                // Size labels from <span x-test-size>
                var sizeNodes = li.SelectNodes(".//span[@x-test-size]");
                string[] sizes = sizeNodes?.Select(n => n.InnerText.Trim()).ToArray() ?? Array.Empty<string>();

                // Capabilities from <span x-test-capability>
                var capNodes = li.SelectNodes(".//span[@x-test-capability]");
                string[] capabilities = capNodes?.Select(n => n.InnerText.Trim()).ToArray() ?? Array.Empty<string>();

                // Pull count from <span x-test-pull-count>
                var pullNode = li.SelectSingleNode(".//span[@x-test-pull-count]");
                string pullCount = pullNode?.InnerText.Trim() ?? "";

                // Tag count from <span x-test-tag-count>
                var tagNode = li.SelectSingleNode(".//span[@x-test-tag-count]");
                int tagCount = 0;
                if (tagNode is not null)
                {
                    _ = int.TryParse(tagNode.InnerText.Trim(), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out tagCount);
                }

                // Last updated from <span x-test-updated>
                var updatedNode = li.SelectSingleNode(".//span[@x-test-updated]");
                string lastUpdated = updatedNode?.InnerText.Trim() ?? "";

                // Cross-reference with installed models
                bool isInstalled = installedSet?.Any(t =>
                    t.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)) ?? false;

                results.Add(new OllamaSearchResult(
                    name, description, sizes, capabilities,
                    pullCount, tagCount, lastUpdated, url, isInstalled));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "OllamaSearchService: failed to parse model card");
            }
        }

        return results;
    }

    private static string NormaliseTag(string tag)
    {
        tag = tag.Trim();
        if (tag.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
        {
            tag = tag[..^7];
        }
        return tag;
    }

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
