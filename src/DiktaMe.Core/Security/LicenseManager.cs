
using System.Globalization;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.Security;

/// <summary>
/// Manages Power License activation and validation via the LemonSqueezy License API.
/// Free tier = Wallet (cloud only). Power License = local STT/LLM/TTS + BYOK.
/// License keys are LemonSqueezy GUIDs validated online against their public API.
/// </summary>
public sealed class LicenseManager
{
    private const string LicenseKeyStorageKey = "license_key";
    private const string InstanceIdStorageKey = "license_instance_id";
    private const string LastValidatedStorageKey = "license_last_validated";
    private const int OfflineGraceDays = 30;

    // LemonSqueezy License API (public — no API key needed)
    private const string ActivateUrl = "https://api.lemonsqueezy.com/v1/licenses/activate";
    private const string ValidateUrl = "https://api.lemonsqueezy.com/v1/licenses/validate";
    private const string DeactivateUrl = "https://api.lemonsqueezy.com/v1/licenses/deactivate";

    // Hard-coded store/product IDs for anti-piracy verification.
    // Keys from other LemonSqueezy stores/products will be rejected.
    private const int ExpectedStoreId = 277708;
    private const int ExpectedProductId = 910127;

    private readonly SecureStorage _secureStorage;
    private readonly HttpClient _httpClient;

    /// <summary>Whether a valid Power License is active.</summary>
    public bool IsLicensed { get; private set; }

    /// <summary>License tier (e.g. "power"). Null if not licensed.</summary>
    public string? LicenseTier { get; private set; }

    /// <summary>Email associated with the license. Null if not licensed.</summary>
    public string? LicenseEmail { get; private set; }

    /// <summary>Last error message from LemonSqueezy API. Null on success.</summary>
    public string? LastError { get; private set; }

    /// <summary>Fired when license state changes (true = activated, false = deactivated).</summary>
    public event Action<bool>? LicenseStateChanged;

