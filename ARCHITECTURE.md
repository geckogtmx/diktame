# Architecture

Technical design document for **dIKta.me V2** — the C# + WinUI 3 rewrite.

> **Note**: This document reflects the V2 architecture. For V1 (Electron + Python), see `E:\git\diktate\ARCHITECTURE.md`.

---

## 1. System Overview

dIKta.me V2 is a **native Windows desktop application** built as a single-process architecture using C# and WinUI 3. It replaces the dual-process Electron + Python V1 with a unified, high-performance binary.

### The Quad Architecture (Evolving)

```
┌─────────────────────────────────────────────────────────────────┐
│                         User                                    │
│  (Hotkeys: Dictate, Ask, Translate, Refine, Oops, Note, Read)   │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│                  ENGINE  (DiktaMe.App)                           │
│              C# + WinUI 3 — Single Process                      │
│                                                                 │
│  ┌─ UI Layer (WinUI 3 / XAML) ─────────────────────────────┐   │
│  │ TrayIcon  │ ControlPanel │ Settings │ Wizard │ QuickChat│   │
│  └──────────────────────────────────────────────────────────┘   │
│                      │ MVVM Binding                             │
│  ┌─ ViewModels ─────────────────────────────────────────────┐   │
│  │ ControlPanelVM  │ SettingsVM  │ WizardVM  │ QuickChatVM  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                      │ Dependency Injection                     │
│  ┌─ Core Services (DiktaMe.Core) ──────────────────────────┐   │
│  │                                                          │   │
│  │  ┌── Pipeline Orchestration ──────────────────────┐      │   │
│  │  │ Dictation │ Refine │ Ask │ Translate │ Note    │      │   │
│  │  │ Streaming │ Chat   │ Read Selection (TTS)       │      │   │
│  │  └────────────┬──────────────┬──────────────┬─────┘      │   │
│  │               │              │              │            │   │
│  │  ┌── EARS ────┴──┐  ┌── BRAIN ──┴──┐  ┌── MOUTH ──┴──┐      │   │
│  │  │ STT Router    │  │ LLM Router   │  │ TTS Router   │      │   │
│  │  │ ┌───────────┐ │  │ ┌──────────┐ │  │ ┌──────────┐ │      │   │
│  │  │ │ Deepgram  │ │  │ │ Gemini   │ │  │ │ Kokoro   │ │      │   │
│  │  │ │ Gemini    │ │  │ │ Anthropic│ │  │ │ OpenAI   │ │      │   │
│  │  │ │ Whisper   │ │  │ │ OpenAI   │ │  │ │ Deepgram │ │      │   │
│  │  │ └───────────┘ │  │ │ Ollama   │ │  │ └──────────┘ │      │   │
│  │  └───────────────┘  │ └──────────┘ │  └──────────────┘      │   │
│  │                     └──────┬───────┘                        │   │
│  │                            │                                │   │
│  │  ┌── Account & Trial ──────┴──────────────────────┐      │   │
│  │  │ TrialProxy │ AuthManager │ JWT/Deeplink Handler │      │   │
│  │  └────────────────────────────────────────────────┘      │   │
│  │                                                          │   │
│  │  ┌── System Services ────────────────────────────┐      │   │
│  │  │ AudioRecorder │ TextInjector │ HotkeyManager  │      │   │
│  │  │ MuteDetector  │ AudioDucker  │ ClipboardMgr   │      │   │
│  │  │ SettingsMgr   │ HistoryMgr   │ SecureStorage   │      │   │
│  │  │ SnippetMgr    │ ModeManager  │ OllamaManager   │      │   │
│  │  │ PipelineMgr   │ TtsPlayer    │ AudioMonitor    │      │   │
│  │  └───────────────────────────────────────────────┘      │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### V1 → V2 Comparison

| Factor | V1 (Electron + Python) | V2 (C# + WinUI 3) |
|--------|:-:|:-:|
| Process model | 2+ processes + JSON IPC | **Single process** |
| Installer size | ~100-200MB | **~70MB** (self-contained, compressed) |
| Memory footprint | ~300MB | **~50-80MB** |
| Startup time | 10-12s (model warmup) | **< 3s** (cloud mode) |
| Windows integration | Wrappers (pycaw, pynput) | **Native APIs** |
| UI framework | Electron (Chromium) | **WinUI 3 (Fluent Design)** |

---

## 2. Solution Structure

```
DiktaMe.sln                        # Visual Studio solution
├── Directory.Build.props           # Shared build settings (C# 12, nullable, V2.0.0)
├── .editorconfig                   # Code style rules
│
├── src/
│   ├── DiktaMe.App/                # WinUI 3 application (UI layer)
│   │   ├── App.xaml(.cs)           # App lifecycle, DI container, Serilog init
│   │   ├── MainWindow.xaml(.cs)    # Primary application window
│   │   ├── app.manifest            # Windows DPI + compatibility manifest
│   │   ├── Views/                   # XAML pages
│   │   │   ├── TrayIconView        # System tray (H.NotifyIcon)
│   │   │   ├── ControlPanelView    # Debug dashboard
│   │   │   ├── SettingsView        # Tabbed settings window
│   │   │   ├── WizardView          # First-run setup wizard
│   │   │   ├── QuickChatView       # Floating LLM chat overlay
│   │   │   └── LoadingView         # Startup loading screen
│   │   ├── ViewModels/              # MVVM ViewModels (CommunityToolkit.Mvvm)
│   │   ├── Converters/              # XAML value converters
│   │   └── Assets/                  # Icons, sounds, images
│   │
│   └── DiktaMe.Core/               # Business logic (class library)
│       ├── Audio/
│       │   ├── AudioRecorder.cs        # NAudio capture (16kHz, 16-bit mono)
│       │   ├── AudioDeviceManager.cs   # Device enumeration + fuzzy-match
│       │   ├── AudioLevelMonitor.cs    # Real-time dB metering for UI
│       │   ├── IAudioDataSource.cs     # Audio abstraction interface
│       │   ├── MuteDetector.cs         # Hardware mute monitoring (CoreAudio COM)
│       │   └── AudioDucker.cs          # WASAPI audio ducking during recording
│       ├── STT/
│       │   ├── ISTTProvider.cs         # Interface + TranscriptionResult
│       │   ├── IStreamingSTTProvider.cs # Interface for real-time WebSocket STT
│       │   ├── STTRouter.cs            # Routes to cloud/local with fallback
│       │   ├── DeepgramProvider.cs     # Cloud STT (Nova-2 REST API)
│       │   ├── DeepgramStreamingProvider.cs # Cloud STT (Nova-2 WebSocket streaming)
│       │   ├── GeminiAudioProvider.cs  # Cloud STT (Gemini multimodal)
│       │   ├── WhisperProvider.cs      # Local STT (Whisper.net / ONNX)
│       │   ├── IWebSocketClient.cs     # WebSocket abstraction for testability
│       │   └── SystemWebSocketClient.cs # Concrete WebSocket implementation
│       ├── LLM/
│       │   ├── ILLMProvider.cs              # Interface + LlmResult record
│       │   ├── LLMRouter.cs                 # Primary/fallback routing
│       │   ├── OpenAICompatibleProvider.cs  # OpenAI, DeepSeek, OpenRouter, Groq,
│       │   │                                #   Together, Fireworks, Perplexity,
│       │   │                                #   Azure OpenAI, LM Studio, vLLM, etc.
│       │   ├── GeminiProvider.cs            # Gemini generateContent (API key + OAuth)
│       │   ├── AnthropicProvider.cs         # Anthropic Messages API
│       │   ├── OllamaProvider.cs            # Local Ollama (localhost:11434)
│       │   ├── ModelInfo.cs                 # Model metadata record
│       │   └── ModelListService.cs          # Dynamic model listing from providers
│       ├── Pipeline/
│       │   ├── DictationPipeline.cs    # STT → LLM (optional) → Inject; raw mode bypass
│       │   ├── StreamingDictationPipeline.cs # Real-time WebSocket dictation
│       │   ├── RefinePipeline.cs       # Autopilot (selection→LLM→replace) + instruction mode
│       │   ├── AskPipeline.cs          # Voice Q&A — answer returned, not injected
│       │   ├── TranslatePipeline.cs    # STT (auto-detect lang) → LLM translate → Inject
│       │   ├── NotePipeline.cs         # STT → LLM format → append to .md file
│       │   ├── ReadSelectionPipeline.cs # Text → TTS playback
│       │   ├── ChatPipeline.cs         # Quick Chat overlay flow (text + voice → LLM)
│       │   ├── PipelineResult.cs       # Shared result: text, latencies, word count
│       │   ├── PipelineState.cs        # Idle/Transcribing/Processing/Injecting/Error
│       │   └── PipelineOptions.cs      # Typed options records per pipeline
│       ├── TTS/
│       │   ├── ITTSProvider.cs         # Interface for TTS providers
│       │   ├── ITtsPlayerService.cs    # Interface for player service
│       │   ├── TTSRouter.cs            # Routes playback to active provider
│       │   ├── TtsPlayerService.cs     # Manages playback queue and audio hardware
│       │   ├── TtsSpeaker.cs           # Voice management and selection
│       │   ├── TtsResult.cs            # TTS result record
│       │   ├── TextCleaner.cs          # Pre-TTS text normalization
│       │   ├── KokoroTtsProvider.cs    # Local TTS (KokoroSharp / ONNX)
│       │   ├── KokoroModelManager.cs   # Model download + initialization
│       │   ├── InworldTtsProvider.cs   # Cloud TTS (Inworld)
│       │   ├── DeepgramTtsProvider.cs  # Cloud TTS (Deepgram Aura-2)
│       │   └── OpenAITtsProvider.cs    # Cloud TTS (OpenAI tts-1)
│       ├── Account/
│       │   ├── IAccountService.cs      # Authentication interface
│       │   ├── ITrialAccountService.cs # Trial management interface
│       │   ├── ITrialService.cs        # Trial service interface
│       │   ├── TrialAccountService.cs  # Managed credit tracking
│       │   ├── TrialGeminiProvider.cs  # Trial proxy for Gemini LLM
│       │   ├── TrialGeminiAudioProvider.cs # Trial proxy for Gemini STT
│       │   ├── TrialStatus.cs          # Trial status record
│       │   └── JwtDecoder.cs           # Deeplink token processing
│       ├── Input/
│       │   ├── HotkeyManager.cs        # Win32 RegisterHotKey P/Invoke
│       │   ├── HotkeyParser.cs         # String-to-key combo parser
│       │   ├── TextInjector.cs         # Clipboard inject; LastInjectedText/ReInjectLast (Oops)
│       │   └── ClipboardManager.cs     # Clipboard save/restore
│       ├── Config/
│       │   ├── AppSettings.cs          # Strongly-typed settings model
│       │   ├── AccountSettings.cs      # Account-specific settings
│       │   ├── AuthMode.cs             # Authentication mode enum
│       │   ├── TrialSettings.cs        # Trial configuration
│       │   ├── SettingsManager.cs      # JSON persistence + migration
│       │   ├── DictationMode.cs        # Mode record + DictationProfile
│       │   ├── DictationModeDefaults.cs # Factory for built-in modes
│       │   ├── DictationModeManager.cs # CRUD for user dictation modes
│       │   ├── PipelineConfig.cs       # Utility pipeline config record
│       │   ├── PipelineConfigManager.cs # CRUD for utility pipeline configs
│       │   ├── ProfileManager.cs       # Dual-profile system (Cloud/Local)
│       │   ├── PromptDefaults.cs       # Built-in system prompts
│       │   ├── PromptRepository.cs     # 16 custom system prompt slots
│       │   ├── SnippetManager.cs       # Voice snippets CRUD + matching
│       │   ├── ISTTProviderFactory.cs  # STT factory interface
│       │   ├── STTProviderFactory.cs   # Instantiate STT from config
│       │   ├── ILLMProviderFactory.cs  # LLM factory interface
│       │   ├── LLMProviderFactory.cs   # Instantiate LLM from config
│       │   ├── ITTSProviderFactory.cs  # TTS factory interface
│       │   ├── TTSProviderFactory.cs   # Instantiate TTS from config
│       │   └── PipelineFactory.cs      # Instantiate pipelines from config
│       ├── Data/
│       │   ├── HistoryManager.cs       # SQLite session logging (90-day)
│       │   ├── ConversationManager.cs  # Multi-turn chat persistence
│       │   ├── ConversationRecord.cs   # Conversation data record
│       │   ├── MetricsCollector.cs     # Performance tracking
│       │   └── NoteWriter.cs           # File-based note appending
│       ├── Security/
│       │   ├── PIIScrubber.cs          # Regex-based PII redaction
│       │   ├── SecureStorage.cs        # DPAPI for API keys
│       │   └── ApiKeyValidator.cs      # Format validation per provider
│       └── System/
│           ├── OllamaManager.cs        # Version sensing + model library
│           ├── OllamaSearchService.cs  # Ollama installation discovery
│           ├── HardwareInfoService.cs  # GPU/hardware detection
│           └── models.json             # Embedded model metadata
│
└── tests/
    └── DiktaMe.Core.Tests/             # xUnit + Moq + FluentAssertions (961 tests)
        ├── ScaffoldTests.cs
        ├── LocalizationTests.cs
        ├── Account/
        │   ├── JwtDecoderTests.cs
        │   ├── TrialAccountServiceTests.cs
        │   ├── TrialGeminiAudioProviderTests.cs
        │   └── TrialGeminiProviderTests.cs
        ├── Audio/
        │   ├── AudioRecorderTests.cs
        │   ├── AudioDeviceManagerTests.cs
        │   ├── AudioDuckerTests.cs
        │   ├── AudioLevelMonitorTests.cs
        │   └── MuteDetectorTests.cs
        ├── Config/
        │   ├── DictationModeDefaultsTests.cs
        │   ├── DictationModeManagerTests.cs
        │   ├── PipelineFactoryTests.cs
        │   ├── ProfileManagerTests.cs
        │   ├── PromptDefaultsTests.cs
        │   ├── PromptRepositoryTests.cs
        │   ├── SettingsManagerTests.cs
        │   └── SnippetManagerTests.cs
        ├── Data/
        │   ├── ConversationManagerTests.cs
        │   ├── HistoryManagerTests.cs
        │   ├── MetricsCollectorTests.cs
        │   └── NoteWriterTests.cs
        ├── Input/
        │   ├── ClipboardManagerTests.cs
        │   ├── HotkeyManagerTests.cs
        │   ├── HotkeyParserTests.cs
        │   └── TextInjectorTests.cs    # [Trait("Category","Hardware")] — CI-excluded
        ├── STT/
        │   ├── STTRouterTests.cs
        │   ├── DeepgramProviderTests.cs
        │   ├── DeepgramStreamingProviderTests.cs
        │   ├── GeminiAudioProviderTests.cs
        │   ├── StreamingSTTEventArgsTests.cs
        │   └── WhisperProviderTests.cs
        ├── LLM/
        │   ├── LLMProviderTests.cs     # All providers + router + LlmResult
        │   ├── LLMRouterTrialTests.cs  # Trial proxy integration
        │   └── ModelListServiceTests.cs
        ├── Pipeline/
        │   ├── PipelineTests.cs        # Dictation, Refine, Ask, Translate, Note, Oops
        │   ├── ChatPipelineTests.cs
        │   ├── ReadSelectionPipelineTests.cs
        │   └── StreamingDictationPipelineTests.cs
        ├── Security/
        │   ├── ApiKeyValidatorTests.cs
        │   ├── PIIScrubberTests.cs
        │   └── SecureStorageTests.cs
        ├── System/
        │   ├── OllamaManagerTests.cs
        │   └── OllamaSearchServiceTests.cs
        └── TTS/
            ├── TTSRouterTests.cs
            ├── TTSProviderFactoryTests.cs
            ├── TtsPlayerServiceTests.cs
            ├── TtsSpeakerTests.cs
            ├── TextCleanerTests.cs
            ├── KokoroTtsProviderTests.cs
            ├── KokoroModelManagerTests.cs
            ├── InworldTtsProviderTests.cs
            ├── DeepgramTtsProviderTests.cs
            └── OpenAITtsProviderTests.cs
