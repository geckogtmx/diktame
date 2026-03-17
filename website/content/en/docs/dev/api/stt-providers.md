# Speech-to-Text (STT) Providers

The `DiktaMe.Core` architecture treats all external AI services as hot-swappable plugins via strictly defined interfaces. 

If you want to add support for a new Speech-to-Text service (like Azure Speech or Google Cloud Speech-to-Text), you simply need to create a class that conforms to our `ISTTProvider` interface and register it inside the Dependency Injection framework.

## The Interfaces

There are two primary interfaces detailing what an STT service is capable of doing.

### 1. `ISTTProvider` (Batch Recognition)

This is the foundation. Any class implementing this is capable of receiving a monolithic byte array of WAV audio and returning a single, finalized transcript.

```csharp
public interface ISTTProvider
{
    // The human-readable name of the provider (e.g. "Whisper.net Local")
    string Name { get; }
    
    // Checks if API keys or offline models are successfully acquired
    Task<bool> IsReadyAsync();

    // The core execution loop
    Task<string> TranscribeAsync(byte[] wavData, string language, CancellationToken cancellationToken);
}
```

*Examples in codebase*: `WhisperProvider.cs`

### 2. `IStreamingSTTProvider` (Live Recognition)

If a provider supports real-time WebSocket ingestion (where transcriptions arrive sequentially while the user is still speaking), it should implement this interface which extends the base `ISTTProvider`.

```csharp
public interface IStreamingSTTProvider : ISTTProvider
{
    // C# 8.0 Async Streams for yielding text natively as it arrives over the wire
    IAsyncEnumerable<string> TranscribeStreamAsync(
        IAsyncEnumerable<byte[]> audioStream, 
        string language, 
        CancellationToken cancellationToken);
}
```

*Examples in codebase*: `DeepgramProvider.cs`, `GeminiAudioProvider.cs`

---

## The STT Router

dIKta.me does not hardcode provider implementations into the view logic. Instead, ViewModels request the singleton `STTRouter`.

The `STTRouter` acts as a traffic director. Its job is to read the user's `$App.Settings.ActiveSttProvider` configuration, reach into the DI container via the `ISTTProviderFactory`, and return the correct provider dynamically.

### Fallback Mechanism
Because we support Bring-Your-Own-Key (BYOK), the `STTRouter` has a vital safety mechanism built into its routing logic. 

If a user selects `Deepgram` as their provider but they have not actually saved an API key, the `IsReadyAsync()` check will fail. When this happens, the Router will **silently fall back** to the `WhisperProvider` (Local execution) so that the user's dictate hotkey doesn't just crash the app.

## Adding a New Provider

1.  Create `MyCustomSttProvider.cs` in `src/DiktaMe.Core/STT/`.
2.  Implement `ISTTProvider`.
3.  Add your provider to the `SttProviderType` selection enum.
4.  Register it logically inside the `STTProviderFactory.cs` switch statement. 
5.  *(Dependency Injection)*: Register your new class as a Transient service in `App.xaml.cs`.
