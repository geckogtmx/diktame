# Large Language Model (LLM) Providers

The `DiktaMe.Core` architecture treats text-formatting AI services as interchangeable providers. Because we support massive Cloud models and lightweight Local models simultaneously, the formatting engine relies entirely on the `ILLMProvider` interface to bridge the gap.

If you wish to add support for a new Language Model endpoint (like Groq, TogetherAI, or Google Vertex), you simply need to implement the `ILLMProvider` interface.

## The Interface

```csharp
public interface ILLMProvider
{
    // The human-readable name of the provider (e.g. "Ollama Local")
    string Name { get; }

    // Ensures the API key exists or the local server is actually running
    Task<bool> IsReadyAsync();

    // The core execution loop for one-shot text replacement
    Task<string> ProcessTextAsync(
        string input, 
        string systemPrompt, 
        CancellationToken cancellationToken);

    // C# 8.0 Async Streams for yielding chat responses natively
    // Required to support the Quick Chat overlay
    IAsyncEnumerable<string> ProcessChatStreamAsync(
        IEnumerable<ChatMessage> history, 
        string systemPrompt, 
        CancellationToken cancellationToken);
}
```

*Examples in codebase*: `AnthropicProvider.cs`, `GeminiProvider.cs`, `OllamaProvider.cs`, `OpenAICompatProvider.cs`

---

## The LLM Router

Exactly like the STT architecture, Views and ViewModels never directly instantiate a provider. They request the `LLMRouter` singleton.

When a dictate pipeline is triggered, the `LLMRouter` determines whether the user is in "Cloud Mode" or "Local Mode" on the main Control Panel overlay. 

*   **Cloud Mode**: The Router reads the configured API provider (e.g., `Anthropic`), reads the user's selected Chat Model (e.g., `claude-3-5-sonnet-20240620`), and passes the execution to the `AnthropicProvider`.
*   **Local Mode**: The Router completely bypasses the BYOK settings and exclusively instantiates the `OllamaProvider`.

### Prompt Ingestion

Unlike STT, which just returns raw text, LLM providers require **System Prompts**. 

dIKta.me supports infinite custom modes, so the `LLMRouter` is also responsible for injecting the correct prompt schema. When a provider's `ProcessTextAsync()` is called, the Router ensures it passes the specific *Cloud Prompt* or *Local Prompt* attached to that Dictation Mode profile natively.

## Adding a New Provider

1.  Create `MyCustomLLMProvider.cs` in `src/DiktaMe.Core/LLM/`.
2.  Implement `ILLMProvider`. Focus heavily on handling HTTP 429 Rate Limits and 401 Unauthorized exceptions gracefully.
3.  Add your provider to the `LlmProviderType` enum.
4.  Register it inside `LLMProviderFactory.cs`.
5.  *(Dependency Injection)*: Register your new class as a Transient service in `App.xaml.cs`.
