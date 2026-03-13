
using DiktaMe.Core.Config;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.Security;
using Microsoft.Data.Sqlite;
using Serilog;

namespace DiktaMe.Core.Data;
/// <summary>
/// Persists pipeline session history to a SQLite database at
/// <c>%APPDATA%\DiktaMe\history.db</c>.
/// Respects the user's privacy level — Ghost logs nothing, Stats logs metrics only,
/// Balanced scrubs PII, Full logs everything.
/// Port of V1's HistoryManager from python/utils/history_manager.py.
/// </summary>
public sealed class HistoryManager : IDisposable
{
    /// <summary>Default path to the SQLite database file.</summary>
    public static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "history.db");

    private readonly SettingsManager _settings;
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private bool _disposed;

    public HistoryManager(SettingsManager settings, string? dbPath = null)
    {
        _settings = settings;
        _dbPath = dbPath ?? DbPath;
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the database, creates the schema if needed, and prunes old records.
    /// Call once at startup.
    /// </summary>
    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        string? dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await CreateSchemaAsync().ConfigureAwait(false);
        await PruneOldRecordsAsync(cancellationToken).ConfigureAwait(false);

        Log.Information("HistoryManager: database ready at '{Path}'", _dbPath);
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Logs a completed pipeline session according to the current privacy level.
    /// </summary>
    public async Task LogSessionAsync(PipelineResult result, CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            Log.Warning("HistoryManager: not initialised — skipping log");
            return;
        }

        var level = _settings.Current.Privacy.Level;

        if (level == PrivacyLevel.Ghost)
        {
            return;  // Log nothing at Ghost level
        }

        string? text = null;
        string? rawTranscript = null;

        if (level >= PrivacyLevel.Stats)
        {
            // Stats and above: capture metrics but only text at Balanced+
        }

        if (level >= PrivacyLevel.Balanced)
        {
            // Apply PII scrubbing at Balanced level
            text = result.Text;
            rawTranscript = result.RawTranscript;

            if (_settings.Current.Privacy.PiiScrubEnabled)
            {
                text = PIIScrubber.Scrub(text ?? string.Empty);
                rawTranscript = rawTranscript is not null
                    ? PIIScrubber.Scrub(rawTranscript)
                    : null;
            }
        }

        if (level == PrivacyLevel.Full)
        {
            // Full level: store verbatim (no scrubbing)
            text = result.Text;
            rawTranscript = result.RawTranscript;
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO history
                (timestamp, mode, text, raw_transcript, stt_provider, llm_provider,
                 word_count, transcription_ms, processing_ms, injection_ms, total_ms, is_success,
                 recording_ms, audio_duration_s, tokens_per_sec, error_message, tts_played_ms)
            VALUES
                ($ts, $mode, $text, $raw, $stt, $llm,
                 $words, $trans_ms, $proc_ms, $inj_ms, $total_ms, $success,
                 $rec_ms, $audio_dur, $tok_sec, $err_msg, $tts_ms)
            """;

        cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$mode", result.Mode);
        cmd.Parameters.AddWithValue("$text", text ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$raw", rawTranscript ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$stt", result.SttProvider ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$llm", result.LlmProvider ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$words", result.WordCount);
        cmd.Parameters.AddWithValue("$trans_ms", result.TranscriptionMs);
        cmd.Parameters.AddWithValue("$proc_ms", result.ProcessingMs);
        cmd.Parameters.AddWithValue("$inj_ms", result.InjectionMs);
        cmd.Parameters.AddWithValue("$total_ms", result.TotalMs);
        cmd.Parameters.AddWithValue("$success", result.IsSuccess ? 1 : 0);
        cmd.Parameters.AddWithValue("$rec_ms", result.RecordingMs);
        cmd.Parameters.AddWithValue("$audio_dur", result.AudioDurationSec.HasValue ? result.AudioDurationSec.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$tok_sec", result.TokensPerSec.HasValue ? result.TokensPerSec.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$err_msg", result.ErrorMessage ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$tts_ms", result.TtsPlayedMs);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the total word count and session count since the given UTC timestamp.
    /// </summary>
    public async Task<(int TotalWords, int TotalSessions)> GetStatsAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return (0, 0);
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(word_count), 0), COUNT(*)
            FROM history
            WHERE timestamp >= $since AND is_success = 1
            """;
        cmd.Parameters.AddWithValue("$since", since.ToUnixTimeSeconds());

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (reader.GetInt32(0), reader.GetInt32(1));
        }

        return (0, 0);
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private async Task CreateSchemaAsync()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS history (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp       INTEGER NOT NULL,
                mode            TEXT    NOT NULL,
                text            TEXT,
                raw_transcript  TEXT,
                stt_provider    TEXT,
                llm_provider    TEXT,
                word_count      INTEGER NOT NULL DEFAULT 0,
                transcription_ms INTEGER NOT NULL DEFAULT 0,
                processing_ms   INTEGER NOT NULL DEFAULT 0,
                injection_ms    INTEGER NOT NULL DEFAULT 0,
                total_ms        INTEGER NOT NULL DEFAULT 0,
                is_success      INTEGER NOT NULL DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS idx_history_timestamp ON history (timestamp);

            CREATE TABLE IF NOT EXISTS system_metrics (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp   INTEGER NOT NULL,
                metric_name TEXT    NOT NULL,
                value       REAL    NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Migration: add diagnostic columns (V1 parity)
        await MigrateSchemaAsync().ConfigureAwait(false);
    }

    private async Task MigrateSchemaAsync()
    {
        // SQLite ALTER TABLE ADD COLUMN is safe to call multiple times via
        // checking column existence first. We use a simple try/catch approach —
        // if the column already exists, the ALTER TABLE will throw, which we ignore.
        string[] newColumns =
        [
            "ALTER TABLE history ADD COLUMN recording_ms INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE history ADD COLUMN audio_duration_s REAL",
            "ALTER TABLE history ADD COLUMN tokens_per_sec REAL",
            "ALTER TABLE history ADD COLUMN error_message TEXT",
            "ALTER TABLE history ADD COLUMN tts_played_ms INTEGER NOT NULL DEFAULT 0",
        ];

        foreach (string ddl in newColumns)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = ddl;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // Column already exists — expected on subsequent runs
            }
        }
    }

    private async Task PruneOldRecordsAsync(CancellationToken cancellationToken = default)
    {
        int days = _settings.Current.Privacy.HistoryRetentionDays;
        long cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();

        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "DELETE FROM history WHERE timestamp < $cutoff";
        cmd.Parameters.AddWithValue("$cutoff", cutoff);
        int deleted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (deleted > 0)
        {
            Log.Information("HistoryManager: pruned {N} records older than {Days} days", deleted, days);
        }
    }

    // ── Wipe ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes all history and metrics records (one-click wipe).
    /// </summary>
    public async Task WipeAllAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM history; DELETE FROM system_metrics;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("HistoryManager: all history wiped");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection?.Dispose();
        _connection = null;
    }
}
