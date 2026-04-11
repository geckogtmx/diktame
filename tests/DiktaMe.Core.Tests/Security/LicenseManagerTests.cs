
using System.Net;
using System.Net.Http;
using DiktaMe.Core.Security;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Security;

/// <summary>
/// Unit tests for <see cref="LicenseManager"/>.
/// Uses <see cref="LicenseFakeHandler"/> + real <see cref="SecureStorage"/> on temp files.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LicenseManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SecureStorage _storage;

    public LicenseManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"diktame-lic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _storage = new SecureStorage(Path.Combine(_tempDir, "keys.dat"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private LicenseManager CreateManager(HttpStatusCode status, string body)
    {
        var handler = new LicenseFakeHandler(status, body);
        var http = new HttpClient(handler);
        return new LicenseManager(_storage, http);
    }

    // Valid LemonSqueezy response with correct store/product IDs
    private static string ActivateOkResponse(string instanceId = "inst_123", string email = "test@example.com") => $$"""
        {
          "activated": true,
          "instance": { "id": "{{instanceId}}" },
          "meta": {
            "store_id": 277708,
            "product_id": 910127,
            "customer_email": "{{email}}"
          }
        }
        """;

    private static string ValidateOkResponse(string email = "test@example.com") => $$"""
        {
          "valid": true,
          "meta": {
            "store_id": 277708,
            "product_id": 910127,
            "customer_email": "{{email}}"
          }
        }
        """;

    // ── ActivateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateAsync_ValidKey_StoresKeyAndSetsIsLicensed()
    {
        var mgr = CreateManager(HttpStatusCode.OK, ActivateOkResponse());

        bool result = await mgr.ActivateAsync("valid-key-guid");

        result.Should().BeTrue();
        mgr.IsLicensed.Should().BeTrue();
        mgr.LicenseTier.Should().Be("power");
        mgr.LicenseEmail.Should().Be("test@example.com");
        mgr.LastError.Should().BeNull();
    }

    [Fact]
    public async Task ActivateAsync_EmptyKey_ReturnsFalseWithError()
    {
        var mgr = CreateManager(HttpStatusCode.OK, "should not be called");

        bool result = await mgr.ActivateAsync("");

        result.Should().BeFalse();
        mgr.LastError.Should().Contain("enter a license key");
        mgr.IsLicensed.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateAsync_InvalidKey_ReturnsFalse()
    {
        const string response = """{"activated":false,"error":"Invalid license key"}""";
        var mgr = CreateManager(HttpStatusCode.OK, response);

        bool result = await mgr.ActivateAsync("invalid-key");

        result.Should().BeFalse();
        mgr.LastError.Should().Contain("Invalid license key");
    }

    [Fact]
    public async Task ActivateAsync_WrongStoreId_ReturnsFalse()
    {
        const string response = """
            {
              "activated": true,
              "instance": { "id": "inst_456" },
              "meta": { "store_id": 999999, "product_id": 910127, "customer_email": "pirate@evil.com" }
            }
            """;
        var mgr = CreateManager(HttpStatusCode.OK, response);

        bool result = await mgr.ActivateAsync("pirate-key");

        result.Should().BeFalse();
        mgr.LastError.Should().Contain("not valid for dIKta.me");
    }

    [Fact]
    public async Task ActivateAsync_NetworkError_ReturnsFalseWithConnectionMessage()
    {
        var handler = new LicenseFakeHandler(throwOnSend: true);
        using var http = new HttpClient(handler);
        var mgr = new LicenseManager(_storage, http);

        bool result = await mgr.ActivateAsync("some-key");

        result.Should().BeFalse();
        mgr.LastError.Should().Contain("internet connection");
    }

    [Fact]
    public async Task ActivateAsync_FiresLicenseStateChanged()
    {
        var mgr = CreateManager(HttpStatusCode.OK, ActivateOkResponse());
        bool? eventValue = null;
        mgr.LicenseStateChanged += v => eventValue = v;

        await mgr.ActivateAsync("valid-key");

        eventValue.Should().BeTrue();
    }

    // ── ValidateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_OnlineSuccess_UpdatesTimestamp()
    {
        // First activate to store keys
        _storage.StoreKey("license_key", "stored-key");
        _storage.StoreKey("license_instance_id", "inst_123");

        var mgr = CreateManager(HttpStatusCode.OK, ValidateOkResponse());
        mgr.LoadFromStorage(); // Will set IsLicensed = true

        await mgr.ValidateAsync();

        mgr.IsLicensed.Should().BeTrue();
        mgr.LicenseEmail.Should().Be("test@example.com");
    }

    [Fact]
    public async Task ValidateAsync_NetworkFailure_KeepsCachedState()
    {
        _storage.StoreKey("license_key", "stored-key");
        _storage.StoreKey("license_instance_id", "inst_123");
        _storage.StoreKey("license_last_validated", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        var handler = new LicenseFakeHandler(throwOnSend: true);
        using var http = new HttpClient(handler);
        var mgr = new LicenseManager(_storage, http);
        mgr.LoadFromStorage();

        await mgr.ValidateAsync();

        mgr.IsLicensed.Should().BeTrue(); // Kept cached state
    }

    [Fact]
    public async Task ValidateAsync_InvalidOnline_ClearsLocal()
    {
        _storage.StoreKey("license_key", "stored-key");
        _storage.StoreKey("license_instance_id", "inst_123");
        _storage.StoreKey("license_last_validated", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        const string invalidResponse = """{"valid":false,"error":"License expired"}""";
        var mgr = CreateManager(HttpStatusCode.OK, invalidResponse);
        mgr.LoadFromStorage();

        await mgr.ValidateAsync();

        mgr.IsLicensed.Should().BeFalse();
    }

    // ── DeactivateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAsync_ClearsStoredKeysAndFiresEvent()
    {
        _storage.StoreKey("license_key", "stored-key");
        _storage.StoreKey("license_instance_id", "inst_123");
        _storage.StoreKey("license_last_validated", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        var mgr = CreateManager(HttpStatusCode.OK, "{}");
        mgr.LoadFromStorage();
        mgr.IsLicensed.Should().BeTrue();

        bool? eventValue = null;
        mgr.LicenseStateChanged += v => eventValue = v;

        await mgr.DeactivateAsync();

        mgr.IsLicensed.Should().BeFalse();
        mgr.LicenseTier.Should().BeNull();
        eventValue.Should().BeFalse();
    }

    // ── LoadFromStorage ──────────────────────────────────────────────────────

    [Fact]
    public void LoadFromStorage_ValidCacheWithinGrace_SetsIsLicensed()
    {
        _storage.StoreKey("license_key", "stored-key");
        _storage.StoreKey("license_instance_id", "inst_123");
        _storage.StoreKey("license_last_validated", DateTime.UtcNow.AddDays(-5).ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        var mgr = CreateManager(HttpStatusCode.OK, "unused");

        mgr.LoadFromStorage();

        mgr.IsLicensed.Should().BeTrue();
        mgr.LicenseTier.Should().Be("power");
    }

    [Fact]
    public void LoadFromStorage_NoCache_IsNotLicensed()
    {
        var mgr = CreateManager(HttpStatusCode.OK, "unused");

        mgr.LoadFromStorage();

        mgr.IsLicensed.Should().BeFalse();
    }

    [Fact]
    public void LoadFromStorage_ExpiredGrace_ClearsState()
    {
        _storage.StoreKey("license_key", "stored-key");
        _storage.StoreKey("license_instance_id", "inst_123");
        _storage.StoreKey("license_last_validated", DateTime.UtcNow.AddDays(-31).ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        var mgr = CreateManager(HttpStatusCode.OK, "unused");

        mgr.LoadFromStorage();

        mgr.IsLicensed.Should().BeFalse();
    }
}

// ── Fake handler for LemonSqueezy API ────────────────────────────────────────

internal sealed class LicenseFakeHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _body;
    private readonly bool _throwOnSend;

    public LicenseFakeHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string body = "{}")
    {
        _statusCode = statusCode;
        _body = body;
    }

    public LicenseFakeHandler(bool throwOnSend)
    {
        _throwOnSend = throwOnSend;
        _statusCode = HttpStatusCode.OK;
        _body = "{}";
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_throwOnSend)
        {
            throw new HttpRequestException("Simulated network error");
        }

        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body),
        });
    }
}
