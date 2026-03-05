
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.Tests;
using Moq;
using Xunit;

namespace DiktaMe.Core.Tests.Data;
[Trait("Category", "Unit")]
[Collection(nameof(SettingsWriterCollection))]
public sealed class MetricsCollectorTests
{
    private static MetricsCollector MakeCollector(out Mock<HistoryManager> historyMock)
    {
        // HistoryManager is not an interface, but it has a public constructor with
        // optional dbPath — we use a real HistoryManager pointing at an in-memory path
        // via a Mock wrapper is not directly possible. Instead we build a real
        // HistoryManager with a temp db and dispose it after.
        // For unit-level tests we skip LogSessionAsync side effects by using a Ghost
        // privacy level so the DB insert is a no-op.
        historyMock = new Mock<HistoryManager>(
            new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_mc_{Guid.NewGuid()}.json")),
            Path.Combine(Path.GetTempPath(), $"metrics_test_{Guid.NewGuid()}.db"))
        {
            CallBase = true,
        };
        return new MetricsCollector(historyMock.Object);
    }

    // ── Session accumulation ──────────────────────────────────────────────────

    [Fact]
    public void GetSessionStats_InitialState_IsAllZero()
    {
        var collector = new MetricsCollector(new HistoryManager(
            new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_mc_{Guid.NewGuid()}.json")),
            Path.Combine(Path.GetTempPath(), $"mc_{Guid.NewGuid()}.db")));

        var stats = collector.GetSessionStats();

        Assert.Equal(0, stats.Words);
        Assert.Equal(0, stats.Sessions);
        Assert.Equal(0.0, stats.AverageLatencyMs);
    }

    [Fact]
    public async Task RecordAsync_AccumulatesWords()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mc_{Guid.NewGuid()}.db");
        var settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_mc_{Guid.NewGuid()}.json"));
        await settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Ghost },
        });
        var history = new HistoryManager(settings, dbPath);
        var collector = new MetricsCollector(history);

        await collector.RecordAsync(new PipelineResult
        {
            Text = "one two three four five",  // 5 words
            IsSuccess = true,
            TotalMs = 100,
            Mode = "dictate",
        });
        await collector.RecordAsync(new PipelineResult
        {
            Text = "one two three",  // 3 words
            IsSuccess = true,
            TotalMs = 200,
            Mode = "dictate",
        });

        var stats = collector.GetSessionStats();
        Assert.Equal(8, stats.Words);
        Assert.Equal(2, stats.Sessions);

        history.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task RecordAsync_FailedResult_IsSkipped()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mc_{Guid.NewGuid()}.db");
        var history = new HistoryManager(new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_mc_{Guid.NewGuid()}.json")), dbPath);
        var collector = new MetricsCollector(history);

        await collector.RecordAsync(new PipelineResult
        {
            Text = "this should be ignored",
            IsSuccess = false,
            TotalMs = 500,
            Mode = "dictate",
        });

        var stats = collector.GetSessionStats();
        Assert.Equal(0, stats.Words);
        Assert.Equal(0, stats.Sessions);

        history.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task GetSessionStats_AverageLatency_IsCorrect()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mc_{Guid.NewGuid()}.db");
        var settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_mc_{Guid.NewGuid()}.json"));
        await settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Ghost },
        });
        var history = new HistoryManager(settings, dbPath);
        var collector = new MetricsCollector(history);

        await collector.RecordAsync(new PipelineResult { Text = "word", IsSuccess = true, TotalMs = 100, Mode = "dictate" });
        await collector.RecordAsync(new PipelineResult { Text = "word", IsSuccess = true, TotalMs = 300, Mode = "dictate" });

        var stats = collector.GetSessionStats();
        Assert.Equal(200.0, stats.AverageLatencyMs);

        history.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public void GetSessionStats_NoSessions_AverageIsZero()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mc_{Guid.NewGuid()}.db");
        var history = new HistoryManager(new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_mc_{Guid.NewGuid()}.json")), dbPath);
        var collector = new MetricsCollector(history);

        var stats = collector.GetSessionStats();
        Assert.Equal(0.0, stats.AverageLatencyMs);

        history.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    // ── SessionStats record ───────────────────────────────────────────────────

    [Fact]
    public void SessionStats_Properties_AreAccessible()
    {
        var stats = new SessionStats(Words: 10, Chars: 50, Sessions: 3, AverageLatencyMs: 150.5, MinutesSinceFirstRequest: 2.0);
        Assert.Equal(10, stats.Words);
        Assert.Equal(50, stats.Chars);
        Assert.Equal(3, stats.Sessions);
        Assert.Equal(150.5, stats.AverageLatencyMs);
        Assert.Equal(2.0, stats.MinutesSinceFirstRequest);
    }

    [Fact]
    public void SessionStats_Equality_ByValue()
    {
        var a = new SessionStats(5, 25, 2, 100.0, 1.0);
        var b = new SessionStats(5, 25, 2, 100.0, 1.0);
        Assert.Equal(a, b);
    }
}
