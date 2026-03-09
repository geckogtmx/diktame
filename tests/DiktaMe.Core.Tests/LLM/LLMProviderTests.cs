
using System.Net;
using System.Net.Http;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using Moq;

namespace DiktaMe.Core.Tests.LLM;
// ── Shared fake handler (scoped to LLM tests) ─────────────────────────────────

/// <summary>Stub HTTP handler that returns a fixed response.</summary>
internal sealed class LlmFakeHandler(HttpStatusCode statusCode, string body)
    : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }
    public string? LastAuthScheme { get; private set; }
    public string? LastAuthParameter { get; private set; }
    public string? LastApiKeyHeader { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAuthScheme = request.Headers.Authorization?.Scheme;
        LastAuthParameter = request.Headers.Authorization?.Parameter;
        if (request.Headers.TryGetValues("x-api-key", out var vals))
        {
            LastApiKeyHeader = string.Join(",", vals);
        }

        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
    }
}

// ── OpenAICompatibleProvider ──────────────────────────────────────────────────

public sealed class OpenAICompatibleProviderTests
{
    private const string OaiResponse = """
        {
          "choices": [{ "message": { "content": "cleaned text" } }],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5 }
        }
        """;

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OpenAICompatibleProvider("https://api.openai.com", "", "gpt-4o-mini"));
    }

    [Fact]
    public void Constructor_EmptyModel_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new OpenAICompatibleProvider("https://api.openai.com", "key", ""));
    }

    [Fact]
    public void ProviderName_IncludesModelAndService()
    {
        using var p = OpenAICompatibleProvider.ForOpenAI("key", "gpt-4o-mini");
        Assert.Contains("gpt-4o-mini", p.ProviderName);
        Assert.Contains("OpenAI", p.ProviderName);
    }

    [Fact]
    public void ForDeepSeek_ProviderName_ContainsDeepSeek()
    {
        using var p = OpenAICompatibleProvider.ForDeepSeek("key");
        Assert.Contains("DeepSeek", p.ProviderName);
    }

    [Fact]
    public void ForOpenRouter_ProviderName_ContainsOpenRouter()
    {
        using var p = OpenAICompatibleProvider.ForOpenRouter("key");
        Assert.Contains("OpenRouter", p.ProviderName);
    }

    [Fact]
    public async Task IsAvailable_WithKey_ReturnsTrue()
    {
        using var p = OpenAICompatibleProvider.ForOpenAI("key");
        Assert.True(await p.IsAvailableAsync());
    }

    [Fact]
    public async Task Process_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OaiResponse);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "key", "gpt-4o-mini", "OpenAI", http);

        var result = await p.ProcessAsync("hello", "You are a cleaner.");

        Assert.Equal("cleaned text", result.Text);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task Process_RequestHasBearerAuth()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OaiResponse);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "mykey", "gpt-4o-mini", "OpenAI", http);

        await p.ProcessAsync("text", "prompt");

        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("mykey", handler.LastAuthParameter);
    }

    [Fact]
    public async Task Process_EndpointUrlEndsWithChatCompletions()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OaiResponse);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.DeepSeekBaseUrl, "key", "deepseek-chat", "DeepSeek", http);

        await p.ProcessAsync("text", "prompt");

        Assert.EndsWith("/v1/chat/completions", handler.LastRequestUri?.AbsolutePath ?? "");
    }

    [Fact]
    public async Task Process_401_ThrowsInvalidOperation()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "bad", "gpt-4o-mini", "OpenAI", http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => p.ProcessAsync("text", "prompt"));
    }

    [Fact]
    public async Task Process_EmptyChoices_ReturnsEmptyText()
    {
        const string noChoices = """{"choices":[]}""";
        var handler = new LlmFakeHandler(HttpStatusCode.OK, noChoices);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "key", "gpt-4o-mini", "OpenAI", http);

        var result = await p.ProcessAsync("text", "prompt");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Process_InputSanitized_BackticksEscaped()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OaiResponse);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "key", "gpt-4o-mini", "OpenAI", http);

        await p.ProcessAsync("```inject```", "prompt");

        Assert.Contains("'''inject'''", handler.LastBody ?? "");
    }

    [Fact]
    public async Task ProcessConversation_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OaiResponse);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "key", "gpt-4o-mini", "OpenAI", http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi there"),
            new("user", "how are you?"),
        };

        var result = await p.ProcessConversationAsync(history, "You are helpful.");

        Assert.Equal("cleaned text", result.Text);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessConversation_BodyContainsSystemAndHistory()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OaiResponse);
        using var http = new HttpClient(handler);
        using var p = new OpenAICompatibleProvider(
            OpenAICompatibleProvider.OpenAIBaseUrl, "key", "gpt-4o-mini", "OpenAI", http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi"),
        };

        await p.ProcessConversationAsync(history, "Be helpful.");

        var body = handler.LastBody ?? "";
        Assert.Contains("\"role\":\"system\"", body);
        Assert.Contains("Be helpful.", body);
        Assert.Contains("\"role\":\"user\"", body);
        Assert.Contains("hello", body);
        Assert.Contains("\"role\":\"assistant\"", body);
        Assert.Contains("hi", body);
    }
}

