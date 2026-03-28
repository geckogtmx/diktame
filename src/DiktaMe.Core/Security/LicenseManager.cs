
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DiktaMe.Core.Security;

/// <summary>
/// Manages Power License activation and validation.
/// Free tier = Wallet (cloud only). Power License = local STT/LLM/TTS + BYOK.
/// License keys are RSA-signed payloads validated offline with an embedded public key.
/// </summary>
public sealed class LicenseManager
{
    private const string StorageKey = "license_key";

    private readonly SecureStorage _secureStorage;
    private LicensePayload? _cachedPayload;

    /// <summary>Whether a valid Power License is active.</summary>
    public bool IsLicensed { get; private set; }

    /// <summary>License tier (e.g. "power"). Null if not licensed.</summary>
    public string? LicenseTier { get; private set; }

    /// <summary>Email associated with the license. Null if not licensed.</summary>
    public string? LicenseEmail { get; private set; }

    /// <summary>Fired when license state changes (true = activated, false = deactivated).</summary>
    public event Action<bool>? LicenseStateChanged;

    public LicenseManager(SecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    /// <summary>
    /// Loads license from DPAPI secure storage on startup. Call once during app init.
    /// </summary>
    public void LoadFromStorage()
    {
        string? storedKey = _secureStorage.RetrieveKey(StorageKey);
        if (string.IsNullOrWhiteSpace(storedKey))
        {
            IsLicensed = false;
            LicenseTier = null;
            LicenseEmail = null;
            _cachedPayload = null;
            return;
        }

        if (ValidateOffline(storedKey, out var payload))
        {
            IsLicensed = true;
            LicenseTier = payload.Tier;
            LicenseEmail = payload.Email;
            _cachedPayload = payload;
            Log.Information("LicenseManager: loaded valid license (tier={Tier}, email={Email})", payload.Tier, payload.Email);
        }
        else
        {
            // Stored key is invalid (corrupted or tampered) — clear it
            IsLicensed = false;
            LicenseTier = null;
            LicenseEmail = null;
            _cachedPayload = null;
            _secureStorage.DeleteKey(StorageKey);
            Log.Warning("LicenseManager: stored license key is invalid, cleared");
        }
    }

    /// <summary>
    /// Activates a license key. Validates offline, stores in DPAPI if valid.
    /// </summary>
    /// <returns>True if the key is valid and was activated.</returns>
    public bool Activate(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return false;
        }

        string trimmed = licenseKey.Trim();

        if (!ValidateOffline(trimmed, out var payload))
        {
            Log.Warning("LicenseManager: activation failed — invalid key");
            return false;
        }

        _secureStorage.StoreKey(StorageKey, trimmed);
        IsLicensed = true;
        LicenseTier = payload.Tier;
        LicenseEmail = payload.Email;
        _cachedPayload = payload;

        Log.Information("LicenseManager: license activated (tier={Tier}, email={Email})", payload.Tier, payload.Email);
        LicenseStateChanged?.Invoke(true);
        return true;
    }

    /// <summary>
    /// Deactivates the current license and removes it from storage.
    /// </summary>
    public void Deactivate()
    {
        _secureStorage.DeleteKey(StorageKey);
        IsLicensed = false;
        LicenseTier = null;
        LicenseEmail = null;
        _cachedPayload = null;

        Log.Information("LicenseManager: license deactivated");
        LicenseStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Validates a license key offline using the embedded RSA public key.
    /// Key format: {base64_payload}.{base64_signature}
    /// </summary>
    public static bool ValidateOffline(string licenseKey, out LicensePayload payload)
    {
        payload = default;

        try
        {
            int dotIndex = licenseKey.IndexOf('.', StringComparison.Ordinal);
            if (dotIndex < 1 || dotIndex >= licenseKey.Length - 1)
            {
                return false;
            }

            string payloadB64 = licenseKey[..dotIndex];
            string signatureB64 = licenseKey[(dotIndex + 1)..];

            byte[] payloadBytes = Convert.FromBase64String(payloadB64);
            byte[] signatureBytes = Convert.FromBase64String(signatureB64);

            // Verify signature with embedded public key
            using var rsa = RSA.Create();
            rsa.ImportFromPem(EmbeddedPublicKey);

            bool valid = rsa.VerifyData(
                payloadBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!valid)
            {
                return false;
            }

            // Deserialize payload (manual parse to avoid IL2026 trim warning)
            string json = Encoding.UTF8.GetString(payloadBytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? tier = root.TryGetProperty("tier", out var t) ? t.GetString() : null;
            if (tier is null)
            {
                return false;
            }

            payload = new LicensePayload
            {
                Email = root.TryGetProperty("email", out var e) ? e.GetString() : null,
                Tier = tier,
                IssuedAt = root.TryGetProperty("issued_at", out var i) ? i.GetString() : null,
                OrderRef = root.TryGetProperty("order_ref", out var o) ? o.GetString() : null,
            };
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "LicenseManager: key validation failed");
            return false;
        }
    }

    // ── Embedded RSA Public Key ──────────────────────────────────────────────
    // The private key lives in the Supabase edge function (generate-license).
    // This public key is safe to embed — it can only VERIFY, not SIGN.
    // Replace this placeholder with the actual public key before release.
    private const string EmbeddedPublicKey = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxznC0hiqYR1ynS/bWzpd
        bUEnCyEbtKWHzPq6QzbXT3LwkaJa9jaJZETvH61aJ7jz1cl+Jlbf67LgLmEjDbK+
        +swWGkvhsKElEyeFE7QH9rU5/gA4PhcyiJvedvhkWYl5ODGArE4uwm4IovCfAJuM
        u7UrfIoeAVw9CYGTeX+Wispwzwl6Y93EaVoeQldsXBMFFvvXW1HMN7VHBp53qu5A
        8shnjzCMeA/+H5poHLreFc4x8HGjPFXdZsoXE04BHRpnQyekpYZsHPLylCi2PwqK
        p2VPiuPE6HQV8h4oTNM6zaRoauGRfDdCLQRubIbawWk5UxCJdTXdJ+9+kLkcRULq
        2wIDAQAB
        -----END PUBLIC KEY-----
        """;
}

/// <summary>
/// Payload embedded in the license key (JSON, base64-encoded, RSA-signed).
/// </summary>
public record struct LicensePayload
{
    public string? Email { get; init; }
    public string? Tier { get; init; }
    public string? IssuedAt { get; init; }
    public string? OrderRef { get; init; }
}
