# dIKta.me V2 — Development Roadmap (C# + WinUI 3 Rewrite)

**Status:** DRAFT
**Date:** 2026-02-14
**Parent:** V1 `SPEC_039_STRATEGIC_ROADMAP.md` (in diktate repo)
**Supersedes:** Python modular split approach (deemed throwaway work)
**Target:** Native Windows app — single process, modular architecture, <30MB installer

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
| Installer size | ~100-200MB | ~20-30MB |
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

- [ ] Single `.exe` installer < 30MB (Native AOT)
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
│   │   │   ├── ILLMProvider.cs       # Interface
│   │   │   ├── LLMRouter.cs          # Routes to cloud/local
│   │   │   ├── GeminiProvider.cs     # Cloud LLM
│   │   │   ├── AnthropicProvider.cs  # Cloud LLM
│   │   │   ├── OpenAIProvider.cs     # Cloud LLM
│   │   │   └── OllamaProvider.cs     # Local LLM
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

#### Task A.2: Configure Native AOT Publishing
**Effort:** 0.5 day

**Steps:**
1. Add `<PublishAot>true</PublishAot>` to `DiktaMe.Core.csproj`
2. Configure trimming: `<PublishTrimmed>true</PublishTrimmed>`
3. Add AOT-compatible attributes where needed
4. Create `publish-release.bat`: `dotnet publish -c Release -r win-x64`
5. Verify output size < 30MB

**Acceptance:** Self-contained single-file exe runs without .NET installed.

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

#### Task C.5: LLM Provider Interface & Router
**Create:** `DiktaMe.Core/LLM/ILLMProvider.cs`, `LLMRouter.cs`
**Effort:** 0.5 day

**Steps:**
1. Define `ILLMProvider` interface (see §3.3)
2. Create `LLMRouter` — per-mode provider selection (dual-profile system)
3. Support: Gemini, Anthropic, OpenAI, Ollama

#### Task C.6: Cloud LLM Providers
**Create:** `GeminiProvider.cs`, `AnthropicProvider.cs`, `OpenAIProvider.cs`
**Effort:** 1 day

**Port from:** `python/core/processor.py` (36KB — 4 classes)

**Steps:**
1. Each provider implements `ILLMProvider`
2. `GeminiProvider`: Gemini API with OAuth and API key support
3. `AnthropicProvider`: Messages API with `anthropic-version` header
4. `OpenAIProvider`: Chat Completions API
5. All use shared `HttpClient` (connection pooling)
6. Retry logic: exponential backoff on 429/500
7. Token/sec tracking for performance metrics

#### Task C.7: Local LLM — Ollama
**Create:** `DiktaMe.Core/LLM/OllamaProvider.cs`
**Effort:** 0.5 day

**Port from:** `python/core/processor.py` (LocalProcessor class)

**Steps:**
1. HTTP client to `localhost:11434/api/generate`
2. Model detection (`/api/tags`)
3. Warmup request on startup
4. GPU fallback detection (tokens/sec monitoring)
5. Keep-alive session management

---

### Work Stream D: Pipeline Orchestration (Priority: HIGH)

#### Task D.1: Dictation Pipeline
**Create:** `DiktaMe.Core/Pipeline/DictationPipeline.cs`
**Effort:** 1 day

**Port from:** `python/core/pipelines.py` (dictation flow)

**Steps:**
1. Orchestrate: Record → STT → LLM (optional cleanup) → Inject
2. Handle "Raw" mode (skip LLM, inject transcription directly)
3. Emit progress events: recording, transcribing, processing, injecting
4. Performance metrics tracking (total_ms, per-stage breakdowns)
5. Error handling: fallback to raw text if LLM fails
6. Session logging to SQLite

#### Task D.2: Refine Pipeline (Dual-Mode)
**Create:** `DiktaMe.Core/Pipeline/RefinePipeline.cs`
**Effort:** 1 day

**Steps:**
1. **Autopilot mode:** Capture selection → LLM cleanup → Replace
2. **Instruction mode:** Capture selection + Record instruction → STT → LLM (selection + instruction) → Replace
3. Fallback: if no text selected, treat as Ask mode

#### Task D.3: Ask, Translate, Note Pipelines
**Create:** `AskPipeline.cs`, `TranslatePipeline.cs`, `NotePipeline.cs`
**Effort:** 1 day

