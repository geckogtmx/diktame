
using System.Net;
using System.Net.Http;
using DiktaMe.Core.Config;
using DiktaMe.Core.STT;

namespace DiktaMe.Core.Tests.STT;
/// <summary>
/// Unit tests for <see cref="DeepgramProvider"/>.
/// Uses a stub <see cref="HttpMessageHandler"/> — no real network calls.
/// </summary>
public sealed class DeepgramProviderTests : IDisposable
{
    // Minimal real Deepgram listen response JSON
    private const string ValidResponse = """
        {
          "results": {
            "channels": [{
              "alternatives": [{
                "transcript": "hello world",
                "confidence": 0.99
              }],
              "detected_language": "en"
            }]
          }
        }
        """;

    private readonly string _tmpWav;

    public DeepgramProviderTests()
    {
        // Create a minimal dummy WAV file (content doesn't matter for unit tests)
        _tmpWav = Path.GetTempFileName();
        File.WriteAllBytes(_tmpWav, [0x52, 0x49, 0x46, 0x46]); // "RIFF" header stub
    }

    public void Dispose() => File.Delete(_tmpWav);

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DeepgramProvider(""));
    }

    [Fact]
    public void Constructor_WhitespaceApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DeepgramProvider("   "));
    }

    // ── IsAvailableAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_WithApiKey_ReturnsTrue()
    {
        using var provider = new DeepgramProvider("dg_test_key");
        Assert.True(await provider.IsAvailableAsync());
    }

    // ── ProviderName ──────────────────────────────────────────────────────────

    [Fact]
    public void ProviderName_DefaultSettings_IsNova3()
    {
        using var provider = new DeepgramProvider("key");
        Assert.Equal("Deepgram nova-3", provider.ProviderName);
    }

    [Fact]
    public void ProviderName_CustomModel_ReflectsModel()
    {
        var settings = new DeepgramSettings { Model = "nova-2" };
        using var provider = new DeepgramProvider("key", settings);
        Assert.Equal("Deepgram nova-2", provider.ProviderName);
    }

    // ── TranscribeAsync — happy path ──────────────────────────────────────────

    [Fact]
    public async Task Transcribe_ValidResponse_ReturnsTranscript()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("dg_key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Equal("hello world", result.Text);
        Assert.True(result.IsSuccess);
        Assert.Equal("Deepgram nova-3", result.Provider);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task Transcribe_AutoLanguage_SendsDetectLanguageParam()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("dg_key", httpClient: http);

        await provider.TranscribeAsync(_tmpWav, "auto");

        Assert.Contains("detect_language=true", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Transcribe_SpecificLanguage_SendsLanguageParam()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("dg_key", httpClient: http);

        await provider.TranscribeAsync(_tmpWav, "es");

        Assert.Contains("language=es", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Transcribe_RequestHasTokenAuthorization()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, ValidResponse);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("my_dg_key", httpClient: http);

        await provider.TranscribeAsync(_tmpWav, "en");

        Assert.Equal("Token", handler.LastAuthScheme);
        Assert.Equal("my_dg_key", handler.LastAuthParameter);
    }

    // ── TranscribeAsync — error responses ─────────────────────────────────────

    [Fact]
    public async Task Transcribe_401_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("bad_key", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    [Fact]
    public async Task Transcribe_429_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler((HttpStatusCode)429, "");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    [Fact]
    public async Task Transcribe_500_ThrowsInvalidOperation()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "error body");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranscribeAsync(_tmpWav, "en"));
    }

    // ── JSON edge cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task Transcribe_EmptyChannels_ReturnsEmptyText()
    {
        const string emptyChannels = """{"results":{"channels":[]}}""";

        var handler = new FakeHttpHandler(HttpStatusCode.OK, emptyChannels);
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Text);
    }

    [Fact]
    public async Task Transcribe_MalformedJson_ReturnsEmptyText()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "not-json-{{{{");
        using var http = new HttpClient(handler);
        using var provider = new DeepgramProvider("key", httpClient: http);

        var result = await provider.TranscribeAsync(_tmpWav, "en");

        Assert.False(result.IsSuccess);
    }

    // ── BuildListenUrl ────────────────────────────────────────────────────────

    [Fact]
    public void BuildListenUrl_DefaultSettings_ContainsNova3AndPunctuateAndDictation()
    {
        using var provider = new DeepgramProvider("key");
        string url = provider.BuildListenUrl("en");

        Assert.Contains("model=nova-3", url, StringComparison.Ordinal);
        Assert.Contains("punctuate=true", url, StringComparison.Ordinal);
        Assert.Contains("dictation=true", url, StringComparison.Ordinal);
        Assert.Contains("language=en", url, StringComparison.Ordinal);
        Assert.DoesNotContain("smart_format", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_SmartFormatOn_OmitsPunctuate()
    {
        var settings = new DeepgramSettings { SmartFormat = true, Punctuate = true };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        Assert.Contains("smart_format=true", url, StringComparison.Ordinal);
        Assert.DoesNotContain("punctuate=true", url, StringComparison.Ordinal);
        // Dictation should still be present (smart_format includes punctuate)
        Assert.Contains("dictation=true", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_PunctuateOff_OmitsDictation()
    {
        var settings = new DeepgramSettings { Punctuate = false, Dictation = true };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        Assert.DoesNotContain("punctuate=true", url, StringComparison.Ordinal);
        Assert.DoesNotContain("dictation=true", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_DictationOff_OmitsDictation()
    {
        var settings = new DeepgramSettings { Punctuate = true, Dictation = false };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        Assert.Contains("punctuate=true", url, StringComparison.Ordinal);
        Assert.DoesNotContain("dictation=true", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_AutoLanguage_SendsDetectLanguage()
    {
        using var provider = new DeepgramProvider("key");
        string url = provider.BuildListenUrl("auto");

        Assert.Contains("detect_language=true", url, StringComparison.Ordinal);
        // Should not contain a "language=xx" param (but detect_language= is fine)
        Assert.DoesNotContain("&language=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_Nova2Model_ContainsNova2()
    {
        var settings = new DeepgramSettings { Model = "nova-2" };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        Assert.Contains("model=nova-2", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_Replacements_AppendsEach()
    {
        var settings = new DeepgramSettings
        {
            Replacements = ["monika:Monica", "zen desk:Zendesk"],
        };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        Assert.Contains("replace=monika%3AMonica", url, StringComparison.Ordinal);
        Assert.Contains("replace=zen%20desk%3AZendesk", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_EmptyReplacements_NoReplaceParam()
    {
        var settings = new DeepgramSettings { Replacements = [] };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        Assert.DoesNotContain("replace=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListenUrl_WhitespaceReplacement_Skipped()
    {
        var settings = new DeepgramSettings { Replacements = ["  ", "valid:term"] };
        using var provider = new DeepgramProvider("key", settings);
        string url = provider.BuildListenUrl("en");

        // Only one replace param (the valid one)
        int count = url.Split("replace=").Length - 1;
        Assert.Equal(1, count);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var provider = new DeepgramProvider("key");
        provider.Dispose();
        var ex = Record.Exception(() => provider.Dispose());
        Assert.Null(ex);
    }
}

/// <summary>Stub HTTP handler that returns a canned response.</summary>
internal sealed class FakeHttpHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}
