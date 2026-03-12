
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace DiktaMe.Core.Security;
/// <summary>
/// Encrypted storage for provider API keys using Windows DPAPI
/// (<see cref="ProtectedData"/> with <see cref="DataProtectionScope.CurrentUser"/>).
/// Keys are stored as an encrypted JSON blob at <c>%APPDATA%\DiktaMe\keys.dat</c>.
/// The file is only decryptable by the same Windows user on the same machine.
/// </summary>
public sealed class SecureStorage
{
    /// <summary>
    /// Default path to the encrypted keys file.
    /// </summary>
    public static readonly string DefaultKeysFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiktaMe", "keys.dat");

    private readonly string _keysFilePath;

    /// <summary>
    /// Creates a SecureStorage using the default keys file at <c>%APPDATA%\DiktaMe\keys.dat</c>.
    /// </summary>
    public SecureStorage() : this(DefaultKeysFilePath) { }

    /// <summary>
    /// Creates a SecureStorage using a custom keys file path (for testing).
    /// </summary>
    internal SecureStorage(string keysFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keysFilePath);
        _keysFilePath = keysFilePath;
    }

    /// <summary>
    /// Allowed provider names for key storage. Case-insensitive.
    /// </summary>
    internal static readonly HashSet<string> ValidProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        // API key providers (stored via Settings > API Keys or Wizard)
        "openai", "deepgram", "gemini", "anthropic", "openrouter",
        // TTS cloud providers (Phase E)
        "inworld",
        // Internal (trial auth JWT)
        "trial_token",
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores an API key for the given provider name, overwriting any existing value.
    /// </summary>
    /// <param name="providerName">Logical provider name (e.g. "openai", "deepgram").</param>
    /// <param name="apiKey">The plaintext API key to encrypt and store.</param>
    public void StoreKey(string providerName, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ValidateProviderName(providerName);

        var keys = LoadDecrypted();
        keys[providerName] = apiKey;
        SaveEncrypted(keys);

        Log.Information("SecureStorage: stored key for provider '{Provider}'", providerName);
    }

    /// <summary>
    /// Retrieves the stored API key for the given provider, or null if not found.
    /// No allowlist validation — factories call this speculatively for any provider type
    /// and handle null returns gracefully.
    /// </summary>
    public string? RetrieveKey(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var keys = LoadDecrypted();
        keys.TryGetValue(providerName, out string? value);

        Log.Debug("SecureStorage: retrieved key for provider '{Provider}' (found={Found})",
            providerName, value is not null);

        return value;
    }

    /// <summary>
    /// Deletes the stored key for the given provider.
    /// No-op if the provider has no stored key.
    /// </summary>
    public void DeleteKey(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var keys = LoadDecrypted();
        if (keys.Remove(providerName))
        {
            SaveEncrypted(keys);
            Log.Information("SecureStorage: deleted key for provider '{Provider}'", providerName);
        }
    }

    /// <summary>
    /// Returns the names of all providers that have a stored key.
    /// </summary>
    public IReadOnlyList<string> ListProviders()
    {
        var keys = LoadDecrypted();
        return keys.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Removes all stored keys and deletes the keys file.
    /// </summary>
    public void WipeAll()
    {
        if (File.Exists(_keysFilePath))
        {
            File.Delete(_keysFilePath);
            Log.Information("SecureStorage: wiped all keys");
        }
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    private static void ValidateProviderName(string providerName)
    {
        if (!ValidProviders.Contains(providerName))
        {
            throw new ArgumentException(
                $"Unknown provider '{providerName}'. Valid providers: {string.Join(", ", ValidProviders)}",
                nameof(providerName));
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Dictionary<string, string> LoadDecrypted()
    {
        if (!File.Exists(_keysFilePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        byte[] encryptedBlob;
        try
        {
            encryptedBlob = File.ReadAllBytes(_keysFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SecureStorage: failed to read keys file — returning empty");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        byte[] plainBytes;
        try
        {
            plainBytes = ProtectedData.Unprotect(encryptedBlob, null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException ex)
        {
            Log.Warning(ex, "SecureStorage: decryption failed — keys may be from a different user");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            string json = Encoding.UTF8.GetString(plainBytes);
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                SecureStorageJsonContext.Default.DictionaryStringString)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Normalise key casing
            return new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "SecureStorage: failed to deserialize keys JSON");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            // Zero the plaintext bytes after parsing
            Array.Clear(plainBytes, 0, plainBytes.Length);
        }
    }

    private void SaveEncrypted(Dictionary<string, string> keys)
    {
        string json = JsonSerializer.Serialize(keys, SecureStorageJsonContext.Default.DictionaryStringString);
        byte[] plainBytes = Encoding.UTF8.GetBytes(json);

        byte[] encryptedBlob;
        try
        {
            encryptedBlob = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        }
        finally
        {
            // Zero the plaintext bytes immediately after encryption
            Array.Clear(plainBytes, 0, plainBytes.Length);
        }

        string? dir = Path.GetDirectoryName(_keysFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Atomic write: write to .tmp then rename
        string tmpPath = _keysFilePath + ".tmp";
        File.WriteAllBytes(tmpPath, encryptedBlob);
        File.Move(tmpPath, _keysFilePath, overwrite: true);
    }
}

/// <summary>
/// Source-generated JSON serialization context for SecureStorage.
/// </summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class SecureStorageJsonContext : JsonSerializerContext { }