```

### Project Dependencies

```
DiktaMe.App ──references──▶ DiktaMe.Core
DiktaMe.Core.Tests ──references──▶ DiktaMe.Core
```

The `App` project depends on `Core` for all business logic. `Core` has **zero dependency on the UI layer** — it is a pure class library that could be consumed by any .NET host (CLI, service, etc.).

---

## 3. Technology Stack

### Runtime & Build

| Component | Technology | Version |
|-----------|-----------|---------|
| **Runtime** | .NET | 8.0 |
| **Target OS** | Windows 10+ | 10.0.19041 (2004) minimum |
| **Language** | C# | 12.0 |
| **UI Framework** | WinUI 3 (Windows App SDK) | 1.6 |
| **MVVM Toolkit** | CommunityToolkit.Mvvm | 8.3.2 |
| **Packaging** | Unpackaged (WindowsPackageType=None) | — |

### NuGet Dependencies

#### DiktaMe.Core (Business Logic)

| Package | Purpose | V1 Equivalent |
|---------|---------|---------------|
| `NAudio` 2.x + `NAudio.Wasapi` 2.x | Audio capture, device management, CoreAudio COM | `pyaudio`, `pycaw` |
| `InputSimulatorStandard` 1.x | Keyboard simulation, clipboard injection | `pynput` |
| `Microsoft.Data.Sqlite` 8.x | History database, metrics persistence | `sqlite3` |
| `Serilog` 3.x + `Serilog.Sinks.File` 5.x | Structured logging with daily rotation | Python `logging` |
| `System.Security.Cryptography.ProtectedData` 8.x | DPAPI encryption for API keys | Electron `safeStorage` |
| `Microsoft.Extensions.DI.Abstractions` 8.x | DI interface contracts | N/A (manual) |
| `KokoroSharp.CPU` 0.6.5 | Local ONNX-based Text-to-Speech | N/A (new in V2) |
| `Whisper.net` 1.9.0 | Local STT (Whisper ONNX) | `faster-whisper` |
| `Whisper.net.Runtime` 1.9.0 | Whisper native runtime | — |
| `Whisper.net.Runtime.Vulkan` 1.9.0 | GPU acceleration via Vulkan | — |
| `HtmlAgilityPack` 1.x | HTML parsing for web content | N/A |
| `System.Management` 8.x | WMI queries for hardware detection | N/A |

#### DiktaMe.App (UI Layer)

| Package | Purpose |
|---------|---------|
| `Microsoft.WindowsAppSDK` 1.6 | WinUI 3 runtime |
| `Microsoft.Windows.SDK.BuildTools` 10.x | Windows SDK build integration |
| `H.NotifyIcon.WinUI` 2.1.0 | System tray icon + context menu |
| `CommunityToolkit.Mvvm` 8.3.2 | ObservableObject, RelayCommand, source generators |
| `CommunityToolkit.WinUI.UI.Controls.Markdown` 7.1.2 | Native markdown rendering for Chat UI |
| `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 | Toast notifications |
| `Microsoft.Extensions.DependencyInjection` 8.0.1 | DI container |
| `WinUI3Localizer` 2.3.x | Dynamic i18n and localization |
| `Serilog.Sinks.Console` 5.x | Console logging for debug |