    public LicenseManager(SecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Loads license from DPAPI secure storage on startup.
    /// If a stored key exists, trusts it (offline grace). Call <see cref="ValidateAsync"/> separately for online re-check.
    /// </summary>
    public void LoadFromStorage()
    {
        string? storedKey = _secureStorage.RetrieveKey(LicenseKeyStorageKey);
        string? instanceId = _secureStorage.RetrieveKey(InstanceIdStorageKey);

        if (string.IsNullOrWhiteSpace(storedKey) || string.IsNullOrWhiteSpace(instanceId))
        {
            IsLicensed = false;
            LicenseTier = null;
            LicenseEmail = null;
            return;
        }

        // Check offline grace period — if last validation was >30 days ago, don't trust cache
        string? lastValidated = _secureStorage.RetrieveKey(LastValidatedStorageKey);
        if (!string.IsNullOrEmpty(lastValidated)
            && DateTime.TryParse(lastValidated, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var lastDate)
            && (DateTime.UtcNow - lastDate).TotalDays > OfflineGraceDays)
        {
            Log.Warning("LicenseManager: offline grace period expired ({Days} days since last validation)", (int)(DateTime.UtcNow - lastDate).TotalDays);
            ClearLocal();
            return;
        }

        // Trust cached license on startup (within grace period).
        // ValidateAsync() will re-verify online when called.
        IsLicensed = true;
        LicenseTier = "power";
        Log.Information("LicenseManager: loaded cached license from storage (grace period OK)");
    }

    /// <summary>
    /// Activates a LemonSqueezy license key. Calls the LemonSqueezy License API,
    /// verifies store/product ownership, and stores the key + instance ID in DPAPI.
    /// </summary>
    /// <returns>True if the key was activated successfully.</returns>
    public async Task<bool> ActivateAsync(string licenseKey)
    {
        LastError = null;

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            LastError = "Please enter a license key.";
            return false;
        }

        string trimmed = licenseKey.Trim();

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["license_key"] = trimmed,
                ["instance_name"] = Environment.MachineName.Length >= 3
                    ? Environment.MachineName
                    : Environment.MachineName + "-PC",
            });

            using var response = await _httpClient.PostAsync(ActivateUrl, content).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Log.Information("LicenseManager: activate response ({StatusCode}): {Json}", (int)response.StatusCode, json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool valid = root.TryGetProperty("activated", out var activatedProp) && activatedProp.GetBoolean();

            // Also accept "valid" field (validate endpoint uses this)
            if (!valid)
            {
                valid = root.TryGetProperty("valid", out var validProp) && validProp.GetBoolean();
            }

            if (!valid)
            {
                string? error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                LastError = error ?? "License key could not be activated.";
                Log.Warning("LicenseManager: activation failed — {Error}", LastError);
                return false;
            }

            // Verify this key belongs to our store and product
            if (!VerifyOwnership(root))
            {
                LastError = "This license key is not valid for dIKta.me.";
                Log.Warning("LicenseManager: activation failed — store/product mismatch");
                return false;
            }

            // Extract instance ID
            string? instanceId = null;
            if (root.TryGetProperty("instance", out var instanceProp) && instanceProp.ValueKind == JsonValueKind.Object)
            {
                instanceId = instanceProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            }

            // Extract email from meta
            string? email = null;
            if (root.TryGetProperty("meta", out var metaProp) && metaProp.ValueKind == JsonValueKind.Object)
            {
                email = metaProp.TryGetProperty("customer_email", out var emailProp) ? emailProp.GetString() : null;
            }

            // Store in DPAPI
            _secureStorage.StoreKey(LicenseKeyStorageKey, trimmed);
            if (!string.IsNullOrEmpty(instanceId))
            {
                _secureStorage.StoreKey(InstanceIdStorageKey, instanceId);
            }

            _secureStorage.StoreKey(LastValidatedStorageKey, DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

            IsLicensed = true;
            LicenseTier = "power";
            LicenseEmail = email;

            Log.Information("LicenseManager: license activated (email={Email}, instanceId={InstanceId})", email, instanceId);
            LicenseStateChanged?.Invoke(true);
            return true;
        }
        catch (HttpRequestException ex)
        {
            LastError = "Could not reach license server. Check your internet connection.";
            Log.Warning(ex, "LicenseManager: activation failed — network error");
            return false;
        }
        catch (Exception ex)
        {
            LastError = "License activation failed. Please try again.";
            Log.Warning(ex, "LicenseManager: activation failed — unexpected error");
            return false;
        }
    }

    /// <summary>
    /// Re-validates the stored license key online.
    /// Call during startup (after LoadFromStorage). On network failure, keeps cached state.
    /// </summary>
    public async Task ValidateAsync()
    {
        string? storedKey = _secureStorage.RetrieveKey(LicenseKeyStorageKey);
        string? instanceId = _secureStorage.RetrieveKey(InstanceIdStorageKey);

        if (string.IsNullOrWhiteSpace(storedKey))
        {
            return; // No stored license — nothing to validate
        }

        try
        {
            var fields = new Dictionary<string, string> { ["license_key"] = storedKey };
            if (!string.IsNullOrEmpty(instanceId))
            {
                fields["instance_id"] = instanceId;
            }

            var content = new FormUrlEncodedContent(fields);
            using var response = await _httpClient.PostAsync(ValidateUrl, content).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool valid = root.TryGetProperty("valid", out var validProp) && validProp.GetBoolean();

            if (valid && VerifyOwnership(root))
            {
                IsLicensed = true;
                LicenseTier = "power";
                _secureStorage.StoreKey(LastValidatedStorageKey, DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

                if (root.TryGetProperty("meta", out var metaProp) && metaProp.ValueKind == JsonValueKind.Object)
                {
                    LicenseEmail = metaProp.TryGetProperty("customer_email", out var emailProp) ? emailProp.GetString() : null;
                }

                Log.Debug("LicenseManager: online validation passed");
            }
            else
            {
                // License is no longer valid — deactivate locally
                string? error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                Log.Warning("LicenseManager: online validation failed ({Error}) — deactivating", error);
                ClearLocal();
            }
        }
        catch (HttpRequestException ex)
        {
            // Network error — trust cached state (offline grace)
            Log.Debug(ex, "LicenseManager: validation skipped — offline, keeping cached state");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LicenseManager: validation error — keeping cached state");
        }
    }

    /// <summary>
    /// Deactivates the current license locally and releases the instance on LemonSqueezy.
    /// </summary>
    public async Task DeactivateAsync()
    {
        string? storedKey = _secureStorage.RetrieveKey(LicenseKeyStorageKey);
        string? instanceId = _secureStorage.RetrieveKey(InstanceIdStorageKey);

        // Try to release the instance on LemonSqueezy (best-effort)
        if (!string.IsNullOrEmpty(storedKey) && !string.IsNullOrEmpty(instanceId))
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["license_key"] = storedKey,
                    ["instance_id"] = instanceId,
                });
                await _httpClient.PostAsync(DeactivateUrl, content).ConfigureAwait(false);
                Log.Information("LicenseManager: instance deactivated on LemonSqueezy");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "LicenseManager: failed to deactivate on LemonSqueezy (best-effort)");
            }
        }

        ClearLocal();
        Log.Information("LicenseManager: license deactivated");
        LicenseStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Synchronous deactivate for backward compat (fire-and-forget the API call).
    /// </summary>
    public void Deactivate()
    {
        _ = DeactivateAsync();
    }

    /// <summary>
    /// Verifies the license key belongs to our store and product.
    /// </summary>
    private static bool VerifyOwnership(JsonElement root)
    {
        // store_id and product_id are in the "meta" object
        if (!root.TryGetProperty("meta", out var metaProp) || metaProp.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int storeId = metaProp.TryGetProperty("store_id", out var storeProp) ? storeProp.GetInt32() : 0;
        int productId = metaProp.TryGetProperty("product_id", out var productProp) ? productProp.GetInt32() : 0;

        return storeId == ExpectedStoreId && productId == ExpectedProductId;
    }

    private void ClearLocal()
    {
        _secureStorage.DeleteKey(LicenseKeyStorageKey);
        _secureStorage.DeleteKey(InstanceIdStorageKey);
        IsLicensed = false;
        LicenseTier = null;
        LicenseEmail = null;
    }
}
