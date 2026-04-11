# Pipeline Architecture

The **Pipeline** pattern orchestrates the flow of data from User Input → Processing → Output.

## IPipeline Interface

All pipelines implement a common flow:

```csharp
public interface IPipeline
{
    Task ExecuteAsync(CancellationToken ct);
    event EventHandler<PipelineStatus> StatusChanged;
}
```

## Standard Dictation Flow (`DictationPipeline`)

1.  **Record**: `AudioRecorder` captures audio until silence or hotkey release.
2.  **Transcribe**: `ISTTProvider` converts audio to text.
3.  **Process**: `ILLMProvider` formats/cleans the text.
4.  **Inject**: `TextInjector` types/pastes the result into the active window.

Telemetry fields emitted after each run: `RecordingMs`, `TranscriptionMs`, `LlmMs`, `InjectionMs`, `TotalMs`, `WordCount`, `Wpm`.

## Wallet Streaming Pipeline (`WalletDictationPipeline`)

When `AuthMode.Wallet` is active, dIKta.me uses **Gemini Live** instead of the standard batch pipeline:

1.  **Stream audio**: A persistent WebSocket connection to the Gemini Live endpoint receives audio chunks in real time.
2.  **Receive transcript**: Gemini streams back partial transcripts. `TranscriptionMs` measures the gap between hotkey release and final transcript arrival.
3.  **Inject**: `TextInjector` pastes the result. No LLM step — Gemini Live handles both STT and optionally formatting in one pass.

This path bypasses all control panel settings (STT/LLM provider selection). `PipelineFactory.GetProviders()` checks `AuthMode.Wallet` first and returns wallet proxies before any mode-based resolution.

> Audio ducking, early stop sound, and the `Transcribing…` UI state are all present in the streaming path.

## Other Pipelines

| Class | Trigger | STT | LLM | Output |
|-------|---------|-----|-----|--------|
| `DictationPipeline` | Hotkey | ✓ | ✓ | Inject |
| `RefinePipeline` | Hotkey | ✓ | ✓ | Inject |
| `AskPipeline` | Hotkey | ✓ | ✓ | Toast/Clipboard/Inject |
| `TranslatePipeline` | Hotkey | ✓ | ✓ | Inject |
| `NotePipeline` | Hotkey | ✓ | ✓ | File |
| `ChatPipeline` | UI | ✗ | ✓ | Quick Chat window |
| `VisionPipeline` | UI | optional | ✓ (multimodal) | Inject/Clipboard |

## Customizing Pipelines

To add a new mode (e.g., "Summarize Selection"), create a class inheriting from `BasePipeline` and register it in `App.xaml.cs`.
