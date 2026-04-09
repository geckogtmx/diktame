
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace DiktaMe.Core.Weather;

/// <summary>
/// Fetches current weather data from Open-Meteo (free, no API key).
/// Location is auto-detected via IP geolocation (ip-api.com).
/// Results are cached for 30 minutes.
/// </summary>
public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(30);

    private WeatherData? _cachedData;
    private DateTime _lastFetch = DateTime.MinValue;
    private double? _latitude;
    private double? _longitude;
    private string? _region;

    public WeatherService(HttpClient? httpClient = null)
    {
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// Returns current weather data, using cache if fresh (under 30 minutes old).
    /// Returns null if data cannot be fetched (network error, geolocation blocked, etc.).
    /// Never throws.
    /// </summary>
    public async Task<WeatherData?> GetCurrentWeatherAsync(CancellationToken cancellationToken = default)
    {
        // Return cached data if still fresh
        if (_cachedData is not null && DateTime.UtcNow - _lastFetch < _refreshInterval)
        {
            return _cachedData;
        }

        if (!await _fetchLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            // Another fetch is in progress — return stale cache
            return _cachedData;
        }

        try
        {
            // Double-check after acquiring lock
            if (_cachedData is not null && DateTime.UtcNow - _lastFetch < _refreshInterval)
            {
                return _cachedData;
            }

            // Step 1: Resolve location via IP geolocation (one-time)
            if (_latitude is null || _longitude is null)
            {
                if (!await ResolveLocationAsync(cancellationToken).ConfigureAwait(false))
                {
                    return _cachedData; // stale or null
                }
            }

            // Step 2: Fetch weather from Open-Meteo
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:F4}&longitude={1:F4}&current_weather=true",
                _latitude!.Value, _longitude!.Value);

            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement.GetProperty("current_weather");

            double temp = current.GetProperty("temperature").GetDouble();
            int code = current.GetProperty("weathercode").GetInt32();
            var (icon, description) = MapWeatherCode(code);

            _cachedData = new WeatherData(temp, code, icon, description, _region ?? "");
            _lastFetch = DateTime.UtcNow;
            Log.Debug("[Weather] Fetched: {Temp}°C, code={Code} ({Desc}) region={Region}", temp, code, description, _region);
            return _cachedData;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            Log.Warning(ex, "[Weather] Failed to fetch weather data");
            return _cachedData; // stale or null
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<bool> ResolveLocationAsync(CancellationToken cancellationToken)
    {
        try
        {
            string geoJson = await _http.GetStringAsync(
                "https://get.geojs.io/v1/ip/geo.json", cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(geoJson);
            _latitude = double.Parse(doc.RootElement.GetProperty("latitude").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            _longitude = double.Parse(doc.RootElement.GetProperty("longitude").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            if (doc.RootElement.TryGetProperty("region", out var regionProp))
            {
                _region = regionProp.GetString();
            }
            Log.Debug("[Weather] Geolocation resolved: lat={Lat}, lon={Lon}, region={Region}", _latitude, _longitude, _region);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            Log.Warning(ex, "[Weather] Failed to resolve IP geolocation");
            return false;
        }
    }

    /// <summary>
    /// Maps a WMO weather code to a Unicode emoji icon and English description.
    /// See: https://www.nodc.noaa.gov/archive/arc0021/0002199/1.1/data/0-data/HTML/WMO-CODE/WMO4677.HTM
    /// </summary>
    public static (string Icon, string Description) MapWeatherCode(int code) => code switch
    {
        0 => ("\u2600", "Clear sky"),           // ☀
        1 => ("\u26C5", "Mainly clear"),         // ⛅
        2 => ("\u26C5", "Partly cloudy"),        // ⛅
        3 => ("\u2601", "Overcast"),             // ☁
        45 or 48 => ("\uD83C\uDF2B", "Fog"),    // 🌫
        51 or 53 or 55 => ("\uD83C\uDF27", "Drizzle"),     // 🌧
        56 or 57 => ("\uD83C\uDF27", "Freezing drizzle"),  // 🌧
        61 or 63 or 65 => ("\uD83C\uDF27", "Rain"),        // 🌧
        66 or 67 => ("\uD83C\uDF27", "Freezing rain"),     // 🌧
        71 or 73 or 75 => ("\u2744", "Snow"),               // ❄
        77 => ("\u2744", "Snow grains"),                     // ❄
        80 or 81 or 82 => ("\uD83C\uDF27", "Showers"),     // 🌧
        85 or 86 => ("\u2744", "Snow showers"),              // ❄
        95 => ("\u26C8", "Thunderstorm"),                     // ⛈
        96 or 99 => ("\u26C8", "Thunderstorm with hail"),   // ⛈
        _ => ("\u2601", "Unknown"),                          // ☁ fallback
    };

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
        _fetchLock.Dispose();
    }
}

/// <summary>
/// Immutable weather data snapshot.
/// </summary>
public sealed record WeatherData(
    double TemperatureCelsius,
    int WeatherCode,
    string Icon,
    string Description,
    string Region);
