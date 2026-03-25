# SPEC_015: V2 Modules Sprint — Plugin Architecture + Vision Core

> **Status:** DRAFT
> **Date:** 2026-03-16
> **Architecture:** Hot-pluggable plugin system. Three plugins (Connectors, Meetings, Memory) as separate assemblies. Vision integrated in Core.
> **Goal:** Complete all remaining V2 feature modules in one sprint. Lock down V2.
> **Design References (source specs):**
> - [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md) — Plugin 1: Connectors (Phases A–C, F, H)
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Plugin 2: Meetings/Scribe (Phases D–E, G, I)
> - [`SPEC_002_VISION.md`](SPEC_002_VISION.md) — Core Integration: Vision/See (Phase 0C)
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Plugin 3: Memory (Phases O–Q)
> - [`SPEC_013_USE_CASES.md`](SPEC_013_USE_CASES.md) — 218 use cases driving connector design decisions
> - [`SPEC_005_STREAMDECK.md`](SPEC_005_STREAMDECK.md) — Future Plugin: Stream Deck (post-sprint)

---

## 1. Executive Summary

This document defines a **plugin architecture** for dIKta.me V2 and uses it to deliver four feature modules — **Connectors**, **Meetings/Scribe**, **Vision/See**, and **Memory** — that complete the V2 feature set.

Three modules are **hot-pluggable plugins**: separate assemblies that can be enabled/disabled at runtime without restarting the app. **Vision** is integrated directly into Core as a new pipeline type. A **Phase 0 core completion sprint** establishes the plugin infrastructure and finishes outstanding test coverage before any plugin work begins.

### Why Plugins?

1. **Independent development** — Each plugin is its own `.csproj`. A developer can work on the Connectors plugin without touching the main app or any other plugin.
2. **Independent deployment** — Plugins ship as DLLs in a `plugins/` folder. Users can install a plugin by dropping a folder, or disable it without removing code from the core app.
3. **Runtime flexibility** — Enable/disable plugins instantly without restarting the app. The core app stays lean; features are additive.
4. **Architecture validation** — Three plugins prove the pattern is rock-solid. Future features (Stream Deck, new integrations) follow the same model.
5. **Market positioning** — Competitors do meetings OR integrations OR vision OR memory. We do all four, locally, and they're independently installable.
6. **V2 lockdown** — This sprint closes the V2 feature scope. Everything after this is polish, optimization, or V3.

### Plugin Architecture

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                           dIKta.me Core App                                      │
│  Pipeline, LLM, STT, Audio, Settings, History, Security, TextInjector           │
│                                                                                  │
│  Core Additions (Phase 0):                                                       │
│    • Vision/ — ScreenCapture, VisionPipeline, multimodal LLM (in Core)          │
│    • PipelineEventBus — publish pipeline events for plugin consumption           │
│    • PluginManager — discover, load, enable/disable plugins                      │
│    • PluginUIRegistry — plugins contribute settings pages, tray items, widgets   │
│                                                                                  │
│  Plugin Hook Points:                                                             │
│    • IPipelineEventBus.OnCompleted()    → Connectors dispatch, Memory store      │
│    • IPipelineEventBus.OnBeforeLlm()    → Memory context injection               │
│    • IPipelineEventBus.OnStateChanged() → Meetings hotkey suppression            │
│    • IPluginUIRegistry                  → Settings pages, tray items, widgets    │
│                                                                                  │
└───────┬──────────────────────────┬──────────────────────────┬────────────────────┘
        │                          │                          │
        │  Assembly.LoadFrom()     │  Assembly.LoadFrom()     │  Assembly.LoadFrom()
        │  plugins/Connectors/     │  plugins/Meetings/       │  plugins/Memory/
        ▼                          ▼                          ▼
┌───────────────────┐  ┌────────────────────┐  ┌─────────────────────┐
│ CONNECTORS PLUGIN │  │ MEETINGS PLUGIN    │  │ MEMORY PLUGIN       │
│ (separate DLL)    │  │ (separate DLL)     │  │ (separate DLL)      │
│                   │  │                    │  │                     │
│ ConnectorManager  │  │ SessionManager     │  │ SqliteMemoryStore   │
│ IConnector impls  │  │ ScribeWindow       │  │ EmbeddingGenerator  │
│ Presets + Inbox   │  │ MeetingRecorder    │  │ IMemoryLayer        │
│ SettingsWindow    │  │ Synthesizer        │  │ MemorySettingsPage  │
│                   │  │ MeetingSettingsPage│  │                     │
│ Hooks:            │  │                    │  │ Hooks:              │
│ • OnCompleted     │  │ Hooks:             │  │ • OnCompleted       │
│   (dispatch)      │  │ • OnStateChanged   │  │   (store embedding) │
│                   │  │   (hotkey suppress) │  │ • OnBeforeLlm      │
│ UI:               │  │                    │  │   (inject context)  │
│ • Settings page   │  │ UI:                │  │                     │
│ • Tray: Connectors│  │ • Scribe window    │  │ UI:                 │
│ • Widget: presets │  │ • Tray: Start/Hist │  │ • Settings page     │
└───────────────────┘  └────────────────────┘  └─────────────────────┘
        │                       │                        │
        └───────────┬───────────┴────────────────────────┘
                    │
         ┌──────────▼──────────┐
         │ CROSS-PLUGIN FLOWS  │
         │ (via PipelineEventBus│
         │  — zero direct deps)│
         │                     │
         │ Scribe → EventBus   │
         │   → Connectors      │
         │                     │
         │ Vision → EventBus   │
         │   → Connectors      │
         │   → Memory          │
         │                     │
         │ All → EventBus      │
         │   → Memory (store)  │
         │                     │
         │ Memory → BeforeLlm  │
         │   → context inject  │
         └─────────────────────┘
```

**Critical rule**: Plugins NEVER depend on each other. All cross-plugin flows go through `PipelineEventBus`:
- **Scribe → Connectors**: Scribe publishes `PipelineResult` to event bus. Connectors plugin subscribes. Neither imports the other's namespace.
- **Vision → Connectors/Memory**: Vision publishes `PipelineResult` to event bus. Connectors and Memory both subscribe independently.
- **All → Memory (store)**: Every pipeline completion publishes to event bus. Memory stores as embedding.
- **Memory → LLM (pull)**: Memory subscribes to `OnBeforeLlmProcessing` hook, injects context into the system prompt. Pipelines don't know Memory exists.

### Plugin vs. Core Decision Matrix

| Module | Decision | Rationale |
|--------|----------|-----------|
| **Connectors** | **Plugin** | Independent lifecycle, own settings window, third-party extensible |
| **Meetings/Scribe** | **Plugin** | Own window, own session engine, own audio pipeline, clearly separable |
| **Memory** | **Plugin** | Own DB, own embedding model, heavy, optional, privacy-sensitive |
| **Vision** | **Core** | Lightweight, extends existing pipeline concept (hotkey→process→inject), shares too much with core (providers, TextInjector, hotkeys) |
| **Stream Deck** | **Future Plugin** | Named pipe IPC, reads from app — natural plugin. See SPEC_005. |

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Assembly isolation | **Default AssemblyLoadContext** | WinUI 3 XAML won't resolve from isolated contexts. Plugins load via `Assembly.LoadFrom()` into the default context. |
| Hot-enable/disable | **Instant, no restart** | Plugin `EnableAsync()`/`DisableAsync()` subscribe/unsubscribe event bus, add/remove UI contributions |
| Hot-install (new DLL) | **Requires restart** | Assembly cannot be loaded dynamically while running (default context limitation). Acceptable for desktop app. |
| Plugin settings | **Separate JSON per plugin** | `%APPDATA%/DiktaMe/plugins/{id}-settings.json`. NOT in AppSettings. Clean lifecycle. |
| UI contribution | **Registry + PageFactory** | Plugins register settings pages, tray items, and widgets via `IPluginUIRegistry`. Host uses `ContentFrame.Content = pageInstance` (not `Navigate(Type)`). |
| Event system | **IPipelineEventBus + IDisposable** | Thread-safe subscribe/publish. Subscriptions return `IDisposable` for automatic cleanup on disable. |
| DI integration | **Plugins resolve host services** | `IPluginContext.Services` exposes the host's `IServiceProvider`. Plugins resolve Core singletons (LLMRouter, STTRouter, etc.). |

### Plugin UI Surface Pattern

```
┌────────────────────┬────────────────────┬────────────────────┬──────────────────────┐
│                    │ Settings Page      │ Tray Icon Menu     │ Control Panel Widget │
│ Module             │ (contributed by    │ (contributed by    │ (contributed by      │
│                    │  plugin at enable) │  plugin at enable) │  plugin at enable)   │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Connectors (plugin)│ ConnectorSettings  │ "Connectors..."    │ Connector Presets    │
│                    │ page: CRUD         │ opens settings     │ row: toggle-on/off   │
│                    │ destinations +     │ window             │ pills, inbox badge   │
│                    │ presets + inbox     │                    │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Meetings (plugin)  │ MeetingSettings    │ "Start Session"    │ (optional) Active    │
│                    │ page: template,    │ → opens Scribe     │ Session indicator    │
│                    │ providers, duck    │ Window             │ with timer + stop    │
│                    │                    │ "Session History"  │                      │
│                    │                    │ → opens list       │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Memory (plugin)    │ MemorySettings     │ —                  │ —                    │
│                    │ page: toggle,      │ (background,       │                      │
│                    │ retention, stats,  │  automatic)        │                      │
│                    │ clear all          │                    │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Vision (core)      │ Vision page in     │ — (hotkey-only:    │ —                    │
│                    │ Settings window    │  Ctrl+Alt+S)       │                      │
│                    │ (hardcoded, not    │                    │                      │
│                    │  plugin-contributed)│                   │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Chat (core)        │ Chat page in       │ "Quick Chat"       │ —                    │
│                    │ Settings window    │ → opens overlay    │                      │
└────────────────────┴────────────────────┴────────────────────┴──────────────────────┘
```

**The pattern**: Plugin pages, tray items, and widgets are **added when the plugin is enabled** and **removed when disabled**. The Settings window listens to `PluginUIRegistry.ContributionsChanged` and rebuilds its navigation. The tray menu rebuilds on next open. Core features (Vision, Chat) remain hardcoded.

---

## 2. Sprint Overview

### Phase Map

| Phase | Scope | Est. Sessions | Depends On |
|-------|-------|---------------|------------|
| **0A** | Core completion: SPEC_009 test coverage (STTProviderFactory, LLMProviderFactory) | 1 | — |
| **0B** | Plugin infrastructure: `IPlugin`, `PipelineEventBus`, `PluginManager`, `PluginUIRegistry` | 1-2 | — |
| **0C** | Vision core integration: `ScreenCapture`, multimodal LLM, `VisionPipeline`, hotkey | 2-3 | 0B |
| **A** | Connectors Plugin: Core framework (`IConnector`, `ConnectorManager`, settings) | 1 | 0B |
| **B** | Connectors Plugin: Obsidian vault connector | 1 | A |
| **C** | Connectors Plugin: Folder, Webhook, Discord, Streamer.bot connectors | 1-2 | A |
| **D** | Meetings Plugin: Core session engine (`Session`, `SessionManager`, recording) | 1-2 | 0B |
| **E** | Meetings Plugin: Scribe window (notepad + AI synthesis + templates) | 1-2 | D |
| **F** | Connectors Plugin: Settings window + Presets UI | 1 | A, B, C |
| **G** | Meetings Plugin: Post-meeting ("Ask this meeting", export, playback) | 1 | D, E |
| **H** | Connectors Plugin: Notifications, inbox, polish | 1 | F |
| **I** | Meetings Plugin: Polish (speaker naming, search, hotkeys, notifications) | 1 | G |
| **J** | Cross-plugin integration: E2E via `PipelineEventBus` | 1 | H, I, 0C |
| **K** | Connectors Plugin: Google OAuth — Calendar + Gmail (Release 2) | 2-3 | J |
| **N** | Meeting captures: Scribe + `ScreenCapture` integration | 1 | 0C, G |
| **O** | Memory Plugin: Core (`IMemoryLayer`, SQLite+VSS, embedding model, privacy) | 1-2 | 0B |
| **P** | Memory Plugin: Pipeline hooks (auto-store, context injection) | 1 | O |
| **Q** | Memory Plugin: Settings page (stats, retention, search, clear) | 1 | P |

**Total: 20-26 sessions** across ~12-14 weeks.

### Parallelization

All plugin tracks are **fully independent** until Phase J/N (integration). Phase 0 must complete first.

```
Timeline:

