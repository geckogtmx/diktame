
using System.Globalization;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Microsoft.Data.Sqlite;
using Serilog;

namespace DiktaMe.Core.Data;
/// <summary>
/// Persists chat conversations and messages to SQLite at
/// <c>%APPDATA%\DiktaMe\history.db</c> (shared with <see cref="HistoryManager"/>).
/// Respects the user's privacy level — Ghost stores nothing, Stats stores metadata only,
/// Balanced scrubs PII, Full stores verbatim.
/// </summary>
public sealed class ConversationManager : IDisposable
{
    private readonly SettingsManager _settings;
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public ConversationManager(SettingsManager settings, string? dbPath = null)
    {
        _settings = settings;
        _dbPath = dbPath ?? HistoryManager.DbPath;
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the database and creates conversation tables if needed.
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

        Log.Information("ConversationManager: database ready at '{Path}'", _dbPath);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new conversation. Returns the record with a generated ID.
    /// At Ghost privacy level, the conversation is not persisted to SQLite.
    /// </summary>
    public async Task<ConversationRecord> CreateAsync(
        string systemPrompt,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        var record = new ConversationRecord(
            Id: id,
            Title: null,
            SystemPrompt: systemPrompt,
            ModelId: modelId,
            CreatedAt: now,
            UpdatedAt: now,
            MessageCount: 0);

        if (IsGhostMode() || _connection is null)
        {
            return record;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO conversations (id, title, system_prompt, model_id, created_at, updated_at, message_count)
                VALUES ($id, $title, $prompt, $model, $created, $updated, 0)
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$title", (object?)null ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$prompt", systemPrompt);
            cmd.Parameters.AddWithValue("$model", modelId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$created", now.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$updated", now.ToUnixTimeSeconds());

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        return record;
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a conversation by ID, or null if not found.
    /// </summary>
    public async Task<ConversationRecord?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, title, system_prompt, model_id, created_at, updated_at, message_count
                FROM conversations WHERE id = $id
                """;
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ReadConversation(reader);
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the most recent conversations, ordered by last update.
    /// </summary>
    public async Task<List<ConversationRecord>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (IsGhostMode() || _connection is null)
        {
            return [];
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, title, system_prompt, model_id, created_at, updated_at, message_count
                FROM conversations
                ORDER BY updated_at DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$limit", limit);

            var results = new List<ConversationRecord>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(ReadConversation(reader));
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns all messages for a conversation, ordered chronologically.
    /// At Stats level, message content is empty (only metadata is stored).
    /// </summary>
    public async Task<List<ConversationMessageRecord>> GetMessagesAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return [];
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, conversation_id, role, content, tokens, created_at
                FROM conversation_messages
                WHERE conversation_id = $convId
                ORDER BY id ASC
                """;
            cmd.Parameters.AddWithValue("$convId", conversationId);

            var results = new List<ConversationMessageRecord>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new ConversationMessageRecord(
                    Id: reader.GetInt64(0),
                    ConversationId: reader.GetString(1),
                    Role: reader.GetString(2),
                    Content: reader.GetString(3),
                    Tokens: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5))));
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a message to a conversation. Respects privacy level for content storage.
    /// Also updates the conversation's <c>updated_at</c> and <c>message_count</c>.
    /// </summary>
    public async Task AddMessageAsync(
        string conversationId,
        string role,
        string content,
        int? tokens = null,
        CancellationToken cancellationToken = default)
    {
        if (IsGhostMode() || _connection is null)
        {
            return;
        }

        string storedContent = ApplyPrivacy(content);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var insertCmd = _connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO conversation_messages (conversation_id, role, content, tokens, created_at)
                VALUES ($convId, $role, $content, $tokens, $created)
                """;
            insertCmd.Parameters.AddWithValue("$convId", conversationId);
            insertCmd.Parameters.AddWithValue("$role", role);
            insertCmd.Parameters.AddWithValue("$content", storedContent);
            insertCmd.Parameters.AddWithValue("$tokens", tokens.HasValue ? tokens.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("$created", now);

            await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Update conversation metadata
            using var updateCmd = _connection.CreateCommand();
            updateCmd.CommandText = """
                UPDATE conversations
                SET updated_at = $updated, message_count = message_count + 1
                WHERE id = $id
                """;
            updateCmd.Parameters.AddWithValue("$updated", now);
            updateCmd.Parameters.AddWithValue("$id", conversationId);

            await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Updates the title of a conversation (e.g. after auto-titling).
    /// </summary>
    public async Task UpdateTitleAsync(
        string conversationId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE conversations SET title = $title WHERE id = $id";
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$id", conversationId);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Updates the model ID for a conversation (when user switches model mid-conversation).
    /// </summary>
    public async Task UpdateModelAsync(
        string conversationId,
        string modelId,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE conversations SET model_id = $model WHERE id = $id";
            cmd.Parameters.AddWithValue("$model", modelId);
            cmd.Parameters.AddWithValue("$id", conversationId);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes a conversation and all its messages (CASCADE).
    /// </summary>
    public async Task DeleteAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Delete messages first (in case FK CASCADE is not enabled)
            using var msgCmd = _connection.CreateCommand();
            msgCmd.CommandText = "DELETE FROM conversation_messages WHERE conversation_id = $id";
            msgCmd.Parameters.AddWithValue("$id", conversationId);
            await msgCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            using var convCmd = _connection.CreateCommand();
            convCmd.CommandText = "DELETE FROM conversations WHERE id = $id";
            convCmd.Parameters.AddWithValue("$id", conversationId);
            await convCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes all conversations and messages.
    /// </summary>
    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM conversation_messages; DELETE FROM conversations;";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("ConversationManager: all conversations deleted");
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsGhostMode() => _settings.Current.Privacy.Level == PrivacyLevel.Ghost;

    private string ApplyPrivacy(string content)
    {
        var level = _settings.Current.Privacy.Level;

        if (level == PrivacyLevel.Stats)
        {
            return string.Empty; // Metadata only — no content stored
        }

        if (level == PrivacyLevel.Balanced && _settings.Current.Privacy.PiiScrubEnabled)
        {
            return PIIScrubber.Scrub(content);
        }

        return content; // Full or Balanced without PII scrubbing
    }

    private static ConversationRecord ReadConversation(SqliteDataReader reader)
    {
        return new ConversationRecord(
            Id: reader.GetString(0),
            Title: reader.IsDBNull(1) ? null : reader.GetString(1),
            SystemPrompt: reader.GetString(2),
            ModelId: reader.IsDBNull(3) ? null : reader.GetString(3),
            CreatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)),
            UpdatedAt: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
            MessageCount: reader.GetInt32(6));
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private async Task CreateSchemaAsync()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS conversations (
                id              TEXT PRIMARY KEY,
                title           TEXT,
                system_prompt   TEXT NOT NULL,
                model_id        TEXT,
                created_at      INTEGER NOT NULL,
                updated_at      INTEGER NOT NULL,
                message_count   INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS conversation_messages (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                conversation_id TEXT NOT NULL,
                role            TEXT NOT NULL,
                content         TEXT NOT NULL,
                tokens          INTEGER,
                created_at      INTEGER NOT NULL,
                FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_conv_msg_conv ON conversation_messages(conversation_id);
            CREATE INDEX IF NOT EXISTS idx_conv_updated ON conversations(updated_at DESC);
            """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();
        _connection?.Dispose();
        _connection = null;
    }
}
