
using DiktaMe.Core.Config;
using Xunit;

namespace DiktaMe.Core.Tests.Config;
[Trait("Category", "Unit")]
public sealed class SnippetManagerTests
{
    // ── ExpandSnippets — basic trigger matching ───────────────────────────────

    [Fact]
    public void ExpandSnippets_ExactMatch_ReplacesTextEnd()
    {
        var mgr = new SnippetManager();
        mgr.Add("my email", "alice@example.com");

        string result = mgr.ExpandSnippets("please contact my email");
        Assert.Equal("please contact alice@example.com", result);
    }

    [Fact]
    public void ExpandSnippets_CaseInsensitive_Matches()
    {
        var mgr = new SnippetManager();
        mgr.Add("My Email", "alice@example.com");

        string result = mgr.ExpandSnippets("contact MY EMAIL");
        Assert.Equal("contact alice@example.com", result);
    }

    [Fact]
    public void ExpandSnippets_TrailingPunctuation_StillMatches()
    {
        var mgr = new SnippetManager();
        mgr.Add("my email", "alice@example.com");

        string result = mgr.ExpandSnippets("contact my email.");
        Assert.Equal("contact alice@example.com", result);
    }

    [Fact]
    public void ExpandSnippets_TriggerInMiddle_DoesNotExpand()
    {
        var mgr = new SnippetManager();
        mgr.Add("my email", "alice@example.com");

        // "my email" appears in the middle, not at the end
        string result = mgr.ExpandSnippets("send to my email please");
        Assert.Equal("send to my email please", result);
    }

    [Fact]
    public void ExpandSnippets_NoSnippets_ReturnsSameText()
    {
        var mgr = new SnippetManager();
        string result = mgr.ExpandSnippets("hello world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ExpandSnippets_EmptyText_ReturnsSameText()
    {
        var mgr = new SnippetManager();
        mgr.Add("trigger", "content");
        Assert.Equal(string.Empty, mgr.ExpandSnippets(string.Empty));
    }

    [Fact]
    public void ExpandSnippets_WhitespaceOnly_ReturnsSameText()
    {
        var mgr = new SnippetManager();
        mgr.Add("trigger", "content");
        Assert.Equal("   ", mgr.ExpandSnippets("   "));
    }

    [Fact]
    public void ExpandSnippets_SingleWordTrigger_Expands()
    {
        var mgr = new SnippetManager();
        mgr.Add("sig", "— Alice Smith, Engineering");

        string result = mgr.ExpandSnippets("Best regards sig");
        Assert.Equal("Best regards — Alice Smith, Engineering", result);
    }

    [Fact]
    public void ExpandSnippets_TriggerAtStartOfText_Expands()
    {
        var mgr = new SnippetManager();
        mgr.Add("hello", "Hello, World!");

        string result = mgr.ExpandSnippets("hello");
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void ExpandSnippets_MultiLineTrigger_Expands()
    {
        var mgr = new SnippetManager();
        mgr.Add("addr", "123 Main St\nSpringfield, IL 62701");

        string result = mgr.ExpandSnippets("Ship to addr");
        Assert.Equal("Ship to 123 Main St\nSpringfield, IL 62701", result);
    }

    // ── ExpandSnippets — no false positives ──────────────────────────────────

    [Fact]
    public void ExpandSnippets_PartialWordMatch_DoesNotExpand()
    {
        var mgr = new SnippetManager();
        mgr.Add("mail", "alice@example.com");

        // "email" ends in "mail" but it's not a word boundary
        string result = mgr.ExpandSnippets("send via email");
        Assert.Equal("send via email", result);
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_AddsSnippet_ReturnsTrue()
    {
        var mgr = new SnippetManager();
        bool added = mgr.Add("trigger", "content");
        Assert.True(added);
        Assert.Single(mgr.GetAll());
    }

    [Fact]
    public void Add_AtLimit_ReturnsFalse()
    {
        var mgr = new SnippetManager();
        for (int i = 0; i < SnippetManager.MaxSnippets; i++)
        {
            mgr.Add($"trigger{i}", "content");
        }

        bool added = mgr.Add("one more", "content");
        Assert.False(added);
        Assert.Equal(SnippetManager.MaxSnippets, mgr.GetAll().Count);
    }

    [Fact]
    public void Update_ExistingId_ReturnsTrue_AndUpdatesContent()
    {
        var mgr = new SnippetManager();
        mgr.Add("old trigger", "old content");
        string id = mgr.GetAll()[0].Id;

        bool updated = mgr.Update(id, "new trigger", "new content");
        Assert.True(updated);
        Assert.Equal("new trigger", mgr.GetAll()[0].Trigger);
        Assert.Equal("new content", mgr.GetAll()[0].Content);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        var mgr = new SnippetManager();
        bool updated = mgr.Update("nonexistent-id", "trigger", "content");
        Assert.False(updated);
    }

    [Fact]
    public void Remove_ExistingId_ReturnsTrue_AndRemovesSnippet()
    {
        var mgr = new SnippetManager();
        mgr.Add("trigger", "content");
        string id = mgr.GetAll()[0].Id;

        bool removed = mgr.Remove(id);
        Assert.True(removed);
        Assert.Empty(mgr.GetAll());
    }

    [Fact]
    public void Remove_UnknownId_ReturnsFalse()
    {
        var mgr = new SnippetManager();
        bool removed = mgr.Remove("nonexistent-id");
        Assert.False(removed);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveAndLoad_RoundTrip_PreservesSnippets()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"snippets_{Guid.NewGuid()}.json");
        try
        {
            var mgr1 = new SnippetManager(tmpFile);
            mgr1.Add("my email", "alice@example.com");
            mgr1.Add("sig", "— Alice Smith");
            await mgr1.SaveAsync();

            var mgr2 = new SnippetManager(tmpFile);
            await mgr2.LoadAsync();

            Assert.Equal(2, mgr2.GetAll().Count);
            Assert.Equal("my email", mgr2.GetAll()[0].Trigger);
            Assert.Equal("alice@example.com", mgr2.GetAll()[0].Content);
            Assert.Equal("sig", mgr2.GetAll()[1].Trigger);
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadAsync_MissingFile_StartsEmpty()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"snippets_{Guid.NewGuid()}.json");
        var mgr = new SnippetManager(tmpFile);
        await mgr.LoadAsync();
        Assert.Empty(mgr.GetAll());
    }
}
