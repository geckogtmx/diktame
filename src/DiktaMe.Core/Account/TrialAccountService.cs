
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.Core.Account;
/// <summary>
/// Manages the trial account lifecycle: browser-based OAuth login,
/// JWT token storage (via <see cref="SecureStorage"/>), server-side
/// status sync, usage reporting, and logout.
/// </summary>
public sealed class TrialAccountService : ITrialAccountService, IDisposable
{
    private const string TokenKey = "trial_token";
    private const string LoginUrl = "https://dikta.me/login?mode=app";
    private const string StatusUrl = "https://www.dikta.me/api/trial/status";
    private const string UsageUrl = "https://www.dikta.me/api/trial/usage";

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public TrialAccountService(
        SecureStorage secureStorage,
        SettingsManager settings,
        HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <inheritdoc />
    public bool HasValidToken => _secureStorage.RetrieveKey(TokenKey) is not null;

    /// <inheritdoc />
    public string? Email => string.IsNullOrEmpty(_settings.Current.Trial.TrialEmail)
        ? null
        : _settings.Current.Trial.TrialEmail;

    /// <inheritdoc />
    public event Action<TrialStatus?>? StatusChanged;

    // ── Login ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Login()
    {
        Log.Information("TrialAccountService: opening browser for login");
        Process.Start(new ProcessStartInfo(LoginUrl) { UseShellExecute = true });
    }

    // ── Auth Callback ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task HandleAuthCallbackAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        // Store JWT in encrypted storage
        _secureStorage.StoreKey(TokenKey, token);

        // Extract email from JWT payload
        string? email = JwtDecoder.ExtractEmail(token);
        Log.Information("TrialAccountService: auth callback — email={Email}", email ?? "(none)");

        // Update AppSettings with AuthMode and email
        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.Trial,
            Trial = _settings.Current.Trial with { TrialEmail = email ?? string.Empty },
        }, cancellationToken).ConfigureAwait(false);

        // Notify UI immediately so signed-in state shows even if status sync fails
        StatusChanged?.Invoke(null);

        // Sync status from server (non-fatal — UI already updated above)
        await RefreshStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    // ── Status Refresh ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TrialStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            Log.Debug("TrialAccountService: no token — skipping status refresh");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, StatusUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("TrialAccountService: 401 on status refresh — auto-logout");
                await LogoutAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("TrialAccountService: status refresh failed with HTTP {Code}",
                    (int)response.StatusCode);
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            var status = ParseTrialStatus(json);
            if (status is null)
            {
                return null;
            }

            // Update local settings with server data
            string now = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            await _settings.UpdateAsync(_settings.Current with
            {
                Trial = new TrialSettings
                {
                    TrialEmail = _settings.Current.Trial.TrialEmail,
                    TrialWordsUsed = status.WordsUsed,
                    TrialWordsQuota = status.WordsQuota,
                    TrialDaysRemaining = status.DaysRemaining,
                    TrialExpiresAt = status.ExpiresAt,
                    TrialActive = status.TrialActive,
                    TrialLastSynced = now,
                },
            }, cancellationToken).ConfigureAwait(false);

            Log.Information("TrialAccountService: status synced — {Used}/{Quota} words, {Days} days left",
                status.WordsUsed, status.WordsQuota, status.DaysRemaining);

            StatusChanged?.Invoke(status);
            return status;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrialAccountService: network error during status refresh");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    // ── Usage Reporting ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task RecordUsageAsync(string provider, string model, int wordsUsed,
        CancellationToken cancellationToken = default)
    {
        // Optimistic local update
        int newUsed = _settings.Current.Trial.TrialWordsUsed + wordsUsed;
        await _settings.UpdateAsync(_settings.Current with
        {
            Trial = _settings.Current.Trial with { TrialWordsUsed = newUsed },
        }, cancellationToken).ConfigureAwait(false);

        string? token = _secureStorage.RetrieveKey(TokenKey);
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        try
        {
            string body = BuildUsageJson(provider, model, wordsUsed);
            using var request = new HttpRequestMessage(HttpMethod.Post, UsageUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("TrialAccountService: usage report failed with HTTP {Code}",
                    (int)response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "TrialAccountService: network error reporting usage");
        }
        catch (TaskCanceledException)
        {
            // Timeout or cancellation — usage already recorded locally
        }
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _secureStorage.DeleteKey(TokenKey);

        await _settings.UpdateAsync(_settings.Current with
        {
            AuthMode = AuthMode.None,
            Trial = new TrialSettings(),
        }, cancellationToken).ConfigureAwait(false);

        Log.Information("TrialAccountService: logged out");
        StatusChanged?.Invoke(null);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static TrialStatus? ParseTrialStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new TrialStatus
            {
                WordsUsed = root.TryGetProperty("wordsUsed", out var wu) ? wu.GetInt32() : 0,
                WordsQuota = root.TryGetProperty("wordsQuota", out var wq) ? wq.GetInt32() : 15_000,
                DaysRemaining = root.TryGetProperty("daysRemaining", out var dr) ? dr.GetInt32() : 0,
                ExpiresAt = root.TryGetProperty("expiresAt", out var ea) ? ea.GetString() ?? string.Empty : string.Empty,
                TrialActive = root.TryGetProperty("trialActive", out var ta) && ta.GetBoolean(),
            };
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "TrialAccountService: failed to parse trial status JSON");
            return null;
        }
    }

    private static string BuildUsageJson(string provider, string model, int wordsUsed)
    {
        string escapedProvider = EscapeJsonString(provider);
        string escapedModel = EscapeJsonString(model);
        return $$"""{"provider":"{{escapedProvider}}","model":"{{escapedModel}}","wordsUsed":{{wordsUsed}}}""";
    }

    private static string EscapeJsonString(string s)
        => s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
