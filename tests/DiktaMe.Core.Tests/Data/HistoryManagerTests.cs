namespace DiktaMe.Core.Tests.Data;

using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Pipeline;
using Xunit;

[Trait("Category", "Integration")]
public sealed class HistoryManagerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;

    public HistoryManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"diktame_test_{Guid.NewGuid()}.db");
        _settings = new SettingsManager();
        _history = new HistoryManager(_settings, _dbPath);
    }

    [Fact]
    public async Task InitAsync_CreatesDatabase()
    {
        await _history.InitAsync();
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public async Task LogSessionAsync_RecordsSuccessfulResult()
    {
        await _history.InitAsync();

        var result = new PipelineResult
        {
            Text = "Hello world",
            RawTranscript = "hello world",
            Mode = "dictate",
            IsSuccess = true,
            TranscriptionMs = 500,
            ProcessingMs = 200,
            InjectionMs = 10,
            TotalMs = 710,
            SttProvider = "Deepgram Nova-2",
            LlmProvider = "gemini-2.0-flash (Gemini)",
        };

        await _history.LogSessionAsync(result);

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(1, sessions);
        Assert.Equal(2, words); // "Hello world" = 2 words
    }

    [Fact]
    public async Task WipeAllAsync_DeletesAllRecords()
    {
        await _history.InitAsync();

        var result = new PipelineResult
        {
            Text = "test",
            Mode = "dictate",
            IsSuccess = true,
        };
        await _history.LogSessionAsync(result);
        await _history.WipeAllAsync();

        var (_, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Equal(0, sessions);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsZero_WhenNoRecords()
    {
        await _history.InitAsync();

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(0, words);
        Assert.Equal(0, sessions);
    }

    public async ValueTask DisposeAsync()
    {
        _history.Dispose();
        await Task.CompletedTask;
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