**Steps:**
1. **Ask:** Record → STT → LLM (Q&A prompt) → Output (clipboard/type/notification)
2. **Translate:** Record → STT (auto-detect language) → LLM (translate) → Inject
3. **Note:** Record → STT → LLM (optional cleanup) → Append to file with timestamp

#### Task D.4: Oops (Re-inject)
**Create:** part of `TextInjector.cs`
**Effort:** 0.25 day

**Steps:**
1. Store last injected text in memory
2. On Oops hotkey, re-inject stored text
3. Volatile (lost on restart) — same as V1

---

### Work Stream E: Data & Security (Priority: HIGH)

#### Task E.1: Settings Manager
**Create:** `DiktaMe.Core/Config/AppSettings.cs`, `SettingsManager.cs`
**Effort:** 1 day

**Port from:** Electron `electron-store` config

**Steps:**
1. `AppSettings` record with all settings (strongly typed, not a dict)
2. Persist to `%APPDATA%/DiktaMe/settings.json`
3. `SettingsManager` — load, save, merge defaults, migrate schema
4. Observable properties for MVVM binding
5. `ProfileManager` — dual-profile system (8 modes × 2 profiles)
6. `PromptRepository` — 16 custom prompt slots
7. Migration from V1 settings (read `electron-store` JSON, convert)

#### Task E.2: History & Metrics (SQLite)
**Create:** `DiktaMe.Core/Data/HistoryManager.cs`, `MetricsCollector.cs`
**Effort:** 0.5 day

**Port from:** `python/utils/history_manager.py`

**Steps:**
1. SQLite database at `~/.diktate/history.db` (same location as V1)
2. Same schema: `history` + `system_metrics` tables
3. 90-day auto-pruning
4. Privacy level compliance (Ghost/Stats/Balanced/Full)
5. PII scrubber integration at Level 2+

#### Task E.3: Security (Secrets + PII)
**Create:** `DiktaMe.Core/Security/SecureStorage.cs`, `PIIScrubber.cs`
**Effort:** 0.5 day

**Steps:**
1. `SecureStorage` — use `ProtectedData.Protect()` (DPAPI) for API keys
2. Store encrypted keys in `%APPDATA%/DiktaMe/keys.dat`
3. `PIIScrubber` — regex-based redaction (emails, phones, API keys)
4. `ApiKeyValidator` — format validation per provider

---

### Work Stream I: Promoted Deferred Features (Priority: MEDIUM-HIGH)

> These features were originally deferred from V1 but are included in V2 from the start.
> See §2.3 for the full list and scoping rationale.

#### Task I.1: Voice Snippets — Core Engine (SPEC_026, Phase 1 only)
**Create:** `DiktaMe.Core/Config/SnippetManager.cs`
**Effort:** 1 day

**Scope:** Phase 1 only — core trigger matching + Settings CRUD. **Skip** dynamic variables (`{{date}}`) and cursor placement (those are V2.1).

**Steps:**
1. `SnippetManager` — load/save `snippets.json` from `%APPDATA%/DiktaMe/`
2. Data model: `{ id, trigger, content }` (simple list)
3. `ExpandSnippets(text)` — runs **post-LLM, pre-inject** in every pipeline
4. Normalize trigger matching (case-insensitive, ignore trailing punctuation)
5. Check `text.EndsWith(trigger)` — replace trigger with snippet content
6. Settings UI: new "Snippets" tab — list view + add/edit/delete modal
7. Limit: 100 snippets max (regex over <100 strings = <1ms)

**Tests:** Trigger matching, punctuation handling, multi-line expansion, no false positives.

#### Task I.2: Quick Chat Overlay (SPEC_042d)
**Create:** `DiktaMe.App/Views/QuickChatView.xaml`, `DiktaMe.Core/Pipeline/ChatPipeline.cs`
**Effort:** 1 day

**Steps:**
1. New global hotkey: `Ctrl+Alt+C` (configurable)
2. Small floating WinUI 3 window (~400×300px), always-on-top, not modal
3. Input field (text) + Send button + Mic button (voice input)
4. Response area (scrollable, supports streaming)
5. `ChatPipeline`: routes to existing `LLMRouter` with chat system prompt
6. Voice input: reuse existing `AudioRecorder` → `STTRouter` → populate input field
7. Whisper-only transcription for voice (Raw mode, no LLM cleanup of the question)
8. `Escape` or click-outside closes overlay
9. Stateless MVP — no conversation history (multi-turn is V2.1)