#### DiktaMe.Core.Tests (Testing)

| Package | Purpose |
|---------|---------|
| `xunit` 2.x | Test framework |
| `Moq` 4.x | Mocking (interfaces, HTTP, hardware) |
| `FluentAssertions` 6.x | Expressive assertion syntax |
| `coverlet.collector` 6.x | Code coverage collection |

---

## 4. Design Patterns

### 4.1 MVVM (Model-View-ViewModel)

WinUI 3 standard pattern using `CommunityToolkit.Mvvm` source generators:

```
View (XAML)  ◄──── Data Binding ────►  ViewModel  ────►  Core Services
                                       (ObservableObject)
```

- **Views** are pure XAML — no business logic
- **ViewModels** expose observable properties and commands
- **Core Services** handle all business logic (DiktaMe.Core)
- Binding via `{x:Bind}` for compile-time safety

### 4.2 Dependency Injection

All services are registered in `App.xaml.cs` and injected via constructor:

```csharp
// App.xaml.cs — service registration
private static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<SettingsManager>();
    services.AddSingleton<AudioRecorder>();
    services.AddSingleton<ISTTProvider, DeepgramProvider>();
    services.AddSingleton<ILLMProvider, GeminiProvider>();
    // ... etc
}
```

This makes unit testing trivial — every dependency is mockable via interfaces.

