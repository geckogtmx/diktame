using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

public sealed class LLMProviderFactoryTests : IDisposable
{
    private readonly string _tempKeysFile;
    private readonly string _tempSettingsFile;
    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;

    public LLMProviderFactoryTests()
    {
        _tempKeysFile = Path.Combine(Path.GetTempPath(), $"diktame_test_keys_{Guid.NewGuid()}.dat");
        _tempSettingsFile = Path.Combine(Path.GetTempPath(), $"diktame_test_settings_{Guid.NewGuid()}.json");
        _secureStorage = new SecureStorage(_tempKeysFile);
        _settings = new SettingsManager(_tempSettingsFile);
    }

    public void Dispose()
    {
        try { File.Delete(_tempKeysFile); } catch { }
        try { File.Delete(_tempSettingsFile); } catch { }
    }

    // ── NullLlmProvider for "none" / "skip" ─────────────────────────────────

    [Theory]
    [InlineData("none")]
    [InlineData("skip")]
    [InlineData("None")]
    [InlineData("SKIP")]
    public void CreateProvider_NoneOrSkip_ReturnsNullLlmProvider(string providerType)
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider(providerType);

        provider.Should().BeOfType<NullLlmProvider>();
    }

    [Fact]
    public void CreateProvider_None_AlwaysReturnsNewInstance()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var a = factory.CreateProvider("none");
        var b = factory.CreateProvider("none");

        // NullLlmProvider is stateless — each call creates a new instance (no caching)
        a.Should().NotBeSameAs(b);
    }

    // ── Cloud providers with explicit API key ───────────────────────────────

    [Fact]
    public void CreateProvider_Gemini_WithApiKey_ReturnsGeminiProvider()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider("gemini", apiKey: "test-key");

        provider.Should().BeOfType<GeminiProvider>();
    }

    [Fact]
    public void CreateProvider_Requesty_StripsNamespacePrefixFromModel()
    {
        // BUG-031 follow-up: ModelListService prefixes Requesty IDs with
        // "requesty:" to distinguish them from OpenRouter. The factory must
        // strip that prefix before handing the model name to the HTTP layer.
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider(
            "requesty",
            apiKey: "test-key",
            model: "requesty:openai/gpt-4o-mini");

        provider.Should().BeOfType<OpenAICompatibleProvider>();
        // ProviderName format is "{model} ({serviceName})" — the bare model
        // should appear without the "requesty:" prefix.
        provider.ProviderName.Should().Contain("openai/gpt-4o-mini");
        provider.ProviderName.Should().NotContain("requesty:");
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("claude")]
    public void CreateProvider_Anthropic_WithApiKey_ReturnsAnthropicProvider(string type)
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider(type, apiKey: "test-key");

        provider.Should().BeOfType<AnthropicProvider>();
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("deepseek")]
    [InlineData("openrouter")]
    [InlineData("groq")]
    [InlineData("together")]
    [InlineData("perplexity")]
    public void CreateProvider_OpenAICompatible_WithApiKey_ReturnsOpenAICompatibleProvider(string type)
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider(type, apiKey: "test-key");

        provider.Should().BeOfType<OpenAICompatibleProvider>();
    }

    [Fact]
    public void CreateProvider_Ollama_ReturnsOllamaProvider()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider("ollama");

        provider.Should().BeOfType<OllamaProvider>();
    }

    // ── Case insensitivity ──────────────────────────────────────────────────

    [Fact]
    public void CreateProvider_IsCaseInsensitive()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider("Gemini", apiKey: "test-key");

        provider.Should().BeOfType<GeminiProvider>();
    }

    // ── API key from SecureStorage ──────────────────────────────────────────

    [Fact]
    public void CreateProvider_Gemini_WithStoredKey_ReturnsGeminiProvider()
    {
        _secureStorage.StoreKey("gemini", "stored-key");
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var provider = factory.CreateProvider("gemini");

        provider.Should().BeOfType<GeminiProvider>();
    }

    // ── Missing API key ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("gemini", "*Gemini*key*")]
    [InlineData("anthropic", "*Anthropic*key*")]
    [InlineData("openai", "*OpenAI*key*")]
    [InlineData("deepseek", "*DeepSeek*key*")]
    [InlineData("openrouter", "*OpenRouter*key*")]
    [InlineData("groq", "*Groq*key*")]
    [InlineData("together", "*Together*key*")]
    [InlineData("perplexity", "*Perplexity*key*")]
    public void CreateProvider_CloudProvider_WithoutKey_ThrowsInvalidOperation(string type, string messagePattern)
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var act = () => factory.CreateProvider(type);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(messagePattern);
    }

    // ── Unknown provider ────────────────────────────────────────────────────

    [Fact]
    public void CreateProvider_UnknownType_ThrowsNotSupported()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var act = () => factory.CreateProvider("unknown-provider");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*unknown-provider*");
    }

    // ── Caching ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateProvider_SameTypeAndModel_ReturnsCachedInstance()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var a = factory.CreateProvider("gemini", apiKey: "test-key", model: "gemini-2.5-flash");
        var b = factory.CreateProvider("gemini", apiKey: "test-key", model: "gemini-2.5-flash");

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void CreateProvider_DifferentModel_ReturnsDifferentInstance()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var a = factory.CreateProvider("gemini", apiKey: "test-key", model: "gemini-2.5-flash");
        var b = factory.CreateProvider("gemini", apiKey: "test-key", model: "gemini-2.5-pro");

        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void CreateProvider_DifferentType_ReturnsDifferentInstance()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var a = factory.CreateProvider("gemini", apiKey: "test-key");
        var b = factory.CreateProvider("openai", apiKey: "test-key");

        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void CreateProvider_Ollama_SameModel_ReturnsCachedInstance()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        var a = factory.CreateProvider("ollama", model: "gemma3:1b");
        var b = factory.CreateProvider("ollama", model: "gemma3:1b");

        a.Should().BeSameAs(b);
    }

    // ── Default model resolution ────────────────────────────────────────────

    [Fact]
    public void CreateProvider_Gemini_NoModel_UsesDefaultModel()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        // Should not throw — uses default model "gemini-2.5-flash"
        var provider = factory.CreateProvider("gemini", apiKey: "test-key");

        provider.Should().NotBeNull();
        provider.Should().BeOfType<GeminiProvider>();
    }

    [Fact]
    public void CreateProvider_Ollama_NoModel_UsesSettingsDefault()
    {
        var factory = new LLMProviderFactory(_secureStorage, _settings);

        // Default OllamaModel from AppSettings is "gemma3:1b"
        var provider = factory.CreateProvider("ollama");

        provider.Should().BeOfType<OllamaProvider>();
    }

    // ── NullLlmProvider behavior ────────────────────────────────────────────

    [Fact]
    public async Task NullLlmProvider_IsAvailable_ReturnsTrue()
    {
        var provider = new NullLlmProvider();

        var result = await provider.IsAvailableAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NullLlmProvider_ProcessAsync_ReturnsEmptyText()
    {
        var provider = new NullLlmProvider();

        var result = await provider.ProcessAsync("test input", "system prompt");

        result.Text.Should().BeEmpty();
        result.Provider.Should().Be("None");
    }

    [Fact]
    public void NullLlmProvider_ProviderName_IsNone()
    {
        var provider = new NullLlmProvider();

        provider.ProviderName.Should().Be("None");
    }
}
