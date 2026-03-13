# Text-to-Speech (TTS) Providers

dIKta.me implements a flexible, multi-tier execution strategy for Text-to-Speech (TTS) generation, enabling offline local inference via ONNX and high-quality cloud generation through a unified provider interface.

## Provider Architecture

All TTS engines implement the `ITTSProvider` interface, allowing the `TtsSpeaker` service to route requests seamlessly.

```csharp
public interface ITTSProvider : IDisposable
{
    string ProviderName { get; }
    bool SupportsStreaming { get; }

    /// <summary>Synthesize text to audio bytes (full generation).</summary>
    Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Check if the provider is ready (model loaded / API reachable).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
```

The `TtsResult` encapsulates the synthesized audio along with metadata, including the PCM/WAV byte array, sample rate, and generation latency.

## Available Providers

### KokoroTtsProvider (Local)
The default local provider utilizes `KokoroSharp`, wrapping the highly efficient Kokoro-ONNX models (82M parameters).
* **Execution:** Runs in-process with zero external dependencies.
* **Variants:** Supports `int8` (88 MB), `fp16` (169 MB), and `fp32` (310 MB) precision models via `Microsoft.ML.OnnxRuntime`.
* **Latency:** Generates speech typically between 25ms (GPU) and 500ms (CPU).
* **License compliance:** Kokoro's internal consumption of the GPLv3 `eSpeak-NG` phonemizer is isolated to process invocations under "mere aggregation," requiring only attribution.

### DeepgramTtsProvider (Cloud Default)
Leverages the existing `Deepgram` .NET SDK integration to execute the Aura-2 TTS models using the same API key deployed for the primary dictate STT operations. Provides 90-200ms TTFB across HTTP/WebSockets.

### InworldTtsProvider & OpenAITtsProvider (Premium Cloud)
Implements standard REST clients for external HTTP integrations using standard ASP.NET `HttpClient` patterns. Authentication keys are dynamically injected via DPAPI `SecureStorage`.

## Lifecycle and Routing

The `TTSProviderFactory` initializes and caches `ITTSProvider` instances to prevent redundant allocations or constant re-loading of local ONNX models.

During playback requests, the `TTSRouter` evaluates the active application `TtsSettings`. It dynamically falls back to alternate providers if the chosen API is unavailable or if the local model has failed to initialize.

### Audio Player Pipeline

Audio is subsequently streamed directly through the `TtsPlayerService` leveraging NAudio's `WasapiOut`. The player instance seamlessly synchronizes with the `AudioDucker` to drop the system volume of background applications during active playback. Playback can be interrupted instantaneously by evaluating hotkey states or application state transitions (e.g. initiating a new dictation instance).

### Text Sanitization

Raw Markdown from LLM responses is heavily sanitized via the static `TextCleaner` class before processing. This strips header (`#`) markers, transforms list bullets into pauses, expands common symbols (`$` to "dollars"), and aggressively truncates excessively long output boundaries prior to inference.