### 4.3 Interface-First Providers

The key abstraction enabling hot-swapping between cloud and local providers:

```csharp
// Same interface regardless of whether it's Deepgram, Gemini, or Whisper
public interface ISTTProvider
{
    Task<TranscriptionResult> TranscribeAsync(string audioFilePath, string language = "en");
    Task<bool> IsAvailableAsync();
    string ProviderName { get; }
}

// Streaming variant for real-time transcription via WebSocket
public interface IStreamingSTTProvider
{
    Task StartStreamingAsync(/* ... */);
    Task StopStreamingAsync();
    event EventHandler<StreamingSTTEventArgs> PartialResultReceived;
}

public interface ILLMProvider
{
    Task<LlmResult> ProcessAsync(string text, string systemPrompt, string mode = "dictate");
    Task<bool> IsAvailableAsync();
    string ProviderName { get; }
}

public interface ITTSProvider
{
    Task<TtsResult> SynthesizeAsync(string text, string voiceId, CancellationToken ct);
    Task<bool> IsAvailableAsync();
    string ProviderName { get; }
}
```

Routers (`STTRouter`, `LLMRouter`, `TTSRouter`) select the active provider based on user configuration and handle automatic fallback.

### 4.4 Pipeline Orchestration

Each workflow mode has a dedicated pipeline class that orchestrates the full flow:

