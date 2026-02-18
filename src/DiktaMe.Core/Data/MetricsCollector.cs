
using DiktaMe.Core.Pipeline;

namespace DiktaMe.Core.Data;
/// <summary>
/// Tracks aggregate performance metrics derived from pipeline results.
/// Works in conjunction with <see cref="HistoryManager"/> for persistence.
/// </summary>
public sealed class MetricsCollector
{
    private readonly HistoryManager _history;

    // In-memory session accumulators (reset on each app start)
    private int _sessionWords;
    private int _sessionCount;
    private long _sessionTotalMs;

    public MetricsCollector(HistoryManager history)
    {
        _history = history;
    }

    /// <summary>
    /// Records metrics from a completed pipeline result and persists to history.
    /// </summary>
    public async Task RecordAsync(PipelineResult result, CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return;
        }

        _sessionWords += result.WordCount;
        _sessionCount++;
        _sessionTotalMs += result.TotalMs;

        await _history.LogSessionAsync(result, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns stats accumulated since the app started.
    /// </summary>
    public SessionStats GetSessionStats() => new(
        Words: _sessionWords,
        Sessions: _sessionCount,
        AverageLatencyMs: _sessionCount > 0 ? (double)_sessionTotalMs / _sessionCount : 0);

    /// <summary>
    /// Returns lifetime stats from the database for today.
    /// </summary>
    public Task<(int TotalWords, int TotalSessions)> GetTodayStatsAsync(
        CancellationToken cancellationToken = default)
        => _history.GetStatsAsync(DateTimeOffset.UtcNow.Date, cancellationToken);
}

/// <summary>
/// In-memory stats for the current application session.
/// </summary>
public sealed record SessionStats(int Words, int Sessions, double AverageLatencyMs);