**Tests:** Pipeline routing, window lifecycle, voice input flow.

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

#### Task I.4: Audio Ducking (SPEC_043d)
**Create:** `DiktaMe.Core/Audio/AudioDucker.cs`
**Effort:** 0.5 day

**Steps:**
1. Use NAudio's `MMDeviceEnumerator` + `AudioSessionManager` (WASAPI, already a dependency)
2. `Duck()` — enumerate non-DiktaMe audio sessions, store original volumes, reduce to configurable level (default: 20%)
3. `Restore()` — restore all sessions to original volumes
4. Hook into `AudioRecorder.RecordingStarted` → `Duck()`, `RecordingStopped` → `Restore()`
5. Settings: enable/disable toggle + duck level slider (0-100%)
6. Safety: always restore in `finally` blocks; cleanup on app start for crash recovery
7. Skip own process + system sounds when enumerating

**Tests:** Duck/restore cycle, edge cases (no audio playing, rapid start/stop).

#### Task I.5: Ollama Update Management (SPEC_031)
**Create:** `DiktaMe.Core/System/OllamaManager.cs`
**Effort:** 1.5 days

**Steps:**
1. **Version Sensing:** Query `http://localhost:11434/api/version` on startup
2. **Pre-Flight Health Check:** Query `/api/tags` → verify selected model is pulled and ready
3. **Compatibility Manifest:** Embedded `models.json` mapping model tags → required Ollama versions
4. **Graceful Fallback:** If selected model requires newer Ollama → auto-select compatible fallback (Gemma) + notify user
5. **"412 Rescue" UI:** Catch precondition-failed errors → show "Incompatible AI Engine" dialog with [Update Ollama] and [Use Fallback] buttons
6. **Model Library Tab:** New section in Settings → Ollama tab:
   - List installed models (name, size, family)
   - One-click pull for recommended models
   - Download progress bars (Ollama pull-progress API)
   - Disk usage stats
7. **Version-Change Smoke Test:** If version changed since last run → silent background check that pinned model still responds

**Tests:** Version parsing, compatibility checking, fallback logic, API mocking.

#### Task I.6: Website Rebrand for V2 Launch (SPEC_042) — ✅ SITE LIVE, update only
**Create:** Updates to existing `website/` codebase (dikta.me already deployed)
**Effort:** 0.5 day *(copy/meta updates, not building from scratch)*

**Steps:**
1. Replace all "dIKtate" references with "dIKta.me" in copy, meta tags, structured data
2. Update download flow for new <30MB native installer (`diktame-setup.exe`)
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

#### Task F.5: Notification System
**Create:** Toast notifications via Windows APIs
**Effort:** 0.25 day

**Steps:**
1. Use `Microsoft.Toolkit.Uwp.Notifications` for toast notifications
2. Sound feedback using `System.Media.SoundPlayer` or NAudio
3. Map all V1 notification types (success, error, mode change)

---

### Work Stream G: Testing (Priority: HIGH)

#### Task G.1: Core Unit Tests (xUnit)
**Create:** `DiktaMe.Core.Tests/`
**Effort:** 2 days

**Target: 150+ tests minimum** (matching V1 coverage)

| Test File | Coverage |
|-----------|----------|
| `AudioRecorderTests.cs` | Recording lifecycle, auto-stop, device selection |
| `TextInjectorTests.cs` | Clipboard save/restore, key simulation |
| `STTRouterTests.cs` | Provider routing, fallback logic |
| `DeepgramProviderTests.cs` | API request/response, error handling |
| `LLMRouterTests.cs` | Per-mode provider selection, dual-profile |
| `GeminiProviderTests.cs` | API calls, retry logic |
| `AnthropicProviderTests.cs` | Headers, response parsing |
| `OllamaProviderTests.cs` | localhost detection, warmup |
| `DictationPipelineTests.cs` | Full flow, fallback, raw mode |
| `RefinePipelineTests.cs` | Both modes, selection capture |
| `SettingsManagerTests.cs` | Load, save, migrate, defaults |
| `HistoryManagerTests.cs` | SQLite CRUD, privacy levels, retention |
| `PIIScrubberTests.cs` | Regex patterns, edge cases |
| `CapabilityDetectorTests.cs` | All stack combinations |
| `SnippetManagerTests.cs` | Trigger matching, punctuation, multi-line, false positives |
| `ChatPipelineTests.cs` | LLM routing, system prompt, voice input flow |
| `AudioDuckerTests.cs` | Duck/restore cycle, no-audio edge cases |
| `OllamaManagerTests.cs` | Version parsing, compatibility, fallback logic |