```
DictationPipeline:          Record → STT → [LLM cleanup] → Inject
StreamingDictationPipeline: Mic → WebSocket STT → Live inject (real-time)
RefinePipeline:             Capture Selection → [Record instruction] → STT → LLM → Replace
AskPipeline:                Record → STT → LLM (Q&A) → Output [+ optional TTS]
TranslatePipeline:          Record → STT (auto-detect) → LLM (translate) → Inject [+ optional TTS]
NotePipeline:               Record → STT → [LLM cleanup] → Append to file
ReadSelectionPipeline:      Capture Selection → TextCleaner → TTS → Audio playback
ChatPipeline:               Text/Voice input → LLM → Display in overlay [+ optional TTS]
```

Pipelines emit progress events (`Recording`, `Transcribing`, `Processing`, `Injecting`) for UI feedback.

### 4.5 CRUD Dictation Modes (Stream J)

Dictation modes are user-creatable entities managed by `DictationModeManager`:
- **DictationMode**: Contains ID, Title, SortOrder, IsBuiltIn flag, and dual profiles.
- **DictationProfile**: Per-mode config for Cloud or Local — system prompt, LLM model, hotkey, UseLlm toggle.
- 4 built-in modes (Standard, Prompt, Professional, RAW) + unlimited custom modes.
- **ActiveProfile** toggle: Switches all modes between their Cloud and Local configurations globally.
- Utility pipelines (Ask, Refine, Translate, Note, Chat) are fixed-behavior, managed separately by `PipelineConfigManager`.

### 4.6 Managed Trial & JWT Auth (Stream K)

Zero-config onboarding via managed Gemini trial:
- **TrialProxy** (`TrialGeminiProvider`, `TrialGeminiAudioProvider`): Intermediary that routes API calls through a managed proxy server, preventing key exposure.
- **Auth Flow**: Website login → `diktame://auth?token=JWT` deeplink → `JwtDecoder` → `SecureStorage`.
- **Credit Tracking**: `TrialAccountService` monitors remaining trial credits and triggers upgrade prompts.

### 4.7 Real-Time Streaming (Stream L)

Full-duplex WebSocket streaming for low-latency dictation:
- **IStreamingSTTProvider**: Separate interface from batch `ISTTProvider` — handles continuous audio chunks.
- **DeepgramStreamingProvider**: Implements streaming via `IWebSocketClient` abstraction (testable via `FakeWebSocket`).
- **StreamingDictationPipeline**: Injects partial transcription results as they arrive, dramatically reducing perceived latency.

---

## 5. Data Flows

### 5.1 Dictation Flow (Standard)

```
1. User presses Ctrl+Alt+D
2. HotkeyManager fires DictateHotkey event
3. DictationPipeline.ExecuteAsync() begins:
   a. AudioRecorder.StartRecording() → WAV file (16kHz, 16-bit mono)
   b. [Optional] AudioDucker.Duck() — reduce other app volumes
   c. User releases hotkey (or max duration reached)
   d. AudioRecorder.StopRecording() → returns audioFilePath
   e. STTRouter.TranscribeAsync(audioFilePath) → TranscriptionResult
   f. [If not Raw mode] LLMRouter.ProcessAsync(text, systemPrompt, "dictate")
   g. SnippetManager.ExpandSnippets(text) — post-LLM, pre-inject
   h. TextInjector.InjectText(text, trailingSpace, additionalKey)
   i. HistoryManager.LogSession(sessionData)
   j. AudioDucker.Restore()
4. Control Panel updates with timing metrics
```

### 5.2 Refine Flow (Dual-Mode)

**Autopilot Mode:**
```
1. User selects text → presses Ctrl+Alt+R
2. ClipboardManager.CaptureSelection() → selected text
3. LLMRouter.ProcessAsync(selectedText, refinePrompt, "refine")
4. TextInjector.InjectText(refinedText) via Ctrl+V → replaces selection
```

**Instruction Mode:**
```
1. User selects text → presses Ctrl+Alt+R → speaks instruction
2. ClipboardManager.CaptureSelection() → selected text
3. AudioRecorder records instruction audio
4. STTRouter.TranscribeAsync(audio) → instruction text
5. LLMRouter.ProcessAsync(selectedText + instruction, refinePrompt, "refine-instruction")
6. TextInjector.InjectText(result) → replaces selection
```

### 5.3 Other Flows

| Mode | Flow |
|------|------|
| **Ask** | Record → STT → LLM (Q&A prompt) → Output (clipboard/type/notification) [+ optional TTS] |
| **Translate** | Record → STT (auto-detect language) → LLM (translate EN↔ES) → Inject [+ optional TTS] |
| **Note** | Record → STT → [LLM cleanup] → Append to file with timestamp |
| **Oops** | Re-inject last stored text from memory (volatile) |
| **Quick Chat** | Text/voice input → LLM → Display in floating overlay [+ optional TTS] |
| **Read Selection** | Capture selection → TextCleaner → TTSRouter → TtsPlayerService → Audio playback |

### 5.4 Streaming Dictation Flow

```
1. User presses streaming hotkey
2. StreamingDictationPipeline begins:
   a. IStreamingSTTProvider.StartStreamingAsync() → opens WebSocket
   b. AudioRecorder streams chunks via IAudioDataSource
   c. DeepgramStreamingProvider receives partial results
   d. Partial text injected incrementally via TextInjector
   e. User releases hotkey
   f. IStreamingSTTProvider.StopStreamingAsync() → final result
   g. [If not Raw mode] LLMRouter.ProcessAsync(finalText)
   h. TextInjector replaces partial with final text
3. HistoryManager.LogSession()
```

### 5.5 Managed Trial Auth Flow

