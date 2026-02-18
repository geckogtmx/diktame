
using DiktaMe.Core.STT;

namespace DiktaMe.Core.Tests.STT;
/// <summary>
/// Unit tests for <see cref="WhisperProvider"/>.
/// Tests that require a real GGML model file (hundreds of MB) are skipped
/// unless the model is already present — suitable for local dev but safe in CI.
/// </summary>
public sealed class WhisperProviderTests : IDisposable
{
    /// <summary>Isolated temp directory used as models dir for each test.</summary>
    private readonly string _tempDir;

    public WhisperProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WhisperTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultModel_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var p = new WhisperProvider(modelsDirectory: _tempDir);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_UnknownModelSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new WhisperProvider("xlarge_v99", modelsDirectory: _tempDir));
    }

    [Theory]
    [InlineData("tiny")]
    [InlineData("base")]
    [InlineData("small")]
    [InlineData("medium")]
    [InlineData("large")]
    [InlineData("turbo")]
    public void Constructor_AllValidModelSizes_DoNotThrow(string size)
    {
        var ex = Record.Exception(() =>
        {
            using var p = new WhisperProvider(size, modelsDirectory: _tempDir);
        });
        Assert.Null(ex);
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ContainsWhisperAndModelSize()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        Assert.Contains("Whisper", p.ProviderName);
        Assert.Contains("tiny", p.ProviderName);
    }

    // ── IsAvailableAsync / IsModelDownloaded ──────────────────────────────────

    [Fact]
    public async Task IsAvailable_WhenModelMissing_ReturnsFalse()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        Assert.False(await p.IsAvailableAsync());
        Assert.False(p.IsModelDownloaded);
    }

    [Fact]
    public async Task IsAvailable_WhenModelPresent_ReturnsTrue()
    {
        // Plant a stub file where the model would live
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        await File.WriteAllBytesAsync(p.ModelPath, [0x01, 0x02]);

        Assert.True(await p.IsAvailableAsync());
        Assert.True(p.IsModelDownloaded);
    }

    // ── ModelPath ─────────────────────────────────────────────────────────────

    [Fact]
    public void ModelPath_TinyModel_EndsWithExpectedFilename()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        Assert.EndsWith("ggml-tiny.bin", p.ModelPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelPath_TurboModel_EndsWithLargeV3TurboFilename()
    {
        using var p = new WhisperProvider("turbo", modelsDirectory: _tempDir);
        Assert.EndsWith("ggml-large-v3-turbo.bin", p.ModelPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModelPath_UsesProvidedDirectory()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        Assert.StartsWith(_tempDir, p.ModelPath, StringComparison.OrdinalIgnoreCase);
    }

    // ── TranscribeAsync — model missing ───────────────────────────────────────

    [Fact]
    public async Task Transcribe_ModelNotPresent_ThrowsFileNotFound()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        var tmpWav = Path.GetTempFileName();
        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => p.TranscribeAsync(tmpWav, "en"));
        }
        finally
        {
            File.Delete(tmpWav);
        }
    }

    // ── DownloadModelAsync — already present ──────────────────────────────────

    [Fact]
    public async Task DownloadModel_AlreadyPresent_ReturnsPathWithoutDownloading()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        // Plant a stub file (no real download)
        await File.WriteAllBytesAsync(p.ModelPath, [0x01]);

        bool progressFired = false;
        p.DownloadProgress += (_, _) => progressFired = true;

        string returnedPath = await p.DownloadModelAsync();

        Assert.Equal(p.ModelPath, returnedPath);
        Assert.False(progressFired, "No download should occur when file already exists");
    }

    // ── DownloadProgress event ────────────────────────────────────────────────

    [Fact]
    public void DownloadProgressEventArgs_PropertiesSet()
    {
        var args = new DownloadProgressEventArgs(50, 512_000, 1_024_000);

        Assert.Equal(50, args.Percent);
        Assert.Equal(512_000, args.BytesReceived);
        Assert.Equal(1_024_000, args.TotalBytes);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        p.Dispose();
        var ex = Record.Exception(() => p.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Transcribe_AfterDispose_ThrowsObjectDisposed()
    {
        using var p = new WhisperProvider("tiny", modelsDirectory: _tempDir);
        p.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => p.TranscribeAsync("dummy.wav", "en"));
    }
}