Phase 0A (SPEC_009 tests) ──┐
Phase 0B (Plugin infra)  ───┼──→ Phase 0C (Vision core)
                            │
                            ├──→ A ──→ B ──→ C ──→ F ──→ H ──────────┐
                            │                                          ├──→ J ──→ K
                            ├──→ D ──→ E ──→ G ──→ I ────────────────┤
                            │                │                         │
                            │                └──→ N ──────────────────┘
                            │                     (0C + G required)
                            │
                            └──→ O ──→ P ──→ Q  (Memory — independent, anytime after 0B)
```

---

## 3. V2 Feature Completeness Audit

Before starting new work, confirm what's done. **Code-verified 2026-03-16:**

| Feature | Spec | Status | Verified |
|---------|------|--------|----------|
| Core dictation (8 modes) | Roadmap | **COMPLETE** | — |
| Cloud STT/LLM/TTS | Roadmap | **COMPLETE** | — |
| Local STT + LLM (Whisper + Ollama) | SPEC_009 | **COMPLETE** (needs tests) | Code verified — all FIX-1 through FIX-16 implemented |
| Quick Chat overlay | SPEC_042d | **COMPLETE** | — |
| Audio Ducking | SPEC_043d | **COMPLETE** | — |
| Voice Macros | SPEC_026 | **COMPLETE** | — |
| OAuth + Wallet | SPEC_008 | **COMPLETE** | Code verified — K.8-K.12, M.1-M.4 all committed |
| Internationalization | SPEC_004 | **COMPLETE** | Code verified — 606 strings, en-US + es-MX, WinUI3Localizer across 80 XAML files |
| Deepgram Streaming | Stream L | **COMPLETE** | — |
| Chat Feature Upgrade | SPEC_007 | **COMPLETE** | — |
| CRUD Dictation Modes | Stream J | **COMPLETE** | — |
| TTS (Kokoro CPU) | SPEC_003 | **COMPLETE** | — |
| **Connectors** | SPEC_013 | **NOT STARTED** | This sprint |
| **Meetings/Scribe** | SPEC_001 | **NOT STARTED** | This sprint |
| **Vision/See** | SPEC_002 | **NOT STARTED** | This sprint |
| **Memory Layer** | SPEC_014 | **NOT STARTED** | This sprint |

### Remaining Gap: SPEC_009 Tests

Local Mode is feature-complete but has **missing test coverage**:
- `STTProviderFactoryTests.cs` — does not exist (~12-15 tests needed)
- `LLMProviderFactoryTests.cs` — does not exist (~12-15 tests needed)
- Current total: **807 tests**

This is addressed in **Phase 0A**.

---

## 4. Phase 0: Core Completion Sprint

### Phase 0A: SPEC_009 Test Coverage [SPEC_015-0A]

> Close the test gap in Local Mode before building new features.

| Task | Description | Files |
|------|-------------|-------|
| 0A.1 | Create `STTProviderFactoryTests.cs` — CreateProvider for whisper/deepgram, caching, model switch triggers disposal + new instance, SupportsStreaming, unknown type throws | `Tests/Config/STTProviderFactoryTests.cs` |
| 0A.2 | Create `LLMProviderFactoryTests.cs` — CreateProvider for ollama/gemini/anthropic/openai, caching by `{type}:{model}` key, NullLlmProvider for "none"/"skip", API key retrieval from SecureStorage, unknown type throws | `Tests/Config/LLMProviderFactoryTests.cs` |
| 0A.3 | Expand OllamaProvider edge case tests: custom baseUrl, keep-alive parameter passthrough, num_ctx parameter | `Tests/LLM/LLMProviderTests.cs` |

**Target**: 807 → ~835 tests

**Verification**: `dotnet test DiktaMe.sln` — all tests pass.

**Commit**: `test: add STTProviderFactory and LLMProviderFactory unit tests [SPEC_015-0A]`

---

### Phase 0B: Plugin Infrastructure [SPEC_015-0B]

> Build the plugin system that all three plugins depend on.

#### New Project: `DiktaMe.Plugin.Abstractions`

```
Type: Class library (net8.0-windows10.0.19041.0)
References: DiktaMe.Core
NO WinUI dependency in the interface layer (plugins bring their own WinUI refs)
```

| Task | Description | Files |
|------|-------------|-------|
| 0B.1 | Create `IPlugin` interface — `Id`, `DisplayName`, `State`, `InitializeAsync(IPluginContext)`, `EnableAsync()`, `DisableAsync()`, `IAsyncDisposable` | `Abstractions/IPlugin.cs` |
| 0B.2 | Create `PluginState` enum — `Unloaded`, `Initialized`, `Enabled`, `Disabled`, `Error` | `Abstractions/PluginState.cs` |
| 0B.3 | Create `[PluginEntry]` attribute — `Id`, `DisplayName`, `Version`. Placed on plugin entry class. PluginManager scans for this. | `Abstractions/PluginEntryAttribute.cs` |
| 0B.4 | Create `IPluginContext` interface — `Services` (host DI), `PipelineEvents`, `Settings`, `UI`, `Dispatcher`, `Logger` | `Abstractions/IPluginContext.cs` |
| 0B.5 | Create `IPipelineEventBus` interface — `OnCompleted()`, `OnBeforeLlmProcessing()`, `OnAfterTranscription()`, `OnStateChanged()`. All return `IDisposable`. | `Abstractions/IPipelineEventBus.cs` |
| 0B.6 | Create `PipelineEventBus` implementation — thread-safe handler lists with `lock`, per-handler exception isolation, publish methods for host | `Abstractions/PipelineEventBus.cs` |
| 0B.7 | Create `BeforeLlmContext` record — `UserText`, `SystemPrompt`, `Mode`, `AdditionalSystemContext` (plugins append to this) | `Abstractions/BeforeLlmContext.cs` |
| 0B.8 | Create `AfterTranscriptionContext` record — `RawTranscript`, `Mode`, `Transcript` (mutable) | `Abstractions/AfterTranscriptionContext.cs` |
| 0B.9 | Create `IPluginSettingsStore` interface — `LoadAsync<T>()`, `SaveAsync<T>()`, `SettingsChanged` event | `Abstractions/IPluginSettingsStore.cs` |
| 0B.10 | Create `JsonPluginSettingsStore` — per-plugin JSON at `%APPDATA%/DiktaMe/plugins/{id}-settings.json`, atomic write-then-rename | `Abstractions/JsonPluginSettingsStore.cs` |
| 0B.11 | Create `IPluginUIRegistry` interface — `AddSettingsPage()`, `RemoveSettingsPage()`, `AddTrayMenuItems()`, `RemoveTrayMenuItems()`, `AddControlPanelWidget()`, `RemoveControlPanelWidget()`, `ContributionsChanged` event | `Abstractions/IPluginUIRegistry.cs` |
| 0B.12 | Create `PluginUIRegistry` implementation — stores contributions, fires `ContributionsChanged`, marshals to UI thread | `Abstractions/PluginUIRegistry.cs` |
| 0B.13 | Create UI contribution records — `PluginSettingsPageInfo` (`PluginId`, `NavigationTag`, `DisplayName`, `IconGlyph`, `PageFactory`), `PluginTrayMenuItem` (`Label`, `OnClick`, `IsSeparatorBefore`), `PluginWidgetInfo` (`PluginId`, `Title`, `WidgetFactory`) | `Abstractions/PluginUIModels.cs` |
| 0B.14 | Create `PluginManager` — discover plugins from `plugins/` subdirectories, `Assembly.LoadFrom()`, find `[PluginEntry]` class, instantiate, `InitializeAsync`, `EnableAsync`/`DisableAsync`, persist enabled state to `plugin-states.json` | `Abstractions/PluginManager.cs` |
| 0B.15 | Create `PluginContext` implementation — wraps host `IServiceProvider`, `PipelineEventBus`, `JsonPluginSettingsStore`, `PluginUIRegistry`, `DispatcherQueue`, Serilog logger | `Abstractions/PluginContext.cs` |
| 0B.16 | Register `PipelineEventBus`, `PluginUIRegistry`, `PluginManager` as singletons in `App.xaml.cs` DI | `App.xaml.cs` |
| 0B.17 | Add plugin discovery + enable to `LoadingViewModel.InitializeAsync()` — after Step 6 (hotkeys), call `PluginManager.DiscoverAndLoadAsync()` then enable previously-enabled plugins | `LoadingViewModel.cs` |
| 0B.18 | Add `_pipelineEventBus.PublishCompleted(result)` to each pipeline completion site in `LoadingViewModel` (RunBatchDictationAsync, RunStreamingDictationAsync, RunAskPipelineAsync, etc.) | `LoadingViewModel.cs` |
| 0B.19 | Add `PipelineEventBus?` optional constructor param to `DictationPipeline`, `AskPipeline`, `ChatPipeline`. Call `PublishBeforeLlmAsync()` before LLM processing. `PipelineFactory` resolves from DI and passes it in. | `DictationPipeline.cs`, `AskPipeline.cs`, `ChatPipeline.cs`, `PipelineFactory.cs` |
| 0B.20 | Modify `SettingsWindow.xaml.cs` — after hardcoded nav items, iterate `PluginUIRegistry.SettingsPages` and add dynamic `NavigationViewItem`s. Listen to `ContributionsChanged` for rebuild. Handle plugin page tags in selection handler via `PageFactory()`. | `SettingsWindow.xaml.cs` |
| 0B.21 | Modify `TrayIconView.xaml.cs` `ShowContextMenu()` — include plugin tray items from `PluginUIRegistry.TrayMenuItems` between "Settings" and the separator before "Quit" | `TrayIconView.xaml.cs` |
| 0B.22 | Add `plugins/` directory creation in build output. Post-build targets copy plugin DLLs to `plugins/{Name}/` subdirectories. | `.csproj` files, `Directory.Build.targets` |
| 0B.23 | Unit tests: `PipelineEventBusTests` (subscribe, publish, dispose removes handler, error isolation), `PluginManagerTests` (discover, load, enable, disable, state persistence), `JsonPluginSettingsStoreTests` (round-trip, default on missing file, atomic write), `PluginUIRegistryTests` (add/remove, ContributionsChanged) | `Tests/Plugin/` or `Abstractions.Tests/` |

**Verification**: `dotnet build DiktaMe.sln -c Release` — 0 warnings. `dotnet test` — all tests pass. Plugin discovery runs on startup (no-op with empty plugins folder).

**Commit**: `feat: add plugin infrastructure (IPlugin, PipelineEventBus, PluginManager) [SPEC_015-0B]`

---

### Phase 0C: Vision Core Integration [SPEC_015-0C]

> Vision is a core pipeline type, not a plugin. Integrates `ScreenCapture`, multimodal LLM providers, and the `VisionPipeline` directly into Core + App.
>
> Full specification: `SPEC_002_VISION.md`
> Core concept: "You talk, dIKta.me looks." Hotkey → screenshot → optional voice query → multimodal LLM → response injected at cursor.

#### Phase 0C-L: Core Vision Infrastructure

| Task | Description | Files |
|------|-------------|-------|
| 0C.1 | Create `ScreenCapture` class — Win32 `PrintWindow()`/`BitBlt` for active window capture, `BitBlt` on virtual screen for region capture. Returns `byte[]` PNG. | `Core/Vision/ScreenCapture.cs` |
| 0C.2 | Image preprocessing: resize if longest side > 2048px, compress to JPEG (quality 85) if PNG > 1MB, base64 encode | `Core/Vision/ImageProcessor.cs` |
| 0C.3 | Extend `ILLMProvider` with `ProcessWithImageAsync(byte[] imageData, string mimeType, string text, string systemPrompt, string mode, CancellationToken)` — default throws `NotSupportedException` | `Core/LLM/ILLMProvider.cs` |
| 0C.4 | Implement multimodal in `GeminiProvider` — `inlineData` with `mimeType` + `data` (base64) in `parts[]` | `Core/LLM/GeminiProvider.cs` |
| 0C.5 | Implement multimodal in `AnthropicProvider` — `image` content block with `source.type = "base64"` | `Core/LLM/AnthropicProvider.cs` |
| 0C.6 | Implement multimodal in `OpenAICompatibleProvider` — `image_url` content with `data:image/png;base64,...` (covers GPT-4o + Ollama LLaVA/Moondream) | `Core/LLM/OpenAICompatibleProvider.cs` |
| 0C.7 | Create `VisionOptions` record: CaptureMode (ActiveWindow/Region), DefaultQuery, MaxImageDimension, AutoRecordQuery, QueryTimeoutSeconds | `Core/Vision/VisionOptions.cs` |
| 0C.8 | Add `VisionSettings` sub-object to `AppSettings` (Enabled, DefaultQuery, MaxImageDimensionPx, AutoRecordQuery, QueryTimeoutSeconds, OutputMode), add to `SanitizeNulls()`. Add `Vision = 8` to `HotkeyId`, add `Vision` hotkey to `HotkeySettings`. | `AppSettings.cs`, `SettingsManager.cs` |

#### Phase 0C-M: Standalone Vision Pipeline

| Task | Description | Files |
|------|-------------|-------|
| 0C.9 | Create `SnippingOverlayWindow.xaml` — transparent fullscreen always-on-top window covering all monitors. Semi-transparent dark fill, click = capture active window, drag = capture region, Esc = cancel. Crosshair cursor. Bottom hint text. | `App/Views/SnippingOverlayWindow.xaml` + `.cs` |
| 0C.10 | Region selection: mouse down → start rect, mouse move → update rect (clear cutout in overlay), mouse up → capture region → `ScreenCapture.CaptureRegion(rect)` | Same |
| 0C.11 | Create `VisionPipeline` — orchestrates: `ScreenCapture` → optional voice query (reuse `AudioRecorder` + STT) → `ILLMProvider.ProcessWithImageAsync()` → `TextInjector.InjectText()` → return `PipelineResult` (mode = "vision"). Publishes to `PipelineEventBus.PublishCompleted()`. | `Core/Pipeline/VisionPipeline.cs` |
| 0C.12 | Voice query flow: after screenshot, auto-record for up to `QueryTimeoutSeconds`. Speak query → STT → text. Silence/skip → use `DefaultQuery`. | Same |
| 0C.13 | Register hotkey `Vision = 8` → `Ctrl+Alt+S` in `HotkeySettings`. Dispatch in `LoadingViewModel`: `case HotkeyId.Vision: _ = RunVisionPipelineAsync()` | `LoadingViewModel.cs` |
| 0C.14 | Output modes: Inject (default), Clipboard, Toast-only. Configurable in `VisionSettings`. | `VisionPipeline.cs` |
| 0C.15 | Vision Settings page in existing Settings window: enable/disable, default query, auto-record toggle, model selectors, output mode | `App/Views/Settings/VisionSettingsPage.xaml` + VM |
| 0C.16 | History integration: store vision results in SQLite (mode = "vision", text = response, screenshot path) | `HistoryManager.cs` |
| 0C.17 | Error handling: no vision model → toast, API error → toast, non-vision Ollama model → toast, capture fails → toast | Pipeline + providers |
| 0C.18 | Unit tests: ScreenCapture mocked, ImageProcessor thresholds, multimodal provider JSON format, VisionPipeline orchestration, output routing, error paths | `Tests/Vision/`, `Tests/LLM/` |

**Key decisions**:
- `ProcessWithImageAsync()` is a default interface method that throws `NotSupportedException`. Providers opt in by overriding.
- VRAM: Vision is discrete — Ollama auto-swaps models. Model swap latency is acceptable.
- Multi-monitor: overlay covers all displays using union of screen bounds.
- Screenshot can be 8MB PNG on 4K — always run `ImageProcessor` before API call.

**Verification**: `dotnet build -c Release` clean. `dotnet test` passes. `Ctrl+Alt+S` hotkey triggers capture overlay, screenshot is processed, result injected.

**Commit**: `feat: add Vision pipeline with ScreenCapture, multimodal LLM, and Ctrl+Alt+S hotkey [SPEC_015-0C]`

---

## 5. Plugin 1: Connectors (SPEC_013)

> Full specification: `SPEC_013_CONNECTORS_IMPLEMENTATION.md`
> Use cases: `SPEC_013_USE_CASES.md` (218 use cases)

```
Project: DiktaMe.Plugin.Connectors
Type: Class library (net8.0-windows10.0.19041.0, UseWinUI=true)
References: DiktaMe.Plugin.Abstractions, DiktaMe.Core
Output: plugins/Connectors/DiktaMe.Plugin.Connectors.dll
Settings file: %APPDATA%/DiktaMe/plugins/connectors-settings.json
Tests: DiktaMe.Plugin.Connectors.Tests
```

### Phase A: Core Framework [SPEC_015-A]

> `IConnector` interface, payload/result records, `ConnectorManager`, plugin entry class.

| Task | Description | Files |
|------|-------------|-------|
| A.1 | Create `IConnector` interface with `SendAsync()` and default `GetContextAsync()` | `Connectors/IConnector.cs` |
| A.2 | Create `ConnectorPayload` record with `FromPipelineResult()` factory | `Connectors/ConnectorPayload.cs` |
| A.3 | Create `ConnectorResult` record with `Success()`/`Failure()` factories | `Connectors/ConnectorResult.cs` |
| A.4 | Create `ConnectorType` enum: File, Webhook, WebSocket, Cloud | `Connectors/ConnectorType.cs` |
| A.5 | Create `ConnectorInputType` flags enum: Voice, Selection, Screenshot, Both, All | `Connectors/ConnectorInputType.cs` |
| A.6 | Create `ConnectorNotifyMode` enum: Silent, Toast, Tts | `Connectors/ConnectorNotifyMode.cs` |
| A.7 | Create `ConnectorPluginSettings` — `Enabled`, `InboxRetentionDays`, `Destinations: List<ConnectorConfig>`, `Presets: List<ConnectorPreset>` (sealed records) | `Connectors/ConnectorPluginSettings.cs` |
| A.8 | Create `ConnectorManager` — resolve connector type → `IConnector` instance, preset-based dispatch loop (`Task.WhenAll`), mode filtering, privacy gating, logging | `Connectors/ConnectorManager.cs` |
| A.9 | Create `ConnectorsPlugin : IPlugin` entry class with `[PluginEntry("connectors", "Connectors", "1.0.0")]`. On `EnableAsync()`: subscribe to `IPipelineEventBus.OnCompleted`, register UI. On `DisableAsync()`: dispose subscriptions, remove UI. | `Connectors/ConnectorsPlugin.cs` |
| A.10 | Unit tests: dispatch with 0 connectors (no-op), mode filtering, privacy gating (Ghost blocks all), preset execution, error isolation | `Connectors.Tests/ConnectorManagerTests.cs` |

**Note**: No changes to `AppSettings.cs` or `SanitizeNulls()`. Connector settings live in plugin's own JSON file via `IPluginSettingsStore`.

**Commit**: `feat: add Connectors plugin with IConnector framework and ConnectorManager [SPEC_015-A]`

---

### Phase B: Obsidian Connector [SPEC_015-B]

> Highest-value, lowest-effort. Direct filesystem write to Obsidian vault.

| Task | Description | Files |
|------|-------------|-------|
| B.1 | Implement `ObsidianConnector : IConnector` — reads `VaultPath`, `SubFolder`, `NoteStrategy` from config | `Connectors/ObsidianConnector.cs` |
| B.2 | Daily note strategy (default): append to `{VaultPath}/{SubFolder}/{DailyNoteFormat}.md` with `---` separator + timestamp | Same |
| B.3 | Standalone strategy: create new `.md` per dictation with full YAML frontmatter | Same |
| B.4 | YAML frontmatter: `date`, `time`, `tags`, `mode`, `wordCount`, `sttProvider`, `llmProvider` | Same |
| B.5 | File name template tokens: `{date}`, `{time}`, `{mode}`, `{title}` (first 5 words, slugified) | Same |
| B.6 | Path validation: reject UNC, require absolute, create dirs if needed | Same |
| B.7 | Unit tests: daily create+append, standalone create, frontmatter format, path validation, template expansion | `Connectors.Tests/ObsidianConnectorTests.cs` |

**Commit**: `feat: add Obsidian vault connector [SPEC_015-B]`

---

### Phase C: Folder, Webhook, Discord, Streamer.bot [SPEC_015-C]

| Task | Description | Files |
|------|-------------|-------|
| C.1 | `FolderConnector` — write `.md` to `OutputPath` with optional `FileNameTemplate` | `Connectors/FolderConnector.cs` |
| C.2 | `WebhookConnector` — HTTP POST with JSON schema | `Connectors/WebhookConnector.cs` |
| C.3 | Webhook: HMAC-SHA256 signing → `X-DiktaMe-Signature: sha256={hex}` | Same |
| C.4 | Webhook: 15s timeout, retry once on 5xx | Same |
| C.5 | Webhook: privacy gating — `"[redacted]"` when privacy is `Stats` | Same |
| C.6 | `DiscordWebhookConnector` — embeds with `content`, `username`, `avatar_url` | `Connectors/DiscordWebhookConnector.cs` |
| C.7 | `StreamerBotConnector` — `ClientWebSocket` to `ws://{Host}:{Port}{Endpoint}` | `Connectors/StreamerBotConnector.cs` |
| C.8 | Streamer.bot: `DoAction` request with `action.name` + `args` | Same |
| C.9 | Streamer.bot: lazy connect, auto-reconnect, graceful `DisposeAsync` | Same |
| C.10 | Unit tests: mock `HttpMessageHandler`, `IWebSocketClient` abstraction | Test files |

