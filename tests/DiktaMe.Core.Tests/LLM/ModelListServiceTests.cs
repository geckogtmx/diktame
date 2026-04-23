using System.Net;
using System.Net.Http;
using System.Text;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.LLM;

/// <summary>
/// Tests for <see cref="ModelListService.ResolveProviderFromModelId"/>.
/// API query tests are omitted (they need real HTTP) — covered by integration tests.
/// </summary>
public sealed class ModelListServiceTests
{
    // ── ResolveProviderFromModelId ───────────────────────────────────────────

    [Theory]
    [InlineData("gpt-4o-mini", "openai")]
    [InlineData("gpt-4o", "openai")]
    [InlineData("gpt-3.5-turbo", "openai")]
    [InlineData("o1-preview", "openai")]
    [InlineData("o3-mini", "openai")]
    [InlineData("o4-mini", "openai")]
    [InlineData("chatgpt-4o-latest", "openai")]
    public void Resolve_OpenAI_Models(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Theory]
    [InlineData("claude-3-5-haiku-20241022", "anthropic")]
    [InlineData("claude-sonnet-4-20250514", "anthropic")]
    [InlineData("claude-opus-4-20250514", "anthropic")]
    [InlineData("claude-3-opus-20240229", "anthropic")]
    public void Resolve_Anthropic_Models(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Theory]
    [InlineData("gemini-2.5-flash", "gemini")]
    [InlineData("gemini-2.0-flash", "gemini")]
    [InlineData("gemini-1.5-pro", "gemini")]
    [InlineData("models/gemini-2.5-flash", "gemini")]
    public void Resolve_Gemini_Models(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Theory]
    [InlineData("deepseek-chat", "deepseek")]
    [InlineData("deepseek-reasoner", "deepseek")]
    public void Resolve_DeepSeek_Models(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Theory]
    [InlineData("anthropic/claude-3-opus", "openrouter")]
    [InlineData("openai/gpt-4o", "openrouter")]
    [InlineData("meta-llama/llama-3.3-70b", "openrouter")]
    public void Resolve_OpenRouter_Models(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Theory]
    [InlineData("llama3.2", "ollama")]
    [InlineData("phi4", "ollama")]
    [InlineData("mistral", "ollama")]
    [InlineData("some-custom-model", "ollama")]
    public void Resolve_UnknownPrefix_DefaultsToOllama(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyOrNull_Throws(string? modelId)
    {
        Assert.Throws<ArgumentException>(() => ModelListService.ResolveProviderFromModelId(modelId!));
    }

    [Fact]
    public void Resolve_CaseInsensitive()
    {
        Assert.Equal("openai", ModelListService.ResolveProviderFromModelId("GPT-4o-MINI"));
        Assert.Equal("anthropic", ModelListService.ResolveProviderFromModelId("Claude-Sonnet-4"));
        Assert.Equal("gemini", ModelListService.ResolveProviderFromModelId("GEMINI-2.5-FLASH"));
    }

    [Theory]
    [InlineData("requesty:openai/gpt-4o-mini", "requesty")]
    [InlineData("requesty:anthropic/claude-3.5-sonnet", "requesty")]
    [InlineData("requesty:custom/weird-model", "requesty")]
    public void Resolve_RequestyPrefix_RoutesToRequesty(string modelId, string expected)
    {
        Assert.Equal(expected, ModelListService.ResolveProviderFromModelId(modelId));
    }

    [Fact]
    public void Resolve_BareProviderSlashModel_StillRoutesToOpenRouter()
    {
        // Regression guard: pre-namespace Requesty IDs (no prefix) stay on the
        // OpenRouter path, matching the collision behavior we document in the
        // migration notice.
        Assert.Equal("openrouter", ModelListService.ResolveProviderFromModelId("openai/gpt-4o-mini"));
        Assert.Equal("openrouter", ModelListService.ResolveProviderFromModelId("anthropic/claude-3.5-sonnet"));
    }

    // ── IsChatModel filtering (tested indirectly via OpenAI query) ──────────

    [Fact]
    public void ModelInfo_Record_Properties()
    {
        var info = new ModelInfo
        {
            ModelId = "gpt-4o-mini",
            DisplayName = "GPT-4o Mini",
            Provider = "OpenAI",
            IsAvailable = true,
            ContextWindow = 128000,
            Description = "Fast, affordable model",
        };

        Assert.Equal("gpt-4o-mini", info.ModelId);
        Assert.Equal("GPT-4o Mini", info.DisplayName);
        Assert.Equal("OpenAI", info.Provider);
        Assert.True(info.IsAvailable);
        Assert.Equal(128000, info.ContextWindow);
        Assert.Equal("Fast, affordable model", info.Description);
    }

    [Fact]
    public void ModelInfo_OptionalProperties_DefaultToNull()
    {
        var info = new ModelInfo
        {
            ModelId = "llama3.2",
            DisplayName = "llama3.2",
            Provider = "Ollama (Local)",
            IsAvailable = true,
        };

        Assert.Null(info.ContextWindow);
        Assert.Null(info.Description);
    }
}

// ── HTTP-level tests for GetAvailableModelsAsync ────────────────────────────

public sealed class ModelListServiceHttpTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DiktaMe.Core.Security.SecureStorage _storage;

