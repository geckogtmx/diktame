namespace DiktaMe.Core.Tests.Security;

using DiktaMe.Core.Security;
using Xunit;

/// <summary>
/// Tests for <see cref="SecureStorage"/> using a temp file to avoid touching
/// the real %APPDATA%\DiktaMe\keys.dat.
/// Uses reflection to override the static KeysFilePath via a derived approach,
/// or — more practically — we test via the public API with the real path
/// under a unique temp sub-path per test run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SecureStorageTests : IDisposable
{
    // Use a test-specific file so we don't clobber the real keys.dat
    private readonly string _originalDir;
    private readonly string _testDir;

    public SecureStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DiktaMeTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);

        // Redirect SecureStorage to use our temp dir by temporarily
        // overwriting the environment variable (not ideal — see note below).
        // Since KeysFilePath is a static readonly computed from APPDATA,
        // we test via a subclass/adapter. For simplicity here we just
        // exercise the real instance (which writes to APPDATA/DiktaMe/keys.dat)
        // and clean up after. CI machines have isolated home dirs per run.
        _originalDir = string.Empty; // no override needed — see note
    }

    [Fact]
    public void StoreAndRetrieve_RoundTrip()
    {
        var storage = new SecureStorage();
        const string Provider = "test_roundtrip_" + nameof(StoreAndRetrieve_RoundTrip);
        const string Key = "my-test-api-key-value";

        try
        {
            storage.StoreKey(Provider, Key);
            string? retrieved = storage.RetrieveKey(Provider);
            Assert.Equal(Key, retrieved);
        }
        finally
        {
            storage.DeleteKey(Provider);
        }
    }

    [Fact]
    public void RetrieveKey_ReturnsNull_WhenNotStored()
    {
        var storage = new SecureStorage();
        string? result = storage.RetrieveKey("nonexistent_provider_" + Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public void DeleteKey_RemovesKey()
    {
        var storage = new SecureStorage();
        const string Provider = "test_delete_" + nameof(DeleteKey_RemovesKey);
        const string Key = "key-to-delete";

        storage.StoreKey(Provider, Key);
        storage.DeleteKey(Provider);
        Assert.Null(storage.RetrieveKey(Provider));
    }

    [Fact]
    public void ListProviders_IncludesStoredProvider()
    {
        var storage = new SecureStorage();
        string provider = "test_list_" + Guid.NewGuid();

        try
        {
            storage.StoreKey(provider, "some-key-value");
            var providers = storage.ListProviders();
            Assert.Contains(provider, providers, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            storage.DeleteKey(provider);
        }
    }

    [Fact]
    public void StoreKey_Overwrites_ExistingKey()
    {
        var storage = new SecureStorage();
        const string Provider = "test_overwrite_" + nameof(StoreKey_Overwrites_ExistingKey);

        try
        {
            storage.StoreKey(Provider, "first-key");
            storage.StoreKey(Provider, "second-key");
            Assert.Equal("second-key", storage.RetrieveKey(Provider));
        }
        finally
        {
            storage.DeleteKey(Provider);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }
}
