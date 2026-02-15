# Architecture

Technical design document for **dIKta.me V2** — the C# + WinUI 3 rewrite.

> **Note**: This document reflects the V2 architecture. For V1 (Electron + Python), see `E:\git\diktate\ARCHITECTURE.md`.

---

## 1. System Overview

dIKta.me V2 is a **native Windows desktop application** built as a single-process architecture using C# and WinUI 3. It replaces the dual-process Electron + Python V1 with a unified, high-performance binary.

### The Triad Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         User                                    │
│     (Hotkeys: Dictate, Ask, Translate, Refine, Oops, Note)      │
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
│  │  └────────────┬──────────────┬────────────────────┘      │   │
│  │               │              │                           │   │
│  │  ┌── EARS ────┴──┐  ┌── BRAIN ──┴────────────────┐      │   │
│  │  │ STT Router    │  │ LLM Router                 │      │   │
│  │  │ ┌───────────┐ │  │ ┌──────────┐ ┌───────────┐ │      │   │
│  │  │ │ Deepgram  │ │  │ │ Gemini   │ │ Anthropic │ │      │   │
│  │  │ │ Gemini    │ │  │ │ OpenAI   │ │ Ollama    │ │      │   │
│  │  │ │ Whisper   │ │  │ └──────────┘ └───────────┘ │      │   │
│  │  │ └───────────┘ │  └────────────────────────────┘      │   │
│  │  └───────────────┘                                       │   │
│  │                                                          │   │
│  │  ┌── System Services ────────────────────────────┐      │   │
│  │  │ AudioRecorder │ TextInjector │ HotkeyManager  │      │   │
│  │  │ MuteDetector  │ AudioDucker  │ ClipboardMgr   │      │   │
│  │  │ SettingsMgr   │ HistoryMgr   │ SecureStorage   │      │   │
│  │  │ SnippetMgr    │ ProfileMgr   │ OllamaManager   │      │   │
│  │  └───────────────────────────────────────────────┘      │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### V1 → V2 Comparison