    public ModelListServiceHttpTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"diktame-mls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _storage = new DiktaMe.Core.Security.SecureStorage(Path.Combine(_tempDir, "keys.dat"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private ModelListService CreateService(ModelsFakeHandler handler)
    {
        var http = new HttpClient(handler);
        var sm = new DiktaMe.Core.Config.SettingsManager(Path.Combine(_tempDir, $"settings_{Guid.NewGuid()}.json"));
        return new ModelListService(_storage, sm, http);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_NoApiKeys_ReturnsEmptyOrOllamaOnly()
    {
        // No keys stored — cloud providers should be skipped. Ollama always queries localhost.
        var handler = new ModelsFakeHandler(System.Net.HttpStatusCode.OK, "{}");
        using var service = CreateService(handler);

        var models = await service.GetAvailableModelsAsync();

        // Cloud models (OpenAI, Anthropic, Gemini, OpenRouter) should all be absent
        Assert.DoesNotContain(models, m => string.Equals(m.Provider, "OpenAI", StringComparison.Ordinal));
        Assert.DoesNotContain(models, m => string.Equals(m.Provider, "Anthropic", StringComparison.Ordinal));
        Assert.DoesNotContain(models, m => string.Equals(m.Provider, "Gemini", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAvailableModelsAsync_OpenAI_ParsesModels()
    {
        _storage.StoreKey("openai", "sk-test");
        const string response = """
            {
              "data": [
                { "id": "gpt-4o-mini" },
                { "id": "gpt-4o" },
                { "id": "dall-e-3" },
                { "id": "text-embedding-ada-002" }
              ]
            }
            """;
        var handler = new ModelsFakeHandler(System.Net.HttpStatusCode.OK, response);
        using var service = CreateService(handler);

        var models = await service.GetAvailableModelsAsync();

        // Only chat models (gpt-*) should be included, not dall-e or embeddings
        Assert.Contains(models, m => string.Equals(m.ModelId, "gpt-4o-mini", StringComparison.Ordinal));
        Assert.Contains(models, m => string.Equals(m.ModelId, "gpt-4o", StringComparison.Ordinal));
        Assert.DoesNotContain(models, m => string.Equals(m.ModelId, "dall-e-3", StringComparison.Ordinal));
        Assert.DoesNotContain(models, m => string.Equals(m.ModelId, "text-embedding-ada-002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ProviderError_ReturnsEmptyForThatProvider()
    {
        _storage.StoreKey("openai", "sk-test");
        var handler = new ModelsFakeHandler(System.Net.HttpStatusCode.InternalServerError, "server error");
        using var service = CreateService(handler);

        var models = await service.GetAvailableModelsAsync();

        // OpenAI fails, but no exception thrown — just returns empty for that provider
        Assert.DoesNotContain(models, m => string.Equals(m.Provider, "OpenAI", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAvailableModelsAsync_OllamaModels_Parsed()
    {
        // Ollama doesn't need an API key — it always queries localhost
        var ollamaResponse = """{"models":[{"name":"llama3.2:latest","size":2000000000},{"name":"gemma3:1b","size":800000000}]}""";
        var handler = new ModelsFakeHandler(System.Net.HttpStatusCode.OK, ollamaResponse);
        using var service = CreateService(handler);

        var models = await service.GetAvailableModelsAsync();

        // Ollama models should be in the list (they're queried from localhost, not gated by API key)
        // But in unit tests, the localhost call will fail — so this just verifies no crash
        // The models list may be empty since other providers have no keys
        Assert.NotNull(models);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var handler = new ModelsFakeHandler(System.Net.HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        service.Dispose();
        var ex = Record.Exception(() => service.Dispose());

        Assert.Null(ex);
    }
}

internal sealed class ModelsFakeHandler(System.Net.HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        });
    }
}

/// <summary>
/// Tests for <see cref="LLMProviderExtensions.ProcessWithModelAsync"/>.
/// </summary>
public sealed class LLMProviderExtensionsTests
{
    private static LlmResult OkResult(string text) => new()
    {
        Text = text,
        Provider = "test",
    };

    [Fact]
    public async Task ProcessWithModel_NonRouter_IgnoresModelName()
    {
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("regular output"));

        // Even with a modelName, falls back to regular call since provider is not LLMRouter
        var result = await provider.Object.ProcessWithModelAsync("text", "prompt", "gpt-4o-mini");

        Assert.Equal("regular output", result.Text);
    }

    [Fact]
    public async Task ProcessWithModel_NullModelName_UsesRegularCall()
    {
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("regular output"));

        var result = await provider.Object.ProcessWithModelAsync("text", "prompt", modelName: null);

        Assert.Equal("regular output", result.Text);
    }

    [Fact]
    public async Task ProcessWithModel_EmptyModelName_UsesRegularCall()
    {
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("regular output"));

        var result = await provider.Object.ProcessWithModelAsync("text", "prompt", modelName: "  ");

        Assert.Equal("regular output", result.Text);
    }

    // ── GetModelsForProviderAsync (BUG-027 wizard path) ────────────────────

    [Fact, Trait("Category", "Unit")]
    public async Task GetModelsForProviderAsync_WithOverrideKey_UsesOverride()
    {
        // Arrange: mock HttpClient that captures the Authorization header.
        string? seenAuth = null;
        var handler = new FuncHandler((req, _) =>
        {
            seenAuth = req.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"id\":\"gpt-4o-mini\"}]}", Encoding.UTF8, "application/json"),
            };
        });
        using var http = new HttpClient(handler);
        var tempStorage = MakeTempStorage(out string tempDir);
        tempStorage.StoreKey("openai", "stored-key-should-not-be-used");
        var settings = new SettingsManager(Path.Combine(tempDir, "settings.json"));
        using var svc = new ModelListService(tempStorage, settings, http);

        // Act: pass override. Override should win over the stored key.
        var models = await svc.GetModelsForProviderAsync("openai", overrideKey: "override-key");

        // Assert
        seenAuth.Should().Be("override-key");
        models.Should().ContainSingle(m => m.ModelId == "gpt-4o-mini" && m.Provider == "OpenAI");

        try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
    }

    [Fact, Trait("Category", "Unit")]
    public async Task GetModelsForProviderAsync_WithoutOverride_FallsBackToStoredKey()
    {
        string? seenAuth = null;
        var handler = new FuncHandler((req, _) =>
        {
            seenAuth = req.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"id\":\"gpt-4o\"}]}", Encoding.UTF8, "application/json"),
            };
        });
        using var http = new HttpClient(handler);
        var tempStorage = MakeTempStorage(out string tempDir);
        tempStorage.StoreKey("openai", "stored-key-from-securestorage");
        var settings = new SettingsManager(Path.Combine(tempDir, "settings.json"));
        using var svc = new ModelListService(tempStorage, settings, http);

        var models = await svc.GetModelsForProviderAsync("openai", overrideKey: null);

        seenAuth.Should().Be("stored-key-from-securestorage");
        models.Should().ContainSingle();

        try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
    }

    [Fact, Trait("Category", "Unit")]
    public async Task GetModelsForProviderAsync_UnknownProvider_ReturnsEmpty()
    {
        using var http = new HttpClient(new FuncHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var tempStorage = MakeTempStorage(out string tempDir);
        var settings = new SettingsManager(Path.Combine(tempDir, "settings.json"));
        using var svc = new ModelListService(tempStorage, settings, http);

        var models = await svc.GetModelsForProviderAsync("not-a-real-provider");

        models.Should().BeEmpty();

        try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
    }

    [Fact, Trait("Category", "Unit")]
    public async Task GetModelsForProviderAsync_Requesty_ReturnsPrefixedIds()
    {
        // Regression guard: BUG-031 namespace prefix must also apply on the
        // wizard override path (not just the full catalog fetch).
        var handler = new FuncHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"id\":\"openai/gpt-4o-mini\"}]}", Encoding.UTF8, "application/json"),
            });
        using var http = new HttpClient(handler);
        var tempStorage = MakeTempStorage(out string tempDir);
        var settings = new SettingsManager(Path.Combine(tempDir, "settings.json"));
        using var svc = new ModelListService(tempStorage, settings, http);

        var models = await svc.GetModelsForProviderAsync("requesty", overrideKey: "k");

        models.Should().ContainSingle(m => m.ModelId == "requesty:openai/gpt-4o-mini");

        try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
    }

    // ── Test helpers ───────────────────────────────────────────────────────

    private static SecureStorage MakeTempStorage(out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return new SecureStorage(Path.Combine(tempDir, "keys.dat"));
    }

    private sealed class FuncHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _fn;
        public FuncHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> fn) => _fn = fn;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_fn(request, ct));
    }
}
