
using DiktaMe.Core.Data;
using FluentAssertions;
using Xunit;

namespace DiktaMe.Core.Tests.Data;

[Trait("Category", "Integration")]
public sealed class WalletManagerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly WalletManager _wallet;

    public WalletManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"diktame_wallet_test_{Guid.NewGuid()}.db");
        _wallet = new WalletManager(_dbPath);
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitAsync_CreatesDatabase()
    {
        await _wallet.InitAsync();
        File.Exists(_dbPath).Should().BeTrue();
    }

    [Fact]
    public async Task InitAsync_IsIdempotent()
    {
        await _wallet.InitAsync();
        var act = () => _wallet.InitAsync();
        await act.Should().NotThrowAsync();
    }

    // ── Balance ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceMicroAsync_EmptyLedger_ReturnsZero()
    {
        await _wallet.InitAsync();

        long balance = await _wallet.GetBalanceMicroAsync();

        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetBalanceMicroAsync_AfterGrant_ReturnsGrantAmount()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);

        long balance = await _wallet.GetBalanceMicroAsync();
        balance.Should().Be(1_000_000);
    }

    [Fact]
    public async Task GetBalanceMicroAsync_AfterGrantAndUsage_ReturnsCorrectBalance()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);
        await _wallet.InsertTransactionAsync(-50_000, WalletTransactionType.Usage);

        long balance = await _wallet.GetBalanceMicroAsync();
        balance.Should().Be(950_000);
    }

    [Fact]
    public async Task GetFullBalanceMicroAsync_ExcludesExpiredGrants()
    {
        await _wallet.InitAsync();

        // Active grant
        await _wallet.InsertTransactionAsync(
            1_000_000,
            WalletTransactionType.Grant,
            expiresAt: DateTimeOffset.UtcNow.AddDays(90));

        // Expired grant
        await _wallet.InsertTransactionAsync(
            500_000,
            WalletTransactionType.Grant,
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        long full = await _wallet.GetFullBalanceMicroAsync();
        full.Should().Be(1_000_000); // Only the active grant
    }

    [Fact]
    public async Task GetFullBalanceMicroAsync_IncludesTransactionsWithNoExpiry()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(2_000_000, WalletTransactionType.Purchase);

        long full = await _wallet.GetFullBalanceMicroAsync();
        full.Should().Be(2_000_000);
    }

    // ── Insert ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertTransactionAsync_ReturnsUniqueId()
    {
        await _wallet.InitAsync();

        string id1 = await _wallet.InsertTransactionAsync(100_000, WalletTransactionType.Grant);
        string id2 = await _wallet.InsertTransactionAsync(200_000, WalletTransactionType.Grant);

        id1.Should().NotBeNullOrEmpty();
        id2.Should().NotBeNullOrEmpty();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public async Task InsertTransactionAsync_ComputesBalanceAfterCorrectly()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);
        await _wallet.InsertTransactionAsync(-100_000, WalletTransactionType.Usage);
        await _wallet.InsertTransactionAsync(500_000, WalletTransactionType.Purchase);

        var txns = await _wallet.GetTransactionsAsync(10);
        // Transactions returned in descending order (newest first)
        txns[0].BalanceAfterMicro.Should().Be(1_400_000); // 1M - 100K + 500K
        txns[1].BalanceAfterMicro.Should().Be(900_000);   // 1M - 100K
        txns[2].BalanceAfterMicro.Should().Be(1_000_000); // 1M
    }

    [Fact]
    public async Task InsertTransactionAsync_ThrowsWhenNotInitialized()
    {
        // Don't call InitAsync
        var act = () => _wallet.InsertTransactionAsync(100_000, WalletTransactionType.Grant);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InsertTransactionAsync_StoresMetadata()
    {
        await _wallet.InitAsync();

        string meta = """{"provider":"gemini-2.0-flash","model":"gemini-2.0-flash"}""";
        await _wallet.InsertTransactionAsync(-50_000, WalletTransactionType.Usage, metadata: meta);

        var txns = await _wallet.GetTransactionsAsync(1);
        txns.Should().ContainSingle();
        txns[0].Metadata.Should().Be(meta);
    }

    [Fact]
    public async Task InsertTransactionAsync_StoresExpiresAt()
    {
        await _wallet.InitAsync();

        var expires = DateTimeOffset.UtcNow.AddDays(90);
        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant, expiresAt: expires);

        var txns = await _wallet.GetTransactionsAsync(1);
        txns.Should().ContainSingle();
        txns[0].ExpiresAt.Should().NotBeNull();
        txns[0].ExpiresAt!.Value.Should().BeCloseTo(expires, TimeSpan.FromSeconds(1));
    }

    // ── Query ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionsAsync_ReturnsInDescendingOrder()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(100_000, WalletTransactionType.Grant);
        await _wallet.InsertTransactionAsync(200_000, WalletTransactionType.Purchase);
        await _wallet.InsertTransactionAsync(-50_000, WalletTransactionType.Usage);

        var txns = await _wallet.GetTransactionsAsync(10);
        txns.Should().HaveCount(3);
        txns[0].Type.Should().Be(WalletTransactionType.Usage);
        txns[1].Type.Should().Be(WalletTransactionType.Purchase);
        txns[2].Type.Should().Be(WalletTransactionType.Grant);
    }

    [Fact]
    public async Task GetTransactionsAsync_RespectsLimit()
    {
        await _wallet.InitAsync();

        for (int i = 0; i < 10; i++)
        {
            await _wallet.InsertTransactionAsync(100_000, WalletTransactionType.Usage);
        }

        var txns = await _wallet.GetTransactionsAsync(3);
        txns.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTransactionsAsync_EmptyLedger_ReturnsEmptyList()
    {
        await _wallet.InitAsync();

        var txns = await _wallet.GetTransactionsAsync(10);
        txns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionCountAsync_ReturnsCorrectCount()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(100_000, WalletTransactionType.Grant);
        await _wallet.InsertTransactionAsync(-50_000, WalletTransactionType.Usage);

        int count = await _wallet.GetTransactionCountAsync();
        count.Should().Be(2);
    }

    // ── Sync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncBalanceAsync_WhenAlreadyInSync_ReturnsFalse()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);

        bool needed = await _wallet.SyncBalanceAsync(1_000_000);
        needed.Should().BeFalse();
    }

    [Fact]
    public async Task SyncBalanceAsync_WhenOutOfSync_InsertsAdjustment()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);

        bool needed = await _wallet.SyncBalanceAsync(800_000);
        needed.Should().BeTrue();

        long balance = await _wallet.GetBalanceMicroAsync();
        balance.Should().Be(800_000);

        var txns = await _wallet.GetTransactionsAsync(10);
        txns[0].Type.Should().Be(WalletTransactionType.Sync);
        txns[0].AmountMicro.Should().Be(-200_000);
    }

    [Fact]
    public async Task SyncBalanceAsync_EmptyLedger_InsertsGrant()
    {
        await _wallet.InitAsync();

        bool needed = await _wallet.SyncBalanceAsync(1_000_000);
        needed.Should().BeTrue();

        long balance = await _wallet.GetBalanceMicroAsync();
        balance.Should().Be(1_000_000);
    }

    // ── Display ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task WalletTransaction_AmountDollars_ConvertsCorrectly()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_500_000, WalletTransactionType.Grant);

        var txns = await _wallet.GetTransactionsAsync(1);
        txns[0].AmountDollars.Should().Be(1.50m);
        txns[0].BalanceAfterDollars.Should().Be(1.50m);
    }

    [Fact]
    public async Task WalletTransaction_NegativeAmountDollars_ConvertsCorrectly()
    {
        await _wallet.InitAsync();

        await _wallet.InsertTransactionAsync(1_000_000, WalletTransactionType.Grant);
        await _wallet.InsertTransactionAsync(-123_456, WalletTransactionType.Usage);

        var txns = await _wallet.GetTransactionsAsync(1);
        txns[0].AmountDollars.Should().Be(-0.123456m);
        txns[0].BalanceAfterDollars.Should().Be(0.876544m);
    }

    // ── Not Initialized Guard ────────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceMicroAsync_WhenNotInitialized_ReturnsZero()
    {
        long balance = await _wallet.GetBalanceMicroAsync();
        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetTransactionsAsync_WhenNotInitialized_ReturnsEmpty()
    {
        var txns = await _wallet.GetTransactionsAsync();
        txns.Should().BeEmpty();
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _wallet.Dispose();
        await Task.Delay(50); // Let SQLite release file lock
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* Best effort cleanup */ }
        }
    }
}
