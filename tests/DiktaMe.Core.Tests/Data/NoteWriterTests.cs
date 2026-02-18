
using DiktaMe.Core.Data;
using Xunit;

namespace DiktaMe.Core.Tests.Data;
[Trait("Category", "Integration")]
public sealed class NoteWriterTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public NoteWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"diktame_notes_{Guid.NewGuid()}");
        _testFile = Path.Combine(_testDir, "notes.md");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ── Basic append ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_CreatesFileAndDirectory()
    {
        await NoteWriter.AppendAsync(_testFile, "Hello notes");

        Assert.True(File.Exists(_testFile));
    }

    [Fact]
    public async Task AppendAsync_WritesMarkdownHeading()
    {
        await NoteWriter.AppendAsync(_testFile, "My note content");

        string content = await File.ReadAllTextAsync(_testFile);
        Assert.Contains("##", content);
        Assert.Contains("My note content", content);
    }

    [Fact]
    public async Task AppendAsync_AppendsMultipleNotes()
    {
        await NoteWriter.AppendAsync(_testFile, "First note");
        await NoteWriter.AppendAsync(_testFile, "Second note");

        string content = await File.ReadAllTextAsync(_testFile);
        Assert.Contains("First note", content);
        Assert.Contains("Second note", content);
    }

    [Fact]
    public async Task AppendAsync_TrimsTailingWhitespace()
    {
        await NoteWriter.AppendAsync(_testFile, "   trimmed text   ");

        string content = await File.ReadAllTextAsync(_testFile);
        // text.Trim() is applied before writing
        Assert.Contains("trimmed text", content);
        Assert.DoesNotContain("   trimmed text   ", content);
    }

    // ── Empty / whitespace guard ──────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_EmptyText_DoesNotCreateFile()
    {
        await NoteWriter.AppendAsync(_testFile, "");

        Assert.False(File.Exists(_testFile));
    }

    [Fact]
    public async Task AppendAsync_WhitespaceOnly_DoesNotCreateFile()
    {
        await NoteWriter.AppendAsync(_testFile, "   \t  \n  ");

        Assert.False(File.Exists(_testFile));
    }

    // ── Timestamp format ──────────────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_UsesDefaultTimestampFormat()
    {
        await NoteWriter.AppendAsync(_testFile, "timestamp test");

        string content = await File.ReadAllTextAsync(_testFile);
        // Default format "yyyy-MM-dd HH:mm" — check year prefix at minimum
        Assert.Contains($"## {DateTime.Now.Year}", content);
    }

    [Fact]
    public async Task AppendAsync_CustomTimestampFormat_IsUsed()
    {
        await NoteWriter.AppendAsync(_testFile, "custom format", timestampFormat: "yyyy");

        string content = await File.ReadAllTextAsync(_testFile);
        Assert.Contains($"## {DateTime.Now.Year}", content);
    }

    // ── Directory creation ────────────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_CreatesNestedDirectories()
    {
        string deepFile = Path.Combine(_testDir, "sub", "deep", "notes.md");
        await NoteWriter.AppendAsync(deepFile, "deep note");

        Assert.True(File.Exists(deepFile));
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendAsync_CancelledToken_ThrowsOrExits()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Already-cancelled token should throw OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NoteWriter.AppendAsync(_testFile, "should not write", cancellationToken: cts.Token));
    }
}
