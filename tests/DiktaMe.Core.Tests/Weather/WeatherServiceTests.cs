
using System.Net;
using System.Net.Http;
using DiktaMe.Core.Weather;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Weather;

/// <summary>Stub handler that returns sequenced responses based on request URL.</summary>
internal sealed class WeatherFakeHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new(StringComparer.OrdinalIgnoreCase);
    private int _callCount;

    public int CallCount => _callCount;

    public void SetResponse(string urlContains, HttpStatusCode status, string body)
    {
        _responses[urlContains] = (status, body);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        string url = request.RequestUri?.ToString() ?? "";

        foreach (var (key, (status, body)) in _responses)
        {
            if (url.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body),
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("No matching stub"),
        });
    }
}

public sealed class WeatherServiceTests : IDisposable
{
    private const string GeoResponse = """{"lat":48.8566,"lon":2.3522,"region":"IDF"}""";
    private const string WeatherResponse = """
        {
            "current_weather": {
                "temperature": 18.5,
                "weathercode": 0,
                "windspeed": 12.3,
                "winddirection": 180,
                "time": "2026-03-22T14:00"
            }
        }
        """;
    private const string RainWeatherResponse = """
        {
            "current_weather": {
                "temperature": 7.2,
                "weathercode": 63,
                "windspeed": 25.0,
                "winddirection": 270,
                "time": "2026-03-22T14:00"
            }
        }
        """;

    private readonly WeatherFakeHandler _handler;
    private readonly HttpClient _http;

    public WeatherServiceTests()
    {
        _handler = new WeatherFakeHandler();
        _handler.SetResponse("ip-api.com", HttpStatusCode.OK, GeoResponse);
        _handler.SetResponse("api.open-meteo.com", HttpStatusCode.OK, WeatherResponse);
        _http = new HttpClient(_handler);
    }

    public void Dispose() => _http.Dispose();

    // ── MapWeatherCode tests ─────────────────────────────────────────

    [Fact]
    public void MapWeatherCode_ClearSky_ReturnsSunIcon()
    {
        var (icon, desc) = WeatherService.MapWeatherCode(0);
        icon.Should().Be("\u2600"); // ☀
        desc.Should().Be("Clear sky");
    }

    [Theory]
    [InlineData(1, "Mainly clear")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(3, "Overcast")]
    public void MapWeatherCode_CloudVariants_ReturnsCorrectDescription(int code, string expectedDesc)
    {
        var (_, desc) = WeatherService.MapWeatherCode(code);
        desc.Should().Be(expectedDesc);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(63)]
    [InlineData(65)]
    public void MapWeatherCode_Rain_ReturnsRainIcon(int code)
    {
        var (icon, _) = WeatherService.MapWeatherCode(code);
        icon.Should().Be("\uD83C\uDF27"); // 🌧
    }

    [Theory]
    [InlineData(71)]
    [InlineData(73)]
    [InlineData(75)]
    [InlineData(77)]
    public void MapWeatherCode_Snow_ReturnsSnowIcon(int code)
    {
        var (icon, _) = WeatherService.MapWeatherCode(code);
        icon.Should().Be("\u2744"); // ❄
    }

    [Theory]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(99)]
    public void MapWeatherCode_Thunderstorm_ReturnsStormIcon(int code)
    {
        var (icon, _) = WeatherService.MapWeatherCode(code);
        icon.Should().Be("\u26C8"); // ⛈
    }

    [Fact]
    public void MapWeatherCode_UnknownCode_ReturnsFallback()
    {
        var (icon, desc) = WeatherService.MapWeatherCode(999);
        icon.Should().Be("\u2601"); // ☁
        desc.Should().Be("Unknown");
    }

    // ── GetCurrentWeatherAsync tests ─────────────────────────────────

    [Fact]
    public async Task GetCurrentWeather_ParsesValidResponse_ReturnsWeatherData()
    {
        using var sut = new WeatherService(_http);

        var result = await sut.GetCurrentWeatherAsync();

        result.Should().NotBeNull();
        result!.TemperatureCelsius.Should().Be(18.5);
        result.WeatherCode.Should().Be(0);
        result.Icon.Should().Be("\u2600"); // ☀
        result.Description.Should().Be("Clear sky");
        result.Region.Should().Be("IDF");
    }

    [Fact]
    public async Task GetCurrentWeather_HttpError_ReturnsNull()
    {
        var errorHandler = new WeatherFakeHandler();
        errorHandler.SetResponse("ip-api.com", HttpStatusCode.InternalServerError, "error");
        using var errorHttp = new HttpClient(errorHandler);
        using var sut = new WeatherService(errorHttp);

        var result = await sut.GetCurrentWeatherAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentWeather_CachesFreshResult_OnlyOneHttpFetch()
    {
        using var sut = new WeatherService(_http);

        var result1 = await sut.GetCurrentWeatherAsync();
        var result2 = await sut.GetCurrentWeatherAsync();

        result1.Should().NotBeNull();
        result2.Should().BeSameAs(result1);
        // 2 calls for first fetch (geo + weather), 0 for second (cached)
        _handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCurrentWeather_GeoLocationFails_ReturnsNull()
    {
        var handler = new WeatherFakeHandler();
        handler.SetResponse("ip-api.com", HttpStatusCode.Forbidden, "blocked");
        using var http = new HttpClient(handler);
        using var sut = new WeatherService(http);

        var result = await sut.GetCurrentWeatherAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentWeather_WeatherApiFails_ReturnsNull()
    {
        var handler = new WeatherFakeHandler();
        handler.SetResponse("ip-api.com", HttpStatusCode.OK, GeoResponse);
        handler.SetResponse("api.open-meteo.com", HttpStatusCode.ServiceUnavailable, "down");
        using var http = new HttpClient(handler);
        using var sut = new WeatherService(http);

        var result = await sut.GetCurrentWeatherAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentWeather_InvalidJson_ReturnsNull()
    {
        var handler = new WeatherFakeHandler();
        handler.SetResponse("ip-api.com", HttpStatusCode.OK, GeoResponse);
        handler.SetResponse("api.open-meteo.com", HttpStatusCode.OK, "not json");
        using var http = new HttpClient(handler);
        using var sut = new WeatherService(http);

        var result = await sut.GetCurrentWeatherAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentWeather_RainResponse_ParsesCorrectly()
    {
        var handler = new WeatherFakeHandler();
        handler.SetResponse("ip-api.com", HttpStatusCode.OK, GeoResponse);
        handler.SetResponse("api.open-meteo.com", HttpStatusCode.OK, RainWeatherResponse);
        using var http = new HttpClient(handler);
        using var sut = new WeatherService(http);

        var result = await sut.GetCurrentWeatherAsync();

        result.Should().NotBeNull();
        result!.TemperatureCelsius.Should().Be(7.2);
        result.WeatherCode.Should().Be(63);
        result.Icon.Should().Be("\uD83C\uDF27"); // 🌧
        result.Description.Should().Be("Rain");
    }
}
