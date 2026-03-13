
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Pipeline;
using Xunit;

namespace DiktaMe.Core.Tests.Data;
[Trait("Category", "Integration")]
public sealed class HistoryManagerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;

    public HistoryManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"diktame_test_{Guid.NewGuid()}.db");
        _settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"diktame_hm_{Guid.NewGuid()}.json"));
        _history = new HistoryManager(_settings, _dbPath);
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitAsync_CreatesDatabase()
    {
        await _history.InitAsync();
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public async Task InitAsync_IsIdempotent()
    {
        await _history.InitAsync();
        var ex = await Record.ExceptionAsync(() => _history.InitAsync());
        Assert.Null(ex);
    }

    // ── Logging ───────────────────────────────────────────────────────────────

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
    public async Task LogSessionAsync_PersistsTtsPlayedMs()
    {
        await _history.InitAsync();

        var result = new PipelineResult
        {
            Text = "spoken text",
            Mode = "ask",
            IsSuccess = true,
            TotalMs = 2000,
            TtsPlayedMs = 1234,
        };

        await _history.LogSessionAsync(result);

        // Read raw column value via direct SQL
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tts_played_ms FROM history ORDER BY id DESC LIMIT 1";
        var value = await cmd.ExecuteScalarAsync();
        Assert.Equal(1234L, (long)value!);
    }

    [Fact]
    public async Task LogSessionAsync_GhostLevel_LogsNothing()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Ghost },
        });
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "secret text",
            Mode = "dictate",
            IsSuccess = true,
        });

        var (_, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(0, sessions);
    }

    [Fact]
    public async Task LogSessionAsync_StatsLevel_RecordsMetrics()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Stats },
        });
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "some text",
            Mode = "dictate",
            IsSuccess = true,
        });

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(1, sessions);
        Assert.Equal(2, words); // "some text" = 2 words (computed)
    }

    [Fact]
    public async Task LogSessionAsync_BalancedLevel_StoresScrubbed()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings
            {
                Level = PrivacyLevel.Balanced,
                PiiScrubEnabled = true,
            },
        });
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "normal text without PII",
            Mode = "dictate",
            IsSuccess = true,
        });

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(1, sessions);
        Assert.Equal(4, words); // "normal text without PII" = 4 words
    }

    [Fact]
    public async Task LogSessionAsync_FullLevel_StoresVerbatim()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { Level = PrivacyLevel.Full },
        });
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "verbatim text",
            Mode = "ask",
            IsSuccess = true,
        });

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(1, sessions);
        Assert.Equal(2, words); // "verbatim text" = 2 words
    }

    [Fact]
    public async Task LogSessionAsync_BeforeInit_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _history.LogSessionAsync(new PipelineResult
        {
            Text = "text",
            Mode = "dictate",
            IsSuccess = true,
        }));
        Assert.Null(ex);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_ReturnsZero_WhenNoRecords()
    {
        await _history.InitAsync();

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(0, words);
        Assert.Equal(0, sessions);
    }

    [Fact]
    public async Task GetStatsAsync_SumsMultipleSessions()
    {
        await _history.InitAsync();

        for (int i = 0; i < 3; i++)
        {
            await _history.LogSessionAsync(new PipelineResult
            {
                Text = "hello world",
                Mode = "dictate",
                IsSuccess = true,
            });
        }

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(3, sessions);
        Assert.Equal(6, words);
    }

    [Fact]
    public async Task GetStatsAsync_ExcludesFailedSessions()
    {
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "success",
            Mode = "dictate",
            IsSuccess = true,
        });
        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "failed attempt",
            Mode = "dictate",
            IsSuccess = false,
        });

        var (words, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(1, sessions); // only the successful one
        Assert.Equal(1, words);   // "success" = 1 word
    }

    [Fact]
    public async Task GetStatsAsync_ExcludesOldRecords()
    {
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "old text",
            Mode = "dictate",
            IsSuccess = true,
        });

        // Future cutoff — record is older than cutoff
        var (_, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(0, sessions);
    }

    // ── Pruning ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitAsync_Pruning_DoesNotThrow()
    {
        await _settings.UpdateAsync(new AppSettings
        {
            Privacy = new PrivacySettings { HistoryRetentionDays = 1 },
        });
        await _history.InitAsync();
        var ex = await Record.ExceptionAsync(() => _history.InitAsync());
        Assert.Null(ex);
    }

    // ── Wipe ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WipeAllAsync_DeletesAllRecords()
    {
        await _history.InitAsync();

        await _history.LogSessionAsync(new PipelineResult
        {
            Text = "test",
            Mode = "dictate",
            IsSuccess = true,
        });
        await _history.WipeAllAsync();

        var (_, sessions) = await _history.GetStatsAsync(DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Equal(0, sessions);
    }

    [Fact]
    public async Task WipeAllAsync_BeforeInit_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _history.WipeAllAsync());
        Assert.Null(ex);
    }

    public async ValueTask DisposeAsync()
    {
        _history.Dispose();
        await Task.CompletedTask;
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
