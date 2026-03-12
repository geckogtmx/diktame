
using DiktaMe.Core.SystemManagement;
using FluentAssertions;
using Xunit;

namespace DiktaMe.Core.Tests.SystemManagement;

/// <summary>
/// Unit tests for <see cref="OllamaSearchService"/> HTML parsing.
/// Uses static <see cref="OllamaSearchService.ParseSearchResults"/> to test parsing
/// without HTTP calls.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OllamaSearchServiceTests
{
    // ── ParseSearchResults ─────────────────────────────────────────────────────

    [Fact]
    public void ParseSearchResults_ValidHtml_ParsesModelCards()
    {
        string html = """
            <html><body>
            <ul>
              <li x-test-model>
                <a href="/library/gemma3">
                  <span x-test-search-response-title>gemma3</span>
                </a>
                <p class="text-neutral-800">Google's lightweight model</p>
                <span x-test-size>1B</span>
                <span x-test-size>4B</span>
                <span x-test-capability>Tools</span>
                <span x-test-pull-count>5.2M</span>
                <span x-test-tag-count>42</span>
                <span x-test-updated>2 days ago</span>
              </li>
              <li x-test-model>
                <a href="/library/llama3.2">
                  <span x-test-search-response-title>llama3.2</span>
                </a>
                <p class="text-neutral-800">Meta's open model</p>
                <span x-test-size>3B</span>
                <span x-test-capability>Vision</span>
                <span x-test-pull-count>12.1M</span>
                <span x-test-tag-count>18</span>
                <span x-test-updated>1 week ago</span>
              </li>
            </ul>
            </body></html>
            """;

        var results = OllamaSearchService.ParseSearchResults(html);

        results.Should().HaveCount(2);

        results[0].Name.Should().Be("gemma3");
        results[0].Description.Should().Contain("lightweight");
        results[0].SizeLabels.Should().BeEquivalentTo(new[] { "1B", "4B" });
        results[0].Capabilities.Should().Contain("Tools");
        results[0].PullCount.Should().Be("5.2M");
        results[0].TagCount.Should().Be(42);
        results[0].LastUpdated.Should().Be("2 days ago");
        results[0].Url.Should().Be("https://ollama.com/library/gemma3");
        results[0].IsInstalled.Should().BeFalse();

        results[1].Name.Should().Be("llama3.2");
        results[1].Capabilities.Should().Contain("Vision");
    }

    [Fact]
    public void ParseSearchResults_WithInstalledSet_MarksInstalledModels()
    {
        string html = """
            <html><body>
            <ul>
              <li x-test-model>
                <a href="/library/gemma3">
                  <span x-test-search-response-title>gemma3</span>
                </a>
                <p class="text-neutral-800">Desc</p>
                <span x-test-pull-count>1M</span>
                <span x-test-tag-count>5</span>
              </li>
              <li x-test-model>
                <a href="/library/llama3.2">
                  <span x-test-search-response-title>llama3.2</span>
                </a>
                <p class="text-neutral-800">Desc</p>
                <span x-test-pull-count>2M</span>
                <span x-test-tag-count>3</span>
              </li>
            </ul>
            </body></html>
            """;

        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gemma3" };

        var results = OllamaSearchService.ParseSearchResults(html, installed);

        results.Should().HaveCount(2);
        results[0].IsInstalled.Should().BeTrue();
        results[1].IsInstalled.Should().BeFalse();
    }

    [Fact]
    public void ParseSearchResults_InstalledWithTag_MatchesByPrefix()
    {
        string html = """
            <html><body>
            <ul>
              <li x-test-model>
                <a href="/library/gemma3">
                  <span x-test-search-response-title>gemma3</span>
                </a>
                <p class="text-neutral-800">Desc</p>
                <span x-test-pull-count>1M</span>
                <span x-test-tag-count>5</span>
              </li>
            </ul>
            </body></html>
            """;

        // Installed as "gemma3:1b" — should still match "gemma3" search result
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gemma3:1b" };

        var results = OllamaSearchService.ParseSearchResults(html, installed);

        results.Should().HaveCount(1);
        results[0].IsInstalled.Should().BeTrue();
    }

    [Fact]
    public void ParseSearchResults_EmptyHtml_ReturnsEmpty()
    {
        var results = OllamaSearchService.ParseSearchResults("<html><body></body></html>");

        results.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResults_NoModelCards_ReturnsEmpty()
    {
        string html = """
            <html><body>
            <ul>
              <li>Not a model card</li>
            </ul>
            </body></html>
            """;

        var results = OllamaSearchService.ParseSearchResults(html);

        results.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResults_MissingTitle_SkipsCard()
    {
        string html = """
            <html><body>
            <ul>
              <li x-test-model>
                <a href="/library/noname">
                </a>
                <p class="text-neutral-800">No title span</p>
              </li>
              <li x-test-model>
                <a href="/library/valid">
                  <span x-test-search-response-title>valid-model</span>
                </a>
                <p class="text-neutral-800">Has title</p>
                <span x-test-pull-count>1M</span>
                <span x-test-tag-count>1</span>
              </li>
            </ul>
            </body></html>
            """;

        var results = OllamaSearchService.ParseSearchResults(html);

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("valid-model");
    }

    [Fact]
    public void ParseSearchResults_MissingOptionalFields_DefaultsGracefully()
    {
        string html = """
            <html><body>
            <ul>
              <li x-test-model>
                <a href="/library/minimal">
                  <span x-test-search-response-title>minimal</span>
                </a>
              </li>
            </ul>
            </body></html>
            """;

        var results = OllamaSearchService.ParseSearchResults(html);

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("minimal");
        results[0].Description.Should().BeEmpty();
        results[0].SizeLabels.Should().BeEmpty();
        results[0].Capabilities.Should().BeEmpty();
        results[0].PullCount.Should().BeEmpty();
        results[0].TagCount.Should().Be(0);
        results[0].LastUpdated.Should().BeEmpty();
    }

    [Fact]
    public void ParseSearchResults_RelativeUrl_PrefixedWithDomain()
    {
        string html = """
            <html><body>
            <ul>
              <li x-test-model>
                <a href="/library/phi4">
                  <span x-test-search-response-title>phi4</span>
                </a>
                <span x-test-pull-count>1M</span>
                <span x-test-tag-count>1</span>
              </li>
            </ul>
            </body></html>
            """;

        var results = OllamaSearchService.ParseSearchResults(html);

        results[0].Url.Should().StartWith("https://ollama.com/");
    }
}
