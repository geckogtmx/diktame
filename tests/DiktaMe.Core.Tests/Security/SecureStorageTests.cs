
using DiktaMe.Core.Security;
using Xunit;

namespace DiktaMe.Core.Tests.Security;
/// <summary>
/// Tests for <see cref="SecureStorage"/> using an isolated temp file
/// so we never touch the real %APPDATA%\DiktaMe\keys.dat.
/// </summary>
[Collection("SecureStorage")]
public sealed class SecureStorageTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testKeysFile;

    public SecureStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DiktaMeTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
        _testKeysFile = Path.Combine(_testDir, "keys.dat");
    }

    /// <summary>Creates a SecureStorage that reads/writes to our temp file, not the real one.</summary>
    private SecureStorage CreateIsolatedStorage() => new(_testKeysFile);

    // ── Core CRUD ──────────────────────────────────────────────────────────────

    [Fact]
    public void StoreAndRetrieve_RoundTrip()
    {
        var storage = CreateIsolatedStorage();
        storage.StoreKey("openai", "my-test-api-key-value");
        Assert.Equal("my-test-api-key-value", storage.RetrieveKey("openai"));
    }

    [Fact]
    public void RetrieveKey_ReturnsNull_WhenNotStored()
    {
        var storage = CreateIsolatedStorage();
        Assert.Null(storage.RetrieveKey("openai"));
    }

    [Fact]
    public void DeleteKey_RemovesKey()
    {
        var storage = CreateIsolatedStorage();
        storage.StoreKey("anthropic", "key-to-delete");
        storage.DeleteKey("anthropic");
        Assert.Null(storage.RetrieveKey("anthropic"));
    }

    [Fact]
    public void ListProviders_IncludesStoredProvider()
    {
        var storage = CreateIsolatedStorage();
        storage.StoreKey("deepgram", "some-key-value");
        var providers = storage.ListProviders();
        Assert.Contains("deepgram", providers, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoreKey_Overwrites_ExistingKey()
    {
        var storage = CreateIsolatedStorage();
        storage.StoreKey("gemini", "first-key");
        storage.StoreKey("gemini", "second-key");
        Assert.Equal("second-key", storage.RetrieveKey("gemini"));
    }

    [Fact]
    public void WipeAll_RemovesAllKeys()
    {
        var storage = CreateIsolatedStorage();
        storage.StoreKey("openai", "key1");
        storage.StoreKey("deepgram", "key2");
        storage.WipeAll();
        Assert.Empty(storage.ListProviders());
    }

    // ── Provider name validation ─────────────────────────────────────────────

    [Theory]
    [InlineData("openai")]
    [InlineData("deepgram")]
    [InlineData("gemini")]
    [InlineData("anthropic")]
    [InlineData("openrouter")]
    [InlineData("trial_token")]
    public void StoreKey_ValidProvider_Succeeds(string provider)
    {
        var storage = CreateIsolatedStorage();
        // Should not throw
        storage.StoreKey(provider, "test-key-value-12345");
        Assert.NotNull(storage.RetrieveKey(provider));
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("DEEPGRAM")]
    [InlineData("Gemini")]
    public void StoreKey_ValidProvider_CaseInsensitive(string provider)
    {
        var storage = CreateIsolatedStorage();
        // Should not throw — case-insensitive matching
        storage.StoreKey(provider, "test-key-value-12345");
        Assert.NotNull(storage.RetrieveKey(provider));
    }

    [Theory]
    [InlineData("invalid_provider")]
    [InlineData("azure")]
    [InlineData("aws")]
    public void StoreKey_InvalidProvider_ThrowsArgumentException(string provider)
    {
        var storage = CreateIsolatedStorage();
        Assert.Throws<ArgumentException>(() => storage.StoreKey(provider, "key"));
    }

    [Fact]
    public void RetrieveKey_UnknownProvider_ReturnsNull()
    {
        // RetrieveKey does NOT validate — factories call it speculatively
        var storage = CreateIsolatedStorage();
        Assert.Null(storage.RetrieveKey("unknown_provider"));
    }

    [Fact]
    public void DeleteKey_UnknownProvider_DoesNotThrow()
    {
        // DeleteKey does NOT validate — allows cleanup of any provider
        var storage = CreateIsolatedStorage();
        storage.DeleteKey("unknown_provider"); // should be no-op, no exception
    }

    // ── Isolation sanity check ──────────────────────────────────────────────

    [Fact]
    public void IsolatedStorage_DoesNotTouchDefaultFile()
    {
        // Verify we're writing to the temp file, not the default path
        var storage = CreateIsolatedStorage();
        storage.StoreKey("openai", "isolation-test-key");

        Assert.True(File.Exists(_testKeysFile), "Should write to temp file");
        // The default file should not have been modified by this test
        // (we can't assert it wasn't touched, but we can verify our file exists)
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }
}