**Commit**: `feat: add Folder, Webhook, Discord, and Streamer.bot connectors [SPEC_015-C]`

---

### Phase F: Connector Settings Window + Presets UI [SPEC_015-F]

> Plugin-contributed settings page + separate settings window for CRUD.

| Task | Description | Files |
|------|-------------|-------|
| F.1 | Create `ConnectorSettingsViewModel` — ObservableCollection for destinations + presets, Add/Edit/Remove/Toggle commands. Loads/saves via `IPluginSettingsStore`. | `Connectors/ViewModels/ConnectorSettingsViewModel.cs` |
| F.2 | Create `ConnectorSettingsPage.xaml` — master toggle, destination list, preset list, inbox viewer. Contributed via `IPluginUIRegistry.AddSettingsPage()`. | `Connectors/Views/ConnectorSettingsPage.xaml` + `.cs` |
| F.3 | Destination type picker: Obsidian / Folder / Webhook / Discord / Streamer.bot | Same |
| F.4 | Per-type settings: folder picker, URL input, host:port — dynamically shown | Same |
| F.5 | Preset editor: title, icon, color, input type, STT/LLM pickers, system prompt, output connector multi-select, notify mode, hotkey, test button | Same |
| F.6 | Control Panel widget: Connector Presets row with toggle pills, inbox badge. Contributed via `IPluginUIRegistry.AddControlPanelWidget()`. | `Connectors/Views/ConnectorPresetsWidget.xaml` |
| F.7 | Tray menu item "Connectors..." via `IPluginUIRegistry.AddTrayMenuItems()` | `ConnectorsPlugin.cs` |
| F.8 | "Test" button — fires synthetic payload through full preset pipeline, shows toast | ViewModel |

