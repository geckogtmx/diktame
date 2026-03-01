# dIKta.me V2 — Development Roadmap (C# + WinUI 3 Rewrite)

**Status:** Feature Complete (Streams A–G, I, J complete; H.1 remaining for release)
**Date:** 2026-02-18
**Parent:** V1 `SPEC_039_STRATEGIC_ROADMAP.md` (in diktate repo)
**Supersedes:** Python modular split approach (deemed throwaway work)
**Target:** Native Windows app — single process, modular architecture, self-contained installer

---

## 🔴 PRIMARY DIRECTIVE: Rebranding

> **All references to "dIKtate" are renamed to "dIKta.me" (brand/UI) or "diktame" (code/files).**
>
> | Context | Old | New |
> |---------|-----|-----|
> | Brand / Marketing / UI text | dIKtate | **dIKta.me** |
> | Code identifiers (namespaces, classes, variables) | dIKtate | **diktame** or **DiktaMe** |
> | File/folder names | diktate | **diktame** |
> | Solution/Project names | dIKtate.App | **DiktaMe.App** |
> | Installer filename | diktate-setup.exe | **diktame-setup.exe** |
> | AppData folder | %APPDATA%/dIKtate/ | **%APPDATA%/DiktaMe/** |
> | User-facing strings | "dIKtate" | **"dIKta.me"** |
>
> This applies to ALL new V2 code. V1 code retains the old name until sunset.

---

## 1. Executive Summary

Phase 2 is a **full rewrite** of dIKta.me (formerly dIKtate) from Python + Electron to **C# + WinUI 3**. Instead of splitting the Python monolith (which would be thrown away), we go directly to the professional target stack.

### Why This Path

| Factor | Python Split (rejected) | C# Rewrite (chosen) |
|--------|:-:|:-:|
| Throwaway code | 15 days of wasted work | Zero — this is the final stack |
| Installer size | ~100-200MB | ~70MB (compressed) |
| Memory footprint | ~300MB (Electron + Python) | ~50-80MB |
| Process model | 2+ processes + IPC | Single process |
| Windows integration | Wrappers (pycaw, pynput) | Native APIs |
| Startup time | 10-12s (model warmup) | <3s (cloud mode) |
| AI coding support | Good | Excellent (Copilot, Claude, etc.) |

### The Triad Architecture (Unchanged)

| Layer | Role | Required? | V2 Implementation |
|-------|------|-----------|-------------------|
| **Engine** | UI, Audio, Hotkeys, Injection | Mandatory | C# + WinUI 3 (single binary) |
| **STT (Ears)** | Speech-to-Text | Mandatory | Cloud default, optional local |
| **LLM (Brain)** | Ask, Refine, Translate | Optional | Cloud APIs or Ollama |

### Success Criteria

- [ ] Self-contained installer (~70MB compressed)
- [ ] All 6 workflow modes functional
- [ ] Cloud-first: works out of the box with API key
- [ ] Local Whisper as optional sidecar (Whisper.net or exe)
- [ ] Local Ollama integration preserved
- [ ] Configuration Wizard for first-run
- [ ] Startup < 3s in cloud mode
- [ ] Memory < 80MB idle
- [ ] Full test suite (xUnit)

---

## 2. Feature Preservation Matrix

> **CRITICAL:** Every V1 feature below MUST exist in V2. Nothing is dropped.

### 2.1 Workflow Modes (All 6)

| # | Mode | Hotkey | STT? | LLM? | V2 Notes |
|---|------|--------|:---:|:---:|----------|
| 1 | **Dictate** | `Ctrl+Alt+D` | ✅ | Optional | Cloud STT default, instant start |
| 2 | **Refine** | `Ctrl+Alt+R` | ✅ (instruction) | ✅ | Both autopilot & instruction |
| 3 | **Ask** | `Ctrl+Alt+A` | ✅ | ✅ | Voice Q&A |
| 4 | **Translate** | `Ctrl+Alt+T` | ✅ | ✅ | EN↔ES bidirectional |
| 5 | **Oops** | `Ctrl+Alt+V` | ❌ | ❌ | Re-inject last text |
| 6 | **Note** | `Ctrl+Alt+N` | ✅ | Optional | Post-it notes to file |

### 2.2 All Preserved Features

| Category | Features | V2 Approach |
|----------|----------|-------------|
| **Core** | Push-to-Talk, Auto-Injection, System Tray, 6 Global Hotkeys, Sound Feedback, Auto-Start | Native C# — better performance |
| **Intelligence** | Dual-Profile (8 modes × 2), Custom Prompts (16 slots), Per-Mode Provider, Model Hot-Swap, +Key Auto-Action | In-memory config, same UX |
| **STT** | Local Whisper, Cloud STT | Cloud default + optional Whisper.net sidecar |
| **LLM** | Ollama, Gemini, Anthropic, OpenAI, BYOK | HttpClient for all providers |
| **Privacy** | 4-Level Privacy, PII Scrubber, One-Click Wipe, Telemetry-Free | Same policies, native implementation |
| **Data** | SQLite History (90-day), Session Logging, Metrics, Log Rotation | Microsoft.Data.Sqlite / EF Core |
| **i18n** | English + Spanish, Auto-Detection, Validation | .NET Resources (.resx) |
| **Settings** | Full settings UI (General, Modes, Audio, Privacy, Ollama, API Keys) | WinUI 3 XAML — modern Fluent Design |

### 2.3 New V2 Features (Promoted from Deferred)

| Source | Feature | V2 Scope |
|--------|---------|----------|
| **SPEC_026** | Voice Snippets (Macros) | Phase 1: SnippetManager, trigger matching, Settings tab (skip cursor placement + dynamic variables) |
| **SPEC_031** | Ollama Update Management | Version sensing, pre-flight health check, model library UI, auto-fallback |
| **SPEC_042d** | Quick Chat Overlay | Hotkey-activated floating LLM chat window (text + voice input), stateless MVP |
| **SPEC_043** | Control Panel Config | Settings toggles to show/hide HUD rows (Modes, Actions, Stats) — *already in V1, port to XAML* |
| **SPEC_043d** | Audio Ducking | WASAPI-based auto-volume reduction of other apps during dictation |
| **SPEC_042** | Website Rebrand | Update dikta.me website copy, meta, downloads for V2 launch — *site already live* |

---

## 3. Architecture

### 3.1 Solution Structure

```
DiktaMe.sln
├── src/
│   ├── DiktaMe.App/              # WinUI 3 application (UI layer)
│   │   ├── App.xaml(.cs)         # App lifecycle, DI container
│   │   ├── Views/                # XAML pages
│   │   │   ├── TrayIconView      # System tray (NotifyIcon)
│   │   │   ├── ControlPanelView  # Debug dashboard
│   │   │   ├── SettingsView      # Tabbed settings window
│   │   │   ├── WizardView        # First-run setup wizard
│   │   │   ├── QuickChatView     # Floating LLM chat overlay (SPEC_042d)
│   │   │   └── LoadingView       # Startup loading
│   │   ├── ViewModels/           # MVVM ViewModels
│   │   ├── Converters/           # XAML value converters
│   │   └── Assets/               # Icons, sounds, images
│   │
│   ├── DiktaMe.Core/             # Business logic (class library)
│   │   ├── Audio/
│   │   │   ├── AudioRecorder.cs      # NAudio capture
│   │   │   ├── AudioDeviceManager.cs # Device enumeration
│   │   │   └── MuteDetector.cs       # Hardware mute monitoring
│   │   ├── STT/
│   │   │   ├── ISTTProvider.cs       # Interface
│   │   │   ├── STTRouter.cs          # Routes to cloud/local
│   │   │   ├── DeepgramProvider.cs   # Cloud STT
│   │   │   ├── GeminiAudioProvider.cs# Cloud STT (Gemini)
│   │   │   └── WhisperProvider.cs    # Local STT (Whisper.net)
│   │   ├── LLM/
│   │   │   ├── ILLMProvider.cs              # Interface + LlmResult record
│   │   │   ├── LLMRouter.cs                 # Primary/fallback routing
│   │   │   ├── OpenAICompatibleProvider.cs  # OpenAI, DeepSeek, OpenRouter, Groq,
│   │   │   │                                #   Together, Fireworks, Perplexity,
│   │   │   │                                #   Azure OpenAI, LM Studio, vLLM, etc.
│   │   │   ├── GeminiProvider.cs            # Gemini generateContent (API key + OAuth)
│   │   │   ├── AnthropicProvider.cs         # Anthropic Messages API
│   │   │   └── OllamaProvider.cs            # Local Ollama (localhost:11434)
│   │   ├── Pipeline/
│   │   │   ├── DictationPipeline.cs  # Orchestrates Record→STT→LLM→Inject
│   │   │   ├── RefinePipeline.cs     # Selection + instruction flows
│   │   │   ├── AskPipeline.cs        # Voice Q&A flow
│   │   │   ├── TranslatePipeline.cs  # Translation flow
│   │   │   ├── NotePipeline.cs       # Post-it note flow
│   │   │   └── ChatPipeline.cs       # Quick Chat flow (SPEC_042d)
│   │   ├── Input/
│   │   │   ├── HotkeyManager.cs      # Global hotkey registration
│   │   │   ├── TextInjector.cs       # Keyboard simulation
│   │   │   └── ClipboardManager.cs   # Clipboard operations
│   │   ├── Config/
│   │   │   ├── AppSettings.cs        # Settings model
│   │   │   ├── SettingsManager.cs    # Persistence (JSON file)
│   │   │   ├── PromptRepository.cs   # System prompts (16 slots)
│   │   │   ├── ProfileManager.cs     # Dual-profile system
│   │   │   └── SnippetManager.cs     # Voice snippets CRUD (SPEC_026)
│   │   ├── Data/
│   │   │   ├── HistoryManager.cs     # SQLite session logging
│   │   │   ├── MetricsCollector.cs   # Performance tracking
│   │   │   └── NoteWriter.cs         # File-based note appending
│   │   ├── Security/
│   │   │   ├── PIIScrubber.cs        # PII redaction
│   │   │   ├── SecureStorage.cs      # DPAPI for API keys
│   │   │   └── ApiKeyValidator.cs    # Format validation
│   │   ├── System/
│   │   │   ├── CapabilityDetector.cs # Runtime capability detection
│   │   │   ├── SystemMonitor.cs      # CPU/GPU/memory metrics
│   │   │   ├── StartupManager.cs     # Auto-start registration
│   │   │   └── OllamaManager.cs      # Version sensing & model library (SPEC_031)
│   │   ├── Audio/AudioDucker.cs      # WASAPI audio ducking (SPEC_043d)
│   │   └── Capabilities.cs           # CapabilityReport model
│   │
│   └── DiktaMe.Whisper/          # Optional: Whisper sidecar (separate exe)
│       ├── WhisperService.cs     # Whisper.net wrapper
│       └── Program.cs            # Standalone TCP/pipe server
│
├── tests/
│   ├── DiktaMe.Core.Tests/       # xUnit tests for business logic
│   └── DiktaMe.App.Tests/        # UI automation tests (optional)
│
└── installer/
    └── setup.iss                 # Inno Setup or MSIX packaging
```

### 3.2 Technology Map (Python → C#)

| Function | V1 (Python) | V2 (C#) | NuGet Package |
|----------|-------------|---------|---------------|
| Audio Capture | `pyaudio` | `NAudio` | `NAudio` |
| Keyboard Simulation | `pynput` | `InputSimulatorStandard` | `InputSimulatorStandard` |
| Clipboard | `pyperclip` | `Windows.ApplicationModel.DataTransfer` | Built-in |
| Mute Detection | `pycaw` (COM) | `NAudio` + CoreAudio COM | `NAudio` |
| HTTP Clients | `requests` | `HttpClient` | Built-in |
| Local STT | `faster-whisper` | `Whisper.net` | `Whisper.net` + ONNX Runtime |
| Local LLM | HTTP to Ollama | `HttpClient` to `localhost:11434` | Built-in |
| SQLite | `sqlite3` | `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite` |
| Settings Storage | `electron-store` (JSON) | JSON file + `System.Text.Json` | Built-in |
| Secrets | `safeStorage` (Electron) | `DPAPI` / `ProtectedData` | Built-in |
| Logging | Python `logging` | `Serilog` | `Serilog` |
| System Tray | Electron tray API | `H.NotifyIcon.WinUI` | `H.NotifyIcon.WinUI` |
| Global Hotkeys | `electron globalShortcut` | `Win32 RegisterHotKey` P/Invoke | Manual (3 lines) |
| GPU Detection | `nvidia-smi` subprocess | `NVML` P/Invoke or `nvidia-smi` | Manual |
| i18n | `i18next` + JSON | `.resx` resource files | Built-in |
| Testing | `pytest` | `xUnit` + `Moq` | `xunit`, `Moq` |
| DI Container | N/A (manual) | `Microsoft.Extensions.DI` | Built-in |

### 3.3 Key Design Patterns

**MVVM (Model-View-ViewModel):** WinUI 3 standard. Views (XAML) bind to ViewModels. Business logic lives in `DiktaMe.Core`.

**Dependency Injection:** All services registered in `App.xaml.cs`, injected via constructor. Makes testing trivial.

**Interface-First Providers:** `ISTTProvider` and `ILLMProvider` interfaces allow hot-swapping between cloud/local without changing pipeline code.

```csharp
// The key abstraction — identical API regardless of provider
public interface ISTTProvider
{
    // NOTE: CancellationToken must be propagated through all pipelines (allows user to cancel long STT calls)
    Task<TranscriptionResult> TranscribeAsync(string audioFilePath, string language = "en",
        CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    string ProviderName { get; }
}

public interface ILLMProvider
{
    // NOTE: CancellationToken must be propagated through all pipelines
    Task<LlmResult> ProcessAsync(string text, string systemPrompt, string mode = "dictate",
        CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    string ProviderName { get; }
}

// LlmResult (mirrors TranscriptionResult for consistency)
public sealed record LlmResult
{
    public required string Text { get; init; }
    public required string Provider { get; init; }
    public long LatencyMs { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Text);
}
```

---

## 4. Task Breakdown

### Work Stream A: Project Scaffolding (Priority: FIRST)

#### Task A.0: Git Repo Prep & V1 Archive ⚡ PRE-WORK
**Effort:** 0.25 day

> This task should be done **before** Day 1 begins. It sets up the clean new repo and archives V1.

**Steps — V1 Baseline (in `E:\git\diktate\` repo):**
1. Ensure all V1 work is committed and pushed
2. Tag the V1 baseline: `git tag -a v1.0.0 -m "V1 baseline — Electron + Python"`
3. Push tag: `git push origin v1.0.0`
4. V1 stays active as daily driver + prototyping sandbox (see §9.5)

**Steps — V2 Repo (new `diktame` repo):**
1. Create GitHub repo: `geckogtmx/diktame` (private)
2. Clone locally: `git clone git@github.com:geckogtmx/diktame.git E:\git\diktame`
3. Create initial `README.md` with project name, description, and tech stack summary
4. Create `.gitignore` from `dotnet` + `rider` + `vs` templates
5. Create `LICENSE` (TBD — placeholder)
6. Initial commit: `git commit -m "chore: initialize diktame repository [A.0]"`
7. Push: `git push origin main`

**Acceptance:** Empty repo on GitHub with README, .gitignore, and v1.0.0 tag on old repo.

#### Task A.1: Create Solution & Projects
**Effort:** 0.5 day

**Steps:**
1. In `E:\git\diktame\`:
2. `dotnet new sln -n DiktaMe`
3. `dotnet new winui3 -n DiktaMe.App` (requires Windows App SDK workload)
4. `dotnet new classlib -n DiktaMe.Core`
5. `dotnet new xunit -n DiktaMe.Core.Tests`
6. Add project references: App → Core, Tests → Core
7. Install NuGet packages (see §3.2)
8. Configure `Directory.Build.props` for shared settings
9. Set target: `net8.0-windows10.0.19041.0` (Windows 10 2004+)
10. Add `.editorconfig` and code style rules
11. Commit: `git commit -m "feat: scaffold DiktaMe solution with WinUI 3 [A.1]"`

**Acceptance:** Solution builds, blank WinUI window appears.

#### Task A.2: Configure Release Publishing (Trimmed, Self-Contained)
**Effort:** 0.5 day

> **Decision:** Native AOT (`PublishAot`) deferred — NAudio COM interop lacks AOT annotations,
> WinUI 3 AOT is still maturing (SDK 1.6, Sep 2024), and several dependencies are incompatible.
> IL trimming provides the size reduction without the compatibility risk. AOT can be layered on
> later as a one-line change when the ecosystem catches up.

**Steps:**
1. Add `<PublishTrimmed>true</PublishTrimmed>` + `<TrimMode>partial</TrimMode>` to `DiktaMe.App.csproj` (Release only)
2. Add `TrimmerRootAssembly` entries for NAudio, InputSimulatorStandard, Notifications (trim-incompatible)
3. Add `<IsTrimmable>true</IsTrimmable>` + `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` to `DiktaMe.Core.csproj`
4. Create `publish-release.cmd`: `dotnet publish -c Release -r win-x64 --self-contained true`
5. Verify published app runs and document output size

**Acceptance:** Self-contained exe runs without .NET installed. Published output trimmed.

---

### Work Stream B: Core Engine (Priority: CRITICAL)

#### Task B.1: Audio Recording (NAudio)
**Create:** `DiktaMe.Core/Audio/AudioRecorder.cs`, `AudioDeviceManager.cs`
**Effort:** 1 day

**Port from:** `python/core/recorder.py` (7.8KB)

**Steps:**
1. Create `AudioRecorder` class using `NAudio.Wave.WaveInEvent`
2. Implement `StartRecording()`, `StopRecording() → string audioFilePath`
3. Implement auto-stop on max duration (configurable, default 60s)
4. Implement `AudioDeviceManager` — enumerate devices, fuzzy-match by label
5. Save to temp WAV file (16kHz, 16-bit mono — Whisper-compatible)
6. Add recording state events: `RecordingStarted`, `RecordingStopped`, `AutoStopped`

**Tests:** Device enumeration, recording lifecycle, auto-stop timer, file format validation.

#### Task B.2: Text Injection (InputSimulator)
**Create:** `DiktaMe.Core/Input/TextInjector.cs`, `ClipboardManager.cs`
**Effort:** 1 day

**Port from:** `python/core/injector.py` (12KB)

**Steps:**
1. Implement `InjectText(string text, bool trailingSpace, string additionalKey)`
2. Two modes: Clipboard paste (`Ctrl+V`) and simulated typing
3. Implement `ClipboardManager` — save/restore clipboard content
4. Implement `CaptureSelection()` — sends `Ctrl+C`, reads clipboard, restores original
5. Implement `PressKey(string key)` — Enter, Tab, Space
6. Handle focus detection — ensure target window is active

**Tests:** Text injection, clipboard save/restore, key simulation.

#### Task B.3: Global Hotkeys (Win32 API)
**Create:** `DiktaMe.Core/Input/HotkeyManager.cs`
**Effort:** 0.5 day

**Port from:** `src/services/hotkeyManager.ts` (Electron globalShortcut)

**Steps:**
1. P/Invoke `RegisterHotKey` / `UnregisterHotKey` from `user32.dll`
2. Register 6 configurable hotkeys (Dictate, Ask, Refine, Translate, Oops, Note)
3. Message pump integration with WinUI 3's `DispatcherQueue`
4. Handle registration failures (hotkey already taken by another app)
5. Support runtime re-registration when user changes hotkeys in settings

**Tests:** Registration/unregistration, conflict detection.

#### Task B.4: Mute Detection
**Create:** `DiktaMe.Core/Audio/MuteDetector.cs`
**Effort:** 0.5 day

**Port from:** `python/core/mute_detector.py` (4KB)

**Steps:**
1. Use NAudio's `MMDeviceEnumerator` for CoreAudio COM access
2. Poll `AudioEndpointVolume.Mute` property every 3 seconds
3. Fuzzy-match device by label (same logic as Python version)
4. Fire `MuteStateChanged` event

#### Task B.5: System Tray
**Create:** `DiktaMe.App/Views/TrayIconView`
**Effort:** 0.5 day

**Port from:** `src/services/trayManager.ts`

**Steps:**
1. Use `H.NotifyIcon.WinUI` NuGet package
2. Create tray icon with context menu (same items as V1)
3. Menu items: Open Control Panel, Settings, Recording Status, Quit
4. Dynamic tooltip: "dIKta.me — [Cloud STT + Gemini LLM]"
5. Icon state changes: Idle, Recording, Processing, Error

---

### Work Stream C: STT & LLM Providers (Priority: CRITICAL)

#### Task C.1: STT Provider Interface & Router
**Create:** `DiktaMe.Core/STT/ISTTProvider.cs`, `STTRouter.cs`
**Effort:** 0.5 day

**Steps:**
1. Define `ISTTProvider` interface (see §3.3)
2. Create `STTRouter` — reads config, instantiates correct provider
3. Implement fallback: if local provider fails, try cloud (with notification)
4. Implement `GetCapabilities()` → which providers are available

#### Task C.2: Cloud STT — Deepgram Nova-2
**Create:** `DiktaMe.Core/STT/DeepgramProvider.cs`
**Effort:** 0.5 day

**Steps:**
1. REST API: `POST https://api.deepgram.com/v1/listen`
2. Send WAV file as binary body
3. Parse JSON response for transcript
4. Support language parameter (en, es, auto)
5. Handle errors: auth, rate limit, timeout
6. Track latency for metrics

#### Task C.3: Cloud STT — Gemini Flash Audio
**Create:** `DiktaMe.Core/STT/GeminiAudioProvider.cs`
**Effort:** 0.5 day

**Steps:**
1. Gemini Multimodal API with audio input
2. Send audio as base64 in request body
3. System prompt: "Transcribe the following audio exactly"
4. Handle API key auth (existing BYOK model)

#### Task C.4: Local STT — Whisper.net (Optional)
**Create:** `DiktaMe.Core/STT/WhisperProvider.cs`
**Effort:** 1 day

**Steps:**
1. Use `Whisper.net` NuGet (ONNX-based, no Python needed)
2. Load model from `AppData/diktate/models/` directory
3. CUDA support via `Whisper.net.Runtime.Cuda` package
4. Support Turbo V3 model
5. In-app model download with progress (or separate sidecar exe)
6. Return transcription + detected language

#### Task C.5: LLM Provider Interface & Router ✅
**Created:** `DiktaMe.Core/LLM/ILLMProvider.cs` (+ `LlmResult` record), `LLMRouter.cs`

- `ILLMProvider.ProcessAsync` returns `LlmResult` (text + latency + token counts)
- `LLMRouter`: primary/fallback pattern (mirrors STTRouter)

#### Task C.6: Cloud LLM Providers ✅
**Created:** `OpenAICompatibleProvider.cs`, `GeminiProvider.cs`, `AnthropicProvider.cs`

**Design decision:** `OpenAICompatibleProvider` replaces the single `OpenAIProvider` —
one class covers any endpoint that speaks `POST /v1/chat/completions` with Bearer auth:
- **OpenAI** (gpt-4o, gpt-4o-mini, o1, o3, …)
- **DeepSeek** (deepseek-chat, deepseek-reasoner)
- **OpenRouter** (routes to 200+ models via a single endpoint)
- **Groq** (llama, mixtral, …)
- **Together AI**, **Fireworks AI**, **Perplexity**
- **Azure OpenAI** (custom base URL per deployment)
- **LM Studio**, **vLLM**, **Llamafile** (local OpenAI-compatible servers)
- Any future provider that adopts the OpenAI spec

Named constructors: `ForOpenAI()`, `ForDeepSeek()`, `ForOpenRouter()`, `ForGroq()`,
`ForTogether()`, `ForPerplexity()`, `ForLocalServer()`.

`GeminiProvider`: generateContent API, API key (query param) + OAuth Bearer (ya29.*)
`AnthropicProvider`: Messages API, `x-api-key` + `anthropic-version` headers

All providers: exponential backoff retry (1s, 2s, 4s), 3 attempts, `LlmResult` with
input/output token counts from API usage fields.

#### Task C.7: Local LLM — Ollama ✅
**Created:** `DiktaMe.Core/LLM/OllamaProvider.cs`

- `POST /api/generate` with `stream: false` and `keep_alive: 10m`
- `WarmUpAsync()`: loads model into VRAM at startup (fire-and-forget)
- `IsAvailableAsync()`: polls `GET /api/tags` — returns false (not throws) if server down
- `LastTokensPerSec`: tracks inference speed; warns if < 20 tok/s (CPU fallback indicator)
5. Keep-alive session management

---

### Work Stream D: Pipeline Orchestration (Priority: HIGH)

#### Task D.1: Dictation Pipeline ✅
**Created:** `DiktaMe.Core/Pipeline/DictationPipeline.cs`

- STT → optional LLM cleanup → `TextInjector.InjectText`
- `RawMode = true` skips LLM entirely (true passthrough — V1 "raw" mode)
- LLM failure falls back to raw transcript (never drops output)
- `PipelineState` events: `Transcribing → Processing → Injecting → Idle`
- `PipelineResult` record: text, raw transcript, per-stage latencies, word count, provider names
- No-throw contract — all errors returned as failed `PipelineResult`

#### Task D.2: Refine Pipeline (Dual-Mode) ✅
**Created:** `DiktaMe.Core/Pipeline/RefinePipeline.cs`

- **Autopilot mode** (`stt = null`): `CaptureSelection → LLM cleanup → InjectText`
- **Instruction mode** (`stt` provided): `TranscribeAudio → CaptureSelection → LLM(selection + instruction) → InjectText`
- Fallback: if no text is selected in instruction mode, treats the spoken instruction as an Ask question (answer returned, not injected — matches V1 behaviour)
- Autopilot with no selection aborts with failure result

#### Task D.3: Ask, Translate, Note Pipelines ✅
**Created:** `AskPipeline.cs`, `TranslatePipeline.cs`, `NotePipeline.cs`

- **Ask:** STT → LLM Q&A → returns answer in `PipelineResult.Text` (not injected)
- **Translate:** STT (language="auto" for auto-detection) → LLM translate → inject; falls back to raw on LLM failure
- **Note:** STT → optional LLM formatting → `File.AppendAllTextAsync` with `## timestamp` header; creates directory if needed

#### Task D.4: Oops (Re-inject) ✅
**Updated:** `DiktaMe.Core/Input/TextInjector.cs`

- `LastInjectedText` property (null on startup, volatile — lost on restart)
- `ReInjectLast(trailingSpace, additionalKey)` — no-op if nothing stored
- `InjectText` now stores text before injection

---

### Work Stream E: Data & Security (Priority: HIGH)

> **Execution order:** E.0 first (unblocks DI), then E.1 + E.3 in parallel (settings model and secure storage are mutually needed), then E.2.

#### Task E.0: Wire DI Container ⚡ PRE-TASK ✅
**Modified:** `DiktaMe.App/App.xaml.cs` (`ConfigureServices`)

- `CancellationToken` added to `ISTTProvider` and `ILLMProvider` interfaces; propagated through all 4 STT providers + STTRouter, 5 LLM providers + LLMRouter, and all 5 pipelines
- `ConfigureServices()` fully wired: `AudioRecorder`, `MuteDetector`, `TextInjector`, `HotkeyManager` as singletons; `WhisperProvider` + `OllamaProvider` as local defaults (no API key required at startup); all 5 pipelines as transient
- Note: `AudioDeviceManager` and `ClipboardManager` are static utility classes — not registered in DI

#### Task E.1: Settings Manager ✅
**Created:** `DiktaMe.Core/Config/AppSettings.cs`, `SettingsManager.cs`, `ProfileManager.cs`, `PromptRepository.cs`, `STTProviderFactory.cs`, `LLMProviderFactory.cs`, `PipelineFactory.cs`

- `AppSettings`: strongly-typed record with `GeneralSettings`, `AudioSettings`, `PrivacySettings`, `HotkeySettings`, `ModeSettings` dictionary (8 modes × 2 profiles keyed `"{mode}_{profile}"`), 16-slot `CustomPrompts[]`, `ActiveProfile`, `NotesFilePath`, `OllamaModel`; `AppSettingsContext` source-generated JSON context for trim compatibility
- `SettingsManager`: persists to `%APPDATA%\DiktaMe\settings.json`; atomic write (`.tmp` → rename); `MergeDefaults()` for schema upgrades; `TryMigrateFromV1Async()` for dIKtate V1 electron-store migration; observable `Current` property + `SettingsChanged` event
- `ProfileManager`: dual-profile with `GetModeSettings(mode)` returning active profile's settings
- `PromptRepository`: CRUD over 16 prompt slots
- `STTProviderFactory` / `LLMProviderFactory`: runtime provider construction from settings + `SecureStorage` API keys; `LLMProviderFactory` includes `NullLlmProvider` for "none"/"skip" modes
- `PipelineFactory`: `CreateXxxPipeline()` helpers using `ProfileManager` + provider factories
- Tests: `SettingsManagerTests.cs` (unit + integration round-trip)

#### Task E.2: History & Metrics (SQLite) ✅
**Created:** `DiktaMe.Core/Data/HistoryManager.cs`, `MetricsCollector.cs`, `NoteWriter.cs`

- `HistoryManager`: SQLite at `%APPDATA%\DiktaMe\history.db`; schema: `history` table (id, timestamp, mode, text, raw_transcript, stt/llm providers, word_count, per-stage latencies, is_success) + `system_metrics` table; 90-day auto-pruning on `InitAsync`; privacy level compliance (Ghost=skip all, Stats=metrics only, Balanced=PII scrubbed, Full=verbatim); `dbPath` constructor param for test isolation
- `MetricsCollector`: in-memory session accumulators; `GetTodayStatsAsync()` via `HistoryManager`
- `NoteWriter`: static `AppendAsync()` helper for timestamped note file appending
- Tests: `HistoryManagerTests.cs` using temp db path

#### Task E.3: Security (Secrets + PII) ✅
**Created:** `DiktaMe.Core/Security/SecureStorage.cs`, `PIIScrubber.cs`, `ApiKeyValidator.cs`

- `SecureStorage`: DPAPI via `ProtectedData.Protect/Unprotect` (`DataProtectionScope.CurrentUser`); stores at `%APPDATA%\DiktaMe\keys.dat` as encrypted JSON blob; atomic writes; zeroes byte arrays in `finally` blocks; `SecureStorageJsonContext` source-generated JSON for trim compatibility
- `PIIScrubber`: compiled Regex patterns for email, US phone, credit card, SSN, API key prefixes; `Scrub(text) → string` replaces matches with `[REDACTED]`; `ContainsPII(text) → bool`
- `ApiKeyValidator`: static validators for OpenAI (`sk-`, ≥48 chars), Anthropic (`sk-ant-`), Gemini (≥30 chars), Deepgram (≥30 chars)
- Tests: `ApiKeyValidatorTests.cs`, `PIIScrubberTests.cs`, `SecureStorageTests.cs`

---

### Work Stream I: Promoted Deferred Features (Priority: MEDIUM-HIGH)

> These features were originally deferred from V1 but are included in V2 from the start.
> See §2.3 for the full list and scoping rationale.

#### Task I.1: Voice Snippets — Core Engine (SPEC_026, Phase 1 only) ✅
**Created:** `DiktaMe.Core/Config/SnippetManager.cs`

- `Snippet` record: `{ Id, Trigger, Content }` (GUID id, simple list)
- `SnippetManager`: load/save `%APPDATA%\DiktaMe\snippets.json` (atomic write); 100-snippet max; CRUD (`Add`, `Update`, `Remove`, `GetAll`)
- `ExpandSnippets(text)`: post-LLM pre-inject expansion; case-insensitive; ignores trailing punctuation; word-boundary check prevents mid-word false positives
- Integrated as optional `SnippetManager?` into `DictationPipeline`, `RefinePipeline`, `TranslatePipeline`, `NotePipeline`
- Registered as singleton in `App.xaml.cs`
- `SnippetListContext` source-generated JSON for trim compatibility
- Tests: `SnippetManagerTests.cs` — trigger matching, punctuation, multi-line, false-positive prevention, CRUD, persistence round-trip
- Note: Settings UI (Snippets tab in F.1) deferred to Stream F. Dynamic variables (`{{date}}`) deferred to V2.1.

#### Task I.2: Quick Chat Overlay (SPEC_042d) — ✅ core complete; UI deferred to F
**Create:** `DiktaMe.App/Views/QuickChatView.xaml`, `DiktaMe.Core/Pipeline/ChatPipeline.cs`
**Effort:** 1 day

**Completed (core):** 2026-02-17
- `ChatOptions` record added to `PipelineOptions.cs` — `TextInput` (typed) or `AudioFilePath` (voice), `SystemPrompt`, `Language`
- `ChatPipeline.cs` — text path skips STT; voice path does raw STT then LLM chat; no injection; follows same event/error pattern as other pipelines; `PipelineFactory.CreateChatPipeline()` added
- `ChatPipelineTests.cs` — 11 tests: text path, empty text, LLM passthrough, voice path, empty transcription, no-STT-provider, neither input, LLM empty, cancellation, state events, latency fields
- Build: 0 errors, 0 warnings; Tests: 343/343 passing

**Remaining (Stream F):**
1. New global hotkey: `Ctrl+Alt+C` (configurable)
2. Small floating WinUI 3 window (~400×300px), always-on-top, not modal
3. Input field (text) + Send button + Mic button (voice input)
4. Response area (scrollable, supports streaming)
5. `Escape` or click-outside closes overlay
6. Stateless MVP — no conversation history (multi-turn is V2.1)

**Tests:** window lifecycle (Stream F).

#### Task I.3: Control Panel Configuration (SPEC_043) — ✅ V1 DONE, port only
**Create:** Part of `SettingsView.xaml` (new "Control Panel" tab)
**Effort:** 0.25 day *(port from existing V1 implementation)*

**Steps:**
1. New Settings tab: "Control Panel" (below Notes tab)
2. Toggle switches mirroring HUD layout:
   - **Show Modes Row** (Standard, Prompt, Professional, RAW)
   - **Show Actions Row** (Sound, Local, +Key, Refine)
   - **Show Session Stats** (SESS, WORDS, WPM, TOK)
   - **Show Performance Stats** (TOT, REC, TRNS, PROC, INJ)
3. Bind to `AppSettings` observable properties
4. `ControlPanelView.xaml` — bind `Visibility` to settings via `BooleanToVisibilityConverter`
5. Default: all `true` (visible)

**Tests:** Toggle persistence, UI binding (manual verification).

#### Task I.4: Audio Ducking (SPEC_043d) ✅
**Create:** `DiktaMe.Core/Audio/AudioDucker.cs`
**Effort:** 0.5 day

**Completed:** 2026-02-17
- `AudioDucker.cs` — NAudio WASAPI `MMDeviceEnumerator`; `Duck()` stores original volumes and reduces to `DuckLevel`, `Restore()` restores them; skips own PID; thread-safe via `lock`
- `AttachTo(AudioRecorder)` / `DetachFrom(AudioRecorder)` — subscribes to `RecordingStarted`/`RecordingStopped`/`AutoStopped` events
- `AudioDuckingSettings` record added to `AppSettings` (Enabled, DuckLevelPercent defaults 20%)
- `AudioDucker` registered as singleton in `App.xaml.cs`; wired to `AudioRecorder` at construction
- `AudioDuckerTests.cs` — 17 tests: defaults, property mutation, disabled guard, idempotency, event wiring, dispose safety, settings defaults
- Build: 0 errors, 0 warnings; Tests: 312/313 passing (1 pre-existing clipboard flake)

#### Task I.5: Ollama Update Management (SPEC_031) ✅ (core complete; UI deferred to F)
**Create:** `DiktaMe.Core/System/OllamaManager.cs`
**Effort:** 1.5 days

**Completed (core):** 2026-02-17
- `OllamaManager.cs` in `DiktaMe.Core.SystemManagement` namespace (avoids BCL `System` collision)
- `models.json` embedded resource — 15 model entries with `minOllamaVersion`, family, recommended flag
- `CheckAsync(modelTag)` → `OllamaCheckResult` with `OllamaStatus` enum: Ready / Offline / VersionTooOld / ModelNotPulled
- Version sensing (`/api/version`), model health check (`/api/tags`), compatibility manifest, graceful fallback selection, version-change detection (persisted to `%APPDATA%\DiktaMe\ollama_last_version.txt`)
- `GetKnownModelsAsync()`, `GetInstalledModelTagsAsync()` — used by future Model Library UI
- `OllamaManager` registered as singleton in `App.xaml.cs`
- `OllamaManagerTests.cs` — 16 tests: version comparison, offline, VersionTooOld, ModelNotPulled, Ready, `:latest` normalisation, manifest loading, dispose safety; all with `FakeHttpHandler`
- Build: 0 errors, 0 warnings; Tests: 332/332 passing

**Deferred to Stream F:**
5. **"412 Rescue" UI:** Dialog with [Update Ollama] and [Use Fallback] buttons
6. **Model Library Tab:** List/pull/progress for installed models

#### Task I.6: Website Rebrand for V2 Launch (SPEC_042) — ✅ SITE LIVE, update only
**Create:** Updates to existing `website/` codebase (dikta.me already deployed)
**Effort:** 0.5 day *(copy/meta updates, not building from scratch)*

**Steps:**
1. Replace all "dIKtate" references with "dIKta.me" in copy, meta tags, structured data
2. Update download flow for new native installer (`diktame-setup.exe`)
3. Update homepage features/specs cards to reflect C# + WinUI 3 architecture
4. Update pricing page if tiers change
5. Remove references to "Electron + Python" stack in all marketing copy
6. Update OG images and social cards with new branding
7. Update `/docs` section with V2 installation guide and system requirements

**Acceptance:** dikta.me accurately represents V2 on launch day.

---

### Work Stream F: UI — WinUI 3 (Priority: HIGH)

#### Task F.1: Settings Window (Tabbed)
**Create:** `SettingsView.xaml`, `SettingsViewModel.cs`
**Effort:** 2 days

**Port from:** `src/settings.html` + `src/settings/*.ts`

**Tabs (same as V1 + new AI Engine tab):**
1. **General** — Language, auto-start, sound feedback, +Key behavior
2. **AI Engine** *(NEW)* — STT provider (Cloud/Local), LLM provider (Cloud/Local/Skip), capability summary
3. **Modes** — 8 mode tabs, each with Local/Cloud profile (model + prompt)
4. **Audio** — Device selection, max recording duration, test recording
5. **Privacy** — Logging intensity slider (0-3), PII scrubber toggle, wipe data
6. **API Keys** — Per-provider key input, test buttons, model listing
7. **Ollama** — Model management, status, warmup

**Design:** Use WinUI 3 Fluent Design — NavigationView with tabs, mica/acrylic materials, dark mode support.

#### Task F.2: Control Panel (Debug Dashboard)
**Create:** `ControlPanelView.xaml`, `ControlPanelViewModel.cs`
**Effort:** 1 day

**Port from:** `src/renderer.ts` (18KB)

**Content:** Recording state, current mode, last transcription, pipeline timing, system metrics (CPU/GPU/RAM), provider status, event log.

#### Task F.3: Configuration Wizard (First-Run)
**Create:** `WizardView.xaml`, `WizardViewModel.cs`
**Effort:** 1.5 days

**Steps:**
1. **Step 1: Welcome** — branding, "Build Your Stack" explanation
2. **Step 2: Choose Ears (STT)** — Cloud (recommended) / Local (advanced)
3. **Step 3: Choose Brain (LLM)** — Cloud / Ollama / Skip
4. **Step 4: Test** — Record 3s, show transcription, confirm working
5. **Step 5: Ready** — summary, "Start Dictating" button
6. Store `wizard_completed` flag in settings

#### Task F.4: Loading Screen
**Create:** `LoadingView.xaml`
**Effort:** 0.5 day

**Steps:**
1. Show during first startup (model warmup, capability detection)
2. Progress indicators per component
3. Skip straight to main app if cloud-only (no warmup needed)

#### Task F.5: Notification System ✅
**Create:** Toast notifications via Windows APIs
**Effort:** 0.25 day

**Steps:**
1. Use `Microsoft.Toolkit.Uwp.Notifications` for toast notifications
2. Sound feedback using `Windows.Media.Playback.MediaPlayer` for custom WAV files
3. Map all V1 notification types (success, error, mode change)
4. ✅ `SoundSettings` record with per-pipeline sound selection (StartSound, StopSound, UtilitySound)
5. ✅ `PlayCustomSound(stem)` resolves stem → `Assets\Sounds\{stem}.wav`, validates with `SafeStemPattern` regex
6. ✅ `GetAvailableSounds()` enumerates WAV files from Assets\Sounds\ directory
7. ✅ Audio settings page: sound enable toggle, start/stop/utility sound pickers with preview buttons
8. ✅ Removed toast popup during normal dictation — only start/stop sounds play (toasts reserved for errors)

---

### Work Stream G: Testing (Priority: HIGH)

#### Task G.1: Core Unit Tests (xUnit) ✅ (414 tests, gaps closed)
**Created:** `DiktaMe.Core.Tests/` — 32 test classes
**Effort:** Built incrementally alongside each stream

**Current state: 414 tests passing** (376 unit-only in CI; 1 pre-existing clipboard flake in TextInjectorTests — pre-existing, not new).

**Test coverage:**

| Test Class | Tests | Coverage |
|-----------|:---:|----------|
| PipelineTests | 27 | All 5 pipelines: full flow, fallback, raw mode, events, word count |
| HotkeyParserTests | 26 | All key combos, modifiers, edge cases |
| ApiKeyValidatorTests | 24 | All 5 providers × valid/invalid/edge |
| WhisperProviderTests | 19 | Model sizes, availability, download, dispose |
| SnippetManagerTests | 19 | CRUD, trigger matching, expansion, persistence |
| TextInjectorTests | 18 | Clipboard, injection, +Key, re-inject |
| AudioDuckerTests | 17 | Duck/restore, disabled, dispose, event wiring |
| OllamaManagerTests | 16 | Version comparison, health check, manifest, fallback |
| PIIScrubberTests | 15 | Email, phone, SSN, credit card, API keys |
| DeepgramProviderTests | 14 | Auth, errors, language, response parsing |
| PromptRepositoryTests | 14 | CRUD on 16 slots, bounds validation, clone isolation |
| GeminiAudioProviderTests | 13 | Auth, errors, language, response parsing |
| ProfileManagerTests | 13 | Active profile, GetModeSettings, SwitchProfile, SetModeSettings |
| MuteDetectorTests | 12 | Polling, device matching, events |
| OpenAICompatibleProviderTests | 12 | Auth, endpoint, sanitization, errors |
| HotkeyManagerTests | 12 | Register/unregister, lifecycle, events |
| ChatPipelineTests | 11 | Text + voice paths, cancellation, state events, error handling |
| AudioRecorderTests | 11 | Lifecycle, auto-stop, device resolution |
| AudioDeviceManagerTests | 11 | Enumeration, device properties |
| PipelineFactoryTests | 11 | Provider wiring, UseLlm=false, mode override, profile switching |
| HistoryManagerTests | 16 | Privacy levels (Ghost/Stats/Balanced/Full), stats aggregation, pruning, wipe |
| NoteWriterTests | 8 | Append, dir creation, empty no-op, timestamp format, cancellation |
| MetricsCollectorTests | 7 | Session accumulation, average latency, failed-skip, SessionStats record |
| ClipboardManagerTests | 7 | Set/get, Unicode, save/restore |
| SettingsManagerTests | 13 | Defaults, round-trip, atomic write, corrupt JSON, V1 migration, SettingsChanged event |
| LLMRouterTests | 7 | Primary/fallback routing |
| GeminiProviderTests | 6 | Auth, model, response parsing |
| OllamaProviderTests | 6 | Availability, localhost, warmup |
| SecureStorageTests | 5 | CRUD, round-trip, overwrite |
| AnthropicProviderTests | 5 | Headers, auth, response parsing |
| ScaffoldTests | 5 | CapabilityReport, TranscriptionResult |
| LlmResultTests | 3 | IsSuccess, empty, whitespace |

**Remaining low-priority gaps (not blocking):**

| Area | What's Missing |
|------|----------------|
| **SecureStorage** | Corruption recovery, concurrent access (DPAPI makes file-based mocking hard) |

**Mocking:** `Moq` for all external dependencies (HTTP, audio hardware, clipboard).

**Test Categorization:** Use xUnit Traits for CI filtering:
```csharp
[Fact, Trait("Category", "Unit")]           // Pure in-process logic, always run
[Fact, Trait("Category", "Integration")]    // Requires file I/O or real HTTP
[Fact, Trait("Category", "Hardware")]       // Requires audio device — skip in CI
```
Filter in CI: `dotnet test --filter "Category!=Integration&Category!=Hardware"`

#### Task G.2: CI/CD Pipeline (GitHub Actions) ✅
**Created:** `.github/workflows/ci-v2.yml` — single `windows-latest` job
**Effort:** 0.5 day

**Steps (in order):**
1. Restore — fails fast on hallucinated/non-existent NuGet packages
2. Lint — `dotnet format --verify-no-changes` (style drift)
3. Build — Meziantou.Analyzer + `TreatWarningsAsErrors=true` catch culture bugs, string comparisons, method-length violations at compile time
4. Test — `dotnet test --filter "Category!=Integration&Category!=Hardware"` (330 unit tests)
5. Test-count threshold — PowerShell parses TRX; fails if passing count < `ci/test-threshold.json` minimum (catches deleted tests)
6. Secret scan — gitleaks full-history scan for API keys / tokens
7. Vulnerability audit — `dotnet list package --vulnerable --include-transitive`; fails on HIGH/CRITICAL
8. Deprecated packages — informational warning, non-blocking
9. Publish — trimmed self-contained win-x64 output
10. Publish size guard — PowerShell checks MB range from `ci/test-threshold.json` (catches large code deletion)
11. Upload coverage artifact (Coverlet cobertura XML, 14-day retention)
12. Upload publish artifact (7-day retention)

**AI-coding safeguards added alongside CI:**
- `Meziantou.Analyzer 2.*` in `Directory.Build.props` — MA0006 (string.Equals), MA0011 (IFormatProvider), MA0051 (method length), MA0074 (culture-sensitive ToString)
- `NuGetAudit=true`, `NuGetAuditLevel=moderate` — blocks vulnerable deps at restore time
- `ci/test-threshold.json` — minimum 370 tests (updated after G.1 gap-fill), publish size 130–250 MB

**Suppression decisions log:** `ci/DECISIONS.md` — records every rule suppressed, the reason, and when it should be revisited. Key suppressions: MA0004 (ConfigureAwait — WinUI 3 sync-context requirement), MA0051 limits raised to 150 lines/80 statements (pipeline orchestrators), MA0074 in test project only (FluentAssertions false positives). Private `static`/`const` fields use PascalCase per convention; editorconfig `_camelCase` rule scoped to instance fields only.

**First real CI run (2026-02-18) caught:**
- `dotnet format` violations: `using` directives inside namespace declarations (auto-fixed), missing braces on `if`/`else` (auto-fixed), whitespace alignment (auto-fixed)
- Editorconfig naming rule too broad: `_camelCase` rule fired on `private static readonly` and `private const` fields — fixed by adding PascalCase rule for static fields at higher priority
- Coverage artifact warning: non-fatal (format lint ran before tests, so no TRX/coverage generated on that run)

---

### Work Stream H: Distribution & Migration (Priority: MEDIUM)

#### Task H.1: Installer (MSIX or Inno Setup)
**Effort:** 1 day

**Options:**
- **MSIX** — Microsoft Store ready, auto-update, sandboxed
- **Inno Setup** — Traditional installer, more control, smaller overhead

**Steps:**
1. Package trimmed self-contained output into installer
2. Include sound assets, icon, default prompts
3. Target installer size: ~70MB (compressed)
4. Register auto-start in Windows Task Scheduler
5. File associations (if needed)

#### Task H.2: V1 → V2 Migration ✅ (N/A — by design)

**Status:** Complete / Not Applicable. V2 is a clean-slate rewrite:
- Settings migration handled by J.3 (`SettingsMigration`) for V2 format evolution
- API keys: Electron `safeStorage` uses process-specific DPAPI context — cannot be decrypted from C#. Users re-enter keys via Configuration Wizard (F.3), which is the intended first-run flow
- History DB: Same SQLite schema at same `%APPDATA%` path — preserved automatically
- Custom prompts: Carried forward via `PromptRepository`

---

## 5. Execution Timeline

```
Day 0 (Pre-Work): A.0 — Git Repo Prep & V1 Archive
├── Tag V1 as v1.0.0 in diktate repo
├── Create geckogtmx/diktame on GitHub
└── Clone + initial commit (README, .gitignore)

Week 1: Foundation + Core Engine
├── Day 1:   A.1 (Scaffold) + A.2 (Publish config)
├── Day 2:   B.1 (Audio Recording)
├── Day 3:   B.2 (Text Injection) + B.3 (Global Hotkeys)
├── Day 4:   B.4 (Mute Detection) + B.5 (System Tray)
└── Day 5:   C.1 (STT Interface) + C.2 (Deepgram) + C.3 (Gemini Audio)
                → Tag: v2.0.0-alpha.1 ("it records and transcribes")

Week 2: Providers + Pipelines
├── Day 6:   C.5 (LLM Interface) + C.6 (Cloud LLM Providers)
├── Day 7:   C.7 (Ollama) + C.4 (Whisper.net — optional)
├── Day 8:   D.1 (Dictation Pipeline)
├── Day 9:   D.2 (Refine Pipeline) + D.3 (Ask/Translate/Note) + D.4 (Oops)
├── Day 10:  E.0 (Wire DI Container — pre-task) + E.1 + E.3 (Settings + Security, parallel)
└── Day 11:  E.2 (History & Metrics)
                → Tag: v2.0.0-alpha.2 ("full dictation pipeline works")

Week 3: UI + Promoted Features
├── Day 12-13: F.1 (Settings Window)
├── Day 14:    F.2 (Control Panel) + I.3 (CP Config toggles) + I.4 (Audio Ducking)
├── Day 15:    F.3 (Wizard) + F.4 (Loading) + F.5 (Notifications)
├── Day 16:    I.1 (Voice Snippets) + I.2 (Quick Chat Overlay)
└── Day 17:    I.5 (Ollama Management — start)
                → Tag: v2.0.0-beta.1 ("feature complete, untested")

Week 4: Testing + Distribution
├── Day 18:    I.5 (Ollama Management — finish) + I.6 (Website Rebrand)
├── Day 19-20: G.1 (Tests — complete 170+ including promoted feature tests)
├── Day 21:    G.2 (CI/CD) + H.1 (Installer)
├── Day 22:    H.2 (Migration) + Manual QA
└── Day 23:    Final polish, README update, release prep
                → Tag: v2.0.0-rc1 → v2.0.0 (release)
```

**Total: ~23 developer-days (4.5 weeks)** + Day 0 pre-work

> *Note: Timeline increased from original 20 days due to 6 promoted deferred features (+~6 days gross, but some tasks overlap with existing work streams, net +3 days including E.0 DI wiring).*

---

## 6. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| WinUI 3 learning curve for AI agents | Medium | Extensive examples in training data; CommunityToolkit helpers |
| IL trimming breaks runtime behavior | Low | Using `TrimMode=partial` (safe default); trim-incompatible packages rooted as `TrimmerRootAssembly` |
| NAudio API differences from PyAudio | Low | NAudio is mature, well-documented, widely used |
| Whisper.net model quality vs faster-whisper | Medium | Benchmark early; keep Python sidecar as fallback option |
| V1 settings migration edge cases | Low | Validate with actual V1 config files from dev machine |
| Global hotkey conflicts on user machines | Low | Same risk as V1; handle gracefully with notification |

---

## 7. Key NuGet Packages

```xml
<!-- DiktaMe.Core -->
<PackageReference Include="NAudio" Version="2.*" />
<PackageReference Include="InputSimulatorStandard" Version="1.*" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />
<PackageReference Include="Serilog" Version="3.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />
<PackageReference Include="Whisper.net" Version="1.*" />              <!-- Optional -->
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.*" /> <!-- Optional -->

<!-- DiktaMe.App -->
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.*" />
<PackageReference Include="H.NotifyIcon.WinUI" Version="2.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.*" />

<!-- DiktaMe.Core.Tests -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
```

---

## 8. Developer Notes

### For AI Coding Agents

This project is designed to be built by AI coding assistants. Each task is self-contained with:
- **Clear input:** what to port from (V1 file reference + size)
- **Clear output:** what files to create
- **Clear acceptance:** what "done" looks like

**Recommended approach per task:**
1. Read the V1 source file listed in "Port from"
2. Understand the behavior and edge cases
3. Implement in C# following the interface patterns in §3.3
4. Write xUnit tests alongside implementation
5. Verify build: `dotnet build`

### Git Workflow

See §9 (Git Strategy) for the full branching model, tagging, and commit conventions.

### Settings File Location
```
%APPDATA%/DiktaMe/
├── settings.json          # All app settings
├── keys.dat               # DPAPI-encrypted API keys
├── history.db             # SQLite (same as V1)
├── models/                # Downloaded Whisper models (optional)
│   └── ggml-large-v3-turbo.bin
└── logs/
    └── diktame_20260214.log
```

---

## 9. Git Strategy

### 9.1 Repository Model

| Repo | Path | Purpose | Status |
|------|------|---------|--------|
| **diktate** | `E:\git\diktate\` / `geckogtmx/diktate` | V1 — Electron + Python | **Living sandbox** — daily driver + prototyping |
| **diktame** | `E:\git\diktame\` / `geckogtmx/diktame` | V2 — C# + WinUI 3 | **Active development** |

**Why a new repo (not a branch):**
- Clean commit history — no 100+ V1 commits cluttering `git log`
- Different tech stack = different `.gitignore`, CI/CD, tooling
- No risk of accidentally merging incompatible code
- V1 stays intact and runnable for daily use and rapid prototyping

### 9.2 Branching Model: Trunk-Based Development

```
main ────●────●────●────●────●────●────●──── ... ────●
         A.0  A.1  B.1  B.2  B.3  C.1  C.2        v2.0.0
```

**No feature branches. No PRs.** Single contributor + AI agent = trunk-based is fastest.

- All commits go directly to `main`
- Each commit = one completed task (or meaningful sub-task)
- If a task takes >1 day, intermediate commits are fine (WIP is okay on main for solo dev)
- Never push broken builds — always `dotnet build` before commit

### 9.3 Commit Conventions

**Format:** [Conventional Commits](https://www.conventionalcommits.org/)

```
<type>(<scope>): <description> [<TASK_ID>]
```

**Types:**

| Type | When |
|------|------|
| `feat` | New feature or capability |
| `fix` | Bug fix |
| `refactor` | Code restructuring (no behavior change) |
| `test` | Adding or updating tests |
| `chore` | Tooling, CI, config, dependencies |
| `docs` | Documentation only |

**Scopes** (match solution structure):

| Scope | Maps to |
|-------|---------|
| `audio` | `DiktaMe.Core/Audio/` |
| `stt` | `DiktaMe.Core/STT/` |
| `llm` | `DiktaMe.Core/LLM/` |
| `pipeline` | `DiktaMe.Core/Pipeline/` |
| `input` | `DiktaMe.Core/Input/` |
| `config` | `DiktaMe.Core/Config/` |
| `data` | `DiktaMe.Core/Data/` |
| `security` | `DiktaMe.Core/Security/` |
| `system` | `DiktaMe.Core/System/` |
| `ui` | `DiktaMe.App/Views/` |
| `ci` | GitHub Actions, build config |
| `installer` | MSIX / Inno Setup |

**Examples:**
```bash
git commit -m "feat(audio): implement AudioRecorder with NAudio [B.1]"
git commit -m "feat(stt): add Deepgram Nova-2 cloud STT provider [C.2]"
git commit -m "feat(pipeline): implement dictation pipeline [D.1]"
git commit -m "feat(config): add SnippetManager with trigger matching [I.1]"
git commit -m "feat(ui): add Quick Chat overlay window [I.2]"
git commit -m "test(pipeline): add DictationPipeline unit tests [G.1]"
git commit -m "chore(ci): add GitHub Actions build + test workflow [G.2]"
```

### 9.4 Tagging Strategy

Semantic versioning with milestone tags tied to the timeline:

| Tag | When | Meaning |
|-----|------|---------|
| `v2.0.0-alpha.1` | End of Week 1 (Day 5) | Core engine works — records audio, transcribes via cloud |
| `v2.0.0-alpha.2` | End of Week 2 (Day 11) | Full pipeline functional — dictate, refine, ask, translate all work |
| `v2.0.0-beta.1` | End of Week 3 (Day 17) | Feature complete — all UI + promoted features, untested |
| `v2.0.0-rc1` | Day 22 | Release candidate — tests pass, installer works |
| `v2.0.0` | Day 23 | 🚀 **Ship it** |

```bash
# Tagging commands
git tag -a v2.0.0-alpha.1 -m "Alpha 1: core engine — audio + cloud STT working"
git push origin v2.0.0-alpha.1
```

### 9.5 V1 Sandbox Strategy

V1 (`diktate`) is **not archived** — it remains a **living sandbox** throughout V2 development.

**Dual-purpose during V2 development:**

| Role | How |
|------|-----|
| **Daily driver** | Keep using V1 for actual dictation work while V2 is being built |
| **Rapid prototyping** | Python is faster to iterate on — test new ideas in V1 before porting to C# |
| **Prompt lab** | Tune system prompts and LLM behaviors in V1, then copy finalized prompts to V2 |
| **Feature validation** | Mock up UX flows in Electron first, validate with real usage, then port the proven design |

**Good candidates for V1 prototyping:**
- Voice Snippet trigger matching logic (Python regex is instant to iterate)
- Audio ducking (test `pycaw` WASAPI before porting to NAudio)
- New LLM provider integrations (new API → test in Python → port to C#)
- Prompt engineering for all 8 modes
- Quick Chat UX flow and system prompt tuning

**V1 tagging (Task A.0):**

```bash
# In E:\git\diktate\
git add -A && git commit -m "chore: V1 baseline before V2 development begins"
git tag -a v1.0.0 -m "V1 baseline — Electron + Python stack"
git push origin main --tags
```

The `v1.0.0` tag marks the starting point, but **development can continue** on V1's `main` branch for experiments. Use `exp/` prefix commits for prototype work:

```bash
git commit -m "exp(snippets): prototype trigger matching in Python"
git commit -m "exp(ducking): test pycaw WASAPI audio ducking"
```

**Sunset timeline:** V1 is retired only after V2 reaches feature parity and passes manual QA. Expected: when `v2.0.0` ships.

> **Spec documents** live in the V1 repo (`diktate/docs/internal/specs/`). During V2 development, reference them by path. Copy finalized specs to `diktame/docs/` once V2 ships.

### 9.6 .gitignore Template

```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.userosscache
*.sln.docstates

# NuGet
packages/
*.nupkg

# Build results
[Dd]ebug/
[Rr]elease/
x64/
x86/

# IDE
.vs/
.vscode/
.idea/
*.swp

# OS
Thumbs.db
.DS_Store

# Project-specific
publish/
*.msix
*.appx
```

---

## 10. Post-Ship: V2.1 Priorities

> These are explicitly out of scope for V2.0. Evaluate after v2.0.0 ships based on user feedback.

| Priority | Feature | Why |
|----------|---------|-----|
| 🥇 1 | **Streaming LLM responses** | Biggest single UX improvement — token-by-token injection dramatically reduces perceived latency for Ask/Translate modes. Requires `IAsyncEnumerable<string>` streaming interface on `ILLMProvider` and WinUI 3 live-update injection. |
| 🥈 2 | **Voice Activity Detection (VAD)** | Hands-free mode — auto-detect speech start/end (WebRTC VAD or Silero). Major differentiator vs Windows Voice Typing. |
| 🥉 3 | **Conversation memory for Ask mode** | Optional rolling context window (last N exchanges) so follow-up questions feel natural. |
| 4 | **Command mode** | Voice commands to control the app: "switch to Spanish", "use local model", "open settings". |
| 5 | **Auto-language detection per-phrase** | Leverage existing `TranscriptionResult.DetectedLanguage` to auto-switch LLM prompts for bilingual users (EN/ES code-switching). Low effort — consider pulling into V2.0 late. |
| 6 | **Recording mode: Toggle vs Hold-to-Record** | Add a setting to choose between "press to start / press to stop" (current) and "hold to record / release to stop". Requires detecting key-up events in HotkeyManager (Win32 `WM_KEYUP` or `RegisterRawInputDevices`). |
| 7 | **Voice Snippets Phase 2** | Dynamic variables (`{{date}}`, `{{clipboard}}`) and cursor placement. |
| 8 | **Plugin/extension system** | Third-party integrations (Notion, Obsidian, VS Code). V3 territory. |

---

---

## 11. Stream J: CRUD Dictation Modes (Post-MVP Enhancement)

> **Status:** ✅ COMPLETE — All tasks (J.1–J.7) finished
> **Effort:** 13-16 hours (est.), ~12 hours spent
> **Context:** After completing hotkey pipeline integration (Stream I), user requested transformation of dictation modes from fixed presets into a CRUD system with dual-profile architecture.

### 11.1 Context & Problem Statement

**V1 Dictation Modes (Fixed):**
- 4 built-in modes: Standard, Prompt, Professional, RAW
- Each mode had a system prompt + optional LLM processing
- Users could NOT create custom modes or rename built-ins
- Mode selection via hotkeys + UI toggle buttons

**V2 Current State:**
- `ModeSettings` record in `AppSettings` with `PromptSlot` field (0-15 for custom, -1 for default)
- `PromptDefaults.cs` provides 6 built-in templates (Dictate, Refine, Ask, Translate, Note, Chat)
- 16 custom prompt slots in `PromptRepository` (slots 0-15)
- Dual-profile system (Cloud vs Local) per mode
- **Gap:** Users still can't create/rename/delete dictation modes as first-class entities

**User Request:**
> "Make modes CRUD. With a title that can be changed by the user (examples: Standard, Prompt, Professional). Need 'Dual Profile' for Local and Cloud, with Cloud option having per-mode model selection. Remember these are **Dictation Modes only** — other pipelines (Ask, Refine, Note) are still managed independently."

**Key Clarifications:**
- **CRUD only for dictation modes** — Ask, Refine, Translate, Note, Chat remain as fixed utility pipelines
- **Cloud profiles support per-mode model selection** — different LLM model per dictation mode
- **Local profiles use global Ollama model** — switching models on-the-fly is difficult with Ollama
- **STT provider resolved by profile** — Cloud uses Deepgram/Gemini, Local uses Whisper

### 11.2 Architecture Design

#### 11.2.1 Core Records

**File:** [src/DiktaMe.Core/Config/DictationMode.cs](src/DiktaMe.Core/Config/DictationMode.cs) (NEW)

```csharp
namespace DiktaMe.Core.Config;

/// <summary>
/// User-configurable dictation mode with dual-profile support.
/// Represents a named workflow (e.g., "Standard", "Professional", "Custom Interview Mode").
/// </summary>
public sealed record DictationMode
{
    /// <summary>Unique ID (GUID) for this mode.</summary>
    public required string Id { get; init; }

    /// <summary>User-visible title (e.g., "Standard", "Professional", "Interview Notes").</summary>
    public required string Title { get; init; }

    /// <summary>Cloud profile configuration (Cloud STT + Cloud LLM).</summary>
    public required DictationProfile CloudProfile { get; init; }

    /// <summary>Local profile configuration (Whisper + Ollama).</summary>
    public required DictationProfile LocalProfile { get; init; }

    /// <summary>Whether this is a built-in mode (cannot be deleted).</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Sort order for UI display (0-based).</summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// Profile-specific configuration for a dictation mode.
/// Cloud profiles support per-mode model selection; Local profiles use global Ollama model.
/// </summary>
public sealed record DictationProfile
{
    /// <summary>System prompt for LLM processing (or null for raw mode).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Whether to use LLM processing (false = raw transcription).</summary>
    public bool UseLlm { get; init; } = true;

    /// <summary>
    /// LLM model name (Cloud profiles only).
    /// Examples: "gpt-4o", "claude-sonnet-4", "gemini-2.0-flash".
    /// For Local profiles, this is ignored (Ollama uses global model from AppSettings).
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>Hotkey string (e.g., "Ctrl+Alt+D"). Null = no hotkey assigned.</summary>
    public string? Hotkey { get; init; }
}
```

**File:** [src/DiktaMe.Core/Config/PipelineConfig.cs](src/DiktaMe.Core/Config/PipelineConfig.cs) (NEW)

```csharp
namespace DiktaMe.Core.Config;

/// <summary>
/// Fixed configuration for utility pipelines (Ask, Refine, Translate, Note, Chat).
/// These are NOT user-creatable — they have fixed behaviors with dual-profile customization.
/// </summary>
public sealed record PipelineConfig
{
    /// <summary>Pipeline type (e.g., "ask", "refine", "translate").</summary>
    public required string PipelineType { get; init; }

    /// <summary>Cloud profile (prompt + model).</summary>
    public required UtilityProfile CloudProfile { get; init; }

    /// <summary>Local profile (prompt only; Ollama model is global).</summary>
    public required UtilityProfile LocalProfile { get; init; }

    /// <summary>Hotkey string (e.g., "Ctrl+Alt+A" for Ask).</summary>
    public string? Hotkey { get; init; }
}

/// <summary>
/// Profile configuration for utility pipelines.
/// Simpler than DictationProfile (no UseLlm flag, LLM is always used except for Note's optional mode).
/// </summary>
public sealed record UtilityProfile
{
    /// <summary>System prompt (required for Ask/Refine/Translate/Chat, optional for Note).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>LLM model name (Cloud profiles only).</summary>
    public string? ModelName { get; init; }
}
```

#### 11.2.2 AppSettings Integration

**File:** [src/DiktaMe.Core/Config/AppSettings.cs](src/DiktaMe.Core/Config/AppSettings.cs) (MODIFY)

Add these properties:

```csharp
/// <summary>User's dictation modes (CRUD). Serialized as JSON array.</summary>
public List<DictationMode> DictationModes { get; init; } = [];

/// <summary>Fixed utility pipeline configs (Ask, Refine, Translate, Note, Chat).</summary>
public List<PipelineConfig> UtilityPipelines { get; init; } = [];

/// <summary>Active profile (Cloud or Local).</summary>
public string ActiveProfile { get; init; } = "Cloud"; // "Cloud" or "Local"
```

Remove deprecated fields:
- `CustomPrompts` (replaced by `DictationMode.CloudProfile.SystemPrompt` / `LocalProfile.SystemPrompt`)
- Mode-specific settings in nested `ModeSettings` records (replaced by `DictationProfile` / `UtilityProfile`)

#### 11.2.3 Default Mode Factory

**File:** [src/DiktaMe.Core/Config/DictationModeDefaults.cs](src/DiktaMe.Core/Config/DictationModeDefaults.cs) (NEW)

```csharp
namespace DiktaMe.Core.Config;

/// <summary>
/// Factory for creating the 4 built-in dictation modes.
/// Called by SettingsManager when initializing AppSettings for the first time.
/// </summary>
public static class DictationModeDefaults
{
    public static List<DictationMode> CreateBuiltInModes()
    {
        return
        [
            new DictationMode
            {
                Id = "dictate-standard",
                Title = "Standard",
                IsBuiltIn = true,
                SortOrder = 0,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.Dictate,
                    UseLlm = true,
                    ModelName = "gpt-4o-mini", // default Cloud model
                    Hotkey = "Ctrl+Alt+D",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.Dictate,
                    UseLlm = true,
                    ModelName = null, // Ollama model from AppSettings.OllamaModelName
                    Hotkey = "Ctrl+Alt+D",
                },
            },

            new DictationMode
            {
                Id = "dictate-prompt",
                Title = "Prompt",
                IsBuiltIn = true,
                SortOrder = 1,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = "Follow the user's custom instruction exactly. Return ONLY the result.",
                    UseLlm = true,
                    ModelName = "gpt-4o",
                    Hotkey = "Ctrl+Alt+P",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = "Follow the user's custom instruction exactly. Return ONLY the result.",
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+P",
                },
            },

            new DictationMode
            {
                Id = "dictate-professional",
                Title = "Professional",
                IsBuiltIn = true,
                SortOrder = 2,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = "Transform this into formal, professional business writing. Fix grammar, remove filler words, use active voice. Return ONLY the polished text.",
                    UseLlm = true,
                    ModelName = "claude-sonnet-4",
                    Hotkey = "Ctrl+Alt+Shift+D",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = "Transform this into formal, professional business writing. Fix grammar, remove filler words, use active voice. Return ONLY the polished text.",
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+Shift+D",
                },
            },

            new DictationMode
            {
                Id = "dictate-raw",
                Title = "RAW",
                IsBuiltIn = true,
                SortOrder = 3,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = null,
                    UseLlm = false, // raw transcription, no LLM
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+R",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = null,
                    UseLlm = false,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+R",
                },
            },
        ];
    }

    public static List<PipelineConfig> CreateBuiltInUtilityPipelines()
    {
        return
        [
            new PipelineConfig
            {
                PipelineType = "ask",
                Hotkey = "Ctrl+Alt+A",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Ask,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Ask,
                    ModelName = null, // Ollama global model
                },
            },

            new PipelineConfig
            {
                PipelineType = "refine",
                Hotkey = "Ctrl+Alt+F",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Refine,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Refine,
                    ModelName = null,
                },
            },

            new PipelineConfig
            {
                PipelineType = "translate",
                Hotkey = "Ctrl+Alt+T",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Translate,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Translate,
                    ModelName = null,
                },
            },

            new PipelineConfig
            {
                PipelineType = "note",
                Hotkey = "Ctrl+Alt+N",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Note,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Note,
                    ModelName = null,
                },
            },

            new PipelineConfig
            {
                PipelineType = "chat",
                Hotkey = "Ctrl+Alt+C",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Chat,
                    ModelName = "gpt-4o",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Chat,
                    ModelName = null,
                },
            },
        ];
    }
}
```

### 11.3 Implementation Tasks

#### Task J.1: Core Data Models ✅
**Effort:** 2 hours | **Tests:** 23 new (DictationModeDefaultsTests, PromptDefaultsTests)
**Create:**
1. [src/DiktaMe.Core/Config/DictationMode.cs](src/DiktaMe.Core/Config/DictationMode.cs) — `DictationMode` and `DictationProfile` records
2. [src/DiktaMe.Core/Config/PipelineConfig.cs](src/DiktaMe.Core/Config/PipelineConfig.cs) — `PipelineConfig` and `UtilityProfile` records
3. [src/DiktaMe.Core/Config/DictationModeDefaults.cs](src/DiktaMe.Core/Config/DictationModeDefaults.cs) — Factory for 4 built-in dictation modes + 5 utility pipelines

**Modify:**
4. [src/DiktaMe.Core/Config/AppSettings.cs](src/DiktaMe.Core/Config/AppSettings.cs) — Add `List<DictationMode>`, `List<PipelineConfig>`, `ActiveProfile` properties; remove deprecated `CustomPrompts` and mode-specific settings

**Tests:**
- Unit tests for default factory (4 built-in modes, correct prompts, sort order)
- Serialization round-trip (JSON → AppSettings → JSON)

---

#### Task J.2: DictationModeManager (CRUD Service) ✅
**Effort:** 2.5 hours | **Tests:** 23 new (DictationModeManagerTests)
**Create:** [src/DiktaMe.Core/Config/DictationModeManager.cs](src/DiktaMe.Core/Config/DictationModeManager.cs), [src/DiktaMe.Core/Config/PipelineConfigManager.cs](src/DiktaMe.Core/Config/PipelineConfigManager.cs)

**Methods:**
```csharp
public sealed class DictationModeManager
{
    private readonly SettingsManager _settings;

    public List<DictationMode> GetAllModes();
    public DictationMode? GetModeById(string id);
    public DictationMode CreateMode(string title, DictationProfile cloudProfile, DictationProfile localProfile);
    public void UpdateMode(string id, string title, DictationProfile cloudProfile, DictationProfile localProfile);
    public void DeleteMode(string id); // throws if IsBuiltIn = true
    public void ReorderModes(List<string> orderedIds); // update SortOrder
    public DictationProfile GetActiveProfile(string modeId); // returns CloudProfile or LocalProfile based on AppSettings.ActiveProfile
}
```

**Tests:**
- Create custom mode, verify ID generation (GUID)
- Update mode, verify changes persist
- Delete built-in mode → throws exception
- Delete custom mode → removed from AppSettings.DictationModes
- Reorder modes → SortOrder updated correctly
- GetActiveProfile("Cloud") → returns CloudProfile
- GetActiveProfile("Local") → returns LocalProfile

---

#### Task J.3: Settings Migration (V1 → V2 CRUD Format) ✅
**Effort:** 2 hours
**Modify:** [src/DiktaMe.Core/Config/SettingsManager.cs](src/DiktaMe.Core/Config/SettingsManager.cs)

**Migration logic:**
- On first load, if `AppSettings.DictationModes` is empty → call `DictationModeDefaults.CreateBuiltInModes()`
- If loading settings from disk with old format (CustomPrompts array) → migrate to new format:
  - Map `CustomPrompts[0]` to first custom dictation mode's CloudProfile.SystemPrompt
  - Map old `ModeSettings` records to `DictationProfile` / `UtilityProfile`
- Write migrated settings back to disk with new JSON schema

**Tests:**
- Load empty settings → auto-populate 4 built-in modes
- Load V1 settings JSON → migrate to CRUD format, verify prompts preserved
- Migration idempotency (running twice doesn't duplicate modes)

---

#### Task J.4: Pipeline Integration (LoadingViewModel Updates) ✅
**Effort:** 2 hours
**Modify:** [src/DiktaMe.App/ViewModels/LoadingViewModel.cs](src/DiktaMe.App/ViewModels/LoadingViewModel.cs)

**Changes:**
- Replace `_profiles.GetModeSettings("dictate")` with `_dictationModes.GetActiveProfile(modeId)`
- Pass `DictationProfile.ModelName` to `LLMRouter` (Cloud profiles only)
- Update hotkey registration loop to iterate over `AppSettings.DictationModes` + `AppSettings.UtilityPipelines`

**Example:**
```csharp
private async Task RunDictationPipelineAsync(string modeId)
{
    DictationProfile profile = _dictationModes.GetActiveProfile(modeId);

    var options = new DictationOptions
    {
        SystemPrompt = profile.UseLlm ? profile.SystemPrompt : null,
        RawMode = !profile.UseLlm,
        Language = _settings.Current.General.Language,
        ModelName = profile.ModelName, // NEW: per-mode model (Cloud only)
        Injection = new PipelineInjectionOptions { /* ... */ },
    };

    var pipeline = _pipelineFactory.CreateDictationPipeline();
    var result = await pipeline.RunAsync(audioFile, options, ct);
}
```

**Tests:**
- Cloud profile with custom model → verify `ModelName` passed to LLMRouter
- Local profile → verify `ModelName` is null (global Ollama model used)
- RAW mode (UseLlm=false) → verify LLM step skipped

---

#### Task J.5: LLMRouter Per-Mode Model Support ✅
**Effort:** 1.5 hours | **Tests:** 35 new (ModelListServiceTests, LLMProviderExtensionsTests, LLMRouterTests)
**Create:**
1. [src/DiktaMe.Core/LLM/ModelInfo.cs](src/DiktaMe.Core/LLM/ModelInfo.cs) — `ModelInfo` record (ModelId, DisplayName, Provider, IsAvailable, ContextWindow)
2. [src/DiktaMe.Core/LLM/ModelListService.cs](src/DiktaMe.Core/LLM/ModelListService.cs) — Queries real API endpoints for all configured providers in parallel

**Modify:**
3. [src/DiktaMe.Core/Pipeline/PipelineOptions.cs](src/DiktaMe.Core/Pipeline/PipelineOptions.cs) — Added `ModelName` to all 6 options records
4. [src/DiktaMe.Core/LLM/LLMRouter.cs](src/DiktaMe.Core/LLM/LLMRouter.cs) — New `ProcessAsync(text, prompt, modelName, mode, ct)` overload; takes `ILLMProviderFactory` for dynamic provider resolution
5. [src/DiktaMe.Core/LLM/ILLMProvider.cs](src/DiktaMe.Core/LLM/ILLMProvider.cs) — Added `LLMProviderExtensions.ProcessWithModelAsync()` extension method
6. All 6 pipeline files — Use `ProcessWithModelAsync()` for transparent model routing
7. [src/DiktaMe.App/ViewModels/LoadingViewModel.cs](src/DiktaMe.App/ViewModels/LoadingViewModel.cs) — All 5 handlers pass `ModelName = profile.ModelName`

**Architecture:**
- `ModelListService` queries real API model-list endpoints (OpenAI `/v1/models`, Anthropic `/v1/models`, Gemini `/v1beta/models`, OpenRouter `/api/v1/models`, Ollama `/api/tags`)
- `ResolveProviderFromModelId()` maps model prefixes to providers (`gpt-*` → openai, `claude-*` → anthropic, `gemini-*` → gemini, `deepseek-*` → deepseek, `*/` → openrouter, unknown → ollama)
- `LLMRouter.ResolveProviderForModel()` delegates to `ILLMProviderFactory.CreateProvider()` which handles API key retrieval from SecureStorage

---

#### Task J.6: UI — Dictation Modes Settings Tab (CRUD Interface) ✅
**Effort:** 3-4 hours (actual: ~3.5 hours)
**Created:**
1. [src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml](src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml)
2. [src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml.cs](src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml.cs)
3. [src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs](src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs)

**Modified:**
1. [src/DiktaMe.Core/Config/DictationModeManager.cs](src/DiktaMe.Core/Config/DictationModeManager.cs) — Removed built-in mode edit restriction
2. [tests/DiktaMe.Core.Tests/Config/DictationModeManagerTests.cs](tests/DiktaMe.Core.Tests/Config/DictationModeManagerTests.cs) — Updated test expectations
3. [src/DiktaMe.App/Views/SettingsWindow.xaml.cs](src/DiktaMe.App/Views/SettingsWindow.xaml.cs) — Replaced legacy ModesSettingsPage
4. [src/DiktaMe.App/App.xaml.cs](src/DiktaMe.App/App.xaml.cs) — Registered DictationModesSettingsViewModel in DI

**Implemented Features:**
- **Left sidebar (200px):** ListView of dictation modes + utility pipelines
  - Dictation modes (CRUD): Standard, Prompt, Professional, RAW + custom modes
  - Separator: "── Utility Pipelines ──"
  - Utility pipelines (update-only): Ask, Refine, Translate, Note, Chat
  - Built-in modes show lock icon 🔒 (can edit but not delete)
  - Each item shows: Title, Subtitle ("Built-in", "Custom", "Utility")
- **Right panel (520px max-width):** Unified editor for selected mode/pipeline
  - **Title** TextField (editable for all)
  - **Cloud Profile section:**
    - Model ComboBox with real-time API discovery via `ModelListService`
    - Refresh button + loading spinner
    - System Prompt TextBox (120-200px, multiline, auto-wrap)
    - "Use LLM" ToggleSwitch (dictation modes only)
    - Hotkey TextField (e.g., "Ctrl+Alt+D")
  - **Local Profile section:**
    - InfoBar banner: "Local profile uses global Ollama model"
    - System Prompt TextBox
    - "Use LLM" ToggleSwitch (dictation modes only)
    - Hotkey TextField
  - **Save** button (Accent style, right-aligned)
- **Bottom toolbar (left sidebar):**
  - [+] New Mode → creates custom mode with default prompt
  - [↑] Move Up → reorders dictation modes
  - [↓] Move Down → reorders dictation modes
  - [🗑️] Delete → deletes custom modes (disabled for built-ins/pipelines)

**Commands Implemented:**
- `CreateModeCommand` → `DictationModeManager.CreateModeAsync()`
- `SaveCommand` → `DictationModeManager.UpdateModeAsync()` or `PipelineConfigManager.UpdatePipelineAsync()`
- `DeleteModeCommand` → `DictationModeManager.DeleteModeAsync()` (throws for built-ins)
- `MoveUpCommand` / `MoveDownCommand` → `DictationModeManager.ReorderModesAsync()`
- `RefreshModelsCommand` → `ModelListService.GetAvailableModelsAsync()`

**Key Behaviors:**
- Model list auto-loads on page open (async, parallel API queries)
- Built-in mode editing now allowed (only deletion is blocked)
- Unified view for dictation modes (CRUD) + utility pipelines (update-only)
- No auto-save (explicit Save button to prevent accidental overwrites)
- Dispatcher queue for cross-thread ObservableCollection updates

---

#### Task J.7: Documentation & Roadmap Update ✅
**Effort:** 0.5 hours
**Modified:**
1. [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md) — Updated Stream J status, J.6 implementation details
2. [MEMORY.md](C:\Users\gecko\.claude\projects\e--git-diktame\memory\MEMORY.md) — Added Stream J completion, new WinUI gotchas

---

### 11.4 Critical Files

**Files to Create (7):**
1. [src/DiktaMe.Core/Config/DictationMode.cs](src/DiktaMe.Core/Config/DictationMode.cs)
2. [src/DiktaMe.Core/Config/PipelineConfig.cs](src/DiktaMe.Core/Config/PipelineConfig.cs)
3. [src/DiktaMe.Core/Config/DictationModeDefaults.cs](src/DiktaMe.Core/Config/DictationModeDefaults.cs)
4. [src/DiktaMe.Core/Config/DictationModeManager.cs](src/DiktaMe.Core/Config/DictationModeManager.cs)
5. [src/DiktaMe.App/Views/DictationModesView.xaml](src/DiktaMe.App/Views/DictationModesView.xaml)
6. [src/DiktaMe.App/ViewModels/DictationModesViewModel.cs](src/DiktaMe.App/ViewModels/DictationModesViewModel.cs)
7. [tests/DiktaMe.Core.Tests/Config/DictationModeManagerTests.cs](tests/DiktaMe.Core.Tests/Config/DictationModeManagerTests.cs)

**Files to Modify (4):**
1. [src/DiktaMe.Core/Config/AppSettings.cs](src/DiktaMe.Core/Config/AppSettings.cs) — Add `DictationModes`, `UtilityPipelines`, `ActiveProfile` properties
2. [src/DiktaMe.Core/Config/SettingsManager.cs](src/DiktaMe.Core/Config/SettingsManager.cs) — Migration logic for old settings format
3. [src/DiktaMe.Core/LLM/LLMRouter.cs](src/DiktaMe.Core/LLM/LLMRouter.cs) — Add `modelName` parameter for per-mode model override
4. [src/DiktaMe.App/ViewModels/LoadingViewModel.cs](src/DiktaMe.App/ViewModels/LoadingViewModel.cs) — Use `DictationModeManager.GetActiveProfile()` instead of `ProfileManager.GetModeSettings()`

---

### 11.5 Verification Steps

**Unit Tests:**
1. DictationModeManagerTests — CRUD operations, reorder, delete built-in throws, active profile resolution
2. DictationModeDefaultsTests — verify 4 built-in modes created with correct prompts and sort order
3. SettingsManagerTests — migration from V1 format to CRUD format, idempotency
4. LLMRouterTests — per-mode model override, fallback to global model

**Manual Tests:**
1. **Create Custom Mode:**
   - Open Settings → Dictation Modes → click [+ New Mode]
   - Set title: "Interview Notes"
   - Cloud: Model="gpt-4o", Prompt="Summarize this interview snippet as bullet points."
   - Local: Prompt="Summarize this interview snippet as bullet points."
   - Assign hotkey: "Ctrl+Alt+I"
   - Save → verify mode appears in list
   - Press Ctrl+Alt+I → record audio → verify custom prompt used

2. **Edit Built-In Mode:**
   - Select "Standard" mode
   - Change Cloud model from "gpt-4o-mini" to "claude-sonnet-4"
   - Save → verify model change persists after app restart
   - Verify delete button is disabled (built-in mode)

3. **Delete Custom Mode:**
   - Select custom mode "Interview Notes"
   - Click delete → confirm dialog → mode removed from list
   - Verify hotkey Ctrl+Alt+I no longer registered

4. **Reorder Modes:**
   - Drag "Professional" mode to top position
   - Save → verify sort order persists (Professional now SortOrder=0)

5. **Profile Switching:**
   - Set active profile to "Cloud"
   - Press Ctrl+Alt+D → verify Cloud model used (check logs: "Using model: gpt-4o-mini")
   - Switch to "Local" profile
   - Press Ctrl+Alt+D → verify Ollama model used (check logs: "Using model: llama3.2:3b")

6. **Settings Migration:**
   - Copy old V1 settings JSON with `CustomPrompts` array
   - Launch app → verify auto-migration to CRUD format
   - Verify old custom prompts now appear as DictationMode.CloudProfile.SystemPrompt

---

### 11.6 Success Criteria

**All criteria met ✅**

✅ Users can create unlimited custom dictation modes with unique titles
✅ Each mode has independent Cloud and Local profiles
✅ Cloud profiles support per-mode model selection (real-time API discovery via ModelListService)
✅ Local profiles use global Ollama model (no per-mode model switching)
✅ Built-in modes (Standard, Prompt, Professional, RAW) can be edited but not deleted
✅ Custom modes can be deleted
✅ Mode reordering via up/down buttons updates SortOrder
✅ Hotkeys editable in UI (dynamic registration deferred to hotkey manager)
✅ Utility pipelines (Ask, Refine, Translate, Note, Chat) shown in unified view with update-only support
✅ Settings migration from V1 format works seamlessly (J.3 complete)
✅ All 521 unit tests pass (including updated DictationModeManager test for built-in mode editing)
✅ Full WinUI 3 Settings tab with left sidebar ListView + right panel editor

---

### 11.7 Deferred Features

These are out of scope for initial CRUD implementation:

1. **Mode Templates** — Pre-built mode templates users can clone (e.g., "Meeting Notes", "Email Drafts", "Code Comments")
2. **Mode Import/Export** — JSON export for sharing custom modes with other users
3. **Per-Mode Audio Settings** — Different max duration or input device per mode
4. **Mode Groups** — Organize modes into folders (e.g., "Work", "Personal", "Experimental")
5. **Conditional Mode Selection** — Auto-switch mode based on active window title or process name

---

**Document Status:** IN PROGRESS
**Completed:** A.0–A.2, B.1–B.5, C.1–C.7, D.1–D.4, E.0–E.3, F.1–F.5, G.1, G.2, H.2, I.1–I.5, I.2-UI, **J.1–J.7 (Stream J Complete ✅)**, Sound feedback settings + pipeline integration ✅, Control Panel V2 rework (Phase 1+2) ✅
**Remaining:** H.1 (Installer), I.6 (Website Rebrand)
**Build:** 0 errors, 0 warnings | **Tests:** 521 passing | **CI unit filter:** 376