```
1. User clicks "Try Free" → opens dikta.me/signup in browser
2. User authenticates on website
3. Website redirects to diktame://auth?token=<JWT>
4. ProtocolRegistrar receives deeplink (via SingleInstanceManager if already running)
5. JwtDecoder validates + decodes JWT → extracts trial credentials
6. SecureStorage.Save(trialToken) → DPAPI-encrypted
7. TrialAccountService activates trial mode
8. TrialGeminiProvider / TrialGeminiAudioProvider proxy all API calls
9. Credit usage tracked; TrialStatus updated after each call
```

---

## 6. Configuration & Data Stores

### 6.1 File Layout

```
%APPDATA%/DiktaMe/
├── settings.json          # All app settings (strongly-typed AppSettings)
├── snippets.json          # Voice snippet triggers + content
├── keys.dat               # DPAPI-encrypted API keys
├── history.db             # SQLite (sessions + system_metrics)
├── models/                # Downloaded Whisper models (optional)
│   └── ggml-large-v3-turbo.bin
└── logs/
    └── diktame_20260215.log   # Serilog daily rotation (7-day retention)
```

### 6.2 Settings Model

Settings are persisted as JSON and mapped to a strongly-typed `AppSettings` record. The `SettingsManager` handles:
- Loading with default fallback
- Saving on change
- Schema migration from V1 (`%APPDATA%/diktate/config.json`)
- Observable properties for MVVM binding

### 6.3 Dual-Profile System

Each dictation mode (built-in + custom) and utility pipeline maintains two independent profiles:

| Profile | Model Selection | Prompt |
|---------|----------------|--------|
| **Local** | Global Ollama model | Per-mode custom prompt |
| **Cloud** | Per-mode provider + model | Per-mode custom prompt |

Dictation modes are CRUD-managed by `DictationModeManager`. Utility pipelines (Ask, Refine, Translate, Note, Chat) are managed by `PipelineConfigManager`. The `ActiveProfile` setting globally switches between Cloud and Local configurations.

### 6.4 History Database (SQLite)

**`history` table:**
- Session metadata: mode, stt_provider, llm_provider, llm_model
- Performance: audio_duration_s, transcription_ms, processing_ms, injection_ms, total_ms
- Content: raw_text, processed_text (respects privacy level)
- Status: success, error_message

**`system_metrics` table:**
- Timestamp-based system snapshots
- CPU/memory/GPU usage
- Ollama model status

**Retention:** 90-day auto-pruning.

---

## 7. Security & Privacy

### 7.1 Privacy Levels

| Level | Name | Behavior |
|-------|------|----------|
| **0** | Ghost Mode | Zero storage — no metrics, no logs, no history |
| **1** | Stats-Only | Counts and timings only — transcriptions discarded |
| **2** | Balanced (Default) | Processed text + metrics — PII redacted if enabled |
| **3** | Full | All data including raw transcriptions |

### 7.2 Secrets Management

API keys are encrypted using **Windows DPAPI** (`ProtectedData.Protect()`) and stored in `%APPDATA%/DiktaMe/keys.dat`. This provides machine-level encryption tied to the Windows user account — keys cannot be read if the encrypted file is copied to another machine.

### 7.3 PII Scrubber

Regex-based redaction applied at privacy Level 2+ (if enabled):
- Email addresses → `[EMAIL]`
- Phone numbers → `[PHONE]`
- API keys (sk-*, AIza*) → `[API_KEY]`

### 7.4 API Key Validation

Format validation per provider before storage:
- **OpenAI**: `sk-*`
- **Gemini**: `AIza*` or `ya29.*`
- **Anthropic**: `sk-ant-*`
- **Deepgram**: length/format checks

---

## 8. System Integration

### 8.1 Global Hotkeys

Win32 `RegisterHotKey` / `UnregisterHotKey` via P/Invoke from `user32.dll`:

| Hotkey | Action | Default |
|--------|--------|---------|
| Dictate | Start/stop dictation | `Ctrl+Alt+D` |
| Ask | Voice question | `Ctrl+Alt+A` |
| Refine | Refine selection | `Ctrl+Alt+R` |
| Translate | Translate speech | `Ctrl+Alt+T` |
| Oops | Re-inject last text | `Ctrl+Alt+V` |
| Note | Voice post-it note | `Ctrl+Alt+N` |
| Quick Chat | Open chat overlay | `Ctrl+Alt+C` |
| Read Selection | TTS playback of selected text | `Ctrl+Alt+S` |

Hotkeys are configurable and support runtime re-registration when changed in settings. Custom dictation modes can have their own hotkeys assigned via `DictationProfile.Hotkey`.

### 8.2 System Tray

Uses `H.NotifyIcon.WinUI` for native Windows system tray integration:
- Context menu: Open Control Panel, Settings, Status, Quit
- Dynamic tooltip: `"dIKta.me — [Cloud STT + Gemini LLM]"`
- Icon states: Idle, Recording, Processing, Error

### 8.3 Audio

NAudio provides all audio functionality:
- **Recording**: `WaveInEvent` (16kHz, 16-bit mono WAV)
- **Device Enumeration**: `MMDeviceEnumerator` with fuzzy-match by label
- **Mute Detection**: `AudioEndpointVolume.Mute` polling (3s interval)
- **Audio Ducking**: `AudioSessionManager` to reduce other app volumes during recording

### 8.4 Text Injection

`InputSimulatorStandard` for keyboard simulation:
- **Clipboard mode**: Save clipboard → set text → Ctrl+V → restore clipboard
- **Typed mode**: Simulate keystrokes (slower, but works in restricted apps)
- Additional key support: Enter, Tab, Space after injection

---

## 9. Internationalization (i18n)

V2 uses **WinUI3Localizer** with `.resw` resource files for dynamic locale switching:

| Language | Status |
|----------|--------|
| English (en) | Default |
| Spanish (es) | Full translation |

