# Architecture

**dIKta.me V2** follows a strict separation between the **UI Layer** (`DiktaMe.App`) and the **Business Logic** (`DiktaMe.Core`).

> **Full Specification**: See [`ARCHITECTURE.md`](https://github.com/geckogtmx/diktame/blob/main/ARCHITECTURE.md) in the repository root.

## Core Concepts

### 1. The Triad
- **Engine (App)**: The host process (WinUI 3).
- **Ears (STT)**: Speech-to-Text providers (Deepgram, Gemini, Whisper).
- **Brain (LLM)**: Large Language Models (Gemini, OpenAI, Anthropic, Ollama).

### 2. Dependency Injection
All services are defined in `DiktaMe.Core` interfaces and injected into `DiktaMe.App` ViewModels.

```csharp
// Example: IServiceProvider registration in App.xaml.cs
services.AddSingleton<ISTTProvider, DeepgramProvider>();
services.AddSingleton<ILLMProvider, GeminiProvider>();
```

### 3. Pipelines
Every user action (Dictate, Refine, Ask) is encapsulated in a **Pipeline**.
See [Pipeline API](api/pipeline.md) for details.