// ── GeminiProvider ────────────────────────────────────────────────────────────

public sealed class GeminiProviderTests
{
    private const string GeminiResponse = """
        {
          "candidates": [{ "content": { "parts": [{ "text": "gemini output" }] } }],
          "usageMetadata": { "promptTokenCount": 8, "candidatesTokenCount": 4 }
        }
        """;

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeminiProvider(""));
    }

    [Fact]
    public void ProviderName_ContainsGeminiAndModel()
    {
        using var p = new GeminiProvider("key", "gemini-2.0-flash");
        Assert.Contains("Gemini", p.ProviderName);
        Assert.Contains("gemini-2.0-flash", p.ProviderName);
    }

    [Fact]
    public async Task Process_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var p = new GeminiProvider("aikey", httpClient: http);

        var result = await p.ProcessAsync("text", "prompt");

        Assert.Equal("gemini output", result.Text);
        Assert.Equal(8, result.InputTokens);
        Assert.Equal(4, result.OutputTokens);
    }

    [Fact]
    public async Task Process_ApiKeyInUrl()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var p = new GeminiProvider("my_gemini_key", httpClient: http);

        await p.ProcessAsync("text", "prompt");

        Assert.Contains("my_gemini_key", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Process_OAuthToken_UsesAuthorizationHeader()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var p = new GeminiProvider("ya29.fake_oauth_token", httpClient: http);

        await p.ProcessAsync("text", "prompt");

        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.DoesNotContain("ya29", handler.LastRequestUri?.Query ?? "");
    }

    [Fact]
    public async Task Process_401_ThrowsInvalidOperation()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var p = new GeminiProvider("bad_key", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => p.ProcessAsync("text", "prompt"));
    }

    [Fact]
    public async Task ProcessConversation_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var p = new GeminiProvider("aikey", httpClient: http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi there"),
        };

        var result = await p.ProcessConversationAsync(history, "Be helpful.");

        Assert.Equal("gemini output", result.Text);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessConversation_BodyUsesModelRoleNotAssistant()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, GeminiResponse);
        using var http = new HttpClient(handler);
        using var p = new GeminiProvider("aikey", httpClient: http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi there"),
        };

        await p.ProcessConversationAsync(history, "Be helpful.");

        var body = handler.LastBody ?? "";
        Assert.Contains("\"role\":\"model\"", body);
        Assert.Contains("systemInstruction", body);
        Assert.DoesNotContain("\"role\":\"assistant\"", body);
    }
}

// ── AnthropicProvider ─────────────────────────────────────────────────────────

public sealed class AnthropicProviderTests
{
    private const string AnthropicResponse = """
        {
          "content": [{ "text": "claude output" }],
          "usage": { "input_tokens": 12, "output_tokens": 6 }
        }
        """;

