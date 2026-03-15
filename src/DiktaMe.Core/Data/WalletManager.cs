
using Microsoft.Data.Sqlite;
using Serilog;

namespace DiktaMe.Core.Data;

/// <summary>
/// Append-only local wallet ledger persisted to <c>%APPDATA%\DiktaMe\wallet.db</c>.
/// This is a UI cache only — the cloud ledger is the sole spending authority.
/// No UPDATE or DELETE methods exist by design (append-only audit trail).
/// </summary>
public sealed class WalletManager : IDisposable
{
    /// <summary>Default path to the wallet SQLite database file.</summary>
    public static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "wallet.db");

    private readonly string _dbPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SqliteConnection? _connection;
    private bool _disposed;

    public WalletManager(string? dbPath = null)
    {
        _dbPath = dbPath ?? DbPath;
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the database and creates the schema if needed. Call once at startup.
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

        Log.Information("WalletManager: database ready at '{Path}'", _dbPath);
    }

    // ── Balance ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current balance in microdollars from the most recent ledger row.
    /// O(1) — reads <c>balance_after_micro</c> from the latest row by <c>rowid</c>.
    /// Returns 0 if the ledger is empty.
    /// </summary>
    public async Task<long> GetBalanceMicroAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return 0;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT balance_after_micro FROM wallet_ledger ORDER BY rowid DESC LIMIT 1";
            object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is long l ? l : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the full balance in microdollars by summing all non-expired amounts.
    /// More accurate than <see cref="GetBalanceMicroAsync"/> but O(n).
    /// Excludes transactions with an <c>expires_at</c> in the past.
    /// </summary>
    public async Task<long> GetFullBalanceMicroAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return 0;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT COALESCE(SUM(amount_micro), 0)
                FROM wallet_ledger
                WHERE expires_at IS NULL OR expires_at > $now
                """;
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is long l ? l : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Insert ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends a transaction to the local ledger. Computes <c>balance_after_micro</c>
    /// from the previous balance + <paramref name="amountMicro"/>.
    /// </summary>
    /// <param name="amountMicro">Amount in microdollars (positive = credit, negative = debit).</param>
    /// <param name="type">One of <see cref="WalletTransactionType"/> constants.</param>
    /// <param name="metadata">Optional JSON metadata (e.g., provider, model, request ID).</param>
    /// <param name="expiresAt">Optional expiration date for grants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated transaction ID.</returns>
    public async Task<string> InsertTransactionAsync(
        long amountMicro,
        string type,
        string metadata = "{}",
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("WalletManager is not initialized. Call InitAsync() first.");
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Read current balance from the latest row
            long currentBalance;
            using (var balCmd = _connection.CreateCommand())
            {
                balCmd.CommandText = "SELECT balance_after_micro FROM wallet_ledger ORDER BY rowid DESC LIMIT 1";
                object? result = await balCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                currentBalance = result is long l ? l : 0;
            }

            long balanceAfter = currentBalance + amountMicro;
            string id = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
            string createdAt = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO wallet_ledger (id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata)
                VALUES ($id, $amount, $balance, $type, $created, $expires, $metadata)
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$amount", amountMicro);
            cmd.Parameters.AddWithValue("$balance", balanceAfter);
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$created", createdAt);
            cmd.Parameters.AddWithValue("$expires", expiresAt.HasValue
                ? expiresAt.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$metadata", metadata);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            Log.Debug("WalletManager: inserted {Type} txn {Id}, amount={Amount}µ$, balance={Balance}µ$",
                type, id, amountMicro, balanceAfter);

            return id;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Query ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent <paramref name="limit"/> transactions in descending order.
    /// </summary>
    public async Task<List<WalletTransaction>> GetTransactionsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var transactions = new List<WalletTransaction>();

        if (_connection is null)
        {
            return transactions;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata
                FROM wallet_ledger
                ORDER BY rowid DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string expiresStr = reader.IsDBNull(5) ? "" : reader.GetString(5);
                DateTimeOffset? expires = string.IsNullOrEmpty(expiresStr) ? null
                    : DateTimeOffset.Parse(expiresStr, System.Globalization.CultureInfo.InvariantCulture);

                transactions.Add(new WalletTransaction
                {
                    Id = reader.GetString(0),
                    AmountMicro = reader.GetInt64(1),
                    BalanceAfterMicro = reader.GetInt64(2),
                    Type = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
                    ExpiresAt = expires,
                    Metadata = reader.IsDBNull(6) ? "{}" : reader.GetString(6),
                });
            }

            return transactions;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the count of transactions in the ledger.
    /// </summary>
    public async Task<int> GetTransactionCountAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return 0;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM wallet_ledger";
            object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is long l ? (int)l : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Sync ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reconciles the local ledger with the server's authoritative balance.
    /// Inserts a SYNC adjustment row if the local balance doesn't match.
    /// </summary>
    /// <param name="serverBalanceMicro">The server's authoritative balance in microdollars.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a sync adjustment was needed, false if already in sync.</returns>
    public async Task<bool> SyncBalanceAsync(long serverBalanceMicro, CancellationToken cancellationToken = default)
    {
        long localBalance = await GetBalanceMicroAsync(cancellationToken).ConfigureAwait(false);

        if (localBalance == serverBalanceMicro)
        {
            return false;
        }

        long adjustment = serverBalanceMicro - localBalance;
        await InsertTransactionAsync(
            adjustment,
            WalletTransactionType.Sync,
            $"{{\"local_before\":{localBalance},\"server\":{serverBalanceMicro}}}",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("WalletManager: synced balance from {Local}µ$ to {Server}µ$ (delta={Delta}µ$)",
            localBalance, serverBalanceMicro, adjustment);

        return true;
    }

    // ── Schema ───────────────────────────────────────────────────────────────

    private async Task CreateSchemaAsync()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS wallet_ledger (
                id                  TEXT    PRIMARY KEY,
                amount_micro        INTEGER NOT NULL,
                balance_after_micro INTEGER NOT NULL,
                type                TEXT    NOT NULL,
                created_at          TEXT    NOT NULL,
                expires_at          TEXT,
                metadata            TEXT    NOT NULL DEFAULT '{}'
            );

            CREATE INDEX IF NOT EXISTS idx_wallet_ledger_type ON wallet_ledger (type);
            """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

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