- Auto-detection: reads `CultureInfo.CurrentUICulture` on first launch
- Fallback: English if requested language unavailable
- Resource files: `Strings/en/Resources.resw`, `Strings/es/Resources.resw`
- Core strings: `Resources/CoreStrings.resx` (for non-UI strings)

---

## 10. Build & Deployment

### 10.1 Build Configuration

Shared properties in `Directory.Build.props`:
- **C# 12**, **Nullable** enabled, **Warnings as errors**
- Version: `2.0.0`
- Implicit usings enabled

### 10.2 Build Commands

```bash
# Restore packages
dotnet restore DiktaMe.sln

# Debug build
dotnet build DiktaMe.sln -c Debug

# Run tests
dotnet test DiktaMe.sln --verbosity normal

# Release build (trimmed, self-contained)
publish-release.cmd
```

### 10.3 Trimmed Publishing (Task A.2)

Target: self-contained trimmed deployment with no .NET runtime dependency.
Native AOT deferred — NAudio COM interop and several dependencies lack AOT compatibility.
IL trimming is the stable prerequisite; AOT can be layered on when the ecosystem matures.

```xml
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>partial</TrimMode>
```

### 10.4 Installer

Options being evaluated:
- **MSIX** — Microsoft Store ready, auto-update, sandboxed
- **Inno Setup** — Traditional installer, more control, smaller overhead

---

## 11. Testing Strategy

### 11.1 Framework

- **xUnit** for test framework
- **Moq** for mocking interfaces (HTTP, audio hardware, clipboard, etc.)
- **FluentAssertions** for expressive test assertions
- **Coverlet** for code coverage collection

### 11.2 Test Organization

```
tests/DiktaMe.Core.Tests/       # 56 test classes, 961 tests
├── Account/                     # JwtDecoder, TrialAccountService, TrialGemini proxies
├── Audio/                       # AudioRecorder, AudioDeviceManager, AudioDucker,
│                                #   AudioLevelMonitor, MuteDetector
├── Config/                      # SettingsManager, ProfileManager, PromptRepository,
│                                #   SnippetManager, PipelineFactory, DictationModeManager,
│                                #   DictationModeDefaults, PromptDefaults
├── Data/                        # HistoryManager, ConversationManager, MetricsCollector, NoteWriter
├── Input/                       # HotkeyManager, HotkeyParser, ClipboardManager,
│                                #   TextInjector (Hardware-tagged, CI-excluded)
├── LLM/                         # All providers + LLMRouter + LlmResult + Trial + ModelListService
├── Pipeline/                    # Dictation, Streaming, Refine, Ask, Translate, Note,
│                                #   ReadSelection, Chat, Oops
├── Security/                    # PIIScrubber, SecureStorage, ApiKeyValidator
├── STT/                         # STTRouter, Deepgram, DeepgramStreaming, GeminiAudio, Whisper
├── System/                      # OllamaManager, OllamaSearchService
└── TTS/                         # TTSRouter, TTSProviderFactory, TtsPlayerService,
                                 #   TtsSpeaker, TextCleaner, Kokoro, Inworld, Deepgram, OpenAI
```

**Current: 961 tests passing** (Hardware/Integration traits excluded from CI).

### 11.3 CI/CD (GitHub Actions)

Single-job pipeline in `.github/workflows/ci-v2.yml` (runs on `windows-latest`):

| Step | Purpose |
|------|---------|
| **Restore** | `dotnet restore` — fails fast on hallucinated NuGet packages |
| **Lint** | `dotnet format --verify-no-changes` — enforces `.editorconfig` style |
| **Build** | `dotnet build -c Release` — `TreatWarningsAsErrors` + Meziantou.Analyzer |
| **Test** | `dotnet test` — excludes `Category=Integration` and `Category=Hardware` traits |
| **Test-count threshold** | Fails if passing tests drop below `ci/test-threshold.json` minimum |
| **Secret scan** | gitleaks v8.21.2 — full git history scan (`.gitleaks.toml` allowlist) |
| **Vulnerability audit** | `dotnet list package --vulnerable` — fails on High/Critical CVEs |
| **Deprecated packages** | `dotnet list package --deprecated` — informational warning only |
| **Publish** | Trimmed self-contained win-x64 build |
| **Publish size guard** | Fails if output deviates >20% from expected range |
| **Artifact upload** | Coverage report + publish output as GitHub Actions artifacts |

See `ci/DECISIONS.md` for suppression rationale and `ci/test-threshold.json` for thresholds.

---

## 12. Git Strategy

### 12.1 Repository Model

| Repo | Purpose | Status |
|------|---------|--------|
| `geckogtmx/diktate` (`E:\git\diktate`) | V1 — Electron + Python | Living sandbox / daily driver |
| `geckogtmx/diktame` (`E:\git\diktame`) | V2 — C# + WinUI 3 | **Active development** |

### 12.2 Branching: Trunk-Based Development

All commits go directly to `main`. No feature branches, no PRs. Single contributor + AI agent.

### 12.3 Commit Convention

```
<type>(<scope>): <description> [<TASK_ID>]
```