    [Fact]
    public void Constructor_EmptyApiKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AnthropicProvider(""));
    }

    [Fact]
    public void ProviderName_ContainsAnthropicAndModel()
    {
        using var p = new AnthropicProvider("key");
        Assert.Contains("Anthropic", p.ProviderName);
    }

    [Fact]
    public async Task Process_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, AnthropicResponse);
        using var http = new HttpClient(handler);
        using var p = new AnthropicProvider("sk-ant-key", httpClient: http);

        var result = await p.ProcessAsync("text", "prompt");

        Assert.Equal("claude output", result.Text);
        Assert.Equal(12, result.InputTokens);
        Assert.Equal(6, result.OutputTokens);
    }

    [Fact]
    public async Task Process_UsesXApiKeyHeader()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, AnthropicResponse);
        using var http = new HttpClient(handler);
        using var p = new AnthropicProvider("my_anthropic_key", httpClient: http);

        await p.ProcessAsync("text", "prompt");

        Assert.Equal("my_anthropic_key", handler.LastApiKeyHeader);
    }

    [Fact]
    public async Task Process_401_ThrowsInvalidOperation()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.Unauthorized, "");
        using var http = new HttpClient(handler);
        using var p = new AnthropicProvider("bad", httpClient: http);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => p.ProcessAsync("text", "prompt"));
    }

    [Fact]
    public async Task ProcessConversation_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, AnthropicResponse);
        using var http = new HttpClient(handler);
        using var p = new AnthropicProvider("sk-ant-key", httpClient: http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi"),
        };

        var result = await p.ProcessConversationAsync(history, "Be helpful.");

        Assert.Equal("claude output", result.Text);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessConversation_BodyHasSystemFieldAndMessages()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, AnthropicResponse);
        using var http = new HttpClient(handler);
        using var p = new AnthropicProvider("sk-ant-key", httpClient: http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi"),
        };

        await p.ProcessConversationAsync(history, "Be helpful.");

        var body = handler.LastBody ?? "";
        Assert.Contains("\"system\":\"Be helpful.\"", body);
        Assert.Contains("\"role\":\"user\"", body);
        Assert.Contains("\"role\":\"assistant\"", body);
    }
}

// ── OllamaProvider ────────────────────────────────────────────────────────────

public sealed class OllamaProviderTests
{
    private const string OllamaResponse = """
        {
          "response": "ollama output",
          "eval_count": 20,
          "eval_duration": 1000000000
        }
        """;

    private const string TagsResponse = """{"models":[{"name":"llama3.2"}]}""";

