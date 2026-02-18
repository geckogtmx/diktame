
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace DiktaMe.Core.Config;
/// <summary>
/// A single voice snippet: a short trigger phrase that expands to longer content.
/// Port of V1's snippet system (SPEC_026).
/// </summary>
public sealed record Snippet
{
    /// <summary>Unique identifier (GUID string).</summary>
    public required string Id { get; init; }

    /// <summary>The trigger phrase the user speaks (e.g. "my email").</summary>
    public required string Trigger { get; init; }

    /// <summary>The expanded content to inject (e.g. "hello@example.com").</summary>
    public required string Content { get; init; }
}

/// <summary>
/// Manages a list of voice snippets persisted to
/// <c>%APPDATA%\DiktaMe\snippets.json</c>.
/// Snippet expansion runs post-LLM, pre-inject in every injecting pipeline.
/// Port of V1's snippet system from SPEC_026 (Phase 1 — core engine only).
/// </summary>
public sealed class SnippetManager
{
    /// <summary>Maximum number of snippets allowed.</summary>
    public const int MaxSnippets = 100;

    /// <summary>Default path to the snippets file.</summary>
    public static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "snippets.json");

    private readonly string _filePath;
    private List<Snippet> _snippets = new();

    public SnippetManager(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads snippets from disk. Creates an empty file if none exists.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _snippets = new List<Snippet>();
            Log.Information("SnippetManager: no snippets file found, starting empty");
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_filePath, cancellationToken)
                .ConfigureAwait(false);
            _snippets = JsonSerializer.Deserialize(json, SnippetListContext.Default.ListSnippet)
                ?? new List<Snippet>();
            Log.Information("SnippetManager: loaded {Count} snippets from '{Path}'",
                _snippets.Count, _filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SnippetManager: failed to load snippets — starting empty");
            _snippets = new List<Snippet>();
        }
    }

    /// <summary>
    /// Saves snippets to disk atomically (write to .tmp then rename).
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = _filePath + ".tmp";
        string json = JsonSerializer.Serialize(_snippets, SnippetListContext.Default.ListSnippet);

        await File.WriteAllTextAsync(tmp, json, cancellationToken).ConfigureAwait(false);
        File.Move(tmp, _filePath, overwrite: true);

        Log.Information("SnippetManager: saved {Count} snippets to '{Path}'",
            _snippets.Count, _filePath);
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    /// <summary>Returns a read-only snapshot of all snippets.</summary>
    public IReadOnlyList<Snippet> GetAll() => _snippets.AsReadOnly();

    /// <summary>
    /// Adds a new snippet. Returns false if the limit of 100 is reached.
    /// </summary>
    public bool Add(string trigger, string content)
    {
        if (_snippets.Count >= MaxSnippets)
        {
            Log.Warning("SnippetManager: max snippet count ({Max}) reached", MaxSnippets);
            return false;
        }

        _snippets.Add(new Snippet
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = trigger.Trim(),
            Content = content,
        });
        return true;
    }

    /// <summary>
    /// Updates an existing snippet by id. Returns false if not found.
    /// </summary>
    public bool Update(string id, string trigger, string content)
    {
        int index = _snippets.FindIndex(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _snippets[index] = _snippets[index] with
        {
            Trigger = trigger.Trim(),
            Content = content,
        };
        return true;
    }

    /// <summary>
    /// Removes a snippet by id. Returns false if not found.
    /// </summary>
    public bool Remove(string id)
    {
        int removed = _snippets.RemoveAll(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        return removed > 0;
    }

    // ── Expansion ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Expands any snippet triggers found at the end of <paramref name="text"/>.
    /// Matching is case-insensitive and ignores trailing punctuation on the trigger.
    /// Called post-LLM, pre-inject in every injecting pipeline.
    /// </summary>
    /// <remarks>
    /// Only the last word (or multi-word trigger) is checked — snippets fire when
    /// the spoken trigger is the final token(s) in the text. This prevents mid-sentence
    /// false positives (e.g. if "my email" is a snippet, "send to my email please" does
    /// not expand, but "send to my email" does).
    /// </remarks>
    public string ExpandSnippets(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _snippets.Count == 0)
        {
            return text;
        }

        // Normalise: strip trailing whitespace, work on lower-case copy for matching
        string trimmed = text.TrimEnd();
        string lower = trimmed.ToLowerInvariant();

        foreach (Snippet snippet in _snippets)
        {
            string trigger = snippet.Trigger.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(trigger))
            {
                continue;
            }

            // Strip trailing punctuation from the text tail before comparing
            string tail = StripTrailingPunctuation(lower);

            if (tail.EndsWith(trigger, StringComparison.Ordinal))
            {
                // Find position in original (preserving case) trimmed string
                // by matching suffix length
                int tailLen = tail.Length;
                int trigLen = trigger.Length;
                int startInTail = tailLen - trigLen;

                // Map startInTail back into trimmed (tail may have had punctuation stripped)
                int punctStripped = lower.Length - tail.Length;
                int startInTrimmed = startInTail;   // tail is a prefix-identical substr of lower

                // Verify a word boundary (not in the middle of a word)
                if (startInTrimmed > 0 && !char.IsWhiteSpace(trimmed[startInTrimmed - 1]))
                {
                    continue;
                }

                string expanded = trimmed[..startInTrimmed] + snippet.Content;
                Log.Debug("SnippetManager: expanded trigger '{Trigger}' → {Chars} chars",
                    snippet.Trigger, snippet.Content.Length);
                return expanded;
            }
        }

        return text;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string StripTrailingPunctuation(string s)
    {
        int end = s.Length;
        while (end > 0 && char.IsPunctuation(s[end - 1]))
        {
            end--;
        }

        return s[..end];
    }
}

/// <summary>
/// Source-generated JSON context for <see cref="Snippet"/> lists.
/// Required for IL-trim compatibility.
/// </summary>
[JsonSerializable(typeof(List<Snippet>))]
[JsonSerializable(typeof(Snippet))]
public partial class SnippetListContext : JsonSerializerContext { }