**Types**: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`

**Scopes**: `audio`, `stt`, `llm`, `pipeline`, `input`, `config`, `data`, `security`, `system`, `ui`, `ci`, `installer`

### 12.4 Tags

| Tag | Milestone |
|-----|-----------|
| `v2.0.0-alpha.1` | Core engine works — records + transcribes |
| `v2.0.0-alpha.2` | Full pipeline functional |
| `v2.0.0-beta.1` | Feature complete |
| `v2.0.0-rc1` | Tests pass, installer works |
| `v2.0.0` | 🚀 Ship |



---

## 13. V2 Modules Sprint (SPEC_015)

> **Full plan:** [`plans/SPEC_015_MODULES_SPRINT.md`](plans/SPEC_015_MODULES_SPRINT.md)
> **Status:** PENDING — all 17 phases (A-Q), 4 modules

The core engine (Streams A-L, TTS) is feature-complete. The final V2 development phase adds **four isolated modules** that extend the pipeline without modifying it. Each module follows the same pattern: depends only on Core contracts (`PipelineResult`, `AppSettings`, `ILLMProvider`), registers via DI, hooks into pipeline through 1-3 lines of code.

### Module Architecture

```
dIKta.me Core (existing — unchanged)
    │
    ├── OnPipelineCompleted() ──→ ConnectorManager.DispatchPresetsAsync()
    ├── OnPipelineCompleted() ──→ MemoryLayer.StoreAsync()
    ├── Before LLM ──→ MemoryLayer.SearchAsync() → context injection
    ├── HotkeyId.Vision ──→ VisionPipeline
    └── SessionManager (standalone)
```

### The Four Modules

| Module | Namespace | Hook Point | UI Surface | Phases |
|--------|-----------|-----------|------------|--------|
| **Connectors** | `DiktaMe.Core.Connectors` | `OnPipelineCompleted()` | Own settings window + Control Panel widget | A-C, F, H, J, K |
| **Meetings (Scribe)** | `DiktaMe.Core.Meetings` | Standalone (SessionManager) | ScribeWindow (tray) + Settings page | D, E, G, I, N |
| **Vision (See)** | `DiktaMe.Core.Vision` | `HotkeyId.Vision` dispatch | Hotkey-only (`Ctrl+Alt+S`) + Settings page | L, M, N |
| **Memory** | `DiktaMe.Core.Memory` | `OnPipelineCompleted()` + before LLM | Background (automatic) + Settings page | O, P, Q |

### Key Principle

Modules NEVER depend on each other directly. Cross-module flows go through shared Core contracts:
- Scribe → Connectors: via `PipelineResult`
- Vision → Connectors: via `PipelineResult`
- All → Memory: via `PipelineResult` (store) and `IMemoryLayer` (retrieve)
- Scribe ← ScreenCapture: shared Core infrastructure, not Vision module

### Design References

| Spec | Module |
|------|--------|
| [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](plans/SPEC_013_CONNECTORS_IMPLEMENTATION.md) | Connectors |
| [`SPEC_001_MEETINGS.md`](plans/SPEC_001_MEETINGS.md) | Meetings/Scribe |
| [`SPEC_002_VISION.md`](plans/SPEC_002_VISION.md) | Vision/See |
| [`SPEC_014_MEMORY_LAYER.md`](plans/SPEC_014_MEMORY_LAYER.md) | Memory |
| [`SPEC_013_USE_CASES.md`](plans/SPEC_013_USE_CASES.md) | 218 use cases |

### New Core Directories

```
src/DiktaMe.Core/
├── Connectors/    # IConnector, ConnectorManager, 5 connector implementations
├── Meetings/      # Session, SessionManager, MeetingRecorder, Synthesizer
├── Vision/        # ScreenCapture, ImageProcessor, VisionOptions
├── Memory/        # IMemoryLayer, SqliteMemoryStore, EmbeddingGenerator
└── Pipeline/
    └── VisionPipeline.cs
```

### New Settings Sub-Objects

`AppSettings` gains 4 new sub-objects following the existing pattern (`sealed record`, `= new()` defaults, added to `SanitizeNulls()`):
- `ConnectorSettings Connectors` (Phase A)
- `MeetingSettings Meetings` (Phase I)
- `VisionSettings Vision` (Phase L)
- `MemorySettings Memory` (Phase O)

---

## 14. Glossary

| Term | Definition |
|------|-----------|
| **Engine** | The main application — UI, audio, hotkeys, text injection |
| **Ears (STT)** | Speech-to-Text layer — converts audio to text |
| **Brain (LLM)** | Large Language Model layer — processes text with AI |
| **Mouth (TTS)** | Text-to-Speech layer — converts text to audio playback |
| **Pipeline** | End-to-end orchestration of a workflow mode |
| **Provider** | Implementation of ISTTProvider, ILLMProvider, or ITTSProvider |
| **Router** | Selects the active provider based on config + handles fallback |
| **Profile** | A Local or Cloud configuration for a specific mode |
| **Dictation Mode** | A user-configurable workflow with title, dual profiles, and hotkey |
| **Utility Pipeline** | A fixed-behavior pipeline (Ask, Refine, etc.) with customizable prompts |
| **Snippet** | A voice-triggered text macro (trigger word → expanded text) |
| **BYOK** | Bring Your Own Key — user provides their own API keys |
| **Managed Trial** | Zero-config onboarding via proxy-based Gemini access |
| **Module** | An isolated add-on (Connectors, Meetings, Vision, Memory) that extends the pipeline via minimal hook points. See Section 13. |
| **Connector Preset** | A composable mini-pipeline: input → STT → LLM → external destination (file, webhook, API) |
| **Scribe** | Meeting Intelligence module — records, transcribes, synthesizes meeting artifacts |
| **IMemoryLayer** | Semantic vector memory interface — stores embeddings, retrieves context for LLM enrichment |

---

**Document Status:** Active — Core engine feature complete (Streams A–L); V2 Modules Sprint (SPEC_015) pending
**Last Updated:** 2026-03-14
**Parent Spec:** `DEVELOPMENT_ROADMAP.md`
**Modules Sprint:** [`plans/SPEC_015_MODULES_SPRINT.md`](plans/SPEC_015_MODULES_SPRINT.md)