**Commit**: `feat: add Connector settings page and Presets UI [SPEC_015-F]`

---

### Phase H: Connector Notifications, Inbox, Polish [SPEC_015-H]

| Task | Description | Files |
|------|-------------|-------|
| H.1 | `ConnectorInboxManager` — SQLite CRUD for `connector_inbox` table (plugin's own DB or shared `history.db`) | `Connectors/ConnectorInboxManager.cs` |
| H.2 | `ConnectorPresetRunner` — executes single preset: optional LLM re-process → fan-out → notify → inbox | `Connectors/ConnectorPresetRunner.cs` |
| H.3 | Success/failure toasts: "Saved to Obsidian (42 words)" or "Webhook failed: 401" | `ConnectorManager.cs` |
| H.4 | Inbox UI panel: recent activity, mark-as-read, re-send failed | `Connectors/Views/ConnectorInboxPanel.xaml` |
| H.5 | Settings validation: valid URL, valid directory, valid host:port | ViewModel |
| H.6 | Edge cases: vault deleted → error toast, webhook 401 → suggest checking URL, disk full → graceful | Connectors |
| H.7 | Unit tests: inbox CRUD, retention cleanup, preset runner, error isolation | Test files |

**Commit**: `feat: add connector notifications, inbox, and polish [SPEC_015-H]`

---

## 6. Plugin 2: Meetings / Scribe (SPEC_001)

> Full specification: `SPEC_001_MEETINGS.md`

```
Project: DiktaMe.Plugin.Meetings
Type: Class library (net8.0-windows10.0.19041.0, UseWinUI=true)
References: DiktaMe.Plugin.Abstractions, DiktaMe.Core
Output: plugins/Meetings/DiktaMe.Plugin.Meetings.dll
Settings file: %APPDATA%/DiktaMe/plugins/meetings-settings.json
Tests: DiktaMe.Plugin.Meetings.Tests
```

### Phase D: Core Session Engine [SPEC_015-D]

| Task | Description | Files |
|------|-------------|-------|
| D.1 | Create `Session` data model — Id, Title, StartedAt, EndedAt, State, AudioPath, TranscriptPath, UserNotesMarkdown, ArtifactMarkdown, TemplateName, Participants, WordCount, ModelUsed | `Meetings/Session.cs` |
| D.2 | Create `SessionState` enum: Recording, Processing, Complete, Failed | `Meetings/SessionState.cs` |
| D.3 | Create `SessionManager` — CRUD, SQLite storage (plugin's own `meetings.db`), `ActiveSession` property, state transitions | `Meetings/SessionManager.cs` |
| D.4 | Create `MeetingRecorder` — `WasapiLoopbackCapture` (system audio) + `WasapiCapture` (mic), mixed into single WAV, disk-streaming for 1hr+ meetings | `Meetings/MeetingRecorder.cs` |
| D.5 | Disk streaming: write to temp `.wav`, ring buffer for level meter only. Auto-create `%APPDATA%/DiktaMe/sessions/{session_id}/` | Same |
| D.6 | Post-recording compression: WAV → Opus via `OpusEncoder` or ffmpeg. Delete WAV after. | `Meetings/AudioCompressor.cs` |
| D.7 | Create `MeetingTranscriber` — batch STT with `diarize=true&utterances=true&smart_format=true` | `Meetings/MeetingTranscriber.cs` |
| D.8 | Create `TranscriptSegment` record: Speaker, Text, StartMs, EndMs, Confidence | `Meetings/TranscriptSegment.cs` |
| D.9 | Create `MeetingSynthesizer` — `(transcript + notes + template) → artifact` via `LLMRouter` | `Meetings/MeetingSynthesizer.cs` |
| D.10 | Create `MeetingsPlugin : IPlugin` entry class with `[PluginEntry("meetings", "Meetings", "1.0.0")]`. On enable: subscribe to `OnStateChanged` for hotkey suppression, register tray items + settings page. On disable: cleanup. | `Meetings/MeetingsPlugin.cs` |
| D.11 | Create `MeetingPluginSettings` — `DefaultTemplate`, `AudioFormat`, `RetentionDays`, `DefaultSttProvider`, `DefaultLlmProvider`, `DefaultLlmModel`, `AutoDuck`, `AutoCompress` | `Meetings/MeetingPluginSettings.cs` |
| D.12 | Hotkey suppression during active session: plugin subscribes to `OnStateChanged`, signals via a shared flag. Voice hotkeys (Dictate, Ask, Translate, Note, Refine Voice) silently disabled. | `MeetingsPlugin.cs` |
| D.13 | Unit tests: SessionManager CRUD + state transitions, TranscriptSegment parsing, Synthesizer prompt construction, hotkey suppression | `Meetings.Tests/` |

**Key decision**: Audio capture uses NAudio `WasapiLoopbackCapture` + `WasapiCapture`, mixed into stereo WAV (system=left, mic=right) for diarization.

**Commit**: `feat: add Meetings plugin with Session engine and recording [SPEC_015-D]`

---

### Phase E: Scribe Window [SPEC_015-E]

| Task | Description | Files |
|------|-------------|-------|
| E.1 | Create `ScribeWindow.xaml` — split-pane: left (notes editor), right (AI output/chat), top bar (title + timer + stop), status bar (recording + template + level) | `Meetings/Views/ScribeWindow.xaml` + `.cs` |
| E.2 | Create `ScribeViewModel` — `ActiveSession`, `UserNotes`, `ArtifactMarkdown`, `IsRecording`, `ElapsedTime`, start/stop commands | `Meetings/ViewModels/ScribeViewModel.cs` |
| E.3 | Left pane: plain `TextBox` with monospace font, auto-save every 5s, persist on crash | ScribeWindow |
| E.4 | Right pane: placeholder during recording → Markdown artifact after synthesis | ScribeWindow |
| E.5 | Recording controls: Start, Stop, timer `HH:MM:SS` | ScribeWindow |
| E.6 | Audio level meter from `MeetingRecorder.LevelChanged` | ScribeWindow status bar |
| E.7 | Template selector: 6 built-in (Meeting Minutes, Interview, Lecture, Brainstorm, Sales Call, Custom) | ScribeWindow status bar |
| E.8 | End-session flow: Stop → "Processing..." → transcription → synthesis → show artifact → toast | ScribeViewModel |
| E.9 | Session list view: history of past meetings, launched from tray "Session History" | `Meetings/Views/SessionListPage.xaml` |
| E.10 | Register tray items "Start Session" + "Session History" via `IPluginUIRegistry.AddTrayMenuItems()` | `MeetingsPlugin.cs` |
| E.11 | Create 6 template prompts | `Meetings/MeetingTemplates.cs` |
| E.12 | Meeting Settings page contributed via `IPluginUIRegistry.AddSettingsPage()` | `Meetings/Views/MeetingSettingsPage.xaml` |

**Commit**: `feat: add Scribe window with notepad and AI synthesis [SPEC_015-E]`

---

### Phase G: Post-Meeting Experience [SPEC_015-G]

| Task | Description | Files |
|------|-------------|-------|
| G.1 | "Ask this meeting" chat — text input, pass transcript + question to LLM | ScribeWindow, ScribeViewModel |
| G.2 | Chat history within session — scrollable Q&A pairs | ScribeWindow |
| G.3 | Copy artifact: Markdown → clipboard | ScribeWindow |
| G.4 | Export artifact: Save as `.md` via file picker | ScribeWindow |
| G.5 | Audio playback: play Opus, waveform/progress, click-to-seek by transcript timestamp | ScribeWindow |
| G.6 | Auto-title: LLM generates title from first 500 words | `MeetingSynthesizer.cs` |
| G.7 | Retention: configurable (default 90 days), auto-delete in `SessionManager.CleanupAsync()` | `SessionManager.cs` |
| G.8 | Unit tests: chat prompt, export format, retention | Test files |

**Commit**: `feat: add post-meeting chat, export, and audio playback [SPEC_015-G]`

---

### Phase I: Meetings Polish [SPEC_015-I]

| Task | Description | Files |
|------|-------------|-------|
| I.1 | Speaker naming UI: post-synthesis, "Speaker 0 → ?" panel with LLM inference suggestions | ScribeWindow |
| I.2 | Session search: full-text across past meetings | SessionListPage |
| I.3 | Global hotkey: start/stop session (configurable) | `MeetingsPlugin.cs` |
| I.4 | `AudioDucker` integration: auto-duck when recording starts | `MeetingRecorder.cs` |
| I.5 | Toast: "Meeting processed — click to view" with action button | Via `NotificationService` |
| I.6 | Unit tests: speaker name inference, search, hotkey management | Test files |

**Commit**: `feat: add speaker naming, search, hotkeys, and meeting polish [SPEC_015-I]`

---

## 7. Plugin 3: Memory (SPEC_014)

> Full specification: [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md)
> Core concept: Semantic vector memory. Stores embeddings, injects context before LLM. Invisible to user — AI just "remembers."

```
Project: DiktaMe.Plugin.Memory
Type: Class library (net8.0-windows10.0.19041.0, UseWinUI=true)
References: DiktaMe.Plugin.Abstractions, DiktaMe.Core
Output: plugins/Memory/DiktaMe.Plugin.Memory.dll
Settings file: %APPDATA%/DiktaMe/plugins/memory-settings.json
Tests: DiktaMe.Plugin.Memory.Tests
```

### Phase O: Core Memory Infrastructure [SPEC_015-O]

> Memory is **invisible infrastructure** — the knowledge backbone consumed by Chaviz, Connectors, Meetings, Refinemmarly, and future plugins. See [SPEC_014](SPEC_014_MEMORY_LAYER.md) for the full 3-tier architecture and cross-module consumer/producer map.

| Task | Description | Files |
|------|-------------|-------|
| O.1 | Create `IMemoryLayer` interface — **Core**: `StoreAsync()`, `SearchAsync()`, `DeleteAsync()`, `ClearAllAsync()`, `GetStatsAsync()`. **Observation extraction**: `ExtractObservationsAsync()`. **User profile (Tier 3)**: `GetProfileAsync()`, `UpdateProfileAsync()`. **Consolidation**: `ConsolidateAsync()`. | `Memory/IMemoryLayer.cs` |
| O.2 | Create models: `MemoryEntryId`, `MemoryResult`, `MemoryMetadata`, `MemoryStats`, `Observation` (with `ObservationType` enum: Fact/Preference/Instruction/Context), `UserProfile`, `ConsolidationResult`, `MemorySearchFilter` (mode scope, type, time range) | `Memory/MemoryModels.cs` |
| O.3 | Integrate SQLite VSS extension — native extension loading | `Memory/SqliteMemoryStore.cs` |
| O.4 | Implement `SqliteMemoryStore : IMemoryLayer` — 3-tier schema: `observations` table (tier, observation_type, mode_scope, confidence, is_novel, source_ids), `user_profile` table (key-value with mode scope), `consolidation_log`. CRUD + similarity search via VSS. | Same |
| O.5 | Local embedding: ONNX `all-MiniLM-L6-v2` (384 dims). Reuse existing ONNX Runtime dep. | `Memory/EmbeddingGenerator.cs` |
| O.6 | Privacy gating — Ghost: disabled, Stats: metadata only, Balanced: encrypted, Full: full storage | `SqliteMemoryStore.cs` |
| O.7 | Novelty detection — cosine similarity dedup on store: >0.95 = skip (duplicate), <0.5 = flag novel (high-signal). Prevents memory bloat from repetitive dictations. | `SqliteMemoryStore.cs` |
| O.8 | Create `MemoryPluginSettings` — `Enabled` (default false), `RetentionDays` (365), `MaxEntries` (10000), `ContextInjectionEnabled` (true), `ContextResultLimit` (5), `MinSimilarity` (0.7), `EmbeddingModel`, `ObservationExtractionEnabled` (true), `ConsolidationMode` (Manual/OnIdle/OnShutdown) | `Memory/MemoryPluginSettings.cs` |
| O.9 | Create `MemoryPlugin : IPlugin` entry class with `[PluginEntry("memory", "Memory", "1.0.0")]`. On enable: init DB + embedding model, subscribe hooks, register settings page. On disable: dispose store + model, remove page. | `Memory/MemoryPlugin.cs` |
| O.10 | Unit tests: store/search/delete, privacy compliance, similarity scoring, metadata filtering, novelty dedup, observation type filtering, mode-scoped queries, tier separation | `Memory.Tests/SqliteMemoryStoreTests.cs` |

**Key decisions**:
- SQLite+VSS for single-file simplicity (aligns with existing HistoryManager pattern)
- `all-MiniLM-L6-v2` for small size + good quality — runs locally via ONNX Runtime (already a dependency)
- Encryption at rest via DPAPI keys from `SecureStorage`
- `Enabled = false` by default — opt-in for privacy
- 3-tier model: Tier 3 (User Profile — stable preferences/style), Tier 2 (Observations — extracted atomic facts), Tier 1 (Session Context — ephemeral, in-memory only)
- Observation types from SurfSense/Honcho: `fact`, `preference`, `instruction`, `context`
- Mode-scoped retrieval (inspired by LOOM world isolation): observations tagged with source mode, queries filter by current mode + global

**Commit**: `feat: add Memory plugin with 3-tier SQLite+VSS store and embeddings [SPEC_015-O]`

---

### Phase P: Pipeline Hooks + Observation Extraction [SPEC_015-P]

> Memory subscribes to PipelineEventBus hooks — no changes to core pipeline code beyond what Phase 0B already added. Post-pipeline observation extraction turns raw text into typed, searchable knowledge. See [SPEC_014 §5](SPEC_014_MEMORY_LAYER.md) for the full extraction pipeline design.

| Task | Description | Files |
|------|-------------|-------|
| P.1 | **Observation extraction on `OnCompleted`**: Subscribe in `MemoryPlugin.EnableAsync()`. On pipeline completion → queue `ExtractObservationsAsync()` via background `Channel<T>`. LLM call extracts typed atomic observations (fact/preference/instruction/context) from the result text. Each observation gets embedded and stored as Tier 2. | `MemoryPlugin.cs`, `Memory/ObservationExtractor.cs` |
| P.2 | **Tier 1 session context buffer**: In-memory ring buffer of recent pipeline results (current session). Auto-included in next pipeline call's context. Cleared on app close — never persisted. | `Memory/SessionContextBuffer.cs` |
| P.3 | **Tier 3 profile injection on `OnBeforeLlmProcessing`**: Retrieve user profile (`GetProfileAsync()`) → format as system prompt section → inject via `AdditionalSystemContext`. Profile includes: writing style per mode, domain vocabulary, correction patterns. | `MemoryPlugin.cs` |
| P.4 | **Semantic context injection on `OnBeforeLlmProcessing`**: `SearchAsync(userText, filter: currentMode + global)` → format top-K Tier 2 observations → append to `AdditionalSystemContext`. Mode-scoped: current mode observations + global, no cross-mode bleed. | `MemoryPlugin.cs` |
| P.5 | **Embedding queue**: Async `Channel<PipelineResult>` with single consumer — observation extraction + embedding generation never blocks the pipeline. Includes backpressure handling. | `Memory/EmbeddingQueue.cs` |
| P.6 | Unit tests: observation extraction (mock LLM), session context buffer, profile injection format, semantic search with mode scope, embedding queue throttling, novelty dedup integration | `Memory.Tests/MemoryPipelineTests.cs` |

**Note**: The Memory plugin subscribes to `OnCompleted` for ALL sources — dictation, meeting synthesis, vision, Chaviz conversations. Each module is both a producer (its output feeds observation extraction) and a consumer (its LLM calls receive injected context). See SPEC_014 §3 consumer/producer map for the full matrix.

**Commit**: `feat: integrate Memory observation extraction + context injection pipeline [SPEC_015-P]`

---

### Phase Q: Memory Settings + Governance UI + Consolidation [SPEC_015-Q]

> Users must be able to see what memory "knows" about them, edit/delete observations, and control the consolidation process. See [SPEC_014 §8](SPEC_014_MEMORY_LAYER.md) for governance design (inspired by LOOM Engine's principle: "Memory is permissioned. Knowledge is deliberate.").

| Task | Description | Files |
|------|-------------|-------|
| Q.1 | Create `MemorySettingsViewModel` — enable/disable, retention, stats (total observations by tier, embeddings count, storage size, oldest/newest), observation extraction toggle, consolidation mode | `Memory/ViewModels/MemorySettingsViewModel.cs` |
| Q.2 | Create `MemorySettingsPage.xaml` — master toggle, stats dashboard, retention slider, extraction toggle, consolidation mode selector, model info. Contributed via `IPluginUIRegistry.AddSettingsPage()`. | `Memory/Views/MemorySettingsPage.xaml` |
| Q.3 | **Tier 3 profile viewer**: Browsable display of what memory "knows" about the user — writing style, vocabulary, correction patterns, domain knowledge. Per-mode sections. User can edit or delete individual profile entries. | Same |
| Q.4 | **Tier 2 observation browser**: Searchable list of stored observations. Filter by type (fact/preference/instruction/context), mode scope, time range. User can delete individual observations. Novelty-flagged observations highlighted. | Same |
| Q.5 | **Consolidation trigger + review**: "Consolidate Now" button triggers `ConsolidateAsync()`. When `ConsolidationMode = Manual`, proposed Tier 3 profile updates are queued for user review (accept/reject each). Auto modes apply silently with undo option. | `MemoryPlugin.cs`, `Memory/ConsolidationService.cs` |
| Q.6 | Retention enforcement: purge observations older than `RetentionDays` on plugin enable. Tier 3 profile entries exempt from time-based purge. | `SqliteMemoryStore.cs` |
| Q.7 | "Clear All" with confirmation — separate options for "Clear observations only" vs "Clear everything (including profile)" | `MemorySettingsPage.xaml` |
| Q.8 | Unit tests: settings round-trip, retention purge (Tier 2 only), stats calculation, consolidation result review, profile CRUD, observation browser filtering | `Memory.Tests/MemorySettingsTests.cs` |

**Commit**: `feat: add Memory governance UI with profile viewer, observation browser, and consolidation [SPEC_015-Q]`

---

## 8. Cross-Plugin Integration

### Phase J: Cross-Plugin Bridge [SPEC_015-J]

> All plugins talk through `PipelineEventBus`. This phase verifies the E2E flows.

| Task | Description | Files |
|------|-------------|-------|
| J.1 | When Scribe synthesis completes, Meetings plugin publishes `PipelineResult` (mode = "meeting") to `PipelineEventBus`. Connectors plugin receives it and dispatches. Verify this path. | `MeetingsPlugin.cs` |
| J.2 | When VisionPipeline completes, it publishes to event bus (already done in Phase 0C). Connectors plugin receives mode = "vision". Verify. | `LoadingViewModel.cs` |
| J.3 | Memory plugin runs observation extraction on ALL `PipelineResult`s (dictation, meeting, vision). Verify all three source types produce typed Tier 2 observations with correct mode scopes and embeddings. | `MemoryPlugin.cs` |
| J.4 | Add `"meeting"` and `"vision"` to Connector Preset mode filter options — presets can opt in/out | `ConnectorPluginSettings.cs` |
| J.5 | Built-in example presets: "Meeting → Obsidian", "Screenshot → Obsidian" | Default settings |
| J.6 | E2E integration tests: mock pipeline → event bus → connectors receive; mock pipeline → event bus → memory stores; verify memory context injection | Integration tests |

**Commit**: `feat: verify cross-plugin flows via PipelineEventBus [SPEC_015-J]`

---

### Phase N: Meeting Captures (Vision + Meetings Integration) [SPEC_015-N]

> Scribe uses `ScreenCapture` (Core infrastructure) for session screenshots.

| Task | Description | Files |
|------|-------------|-------|
| N.1 | Add `CapturedImages: List<SessionCapture>` to `Session`. `SessionCapture`: `Id`, `Timestamp`, `ImagePath`, `Query?`, `AiDescription?` | `Meetings/Session.cs` |
| N.2 | Add "Capture" button to ScribeWindow. Click → `ScreenCapture.CaptureActiveWindow()` or region → save PNG → add to session | `ScribeWindow.xaml`, `ScribeViewModel.cs` |
| N.3 | Optional: voice query after capture → `ProcessWithImageAsync()` → store `AiDescription` | `ScribeViewModel.cs` |
| N.4 | Synthesis enrichment: include image descriptions in prompt | `MeetingSynthesizer.cs` |
| N.5 | Display captures: thumbnail strip in ScribeWindow | `ScribeWindow.xaml` |
| N.6 | Unit tests: capture storage, synthesis with images, cleanup | `Meetings.Tests/` |

**Note**: Uses `ScreenCapture` from Core (Phase 0C), not from the Vision pipeline. No dependency on the Vision module.

**Commit**: `feat: add meeting screenshot captures with synthesis enrichment [SPEC_015-N]`

---

## 9. Project Structure

### Solution Layout

```
DiktaMe.sln
├── src/
│   ├── DiktaMe.App/                              # WinUI 3 host (existing, modified)
│   ├── DiktaMe.Core/                             # Business logic (existing + Vision additions)
│   │   └── Vision/                               # NEW: ScreenCapture, ImageProcessor, VisionOptions
│   ├── DiktaMe.Plugin.Abstractions/              # NEW: IPlugin, hooks, settings, UI contracts
│   ├── DiktaMe.Plugin.Connectors/                # NEW: Connectors plugin
│   │   ├── Connectors/                           # IConnector impls, Manager, Presets
│   │   ├── Views/                                # Settings page, inbox panel, presets widget
│   │   └── ViewModels/                           # Settings VM, inbox VM
│   ├── DiktaMe.Plugin.Meetings/                  # NEW: Meetings/Scribe plugin
│   │   ├── Meetings/                             # Session, Recorder, Transcriber, Synthesizer
│   │   ├── Views/                                # ScribeWindow, SessionList, settings page
│   │   └── ViewModels/                           # ScribeVM, settings VM
│   └── DiktaMe.Plugin.Memory/                    # NEW: Memory plugin
│       ├── Memory/                               # SqliteMemoryStore, EmbeddingGenerator
│       ├── Views/                                # Settings page
│       └── ViewModels/                           # Settings VM
├── tests/
│   ├── DiktaMe.Core.Tests/                       # Existing + SPEC_009 + Vision tests
│   ├── DiktaMe.Plugin.Abstractions.Tests/        # NEW: EventBus, Manager, Settings, Registry
│   ├── DiktaMe.Plugin.Connectors.Tests/          # NEW
│   ├── DiktaMe.Plugin.Meetings.Tests/            # NEW
│   └── DiktaMe.Plugin.Memory.Tests/              # NEW
```

### Reference Graph

```
DiktaMe.Plugin.Abstractions
    └── References: DiktaMe.Core

DiktaMe.Plugin.Connectors (UseWinUI=true)
    ├── References: DiktaMe.Plugin.Abstractions
    └── References: DiktaMe.Core

DiktaMe.Plugin.Meetings (UseWinUI=true)
    ├── References: DiktaMe.Plugin.Abstractions
    └── References: DiktaMe.Core

DiktaMe.Plugin.Memory (UseWinUI=true)
    ├── References: DiktaMe.Plugin.Abstractions
    └── References: DiktaMe.Core

DiktaMe.App
    ├── References: DiktaMe.Core (existing)
    ├── References: DiktaMe.Plugin.Abstractions (for PluginManager, EventBus)
    └── Does NOT reference any plugin project (runtime discovery)
```

---

## 10. Settings Architecture

### Core AppSettings (only Vision added)

```
AppSettings
├── ... (existing 12 sub-objects, unchanged)
│
└── Vision: VisionSettings (NEW — Phase 0C, in Core)
    ├── Enabled: bool (default: true)
    ├── DefaultQuery: string (default: "Describe what you see and extract any visible text.")
    ├── MaxImageDimensionPx: int (default: 2048)
    ├── AutoRecordQuery: bool (default: true)
    ├── QueryTimeoutSeconds: int (default: 10)
    └── OutputMode: string (default: "inject") — "inject" | "clipboard" | "toast"

HotkeySettings
├── ... (existing hotkeys)
└── Vision: string (default: "Ctrl+Alt+S")
```

### Plugin Settings (separate files, NOT in AppSettings)

```
%APPDATA%/DiktaMe/
├── settings.json                          # Core app (unchanged except +VisionSettings)
├── plugin-states.json                     # { "connectors": true, "meetings": true, "memory": false }
└── plugins/
    ├── connectors-settings.json           # ConnectorPluginSettings
    │   ├── Enabled: bool (false)
    │   ├── InboxRetentionDays: int (30)
    │   ├── Destinations: List<ConnectorConfig>
    │   └── Presets: List<ConnectorPreset>
    │
    ├── meetings-settings.json             # MeetingPluginSettings
    │   ├── DefaultTemplate: string ("meeting_minutes")
    │   ├── AudioFormat: string ("opus")
    │   ├── RetentionDays: int (90)
    │   ├── DefaultSttProvider: string? (null = global)
    │   ├── DefaultLlmProvider: string? (null = global)
    │   ├── DefaultLlmModel: string? (null = global)
    │   ├── AutoDuck: bool (true)
    │   └── AutoCompress: bool (true)
    │
    └── memory-settings.json               # MemoryPluginSettings
        ├── Enabled: bool (false — opt-in)
        ├── RetentionDays: int (365)
        ├── MaxEntries: int (10000)
        ├── ContextInjectionEnabled: bool (true)
        ├── ContextResultLimit: int (5)
        ├── MinSimilarity: double (0.7)
        └── EmbeddingModel: string ("all-MiniLM-L6-v2")
```

---

## 11. Modified Files (Host App)

| File | Change | Phase |
|------|--------|-------|
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `VisionSettings Vision` property, add `Vision = 8` to `HotkeyId`, add `Vision` to `HotkeySettings` | 0C |
| `src/DiktaMe.Core/Config/SettingsManager.cs` | Add `Vision` to `SanitizeNulls()` | 0C |
| `src/DiktaMe.Core/LLM/ILLMProvider.cs` | Add `ProcessWithImageAsync()` default interface method | 0C |
| `src/DiktaMe.Core/LLM/GeminiProvider.cs` | Override `ProcessWithImageAsync()` | 0C |
| `src/DiktaMe.Core/LLM/AnthropicProvider.cs` | Override `ProcessWithImageAsync()` | 0C |
| `src/DiktaMe.Core/LLM/OpenAICompatibleProvider.cs` | Override `ProcessWithImageAsync()` | 0C |
| `src/DiktaMe.App/App.xaml.cs` | Register `PipelineEventBus`, `PluginUIRegistry`, `PluginManager` singletons | 0B |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Add plugin discovery + enable; Add `PublishCompleted()` calls; Add Vision hotkey dispatch | 0B, 0C |
| `src/DiktaMe.Core/Pipeline/DictationPipeline.cs` | Add `PipelineEventBus?` optional param, call `PublishBeforeLlmAsync()` | 0B |
| `src/DiktaMe.Core/Pipeline/AskPipeline.cs` | Same BeforeLlm hook | 0B |
| `src/DiktaMe.Core/Pipeline/ChatPipeline.cs` | Same BeforeLlm hook | 0B |
| `src/DiktaMe.Core/Config/PipelineFactory.cs` | Resolve `PipelineEventBus` from DI, pass to pipelines | 0B |
| `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` | Dynamic NavigationViewItems from `PluginUIRegistry`, `ContributionsChanged` handler | 0B |
| `src/DiktaMe.App/Views/TrayIconView.xaml.cs` | Plugin tray items in `ShowContextMenu()` | 0B |

---

## 12. Test Targets

| Phase | New Tests | Cumulative |
|-------|-----------|------------|
| 0A (SPEC_009) | ~28 (Factory tests) | 835+ |
| 0B (Plugin infra) | ~35 (EventBus, Manager, Settings, Registry) | 870+ |
| 0C (Vision) | ~20 (ScreenCapture, ImageProcessor, multimodal, VisionPipeline) | 890+ |
| A (Connector framework) | ~10 | 900+ |
| B (Obsidian) | ~8 | 908+ |
| C (4 connectors) | ~20 | 928+ |
| D (Session engine) | ~15 | 943+ |
| E (Scribe window) | ~5 | 948+ |
| F (Connector UI) | ~5 | 953+ |
| G (Post-meeting) | ~8 | 961+ |
| H (Inbox, polish) | ~12 | 973+ |
| I (Meeting polish) | ~8 | 981+ |
| J (Cross-plugin E2E) | ~6 | 987+ |
| N (Meeting captures) | ~6 | 993+ |
| O (Memory store) | ~10 | 1003+ |
| P (Memory hooks) | ~8 | 1011+ |
| Q (Memory settings) | ~5 | 1016+ |
| **Total** | **~209 new tests** | **1016+** |

---

## 13. Commit Strategy

Trunk-based, one commit per phase:

```
test: add STTProviderFactory and LLMProviderFactory unit tests [SPEC_015-0A]
feat: add plugin infrastructure (IPlugin, PipelineEventBus, PluginManager) [SPEC_015-0B]
feat: add Vision pipeline with ScreenCapture, multimodal LLM, and Ctrl+Alt+S hotkey [SPEC_015-0C]
feat: add Connectors plugin with IConnector framework and ConnectorManager [SPEC_015-A]
feat: add Obsidian vault connector [SPEC_015-B]
feat: add Folder, Webhook, Discord, and Streamer.bot connectors [SPEC_015-C]
feat: add Meetings plugin with Session engine and recording [SPEC_015-D]
feat: add Scribe window with notepad and AI synthesis [SPEC_015-E]
feat: add Connector settings page and Presets UI [SPEC_015-F]
feat: add post-meeting chat, export, and audio playback [SPEC_015-G]
feat: add connector notifications, inbox, and polish [SPEC_015-H]
feat: add speaker naming, search, hotkeys, and meeting polish [SPEC_015-I]
feat: verify cross-plugin flows via PipelineEventBus [SPEC_015-J]
feat: add Google Calendar and Gmail connectors [SPEC_015-K]
feat: add meeting screenshot captures with synthesis enrichment [SPEC_015-N]
feat: add Memory plugin with SQLite+VSS vector store and embeddings [SPEC_015-O]
feat: integrate Memory plugin with pipeline event hooks [SPEC_015-P]
feat: add Memory settings page with stats, retention, and search [SPEC_015-Q]
```

---

## 14. Progress Tracker

| Phase | Scope | Status | Commit | Tests |
|-------|-------|--------|--------|-------|
| 0A: SPEC_009 Tests | Core | `PENDING` | — | — |
| 0B: Plugin Infrastructure | Core | `PENDING` | — | — |
| 0C: Vision Core Integration | Core | `PENDING` | — | — |
| A: Connector Framework | Connectors Plugin | `PENDING` | — | — |
| B: Obsidian Connector | Connectors Plugin | `PENDING` | — | — |
| C: Folder/Webhook/Discord/SB | Connectors Plugin | `PENDING` | — | — |
| D: Session Engine | Meetings Plugin | `PENDING` | — | — |
| E: Scribe Window | Meetings Plugin | `PENDING` | — | — |
| F: Connector Settings + UI | Connectors Plugin | `PENDING` | — | — |
| G: Post-Meeting Experience | Meetings Plugin | `PENDING` | — | — |
| H: Notifications + Inbox | Connectors Plugin | `PENDING` | — | — |
| I: Meetings Polish | Meetings Plugin | `PENDING` | — | — |
| J: Cross-Plugin Bridge | All | `PENDING` | — | — |
| K: Google OAuth (Release 2) | Connectors Plugin | `PENDING` | — | — |
| N: Meeting Captures | Meetings Plugin | `PENDING` | — | — |
| O: Memory Infrastructure | Memory Plugin | `PENDING` | — | — |
| P: Memory Pipeline Hooks | Memory Plugin | `PENDING` | — | — |
| Q: Memory Settings | Memory Plugin | `PENDING` | — | — |

---

## 15. Multi-Session Instructions

### Session Workflow

1. **Start of session**: Read this spec. Check `git log --oneline -10` for last `[SPEC_015-*]` commit.
2. **Pick the next uncompleted phase** from the Progress Tracker above.
3. **Check prerequisites**: Follow the dependency graph (Section 2).
4. **Implement all tasks in the phase**: Follow the task table row by row.
5. **Run tests**: `dotnet test DiktaMe.sln` — ALL tests must pass.
6. **Build check**: `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors.
7. **Commit**: Use the commit message from Section 13.
8. **Update Progress Tracker**: Mark phase as `COMPLETE`.

### Key Patterns

**Core patterns (existing):**
- **Settings records**: `sealed record`, `= new()` defaults, add to `SanitizeNulls()` (only for Core settings like Vision)
- **DI registration**: Singleton for managers, Transient for ViewModels
- **HTTP clients**: Cached with `ConnectionClose = false` (LLMProviderFactory pattern)
- **Logging**: Structured Serilog: `Log.Information("ConnectorManager: ...")`
- **Error handling**: Never let module failure crash core pipeline. Catch all, log, toast, continue.
- **Test mocking**: Moq. Always pass `It.IsAny<CancellationToken>()` explicitly.
- **XAML**: No `x:Bind` on `Run.Text`. Use computed ViewModel properties instead of converters in `Window`.
- **Namespaces**: `DiktaMe.Core.Vision` (Core). Never `DiktaMe.Core.System.*`.

**Plugin patterns (new):**
- **Entry class**: `[PluginEntry("id", "Name", "1.0.0")]` attribute on the `IPlugin` implementation
- **Lifecycle**: `InitializeAsync()` = lightweight init. `EnableAsync()` = subscribe hooks + register UI. `DisableAsync()` = dispose subscriptions + remove UI.
- **Settings**: `await _context.Settings.LoadAsync<MyPluginSettings>()` / `SaveAsync()`. Each plugin has its own JSON file.
- **Event subscriptions**: All `On*()` calls return `IDisposable`. Store them in a `List<IDisposable>`, dispose all in `DisableAsync()`.
- **UI contribution**: `_context.UI.AddSettingsPage(...)`, `_context.UI.AddTrayMenuItems(...)`, `_context.UI.AddControlPanelWidget(...)`. Remove all in `DisableAsync()`.
- **Page creation**: Plugin's `PageFactory` lambda creates the XAML `Page` instance, passing services from `IPluginContext`. Host sets `ContentFrame.Content = page`, not `Navigate(Type)`.
- **Core service access**: `var llm = _context.Services.GetRequiredService<LLMRouter>()`. Plugins resolve Core singletons.
- **Build output**: Plugin DLLs go to `plugins/{Name}/` subfolder. Post-build targets handle copying.
- **Plugin namespaces**: `DiktaMe.Plugin.Connectors`, `DiktaMe.Plugin.Meetings`, `DiktaMe.Plugin.Memory`

### Critical Gotchas

**Core gotchas (existing):**
- `SanitizeNulls()` — Only add `Vision` (Core). Plugin settings are not in AppSettings.
- Cross-thread `ObservableCollection` — `DispatcherQueue.TryEnqueue()` for all UI-bound updates
- NRE in UI thread = silent crash (exit 127) — guard ALL property change paths
- `ProcessWithImageAsync()` default throws `NotSupportedException` — catch and toast
- Screenshot `byte[]` can be 8MB PNG — always run `ImageProcessor` before API
- Multi-monitor snipping overlay must cover all displays

**Plugin gotchas (new):**
- `Assembly.LoadFrom()` loads into default context — assemblies cannot be unloaded. "Disable" = cleanup, not unload.
- Plugin XAML `.xbf` files must be in the same directory as the plugin DLL for WinUI to find them
- `ContentFrame.Content = page` (direct assignment), NOT `Navigate(Type)` for plugin pages
- Plugin `EnableAsync`/`DisableAsync` run on UI thread — keep them fast, defer heavy work to background
- `PipelineEventBus.OnCompleted` fires from background threads — plugins must handle thread safety
- Plugin settings use `JsonSerializer` (System.Text.Json), not the host's source-generated serializer context
- Moq optional params — always explicit `It.IsAny<CancellationToken>()`

**Meetings plugin gotchas:**
- NAudio `WasapiLoopbackCapture` requires WASAPI shared mode
- Long recordings: stream to disk, never buffer in RAM (1hr WAV = ~660MB)
- Opus compression: use managed NuGet (Concentus) or shell to ffmpeg

**Memory plugin gotchas:**
- SQLite VSS native extension needs loading alongside existing SQLite — test x64 and arm64
- Embedding generation ~50ms per — never block pipeline. Use `Channel<T>` queue.
- ONNX Runtime shared with Kokoro TTS — watch for version conflicts
- `Enabled = false` default — privacy-first, user must opt in

---

## 16. Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| WinUI 3 XAML from plugin assemblies fails to resolve | All plugin UI broken | Medium | Test early in Phase 0B. Fallback: plugins provide code-behind UI (no XAML). |
| Plugin load order matters (Memory before Connectors) | Event bus race | Low | PluginManager sorts by priority field. Events are async — ordering rarely matters. |
| Audio capture fails on some machines | Meeting recording broken | Medium | Graceful error, fallback to mic-only |
| Deepgram batch transcription cost for long meetings | User cost surprise | Low | Show estimated cost, local Whisper fallback |
| Scribe XAML complexity | Long Phase E | High | Simple TextBox for notes, WebView2 for Markdown |
| Google OAuth verification delays | Phase K blocked | Medium | Ship without Google, use testing mode |
| Snipping overlay multi-monitor / mixed DPI | Visual glitches | Medium | Union of display bounds, WinUI 3 DPI support |
| SQLite VSS native extension fails on some machines | Memory broken | Medium | Fallback to pure-managed cosine similarity for small datasets |
| ONNX Runtime version conflict | Build failure | Medium | Pin shared version, test both embedding + TTS |
| Plugin DLL locked while app running | Can't update without restart | Low | Expected — document that plugin updates require restart |

---

## 17. Future Plugins

### Stream Deck Plugin (`DiktaMe.Plugin.StreamDeck`)

> Full specification: [`SPEC_005_STREAMDECK.md`](SPEC_005_STREAMDECK.md)

A natural fit for the plugin architecture. The Stream Deck plugin would:
- Use `IPipelineEventBus.OnCompleted` to reflect pipeline state on deck buttons
- Expose named pipe IPC server for the Stream Deck Plugin (C# SDK) to connect to
- Two actions: **Pipeline Trigger** (fire dictation/ask/translate from a button) and **Settings Toggle** (flip STT local/cloud, mute, etc.)
- Est. effort: ~26 hours (per SPEC_005)
- **Not in this sprint** — planned as the first third-party-style plugin after V2 ships

---

## 18. Market Impact

### What This Sprint Delivers

| Capability | Competitive Standing |
|------------|---------------------|
| Voice → Obsidian vault (daily note + standalone) | **Only tool on any platform** |
| Voice → Webhook → Zapier/n8n → 1000+ apps | Matches Fireflies/Otter at $0 |
| Meeting recording + AI synthesis (local-first) | Matches Granola ($14/mo) at $0, with privacy |
| Meeting → Obsidian + Webhook auto-dispatch | **No competitor** (meetings + integrations + local) |
| Streamer.bot voice control | **Unique** — voice-to-automation bridge |
| "Ask this meeting" with local LLM | Matches Granola/Fellow chat, with privacy |
| Composable Connector Presets | **Novel** — no competitor has per-preset routing |
| System-wide screenshot → AI analysis at cursor | **No competitor** has hotkey → capture → LLM → inject |
| Meeting whiteboard capture → synthesis enrichment | Zero visual capture in Granola/Fellow |
| Local multimodal (Ollama LLaVA) | Screenshot analysis without cloud — unique |
| Semantic memory with local embeddings | **No desktop dictation tool** has persistent AI memory |
| Context-aware dictation (memory-enriched prompts) | AI that improves with use — unique in voice tools |
| **Hot-pluggable module system** | **Architectural differentiator** — extensible, community-ready |

### V2 Completion Scope

After this sprint, V2's feature set is **locked**:

| Feature | Status |
|---------|--------|
| Core dictation pipeline (8 modes) | Shipped |
| Cloud + Local STT | Shipped |
| Cloud + Local LLM | Shipped |
| TTS (Kokoro) | Shipped |
| CRUD Dictation Modes | Shipped |
| OAuth & Wallet | Shipped |
| Deepgram Streaming | Shipped |
| Chat (QuickChat) | Shipped |
| Internationalization (en + es-MX) | Shipped |
| **Plugin Infrastructure** | **This sprint (Phase 0B)** |
| **Vision Pipeline (Core)** | **This sprint (Phase 0C)** |
| **Connectors Plugin** | **This sprint** |
| **Meetings Plugin (Scribe)** | **This sprint** |
| **Memory Plugin** | **This sprint** |
| Google OAuth (Calendar + Gmail) | Release 2 (Phase K) |
| Stream Deck Plugin | Post-V2 (SPEC_005) |
| Notion / Slack / CRM connectors | Release 2-3 |

---

## Next: SPEC_016 — Refinemmarly (Grammarly-like Grammar Check)

> **Prerequisite:** All SPEC_015 phases complete (plugin infrastructure + all three plugins + vision core shipped).
>
> Once SPEC_015 is done, proceed to [`SPEC_016_V2.1_REFINEMMARLY.md`](SPEC_016_V2.1_REFINEMMARLY.md) — enhances Refine Auto into a Grammarly-like grammar checker with inline diff popup, per-word/phrase accept/reject, and passive clipboard monitoring. This is a V2.1 feature that builds on the completed V2 foundation.

---

## 19. Research Notes

### SurfSense (github.com/MODSetter/SurfSense) — 2026-03-22

Open-source NotebookLM/Perplexity alternative. Python FastAPI + Next.js + LangGraph. Researched for patterns applicable to SPEC_014 (Memory) and SPEC_015 (Plugin Architecture).

**Applicable patterns for Memory Plugin (Phases O–Q):**
- **Hierarchical indexing**: Documents stored with both summary-level AND chunk-level embeddings. Could apply to dictation sessions (session summary + per-segment chunks).
- **4-category memory**: `preference`, `fact`, `instruction`, `context` — stored with pgvector embeddings. Agent decides when to save/recall. Relevant for auto-extracting correction patterns, vocabulary, domain terms.
- **Hybrid search with RRF**: Semantic vector search + full-text search merged via Reciprocal Rank Fusion (k=60). For local: SQLite FTS5 + VSS with simpler RRF.
- **Content hashing for dedup**: `compute_content_hash()` + `compute_unique_identifier_hash()` prevent duplicate indexing. Good for incremental updates.
- **Capped memory with LRU eviction**: 100 memories per user, oldest evicted. Simple but effective for bounded memory.

**Applicable patterns for Plugin Architecture (Phase 0B):**
- **Tool Registry**: `ToolDefinition` dataclass + factory + dependency injection + enable/disable + hidden/WIP. Maps well to C# `IPlugin` + DI.
- **MCP (Model Context Protocol)**: Dynamically loads tools from external servers (stdio/HTTP). Forward-looking extensibility — could expose pipeline stages as MCP tools.
- **Connector indexer pattern**: authenticate → fetch → convert to common format → index. Mirrors our connector output pattern.

**What SurfSense does NOT have:**
- No dedicated translation pipeline (language = LLM system prompt instruction only).
- Server-centric architecture (PostgreSQL + Celery). Our local-first approach needs same abstractions backed by SQLite.

**Notable shared tech:** Kokoro for TTS (9 lang codes), faster-whisper for STT with auto language detection.

**Potential future idea: Help Agent via Chat window** — SurfSense's knowledge base + hybrid search pattern could power an in-app help agent. Index all app docs/knowledge into the Memory Plugin, then the Chat window queries it via semantic search to answer user questions about the app. See "Help Agent" section below.

### Help Agent Concept (Future — post-SPEC_015)

Use the Memory Plugin's vector store + hybrid search to power an in-app help agent accessible via the existing Chat window. Index all app documentation, feature descriptions, and usage guides into a dedicated "help" memory partition. When the user asks a question in Chat, the system:
1. Detects help-intent (or user toggles "Help Mode")
2. Searches the help knowledge base via semantic similarity
3. Injects top-K relevant doc chunks into the LLM system prompt
4. LLM responds with app-specific guidance grounded in actual docs

This is a **RAG (Retrieval-Augmented Generation)** pattern applied to self-help. The knowledge base could be:
- Bundled markdown files shipped with the app (features, settings explanations, troubleshooting)
- Auto-indexed from the FeaturesModal content and settings descriptions
- Versioned with the app (re-index on update)

Advantages over a static FAQ: understands paraphrased questions, can combine knowledge from multiple docs, stays current with the app version.

**Implementation sketch (depends on Memory Plugin Phase O being complete):**
- New `HelpKnowledgeBase` class that indexes bundled `.md` files on first launch
- `ChatPipeline` gains a `HelpMode` toggle: when active, prepends help context to system prompt
- Small dedicated SQLite table (or partition in memory.db) for help vectors
- No cloud dependency — runs entirely via local embeddings + local or cloud LLM

### Honcho (github.com/plastic-labs/honcho) — 2026-03-24

Open-source Python agent memory library by Plastic Labs. Researched for cognitive architecture patterns applicable to SPEC_014 (Memory Layer) and Phases O–Q.

**Architecture — 3-stage cognitive pipeline:**
1. **Deriver**: Post-conversation, a single LLM call extracts **atomic, self-contained observations** from the interaction. Each observation is explicitly stated in the text (no inference). Stored with embeddings for later retrieval.
2. **Dreamer**: Background consolidation with three phases:
   - **Surprisal scoring**: Geometric embedding distance via tree-based structure identifies novel vs. redundant observations. High-surprisal = novel, low = skip.
   - **Deduction specialist**: Logical synthesis — combines existing observations to derive new conclusions ("User works at Contoso" + "User mentions Kubernetes daily" → "User likely does DevOps at Contoso").
   - **Induction specialist**: Pattern recognition — identifies recurring themes across many observations to surface preferences and tendencies.
3. **Dialectic Agent**: At query time, uses **agentic reasoning with tools** (not just cosine similarity). 8-step iterative workflow: examine query → search observations → assess sufficiency → search again or synthesize → respond. Tools include `retrieve_memories`, `retrieve_user_representation`.

**Key patterns adopted in SPEC_014:**
- Observation extraction (Deriver → our `ExtractObservationsAsync()`)
- Typed observations (explicit, deductive, inductive → our fact/preference/instruction/context)
- Novelty detection via embedding similarity (Surprisal → our cosine dedup threshold)
- Consolidation as background process (Dreamer → our `ConsolidateAsync()`)

**What we explicitly excluded:**
- Full dialectic agent (too heavy for inline pipeline latency — 8-step agentic loop adds seconds)
- Multi-peer modeling (single-user desktop app, not multi-tenant)
- Separate deduction + induction specialist LLM calls (simplified to single consolidation pass)
- Geometric tree-based surprisal (simplified to flat cosine similarity thresholds)

### LOOM Engine (user's cognitive architecture project) — 2026-03-24

4-layer cognitive architecture designed for AI agent memory governance. Researched for hierarchical memory model and governance patterns applicable to SPEC_014.

**Architecture — 4-layer memory hierarchy:**
- **L4 Identity/Telos** (immutable): Core identity, purpose, ethical constraints. Never modified by runtime.
- **L3 Knowledge** (persistent): Accumulated knowledge, preferences, learned patterns. Promoted deliberately from L2.
- **L2 Episodic** (session summaries): HAII (High-signal Annotation Identification and Indexing) extracts key moments from conversations. Session-level, condensed for long-term.
- **L1 Active** (ephemeral): Runtime working memory. Cleared after session.

**Core principles:**
- *One-way authority*: Higher layers constrain lower, never the reverse.
- *Memory is permissioned*: All promotion from L2→L3 is deliberate, auditable, human-authorized.
- *World isolation*: Hard cognitive sandboxes per project/domain — no cross-contamination.
- *"Memory is permissioned. Knowledge is deliberate. Experience is condensed. Execution is temporary."*

**Key patterns adopted in SPEC_014:**
- 3-tier hierarchy (LOOM's L4+L3 → our Tier 3 Profile, L2 → our Tier 2 Observations, L1 → our Tier 1 Session Context). We collapsed L4/L3 since dIKta.me has no immutable identity kernel.
- Mode-scoped retrieval (World isolation → our mode scope tags on observations, queries filter by current mode + global)
- User governance UI (META governance agent → our Memory Settings page with profile viewer, observation browser, consolidation review)
- Deliberate promotion (L2→L3 promotion gates → our consolidation with optional manual review)

**What we explicitly excluded:**
- L4 immutable identity kernel (desktop app, not autonomous agent framework)
- META governance agent (replaced with user-facing settings UI — simpler, more transparent)
- Agent University / versioning (no autonomous agent lifecycle management)
- Replication Layer (single-user, no cross-node validation needed)

---

## Appendix: Vision Module — PMF Gap Analysis (March 2026)

> Cross-reference of screenshot market PMF research against current dIKta.me Vision capabilities.
> Market data validated against [TBRC](https://www.thebusinessresearchcompany.com/market-insights/screen-capture-software-market-insights-2025) and [OpenPR](https://www.openpr.com/news/4044887/global-screen-capture-software-market-outlook-2025-2034) reports.
> Screen capture market: **$9B (2025) → $30.1B (2034), 14.3% CAGR** (not 18.5% as some sources claim).

### What We Already Have (Competitive Advantages)

| Feature | Status | Competitor Parity |
|---------|--------|-------------------|
| Multi-mode capture (region, window, fullscreen) | Done | Table stakes — all competitors have this |
| Hotkey-driven (Ctrl+Alt+S) | Done | Table stakes |
| AI vision analysis (Cloud + Local) | Done | **Unique** — no screenshot tool does this |
| Multi-turn vision chat | Done | **Unique** — conversational image analysis |
| Vision + Voice multimodal notes | Done | **Unique** — screenshot + voice + AI in one note |
| Local AI (Ollama, on-device) | Done | **Unique** — privacy-first, no cloud upload needed |
| 4-action modal (Save/Clipboard/Chat/Note) | Done | Beyond competitors — they only copy/save |
| 4 LLM providers (Gemini, Ollama, OpenAI, Anthropic) | Done | No competitor has provider choice |
| PNG + auto-JPEG compression | Done | Table stakes |

### Table-Stakes Gaps (Missing = Not a Real Screenshot Tool)

| ID | Feature | Severity | Effort | Notes |
|----|---------|----------|--------|-------|
| VG-1 | **Copy screenshot IMAGE to clipboard** | HIGH | LOW | Currently only copies AI text. Users expect image bytes on clipboard (Win+Shift+S parity). |
| VG-2 | **Basic annotation** (arrows, shapes, text, highlight, blur) | CRITICAL | HIGH | Every competitor has this. Post-capture editor window needed. |
| VG-3 | **Cloud upload + share link** | HIGH | MEDIUM | One-keystroke share returning a URL. Supabase Storage infra already exists. |
| VG-4 | **Scrolling / full-page capture** | CRITICAL | HIGH | Web pages, long documents, chat threads. Win32 scroll-and-stitch. |

### AI-Native Differentiators (We're Uniquely Positioned)

| ID | Feature | Effort | Why Us |
|----|---------|--------|--------|
| VD-1 | **Dedicated OCR mode** | LOW | Current "extract text" is a generic LLM prompt. Dedicated OCR prompt + structured output + copy button. minicpm-v is OCR-capable. |
| VD-2 | **Copy-as-table** (CSV/TSV) | LOW | AI extracts tabular data → paste as spreadsheet-ready text. |
| VD-3 | **AI auto-redaction** (PII blur) | MEDIUM | LLM identifies PII regions → overlay blur rectangles. Local mode = private redaction. |
| VD-4 | **AI smart crop** | MEDIUM | LLM identifies "interesting region" → auto-crop. No competitor has this. |
| VD-5 | **Searchable screenshot vault** | MEDIUM | SQLite index with AI-generated descriptions. HistoryManager pattern exists. |
| VD-6 | **Post-capture workflow chains** | MEDIUM | PipelineEventBus + plugin system ready. Capture → OCR → clipboard → notification. |

### Nice-to-Have (Lower Priority)

| ID | Feature | Effort | Notes |
|----|---------|--------|-------|
| VN-1 | Background beautification (gradients, shadows) | MEDIUM | CleanShot X's signature. Marketing use case. |
| VN-2 | Screen recording + GIF | HIGH | Different product category entirely. |
| VN-3 | Color picker / pixel ruler | LOW | Developer niche. Win32 `GetPixel`. |
| VN-4 | Multi-monitor picker | LOW | Already capture active monitor; need selector for others. |
| VN-5 | WebP output | LOW | Trivial ImageProcessor extension. |
| VN-6 | Watermarking automation | LOW | Logo/text overlay on output. |

### Recommended Build Order (Integrate Into Module Phases)

**Can tackle during other module work:**
1. **VG-1** (image clipboard copy) — trivial, do during any Vision touch
2. **VD-1** (OCR mode) — add as a 5th action button in VisionActionWindow, or prompt preset
3. **VD-2** (copy-as-table) — variant of OCR with structured output format

**Dedicated sprint needed:**
4. **VG-2** (annotation editor) — new AnnotationWindow with canvas drawing tools
5. **VG-3** (share link) — Supabase Storage upload + short URL generation
6. **VD-5** (screenshot vault) — SQLite table + search UI in Settings

**Later phases:**
7. **VD-3** (AI redaction) — after annotation editor exists (needs blur drawing)
8. **VG-4** (scrolling capture) — complex Win32 work, defer to dedicated sprint
9. **VD-4** (AI smart crop) — nice addition once core gaps are closed

### Our Moat (What No Competitor Has)

1. **Voice-first** — "What does this error mean?" spoken after capture
2. **Multi-turn vision chat** — back-and-forth conversation about a screenshot
3. **Multimodal notes** — screenshot + voice + AI description in one entry
4. **Local AI privacy** — on-device vision without cloud upload
5. **Integrated suite** — not standalone; part of dictation + chat + notes ecosystem

---

*End of SPEC_015*