    [Fact]
    public void Constructor_EmptyModel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new OllamaProvider(""));
    }

    [Fact]
    public void ProviderName_ContainsOllamaAndModel()
    {
        using var p = new OllamaProvider("llama3.2");
        Assert.Contains("Ollama", p.ProviderName);
        Assert.Contains("llama3.2", p.ProviderName);
    }

    [Fact]
    public async Task IsAvailable_ServerUp_ReturnsTrue()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, TagsResponse);
        using var http = new HttpClient(handler);
        using var p = new OllamaProvider("llama3.2", httpClient: http);

        Assert.True(await p.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailable_ServerDown_ReturnsFalse()
    {
        // Simulate unreachable server with a bad URL (exception path)
        using var p = new OllamaProvider("llama3.2", "http://127.0.0.1:1");
        Assert.False(await p.IsAvailableAsync());
    }

    [Fact]
    public async Task Process_ValidResponse_ReturnsText()
    {
        var handler = new LlmFakeHandler(HttpStatusCode.OK, OllamaResponse);
        using var http = new HttpClient(handler);
        using var p = new OllamaProvider("llama3.2", httpClient: http);

        var result = await p.ProcessAsync("text", "prompt");

        Assert.Equal("ollama output", result.Text);
        Assert.True(result.IsSuccess);
        // 20 tokens in 1s = 20 tok/s
        Assert.NotNull(p.LastTokensPerSec);
        Assert.Equal(20.0, p.LastTokensPerSec!.Value, precision: 1);
    }

    [Fact]
    public async Task Process_EmptyResponse_ReturnsEmptyText()
    {
        const string emptyOllama = """{"response":""}""";
        var handler = new LlmFakeHandler(HttpStatusCode.OK, emptyOllama);
        using var http = new HttpClient(handler);
        using var p = new OllamaProvider("llama3.2", httpClient: http);

        var result = await p.ProcessAsync("text", "prompt");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessConversation_ValidResponse_ReturnsText()
    {
        const string chatResponse = """{"message":{"role":"assistant","content":"ollama chat output"}}""";
        var handler = new LlmFakeHandler(HttpStatusCode.OK, chatResponse);
        using var http = new HttpClient(handler);
        using var p = new OllamaProvider("llama3.2", httpClient: http);

        var history = new List<ConversationTurn>
        {
            new("user", "hello"),
            new("assistant", "hi"),
            new("user", "how are you?"),
        };

        var result = await p.ProcessConversationAsync(history, "Be helpful.");

        Assert.Equal("ollama chat output", result.Text);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessConversation_UsesChatEndpoint()
    {
        const string chatResponse = """{"message":{"role":"assistant","content":"output"}}""";
        var handler = new LlmFakeHandler(HttpStatusCode.OK, chatResponse);
        using var http = new HttpClient(handler);
        using var p = new OllamaProvider("llama3.2", httpClient: http);

        var history = new List<ConversationTurn> { new("user", "hi") };
        await p.ProcessConversationAsync(history, "prompt");

        Assert.Contains("/api/chat", handler.LastRequestUri?.AbsolutePath ?? "");
    }
}

// ── LLMRouter ─────────────────────────────────────────────────────────────────

public sealed class LLMRouterTests
{
    private static LlmResult OkResult(string text, string provider) => new()
    {
        Text = text,
        Provider = provider,
        LatencyMs = 5
    };

    private static LlmResult EmptyResult(string provider) => new()
    {
        Text = string.Empty,
        Provider = provider,
        LatencyMs = 0
    };

    private static Mock<ILLMProviderFactory> MockFactory() => new();

    [Fact]
    public void ProviderName_NullFallback_ReturnsPrimaryName()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        var router = new LLMRouter(primary.Object, MockFactory().Object);
        Assert.Equal("Primary", router.ProviderName);
    }

    [Fact]
    public void ProviderName_WithFallback_IncludesBoth()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        var fallback = new Mock<ILLMProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        var router = new LLMRouter(primary.Object, MockFactory().Object, fallback.Object);
        Assert.Contains("Primary", router.ProviderName);
        Assert.Contains("Fallback", router.ProviderName);
    }

    [Fact]
    public async Task Process_PrimarySucceeds_ReturnsPrimaryResult()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
               .ReturnsAsync(OkResult("primary output", "Primary"));
        var router = new LLMRouter(primary.Object, MockFactory().Object);

        var result = await router.ProcessAsync("text", "prompt");

        Assert.Equal("primary output", result.Text);
    }

    [Fact]
    public async Task Process_PrimaryReturnsEmpty_UsesFallback()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
               .ReturnsAsync(EmptyResult("Primary"));

        var fallback = new Mock<ILLMProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("fallback output", "Fallback"));

        var router = new LLMRouter(primary.Object, MockFactory().Object, fallback.Object);

        var result = await router.ProcessAsync("text", "prompt");

        Assert.Equal("fallback output", result.Text);
    }

    [Fact]
    public async Task Process_PrimaryThrows_UsesFallback()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("network error"));

        var fallback = new Mock<ILLMProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("fallback output", "Fallback"));

        var router = new LLMRouter(primary.Object, MockFactory().Object, fallback.Object);

        var result = await router.ProcessAsync("text", "prompt");

        Assert.Equal("fallback output", result.Text);
    }

    [Fact]
    public async Task Process_BothFail_ReturnsEmptyResult()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException());

        var fallback = new Mock<ILLMProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException());

        var router = new LLMRouter(primary.Object, MockFactory().Object, fallback.Object);

        var result = await router.ProcessAsync("text", "prompt");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Text);
    }

    [Fact]
    public async Task IsAvailable_PrimaryAvailable_ReturnsTrue()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var router = new LLMRouter(primary.Object, MockFactory().Object);
        Assert.True(await router.IsAvailableAsync());
    }

    [Fact]
    public async Task ProcessWithModel_DelegatesToFactory()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");

        var resolved = new Mock<ILLMProvider>();
        resolved.Setup(p => p.ProviderName).Returns("OpenAI");
        resolved.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(OkResult("model output", "OpenAI"));

        var factory = new Mock<ILLMProviderFactory>();
        factory.Setup(f => f.CreateProvider("openai", null, "gpt-4o-mini")).Returns(resolved.Object);

        var router = new LLMRouter(primary.Object, factory.Object);
        var result = await router.ProcessAsync("text", "prompt", modelName: "gpt-4o-mini");

        Assert.Equal("model output", result.Text);
        factory.Verify(f => f.CreateProvider("openai", null, "gpt-4o-mini"), Times.Once);
    }

    [Fact]
    public async Task ProcessWithModel_NullModelName_UsesPrimary()
    {
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProcessAsync("text", "prompt", "dictate", It.IsAny<CancellationToken>()))
               .ReturnsAsync(OkResult("primary output", "Primary"));

        var factory = new Mock<ILLMProviderFactory>();
        var router = new LLMRouter(primary.Object, factory.Object);

        var result = await router.ProcessAsync("text", "prompt", modelName: null);

        Assert.Equal("primary output", result.Text);
        factory.Verify(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ProcessConversation_PrimarySucceeds_ReturnsPrimaryResult()
    {
        var history = new List<ConversationTurn> { new("user", "hi") };
        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProcessConversationAsync(history, "prompt", "chat", It.IsAny<CancellationToken>()))
               .ReturnsAsync(OkResult("primary chat", "Primary"));
        var router = new LLMRouter(primary.Object, MockFactory().Object);

        var result = await router.ProcessConversationAsync(history, "prompt");

        Assert.Equal("primary chat", result.Text);
    }

    [Fact]
    public async Task ProcessConversation_PrimaryFails_UsesFallback()
    {
        var history = new List<ConversationTurn> { new("user", "hi") };

        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("primary failed"));

        var fallback = new Mock<ILLMProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(OkResult("fallback chat", "Fallback"));

        var router = new LLMRouter(primary.Object, MockFactory().Object, fallback.Object);
        var result = await router.ProcessConversationAsync(history, "prompt");

        Assert.Equal("fallback chat", result.Text);
    }

    [Fact]
    public async Task ProcessConversation_BothFail_ReturnsEmptyResult()
    {
        var history = new List<ConversationTurn> { new("user", "hi") };

        var primary = new Mock<ILLMProvider>();
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException());

        var fallback = new Mock<ILLMProvider>();
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.ProcessConversationAsync(
                It.IsAny<IReadOnlyList<ConversationTurn>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException());

        var router = new LLMRouter(primary.Object, MockFactory().Object, fallback.Object);
        var result = await router.ProcessConversationAsync(history, "prompt");

        Assert.False(result.IsSuccess);
    }
}

// ── LlmResult ─────────────────────────────────────────────────────────────────

public sealed class LlmResultTests
{
    [Fact]
    public void IsSuccess_NonEmptyText_ReturnsTrue()
    {
        var r = new LlmResult { Text = "output", Provider = "test" };
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void IsSuccess_EmptyText_ReturnsFalse()
    {
        var r = new LlmResult { Text = string.Empty, Provider = "test" };
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void IsSuccess_WhitespaceText_ReturnsFalse()
    {
        var r = new LlmResult { Text = "   ", Provider = "test" };
        Assert.False(r.IsSuccess);
    }
}
