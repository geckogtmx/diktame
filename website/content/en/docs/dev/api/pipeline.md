# Pipeline Architecture

The **Pipeline** pattern orchestrates the flow of data from User Input -> Processing -> Output.

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

## Customizing Pipelines

To add a new mode (e.g., "Summarize Selection"), create a class inheriting from `BasePipeline` and register it in `App.xaml.cs`.
