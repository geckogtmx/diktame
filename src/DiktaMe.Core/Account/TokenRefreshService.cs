
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Microsoft.Win32;
using Serilog;

namespace DiktaMe.Core.Account;

/// <summary>
/// Background service that refreshes Supabase JWTs before they expire.
/// Timer checks every 5 minutes; refreshes when &lt; 10 min remaining.
/// Also provides reactive refresh (call <see cref="TryRefreshAsync"/> on 401).
///
/// Concurrency: A SemaphoreSlim ensures only one refresh call reaches
/// the server at a time, preventing Supabase refresh-token-rotation
/// race conditions that revoke the entire session family.
/// </summary>
public sealed class TokenRefreshService : IDisposable
{
    private const string TokenKey = "trial_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string RefreshUrl = "https://dikta.me/api/auth/refresh";
    private const int MaxRetries = 3;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SkipWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(15);

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private DateTimeOffset _lastRefreshSuccess = DateTimeOffset.MinValue;

    /// <summary>Raised when refresh fails and the user must re-authenticate.</summary>
    public event Action? SessionExpired;

    public TokenRefreshService(SecureStorage secureStorage, SettingsManager settings, HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    private Timer? _timer;

    /// <summary>Starts the background refresh timer and subscribes to power events.</summary>
    public void Start()
    {
        _timer?.Dispose();
        _timer = new Timer(_ => _ = CheckAndRefreshAsync(), null, TimeSpan.Zero, CheckInterval);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Log.Information("TokenRefreshService: started (check every {Interval})", CheckInterval);
    }

    /// <summary>Stops the background refresh timer and unsubscribes from power events.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    /// <summary>
    /// Checks if the current JWT is near expiry and refreshes if needed.
    /// Called by the timer and can also be called manually.
    /// </summary>
    public async Task CheckAndRefreshAsync()
    {
        try
        {
            string? token = _secureStorage.RetrieveKey(TokenKey);
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            var expiry = JwtDecoder.ExtractExpiry(token);
            if (expiry is null)
            {
                return;
            }

            var remaining = expiry.Value - DateTimeOffset.UtcNow;
            if (remaining > RefreshThreshold)
            {
                Log.Debug("TokenRefreshService: JWT expires in {Remaining} — no refresh needed", remaining);
                return;
            }

            Log.Information("TokenRefreshService: JWT expires in {Remaining} — refreshing", remaining);
            bool success = await TryRefreshAsync().ConfigureAwait(false);
            if (!success)
            {
                Log.Warning("TokenRefreshService: proactive refresh failed");
                SessionExpired?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TokenRefreshService: check-and-refresh error");
        }
    }

    /// <summary>
    /// Attempts to refresh the JWT using the stored refresh token.
    /// Returns true on success. On failure, does NOT fire SessionExpired
    /// (caller decides what to do).
    ///
    /// Thread-safe: only one refresh call executes at a time. If a
    /// refresh succeeded within the last 30 seconds, returns true
    /// immediately without hitting the server.
    /// Retries up to 3 times with exponential backoff on transient failures.
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        if (!await _refreshLock.WaitAsync(LockTimeout).ConfigureAwait(false))
        {
            Log.Warning("TokenRefreshService: lock timeout — another refresh in progress");
            return false;
        }

        try
        {
            // If a refresh just succeeded, skip the redundant call
            if (DateTimeOffset.UtcNow - _lastRefreshSuccess < SkipWindow)
            {
                Log.Debug("TokenRefreshService: recent refresh still valid, skipping");
                return true;
            }

            string? refreshToken = _secureStorage.RetrieveKey(RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
            {
                Log.Warning("TokenRefreshService: no refresh token available");
                return false;
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    bool result = await ExecuteRefreshAsync(refreshToken).ConfigureAwait(false);
                    if (result)
                    {
                        _lastRefreshSuccess = DateTimeOffset.UtcNow;
                        return true;
                    }

                    // Non-retryable failure (e.g. 401 — token revoked)
                    Log.Warning("TokenRefreshService: refresh returned failure on attempt {Attempt}/{Max}",
                        attempt, MaxRetries);
                    return false;
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    int delayMs = (int)Math.Pow(2, attempt) * 1000; // 2s, 4s
                    Log.Warning(ex, "TokenRefreshService: transient error on attempt {Attempt}/{Max}, retrying in {Delay}ms",
                        attempt, MaxRetries, delayMs);
                    await Task.Delay(delayMs).ConfigureAwait(false);
                }
            }

            // Final attempt threw — already logged above
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Executes a single refresh HTTP call. Returns true on success, false on
    /// non-retryable server response (401). Throws on transient errors (network, 5xx).
    /// </summary>
    private async Task<bool> ExecuteRefreshAsync(string refreshToken)
    {
        // Build JSON manually to avoid IL2026 (JsonSerializer.Serialize with anonymous types)
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("refresh_token", refreshToken);
            writer.WriteEndObject();
        }

        string requestJson = Encoding.UTF8.GetString(ms.ToArray());
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(RefreshUrl, content).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Log.Warning("TokenRefreshService: server returned 401 — refresh token revoked");
            return false;
        }

        if ((int)response.StatusCode >= 500)
        {
            // Throw so the retry loop catches it
            throw new HttpRequestException($"Server error {(int)response.StatusCode}");
        }

        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("TokenRefreshService: refresh returned {StatusCode}", (int)response.StatusCode);
            return false;
        }

        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        string? newAccessToken = root.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
        string? newRefreshToken = root.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() : null;

        if (string.IsNullOrEmpty(newAccessToken))
        {
            Log.Warning("TokenRefreshService: empty refresh response");
            return false;
        }

        // Store new tokens atomically
        _secureStorage.StoreKey(TokenKey, newAccessToken);
        if (!string.IsNullOrEmpty(newRefreshToken))
        {
            _secureStorage.StoreKey(RefreshTokenKey, newRefreshToken);
        }

        // Update cached email/display name from new JWT
        string? email = JwtDecoder.ExtractEmail(newAccessToken);
        string? displayName = JwtDecoder.ExtractDisplayName(newAccessToken);
        await _settings.UpdateAsync(_settings.Current with
        {
            Account = _settings.Current.Account with
            {
                Email = email ?? _settings.Current.Account.Email,
                DisplayName = displayName ?? _settings.Current.Account.DisplayName,
            },
        }).ConfigureAwait(false);

        Log.Information("TokenRefreshService: refreshed successfully — new expiry = {Expiry}",
            JwtDecoder.ExtractExpiry(newAccessToken));
        return true;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Log.Information("TokenRefreshService: system resumed from sleep — triggering immediate refresh check");
            _ = CheckAndRefreshAsync();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _refreshLock.Dispose();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