**Mocking:** Use `Moq` for all external dependencies (HTTP, audio hardware, clipboard).

#### Task G.2: CI/CD Pipeline (GitHub Actions)
**Create:** `.github/workflows/ci-v2.yml`
**Effort:** 0.5 day

**Jobs:**
1. `build` — `dotnet build`
2. `test` — `dotnet test` (xUnit)
3. `lint` — `dotnet format --verify-no-changes`
4. `publish` — `dotnet publish -c Release` (verify AOT output)

---

### Work Stream H: Distribution & Migration (Priority: MEDIUM)

#### Task H.1: Installer (MSIX or Inno Setup)
**Effort:** 1 day

**Options:**
- **MSIX** — Microsoft Store ready, auto-update, sandboxed
- **Inno Setup** — Traditional installer, more control, smaller overhead

**Steps:**
1. Package Native AOT output into installer
2. Include sound assets, icon, default prompts
3. Target installer size: < 30MB
4. Register auto-start in Windows Task Scheduler
5. File associations (if needed)

#### Task H.2: V1 → V2 Migration
**Effort:** 0.5 day

**Steps:**
1. Detect existing V1 installation (`%APPDATA%/diktate/config.json`) — note: V1 uses old name
2. Read and convert V1 settings to V2 `AppSettings` format
3. Migrate API keys from Electron `safeStorage` to DPAPI
4. Preserve history database (same SQLite schema, same path)
5. Preserve custom prompts, hotkey bindings, privacy settings
6. Show "Welcome to V2" migration summary

---

## 5. Execution Timeline

```
Day 0 (Pre-Work): A.0 — Git Repo Prep & V1 Archive
├── Tag V1 as v1.0.0 in diktate repo
├── Create geckogtmx/diktame on GitHub
└── Clone + initial commit (README, .gitignore)

Week 1: Foundation + Core Engine
├── Day 1:   A.1 (Scaffold) + A.2 (AOT config)
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
└── Day 10:  E.1 (Settings) + E.2 (History) + E.3 (Security)
                → Tag: v2.0.0-alpha.2 ("full dictation pipeline works")

Week 3: UI + Promoted Features
├── Day 11-12: F.1 (Settings Window)
├── Day 13:    F.2 (Control Panel) + I.3 (CP Config toggles) + I.4 (Audio Ducking)
├── Day 14:    F.3 (Wizard) + F.4 (Loading) + F.5 (Notifications)
├── Day 15:    I.1 (Voice Snippets) + I.2 (Quick Chat Overlay)
└── Day 16:    I.5 (Ollama Management — start)
                → Tag: v2.0.0-beta.1 ("feature complete, untested")

Week 4: Testing + Distribution
├── Day 17:    I.5 (Ollama Management — finish) + I.6 (Website Rebrand)
├── Day 18-19: G.1 (Tests — complete 170+ including promoted feature tests)
├── Day 20:    G.2 (CI/CD) + H.1 (Installer)
├── Day 21:    H.2 (Migration) + Manual QA
└── Day 22:    Final polish, README update, release prep
                → Tag: v2.0.0-rc1 → v2.0.0 (release)
```

**Total: ~22 developer-days (4.5 weeks)** + Day 0 pre-work

> *Note: Timeline increased from original 20 days due to 6 promoted deferred features (+~6 days gross, but some tasks overlap with existing work streams, net +2 days).*

---

## 6. Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| WinUI 3 learning curve for AI agents | Medium | Extensive examples in training data; CommunityToolkit helpers |
| Native AOT incompatible with some NuGet packages | High | Test AOT early (Day 1); have trimming workarounds ready |
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
| `v2.0.0-alpha.2` | End of Week 2 (Day 10) | Full pipeline functional — dictate, refine, ask, translate all work |
| `v2.0.0-beta.1` | End of Week 3 (Day 16) | Feature complete — all UI + promoted features, untested |
| `v2.0.0-rc1` | Day 21 | Release candidate — tests pass, installer works |
| `v2.0.0` | Day 22 | 🚀 **Ship it** |

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

**Document Status:** DRAFT — Ready for review
**Next Step:** Approve plan → Run Task A.0 (Git Prep) → Begin Task A.1 (scaffold)
**Prerequisite:** Ship V1 (dIKtate) as-is first, start collecting revenue
