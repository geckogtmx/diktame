# Large Language Model (LLM) Providers

The `DiktaMe.Core` architecture treats text-formatting AI services as interchangeable providers. Because we support massive Cloud models and lightweight Local models simultaneously, the formatting engine relies entirely on the `ILLMProvider` interface to bridge the gap.

If you wish to add support for a new Language Model endpoint (like Groq, TogetherAI, or Google Vertex), you simply need to implement the `ILLMProvider` interface.

## The Interface

```csharp
public interface ILLMProvider
{
    string Name { get; }

    // Health check — confirms API key exists or local server is reachable
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    // One-shot text formatting (Dictation, Ask, Refine, Translate, Note)
    Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default);

    // Multi-turn conversation (Quick Chat)
    Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        CancellationToken cancellationToken = default);

    // Multimodal — default throws NotSupportedException; providers opt in by overriding
    virtual Task<LlmResult> ProcessWithImageAsync(
        byte[] imageData,
        string mimeType,
        string text,
        string systemPrompt,
        string mode = "vision",
        CancellationToken cancellationToken = default);
}
```

`LlmResult` is a record with `Text`, `Provider`, `LatencyMs`, optional `InputTokens`, `OutputTokens`, `TokensPerSec`, and a computed `IsSuccess` property.

`ConversationTurn` is a record with `Role`, `Content`, and optional `ImageData`/`ImageMimeType` for attaching images to chat turns.

*Implementations*: `AnthropicProvider.cs`, `GeminiProvider.cs`, `OllamaProvider.cs`, `OpenAICompatibleProvider.cs`

### OpenAI-compatible providers

`OpenAICompatibleProvider` implements `ILLMProvider` for any endpoint that speaks the OpenAI Chat Completions spec (`POST {baseUrl}/v1/chat/completions`). Named constructors exist for each well-known service:

| Provider | Factory method | Default model | Notes |
|----------|---------------|---------------|-------|
| OpenAI | `ForOpenAI(key)` | `gpt-4o-mini` | api.openai.com |
| OpenRouter | `ForOpenRouter(key)` | `openai/gpt-4o-mini` | Routes to 200+ models; key prefix `sk-or-...` |
| Requesty | `ForRequesty(key)` | *(provider default)* | Unified gateway for 300+ models |
| DeepSeek | `ForDeepSeek(key)` | `deepseek-chat` | api.deepseek.com |
| Groq | `ForGroq(key)` | `llama-3.3-70b-versatile` | Fast inference |

To add a new OpenAI-compatible provider, use the generic constructor directly: `new OpenAICompatibleProvider(baseUrl, apiKey, model, name)` — no code changes needed.

### Multimodal support

`ProcessWithImageAsync` has a default virtual implementation that throws `NotSupportedException`. All four current providers override it to support image inputs for the Vision pipeline.

---

## The LLM Router

Exactly like the STT architecture, Views and ViewModels never directly instantiate a provider. They request the `LLMRouter` singleton.

When a dictate pipeline is triggered, the `LLMRouter` determines whether the user is in "Cloud Mode" or "Local Mode" on the main Control Panel overlay. 

*   **Cloud Mode**: The Router reads the configured API provider (e.g., `Anthropic`), reads the user's selected Chat Model (e.g., `claude-3-5-sonnet-20240620`), and passes the execution to the `AnthropicProvider`.
*   **Local Mode**: The Router completely bypasses the BYOK settings and exclusively instantiates the `OllamaProvider`.

### Prompt Ingestion

Unlike STT, which just returns raw text, LLM providers require **System Prompts**. 

dIKta.me supports infinite custom modes, so the `LLMRouter` is also responsible for injecting the correct prompt schema. When a provider's `ProcessAsync()` is called, the Router ensures it passes the specific *Cloud Prompt* or *Local Prompt* attached to that Dictation Mode profile natively.

## Adding a New Provider

### OpenAI-compatible endpoints (recommended path)

If the provider speaks the OpenAI Chat Completions spec, you don't need a new class:

```csharp
// In LLMProviderFactory.cs, add a new case:
"myprovider" => new OpenAICompatibleProvider(
    "https://api.myprovider.com",
    key ?? throw new InvalidOperationException("MyProvider API key not configured."),
    "my-model-name",
    "MyProvider"),
```

Then add the key name to `SecureStorage.ValidProviders` and wire up the UI in `ApiKeysSettingsViewModel`.

### Custom protocol providers

1.  Create `MyCustomLLMProvider.cs` in `src/DiktaMe.Core/LLM/`.
2.  Implement `ILLMProvider`. Handle HTTP 429 (rate limit) and 401 (unauthorized) gracefully — do not throw uncaught exceptions.
3.  Register it inside `LLMProviderFactory.cs` with a string key.
4.  Add the API key name to `SecureStorage.ValidProviders`.
5.  Wire up the save/delete commands in `ApiKeysSettingsViewModel` and the UI card in `AIEngineSettingsPage.xaml`.