| Factor | V1 (Electron + Python) | V2 (C# + WinUI 3) |
|--------|:-:|:-:|
| Process model | 2+ processes + JSON IPC | **Single process** |
| Installer size | ~100-200MB | **< 30MB** (Native AOT) |
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
│       │   ├── MuteDetector.cs         # Hardware mute monitoring (CoreAudio COM)
│       │   └── AudioDucker.cs          # WASAPI audio ducking during recording
│       ├── STT/
│       │   ├── ISTTProvider.cs         # Interface + TranscriptionResult
│       │   ├── STTRouter.cs            # Routes to cloud/local with fallback
│       │   ├── DeepgramProvider.cs     # Cloud STT (Nova-2 REST API)
│       │   ├── GeminiAudioProvider.cs  # Cloud STT (Gemini multimodal)
│       │   └── WhisperProvider.cs      # Local STT (Whisper.net / ONNX)
│       ├── LLM/
│       │   ├── ILLMProvider.cs         # Interface
│       │   ├── LLMRouter.cs            # Per-mode provider routing (dual-profile)
│       │   ├── GeminiProvider.cs       # Cloud LLM
│       │   ├── AnthropicProvider.cs    # Cloud LLM
│       │   ├── OpenAIProvider.cs       # Cloud LLM
│       │   └── OllamaProvider.cs       # Local LLM (localhost:11434)
│       ├── Pipeline/
│       │   ├── DictationPipeline.cs    # Record → STT → LLM → Inject
│       │   ├── RefinePipeline.cs       # Selection + instruction flows
│       │   ├── AskPipeline.cs          # Voice Q&A
│       │   ├── TranslatePipeline.cs    # EN↔ES bidirectional
│       │   ├── NotePipeline.cs         # Post-it notes to file
│       │   └── ChatPipeline.cs         # Quick Chat overlay flow
│       ├── Input/
│       │   ├── HotkeyManager.cs        # Win32 RegisterHotKey P/Invoke
│       │   ├── TextInjector.cs         # Keyboard simulation (InputSimulator)
│       │   └── ClipboardManager.cs     # Clipboard save/restore
│       ├── Config/
│       │   ├── AppSettings.cs          # Strongly-typed settings model
│       │   ├── SettingsManager.cs      # JSON persistence + migration
│       │   ├── PromptRepository.cs     # 16 custom system prompt slots
│       │   ├── ProfileManager.cs       # Dual-profile system (8 × 2)
│       │   └── SnippetManager.cs       # Voice snippets CRUD + matching
│       ├── Data/
│       │   ├── HistoryManager.cs       # SQLite session logging (90-day)
│       │   ├── MetricsCollector.cs     # Performance tracking
│       │   └── NoteWriter.cs           # File-based note appending
│       ├── Security/
│       │   ├── PIIScrubber.cs          # Regex-based PII redaction
│       │   ├── SecureStorage.cs        # DPAPI for API keys
│       │   └── ApiKeyValidator.cs      # Format validation per provider
│       ├── System/
│       │   ├── CapabilityDetector.cs   # Runtime hardware/software detection
│       │   ├── SystemMonitor.cs        # CPU/GPU/memory metrics
│       │   ├── StartupManager.cs       # Auto-start registration
│       │   └── OllamaManager.cs        # Version sensing + model library
│       └── Capabilities.cs             # CapabilityReport model
│
└── tests/
    └── DiktaMe.Core.Tests/             # xUnit + Moq + FluentAssertions
        └── ScaffoldTests.cs             # Initial build verification tests
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
| **UI Framework** | WinUI 3 (Windows App SDK) | 1.5 |
| **MVVM Toolkit** | CommunityToolkit.Mvvm | 8.3.2 |
| **Packaging** | Unpackaged (WindowsPackageType=None) | — |

### NuGet Dependencies

#### DiktaMe.Core (Business Logic)

| Package | Purpose | V1 Equivalent |
|---------|---------|---------------|
| `NAudio` 2.x | Audio capture, device management, CoreAudio COM | `pyaudio`, `pycaw` |
| `InputSimulatorStandard` 1.x | Keyboard simulation, clipboard injection | `pynput` |
| `Microsoft.Data.Sqlite` 8.x | History database, metrics persistence | `sqlite3` |
| `Serilog` 3.x + `Serilog.Sinks.File` 5.x | Structured logging with daily rotation | Python `logging` |
| `System.Security.Cryptography.ProtectedData` 8.x | DPAPI encryption for API keys | Electron `safeStorage` |
| `Microsoft.Extensions.DI.Abstractions` 8.x | DI interface contracts | N/A (manual) |

#### DiktaMe.App (UI Layer)

| Package | Purpose |
|---------|---------|
| `Microsoft.WindowsAppSDK` 1.5 | WinUI 3 runtime |
| `Microsoft.Windows.SDK.BuildTools` 10.x | Windows SDK build integration |
| `H.NotifyIcon.WinUI` 2.1.0 | System tray icon + context menu |
| `CommunityToolkit.Mvvm` 8.3.2 | ObservableObject, RelayCommand, source generators |
| `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 | Toast notifications |
| `Microsoft.Extensions.DependencyInjection` 8.0.1 | DI container |

#### DiktaMe.Core.Tests (Testing)

| Package | Purpose |
|---------|---------|
| `xunit` 2.x | Test framework |
| `Moq` 4.x | Mocking (interfaces, HTTP, hardware) |
| `FluentAssertions` 6.x | Expressive assertion syntax |
| `coverlet.collector` 6.x | Code coverage collection |

### Future / Optional

| Package | Purpose | When |
|---------|---------|------|
| `Whisper.net` 1.x | Local STT (ONNX-based, no Python) | Task C.4 |
| `Whisper.net.Runtime.Cuda` 1.x | GPU acceleration for Whisper.net | Task C.4 |

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

public interface ILLMProvider
{
    Task<string> ProcessAsync(string text, string systemPrompt, string mode);
    Task<bool> IsAvailableAsync();
    string ProviderName { get; }
}
```

Routers (`STTRouter`, `LLMRouter`) select the active provider based on user configuration and handle automatic fallback.

### 4.4 Pipeline Orchestration

Each workflow mode has a dedicated pipeline class that orchestrates the full flow:

```
DictationPipeline:  Record → STT → [LLM cleanup] → Inject
RefinePipeline:     Capture Selection → [Record instruction] → STT → LLM → Replace
AskPipeline:        Record → STT → LLM (Q&A) → Output
TranslatePipeline:  Record → STT (auto-detect) → LLM (translate) → Inject
NotePipeline:       Record → STT → [LLM cleanup] → Append to file
ChatPipeline:       Text/Voice input → LLM → Display in overlay
```

Pipelines emit progress events (`Recording`, `Transcribing`, `Processing`, `Injecting`) for UI feedback.

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
| **Ask** | Record → STT → LLM (Q&A prompt) → Output (clipboard/type/notification) |
| **Translate** | Record → STT (auto-detect language) → LLM (translate EN↔ES) → Inject |
| **Note** | Record → STT → [LLM cleanup] → Append to file with timestamp |
| **Oops** | Re-inject last stored text from memory (volatile) |
| **Quick Chat** | Text/voice input → LLM → Display in floating overlay |

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

Each of the 8 workflow modes maintains two independent profiles:

| Profile | Model Selection | Prompt |
|---------|----------------|--------|
| **Local** | Global Ollama model | Per-mode custom prompt |
| **Cloud** | Per-mode provider + model | Per-mode custom prompt |

Total: **8 modes × 2 profiles × 3 settings = 48 configuration keys**

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

Hotkeys are configurable and support runtime re-registration when changed in settings.

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

V2 uses **.NET resource files** (`.resx`) instead of V1's `i18next` JSON files:

| Language | Status |
|----------|--------|
| English (en) | Default |
| Spanish (es) | Full translation |

- Auto-detection: reads `CultureInfo.CurrentUICulture` on first launch
- Fallback: English if requested language unavailable
- Resource files: `Properties/Resources.resx`, `Properties/Resources.es.resx`

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

# Release build (Native AOT)
dotnet publish src/DiktaMe.App -c Release -r win-x64
```

### 10.3 Native AOT Publishing (Task A.2)

Target: self-contained single-file `.exe` < 30MB with no .NET runtime dependency.

```xml
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
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
tests/DiktaMe.Core.Tests/
├── Audio/
│   ├── AudioRecorderTests.cs
│   └── AudioDuckerTests.cs
├── STT/
│   ├── STTRouterTests.cs
│   └── DeepgramProviderTests.cs
├── LLM/
│   ├── LLMRouterTests.cs
│   ├── GeminiProviderTests.cs
│   └── OllamaProviderTests.cs
├── Pipeline/
│   ├── DictationPipelineTests.cs
│   └── RefinePipelineTests.cs
├── Config/
│   ├── SettingsManagerTests.cs
│   └── SnippetManagerTests.cs
├── Data/
│   └── HistoryManagerTests.cs
├── Security/
│   └── PIIScrubberTests.cs
└── System/
    └── OllamaManagerTests.cs
```

**Target: 170+ tests** (matching or exceeding V1's 255 test count).

### 11.3 CI/CD (GitHub Actions)

```yaml
Jobs:
  build:   dotnet build
  test:    dotnet test (xUnit)
  lint:    dotnet format --verify-no-changes
  publish: dotnet publish -c Release (verify AOT output)
```

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

## 13. V1 Migration Path

When V2 reaches feature parity, a migration path exists for V1 users:

1. Detect V1 installation (`%APPDATA%/diktate/config.json`)
2. Convert V1 `electron-store` settings → V2 `AppSettings` format
3. Migrate API keys from Electron `safeStorage` → DPAPI
4. Preserve SQLite history database (same schema)
5. Preserve custom prompts, hotkey bindings, privacy settings
6. Show "Welcome to V2" migration summary

---

## 14. Glossary

| Term | Definition |
|------|-----------|
| **Engine** | The main application — UI, audio, hotkeys, text injection |
| **Ears (STT)** | Speech-to-Text layer — converts audio to text |
| **Brain (LLM)** | Large Language Model layer — processes text with AI |
| **Pipeline** | End-to-end orchestration of a workflow mode |
| **Provider** | Implementation of ISTTProvider or ILLMProvider |
| **Router** | Selects the active provider based on config + handles fallback |
| **Profile** | A Local or Cloud configuration for a specific mode |
| **Snippet** | A voice-triggered text macro (trigger word → expanded text) |
| **BYOK** | Bring Your Own Key — user provides their own API keys |

---

**Document Status:** Active — Updated as implementation progresses
**Last Updated:** 2026-02-15
**Parent Spec:** `DEVELOPMENT_ROADMAP.md`
