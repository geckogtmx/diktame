using DiktaMe.Core.LLM;
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
}
