
namespace DiktaMe.Core.Data;

/// <summary>
/// Transaction types for the wallet ledger.
/// </summary>
public static class WalletTransactionType
{
    public const string Grant = "GRANT";
    public const string Purchase = "PURCHASE";
    public const string Usage = "USAGE";
    public const string Expiry = "EXPIRY";
    public const string Refund = "REFUND";
    public const string Sync = "SYNC";
}

/// <summary>
/// Display-friendly representation of a wallet ledger row.
/// Amounts are in dollars (converted from microdollars for UI display).
/// </summary>
public sealed record WalletTransaction
{
    public required string Id { get; init; }
    public required long AmountMicro { get; init; }
    public required long BalanceAfterMicro { get; init; }
    public required string Type { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Metadata { get; init; } = "{}";

    /// <summary>Dollar amount for display (positive = credit, negative = debit).</summary>
    public decimal AmountDollars => AmountMicro / 1_000_000m;

    /// <summary>Balance after this transaction for display.</summary>
    public decimal BalanceAfterDollars => BalanceAfterMicro / 1_000_000m;
}

/// <summary>
/// Aggregated daily usage summary or individual non-usage event for Settings display.
/// </summary>
public sealed record WalletDailySummary
{
    public required DateTimeOffset Date { get; init; }
    public required string DateLabel { get; init; }
    public required string Type { get; init; }
    public required long AmountMicro { get; init; }
    public required long BalanceAfterMicro { get; init; }

    /// <summary>Number of sync/usage rows aggregated (0 for individual events).</summary>
    public int RequestCount { get; init; }

    /// <summary>True if this is a daily aggregate, false for individual GRANT/PURCHASE rows.</summary>
    public bool IsAggregate { get; init; }
}
