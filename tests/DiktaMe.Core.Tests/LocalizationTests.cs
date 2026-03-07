using System.Xml.Linq;

namespace DiktaMe.Core.Tests;

/// <summary>
/// Validates that all localization resource files are consistent:
/// every English key has a corresponding translation, and vice versa.
/// </summary>
public sealed class LocalizationTests
{
    // Resolve paths relative to the test assembly location → navigate up to repo root
    private static string RepoRoot
    {
        get
        {
            string dir = AppContext.BaseDirectory;
            // Walk up from bin/Debug/net8.0-windows10.0.19041.0/ to repo root
            while (dir is not null && !File.Exists(Path.Combine(dir, "DiktaMe.sln")))
            {
                dir = Path.GetDirectoryName(dir)!;
            }

            return dir ?? throw new InvalidOperationException("Could not find repo root (DiktaMe.sln).");
        }
    }

    // ── App .resw tests ─────────────────────────────────────────────────────

    [Fact]
    public void AllEnglishReswKeys_HaveSpanishTranslations()
    {
        var enKeys = GetReswKeys(Path.Combine(RepoRoot, "src", "DiktaMe.App", "Strings", "en", "Resources.resw"));
        var esKeys = GetReswKeys(Path.Combine(RepoRoot, "src", "DiktaMe.App", "Strings", "es-MX", "Resources.resw"));

        var missing = enKeys.Except(esKeys).ToList();

        Assert.True(missing.Count == 0,
            $"English keys missing in es-MX Resources.resw ({missing.Count}):\n  {string.Join("\n  ", missing)}");
    }

    [Fact]
    public void AllSpanishReswKeys_ExistInEnglish()
    {
        var enKeys = GetReswKeys(Path.Combine(RepoRoot, "src", "DiktaMe.App", "Strings", "en", "Resources.resw"));
        var esKeys = GetReswKeys(Path.Combine(RepoRoot, "src", "DiktaMe.App", "Strings", "es-MX", "Resources.resw"));

        var extra = esKeys.Except(enKeys).ToList();

        Assert.True(extra.Count == 0,
            $"es-MX keys not in English Resources.resw ({extra.Count}):\n  {string.Join("\n  ", extra)}");
    }

    [Fact]
    public void SpanishReswValues_PreserveFormatPlaceholders()
    {
        string enPath = Path.Combine(RepoRoot, "src", "DiktaMe.App", "Strings", "en", "Resources.resw");
        string esPath = Path.Combine(RepoRoot, "src", "DiktaMe.App", "Strings", "es-MX", "Resources.resw");

        var enEntries = GetReswEntries(enPath);
        var esEntries = GetReswEntries(esPath);

        var broken = new List<string>();
        foreach (var (key, enValue) in enEntries)
        {
            if (!esEntries.TryGetValue(key, out string? esValue))
            {
                continue; // Missing key — caught by other test
            }

            // Check {0}, {1}, {0:N0} etc. are preserved
            var enPlaceholders = ExtractPlaceholders(enValue);
            var esPlaceholders = ExtractPlaceholders(esValue);

            if (!enPlaceholders.SetEquals(esPlaceholders))
            {
                broken.Add($"{key}: EN has {{{string.Join(", ", enPlaceholders)}}} but ES has {{{string.Join(", ", esPlaceholders)}}}");
            }
        }

        Assert.True(broken.Count == 0,
            $"Format placeholders mismatch ({broken.Count}):\n  {string.Join("\n  ", broken)}");
    }

    // ── Core .resx tests ────────────────────────────────────────────────────

    [Fact]
    public void AllEnglishCoreResxKeys_HaveSpanishTranslations()
    {
        var enKeys = GetResxKeys(Path.Combine(RepoRoot, "src", "DiktaMe.Core", "Resources", "CoreStrings.resx"));
        var esKeys = GetResxKeys(Path.Combine(RepoRoot, "src", "DiktaMe.Core", "Resources", "CoreStrings.es-MX.resx"));

        var missing = enKeys.Except(esKeys).ToList();

        Assert.True(missing.Count == 0,
            $"English keys missing in CoreStrings.es-MX.resx ({missing.Count}):\n  {string.Join("\n  ", missing)}");
    }

    [Fact]
    public void AllSpanishCoreResxKeys_ExistInEnglish()
    {
        var enKeys = GetResxKeys(Path.Combine(RepoRoot, "src", "DiktaMe.Core", "Resources", "CoreStrings.resx"));
        var esKeys = GetResxKeys(Path.Combine(RepoRoot, "src", "DiktaMe.Core", "Resources", "CoreStrings.es-MX.resx"));

        var extra = esKeys.Except(enKeys).ToList();

        Assert.True(extra.Count == 0,
            $"es-MX keys not in English CoreStrings.resx ({extra.Count}):\n  {string.Join("\n  ", extra)}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HashSet<string> GetReswKeys(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants("data")
            .Select(d => d.Attribute("name")?.Value)
            .Where(n => n is not null)
            .ToHashSet()!;
    }

    private static HashSet<string> GetResxKeys(string path)
        => GetReswKeys(path); // Same XML format

    private static Dictionary<string, string> GetReswEntries(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants("data")
            .Where(d => d.Attribute("name")?.Value is not null)
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")?.Value ?? string.Empty);
    }

    private static HashSet<string> ExtractPlaceholders(string value)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(value, @"\{[0-9]+(?::[^}]+)?\}");
        return matches.Select(m => m.Value).ToHashSet();
    }
}
