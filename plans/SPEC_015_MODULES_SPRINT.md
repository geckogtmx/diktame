# SPEC_015: V2 Modules Sprint — Connectors + Meetings + Vision + Memory

> **Status:** DRAFT
> **Date:** 2026-03-14
> **Architecture:** Four isolated modules sharing common infrastructure, zero coupling between them
> **Goal:** Complete all remaining V2 feature modules in one sprint. Lock down V2.
> **Design References (source specs):**
> - [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md) — Module 1: Connectors (Phases A–E + J)
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Module 2: Meetings/Scribe (Phases F–I + N)
> - [`SPEC_002_VISION.md`](SPEC_002_VISION.md) — Module 3: Vision/See (Phases L–N)
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Module 4: Memory (Phases O–Q)
> - [`SPEC_013_USE_CASES.md`](SPEC_013_USE_CASES.md) — 218 use cases driving connector design decisions

---

## 1. Executive Summary

This document combines four independent modules — **Connectors** (SPEC_013), **Meetings/Scribe** (SPEC_001), **Vision/See** (SPEC_002), and **Memory** (SPEC_014) — into a single development sprint that completes the V2 feature set. All four are designed as isolated add-ons that plug into the existing dIKta.me pipeline through minimal, well-defined hook points.

### Why Together?

1. **Shared infrastructure** — All four need LLM providers, notifications, and settings. Meetings + Vision share audio capture. Connectors + Vision share the `ConnectorInputType.Screenshot` input path. Memory enhances LLM context for all modules. Building one lays groundwork for the others.
2. **The killer workflows** — Meeting ends → Scribe produces summary → Connector Preset auto-fires → Obsidian + Slack. Screenshot during meeting → Vision analyzes whiteboard → attached to session artifact. Memory recalls "last time we discussed this topic" and enriches every LLM prompt. No single module delivers these alone.
3. **Modular architecture validates at scale** — Four modules prove the pattern. If all stay isolated, the architecture is rock-solid.
4. **Market positioning** — Competitors do meetings OR integrations OR vision OR memory. We do all four, locally, in one app.
5. **V2 lockdown** — This sprint closes the V2 feature scope. Everything after this is polish, optimization, or V3.

### Module Independence

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                              dIKta.me Core                                            │
│  Pipeline, LLM, STT, Audio, Settings, History, Security, TextInjector                │
│                                                                                       │
│  Hook Points:                                                                         │
│    • OnPipelineCompleted() → ConnectorManager (1 line)                               │
│    • OnPipelineCompleted() → MemoryLayer.StoreAsync() (1 line)                       │
│    • Before LLM → MemoryLayer.SearchAsync() → context injection                     │
│    • SessionManager (standalone, no hook needed)                                      │
│    • HotkeyId.Vision → VisionPipeline (hotkey dispatch)                              │
│                                                                                       │
│  Shared Infrastructure (new):                                                         │
│    • Vision/ScreenCapture.cs — used by Vision standalone + Meetings captures          │
│    • ILLMProvider.ProcessWithImageAsync() — multimodal LLM extension                 │
│    • Memory/IMemoryLayer.cs — semantic store/search, used by all LLM-backed modules  │
└───┬──────────────────┬──────────────────────┬──────────────────────┬─────────────────┘
    │                  │                      │                      │
┌───▼─────────────┐  ┌▼──────────────────┐  ┌▼────────────────────┐ ┌▼────────────────┐
│ CONNECTOR MODULE│  │ MEETINGS MODULE   │  │  VISION MODULE      │ │ MEMORY MODULE   │
│                 │  │ (Scribe)          │  │  (See)              │ │                 │
│ Connectors/     │  │ Meetings/         │  │  Vision/            │ │ Memory/         │
│ ConnectorMgr    │  │ SessionManager    │  │  ScreenCapture      │ │ IMemoryLayer    │
│ IConnector impls│  │ ScribeWindow      │  │  SnippingOverlay    │ │ SqliteMemory    │
│ Presets + Inbox │  │ MeetingRecorder   │  │  VisionPipeline     │ │ EmbeddingModel  │
│ SettingsWindow  │  │ Synthesizer       │  │  VisionSettings     │ │ MemorySettings  │
│                 │  │                   │  │                     │ │                 │
│ Depends on:     │  │ Depends on:       │  │ Depends on:         │ │ Depends on:     │
│ • PipelineResult│  │ • AudioRecorder   │  │ • ILLMProvider      │ │ • PipelineResult│
│ • ILLMProvider  │  │ • ILLMProvider    │  │   (multimodal ext)  │ │ • HistoryMgr    │
│ • AppSettings   │  │ • STT providers   │  │ • STT (voice query) │ │ • AppSettings   │
│ • HistoryMgr    │  │ • AppSettings     │  │ • TextInjector      │ │ • SecureStorage │
│ • NotificationS │  │ • HistoryMgr      │  │ • AppSettings       │ │   (encryption)  │
│                 │  │ • NotificationSvc │  │ • HistoryMgr        │ │                 │
│                 │  │ • ScreenCapture ──┤──┤── (shared)          │ │                 │
└─────────────────┘  └───────────────────┘  └─────────────────────┘ └─────────────────┘
         │                    │                        │                      │
         └────────┬───────────┴────────────────────────┴──────────────────────┘
                  │
       ┌──────────▼──────────┐
       │  CROSS-MODULE FLOWS │
       │                     │
       │  Scribe → Connector │
       │  (PipelineResult)   │
       │                     │
       │  Scribe ← Vision   │
       │  (session captures) │
       │                     │
       │  Vision → Connector │
       │  (screenshot preset)│
       │                     │
       │  * → Memory (store) │
       │  (all PipelineResults│
       │   auto-stored)      │
       │                     │
       │  Memory → LLM (pull)│
       │  (context injection │
       │   before processing)│
       └─────────────────────┘
```

**Critical rule**: Modules NEVER depend on each other directly. All cross-module flows go through shared Core contracts:
- **Scribe → Connectors**: Scribe produces `PipelineResult`, `ConnectorManager` dispatches it. Neither imports the other's namespace.
- **Scribe ← Vision**: Scribe calls `ScreenCapture` (shared Core infrastructure), stores captures in `Session.CapturedImages`. Vision module is not involved — Scribe uses the same `ScreenCapture` class directly.
- **Vision → Connectors**: Vision produces `PipelineResult` (mode = "vision"), connectors dispatch it if enabled. Same contract as dictation.
- **All → Memory (store)**: Every module produces `PipelineResult`. Memory stores it as an embedding. No module imports Memory's namespace — the store hook is in `OnPipelineCompleted()`.
- **Memory → LLM (pull)**: Before LLM processing, the pipeline queries `IMemoryLayer.SearchAsync()` for relevant context. Memory enriches prompts — modules don't know or care.

### Module UI Surface Pattern

Every module follows the same UI surface pattern:

```
┌────────────────────┬────────────────────┬────────────────────┬──────────────────────┐
│                    │ Settings Window     │ Tray Icon Menu     │ Control Panel Widget │
│ Module             │ (launched from      │ (quick access)     │ (inline, optional)   │
│                    │  Settings menu)     │                    │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Connectors         │ ConnectorSettings  │ "Connectors..."    │ Connector Presets    │
│                    │ Window: CRUD       │ opens settings     │ row: toggle-on/off   │
│                    │ destinations +     │ window             │ pills, inbox badge   │
│                    │ presets + inbox     │                    │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Meetings (Scribe)  │ MeetingSettings    │ "Start Session"    │ (optional) Active    │
│                    │ page in Settings   │ → opens Scribe     │ Session indicator    │
│                    │ window             │ Window             │ with timer + stop    │
│                    │                    │ "Session History"  │                      │
│                    │                    │ → opens list       │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Vision (See)       │ Vision page in     │ — (hotkey-only:    │ —                    │
│                    │ Settings window    │  Ctrl+Alt+S)       │                      │
│                    │ (model selection,  │                    │                      │
│                    │  default query)    │                    │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Memory             │ Memory page in     │ —                  │ —                    │
│                    │ Settings window    │ (background,       │                      │
│                    │ (enable/disable,   │  automatic)        │                      │
│                    │  retention, stats, │                    │                      │
│                    │  clear all)        │                    │                      │
├────────────────────┼────────────────────┼────────────────────┼──────────────────────┤
│ Chat (existing)    │ Chat page in       │ "Quick Chat"       │ —                    │
│                    │ Settings window    │ → opens overlay    │                      │
└────────────────────┴────────────────────┴────────────────────┴──────────────────────┘
```

**The pattern**: Each module owns its own window (if applicable), launched from the system tray icon menu or hotkey, with settings accessible from the main Settings window. Control Panel widgets are optional — only added when the module has "presets" or "active state" that benefits from quick toggling.

- **Connectors**: Has a Control Panel widget (Connector Presets row) because presets are toggled on/off frequently during work.
- **Meetings**: ScribeWindow is its own standalone window (like QuickChat). Tray menu has "Start Session" (opens ScribeWindow + begins recording) and "Session History" (opens session list). Settings are a page in the existing Settings window. A minimal Control Panel indicator can show when a session is active (timer + stop), but no full preset row.
- **Vision**: No window, no tray menu — purely hotkey-driven (`Ctrl+Alt+S`). The snipping overlay is a transient fullscreen capture window, not a persistent module window. Settings are a page in the existing Settings window (model selection, default query, image options).
- **Memory**: No window, no tray menu — fully background/automatic. Stores embeddings on every pipeline completion, injects context before LLM processing. Settings are a page in the existing Settings window (enable/disable, retention days, storage stats, "Clear All" button, embedding model selection). Invisible to the user except through smarter AI responses.
- **Chat**: Already follows this pattern — QuickChat is a standalone window from tray, chat settings are in main Settings.

---

## 2. Sprint Overview

### Phase Map

| Phase | Module | Scope | Est. Sessions | Depends On |
|-------|--------|-------|---------------|------------|
| **A** | Connectors | Core framework: `IConnector`, `ConnectorPayload`, `ConnectorManager`, settings | 1 | — |
| **B** | Connectors | Obsidian vault connector | 1 | A |
| **C** | Connectors | Folder, Webhook, Discord, Streamer.bot connectors | 1-2 | A |
| **D** | Meetings | Core session engine: `Session` model, `SessionManager`, audio recording | 1-2 | — |
| **E** | Meetings | Scribe window: notepad + AI synthesis + template system | 1-2 | D |
| **F** | Connectors | Connector Settings window + Connector Presets UI | 1 | A, B, C |
| **G** | Meetings | Post-meeting: "Ask this meeting" chat, audio playback, template selector | 1 | D, E |
| **H** | Connectors | Notifications, telemetry, inbox, polish | 1 | F |
| **I** | Meetings | Polish: search, speaker naming, hotkeys, notifications | 1 | G |
| **J** | All | Cross-module: Scribe → ConnectorManager dispatch, combined E2E testing | 1 | H, I, L |
| **K** | Connectors | Google OAuth: Calendar + Gmail (Release 2) | 2-3 | J |
| **L** | Vision | Core: `ScreenCapture`, `ILLMProvider.ProcessWithImageAsync()`, multimodal providers | 1-2 | — |
| **M** | Vision | Snipping overlay + `VisionPipeline` + hotkey + settings | 1 | L |
| **N** | Vision+Meetings | Meeting captures: Scribe capture button, session-bound screenshots, synthesis with images | 1 | L, G |
| **O** | Memory | Core: `IMemoryLayer`, SQLite+VSS vector store, embedding model, privacy gating | 1-2 | — |
| **P** | Memory | Pipeline integration: auto-store on completion, context injection before LLM | 1 | O |
| **Q** | Memory | Settings UI, stats, retention, search, clear all | 1 | P |

**Total: 18-23 sessions** across ~10-12 weeks.

### Parallelization

All four module tracks are **fully independent** until Phase J/N (integration). They can be developed in parallel or interleaved.

```
Timeline:
  A ──→ B ──→ C ──→ F ──→ H ──────────────┐
                                            ├──→ J ──→ K
  D ──→ E ──→ G ──→ I ────────────────────┤
                     │                      │
  L ──→ M ──────────┼──────────────────────┘
       └──→ N ──────┘
            (L + G required)

  O ──→ P ──→ Q    (Memory — fully independent, can run anytime)
```

---

## 3. Module 1: Connectors (SPEC_013)

> Full specification: `SPEC_013_CONNECTORS_IMPLEMENTATION.md`
> Use cases: `SPEC_013_USE_CASES.md` (218 use cases)

### Phase A: Core Framework [SPEC_015-A]

> Foundation: `IConnector` interface, payload/result records, `ConnectorManager`, settings model.

| Task | Description | Files |
|------|-------------|-------|
| A.1 | Create `IConnector` interface with `SendAsync()` and default `GetContextAsync()` | `Core/Connectors/IConnector.cs` |
| A.2 | Create `ConnectorPayload` record with `FromPipelineResult()` factory | `Core/Connectors/ConnectorPayload.cs` |
| A.3 | Create `ConnectorResult` record with `Success()`/`Failure()` factories | `Core/Connectors/ConnectorResult.cs` |
| A.4 | Create `ConnectorType` enum: File, Webhook, WebSocket, Cloud | `Core/Connectors/ConnectorType.cs` |
| A.5 | Create `ConnectorInputType` flags enum: Voice, Selection, Screenshot, Both, All | `Core/Connectors/ConnectorInputType.cs` |
| A.6 | Create `ConnectorNotifyMode` enum: Silent, Toast, Tts | `Core/Connectors/ConnectorNotifyMode.cs` |
| A.7 | Create `ConnectorSettings`, `ConnectorConfig`, `ConnectorPreset` sealed records | `Core/Config/ConnectorSettings.cs` |
| A.8 | Add `ConnectorSettings Connectors` to `AppSettings` (default: `Enabled = false`), add to `SanitizeNulls()` | `AppSettings.cs`, `SettingsManager.cs` |
| A.9 | Create `ConnectorManager` — resolve connector type → `IConnector` instance, preset-based dispatch loop (`Task.WhenAll`), mode filtering, privacy gating, logging | `Core/Connectors/ConnectorManager.cs` |
| A.10 | Wire `ConnectorManager` as singleton in DI, inject into `ControlPanelViewModel`, add `_ = _connectorManager.DispatchPresetsAsync(result, _activeConnectorPresetIds)` in `OnPipelineCompleted` | `App.xaml.cs`, `ControlPanelViewModel.cs` |
| A.11 | Unit tests: dispatch with 0 connectors (no-op), mode filtering, privacy gating (Ghost blocks all), preset execution, error isolation | `Tests/Connectors/ConnectorManagerTests.cs` |

**Verification**: `dotnet build DiktaMe.sln -c Release` — 0 warnings. `dotnet test DiktaMe.sln` — all existing 950+ tests pass. `DispatchPresetsAsync` called on every pipeline completion (no-op with no presets).

**Commit**: `feat: add IConnector framework and ConnectorManager [SPEC_015-A]`

---

### Phase B: Obsidian Connector [SPEC_015-B]

> Highest-value, lowest-effort. Direct filesystem write to Obsidian vault.

| Task | Description | Files |
|------|-------------|-------|
| B.1 | Implement `ObsidianConnector : IConnector` — reads `VaultPath`, `SubFolder`, `NoteStrategy` from config | `Core/Connectors/ObsidianConnector.cs` |
| B.2 | Daily note strategy (default): append to `{VaultPath}/{SubFolder}/{DailyNoteFormat}.md` with `---` separator + timestamp per entry | Same |
| B.3 | Standalone strategy: create new `.md` per dictation with full YAML frontmatter | Same |
| B.4 | YAML frontmatter: `date`, `time`, `tags`, `mode`, `wordCount`, `sttProvider`, `llmProvider` | Same |
| B.5 | File name template tokens: `{date}`, `{time}`, `{mode}`, `{title}` (first 5 words, slugified) | Same |
| B.6 | Path validation: reject UNC, require absolute, create dirs if needed (adapt `NoteWriter.ValidateFilePath`) | Same |
| B.7 | Unit tests: daily create+append, standalone create, frontmatter format, path validation, template expansion | `Tests/Connectors/ObsidianConnectorTests.cs` |

**Commit**: `feat: add Obsidian vault connector [SPEC_015-B]`

---

### Phase C: Folder, Webhook, Discord, Streamer.bot [SPEC_015-C]

> Four connectors. May split across 2 sessions: C.1-C.5 (file + HTTP), then C.6-C.10 (WebSocket).

| Task | Description | Files |
|------|-------------|-------|
| C.1 | `FolderConnector` — write `.md` to `OutputPath` with optional `FileNameTemplate` | `Core/Connectors/FolderConnector.cs` |
| C.2 | `WebhookConnector` — HTTP POST with Section 5.9 JSON schema | `Core/Connectors/WebhookConnector.cs` |
| C.3 | Webhook: HMAC-SHA256 signing when `SigningSecret` set → `X-DiktaMe-Signature: sha256={hex}` | Same |
| C.4 | Webhook: 15s timeout, retry once on 5xx, log all failures with status | Same |
| C.5 | Webhook: privacy gating — `"[redacted]"` for text when privacy is `Stats` | Same |
| C.6 | `DiscordWebhookConnector` — embeds with `content`, `username`, `avatar_url` | `Core/Connectors/DiscordWebhookConnector.cs` |
| C.7 | `StreamerBotConnector` — `ClientWebSocket` to `ws://{Host}:{Port}{Endpoint}` | `Core/Connectors/StreamerBotConnector.cs` |
| C.8 | Streamer.bot: `DoAction` request with `action.name` + `args` (text, mode, rawTranscript) | Same |
| C.9 | Streamer.bot: lazy connect, auto-reconnect, graceful `DisposeAsync` | Same |
| C.10 | Unit tests: mock `HttpMessageHandler` for HTTP connectors, `IWebSocketClient` abstraction for SB | Test files |

**Commit**: `feat: add Folder, Webhook, Discord, and Streamer.bot connectors [SPEC_015-C]`

---

### Phase F: Connector Settings Window + Presets UI [SPEC_015-F]

> Separate settings window for the Connector Module. CRUD for destinations and presets.

| Task | Description | Files |
|------|-------------|-------|
| F.1 | Create `ConnectorSettingsViewModel` — ObservableCollection for destinations + presets, Add/Edit/Remove/Toggle commands | `App/ViewModels/ConnectorSettingsViewModel.cs` |
| F.2 | Create `ConnectorSettingsWindow.xaml` — master toggle, destination list, preset list, inbox viewer | `App/Views/ConnectorSettingsWindow.xaml` + `.cs` |
| F.3 | Destination type picker: Obsidian / Folder / Webhook / Discord / Streamer.bot | Same |
| F.4 | Per-type settings: folder picker, URL input, host:port — dynamically shown | Same |
| F.5 | Preset editor: title, icon, color, input type, STT/LLM pickers, system prompt, output connector multi-select, notify mode, hotkey, test button | Same |
| F.6 | Control Panel Connector Presets row: toggle-on/off pills, multiple active, visual indicators, inbox badge | `App/Views/ControlPanel.xaml`, `ControlPanelViewModel.cs` |
| F.7 | Register window + ViewModels in DI | `App.xaml.cs` |
| F.8 | "Test" button — fires synthetic payload through full preset pipeline, shows success/failure toast | ViewModel |

**Commit**: `feat: add Connector Settings window and Presets UI [SPEC_015-F]`

---

### Phase H: Connector Notifications, Inbox, Polish [SPEC_015-H]

> Toast notifications, inbox persistence, telemetry, edge cases.

| Task | Description | Files |
|------|-------------|-------|
| H.1 | `ConnectorInboxManager` — SQLite CRUD for `connector_inbox` table in `history.db` | `Core/Data/ConnectorInboxManager.cs` |
| H.2 | `ConnectorPresetRunner` — executes single preset: optional LLM re-process → fan-out → notify → inbox | `Core/Connectors/ConnectorPresetRunner.cs` |
| H.3 | Success/failure toasts: "Saved to Obsidian (42 words)" or "Webhook failed: 401" | `ConnectorManager.cs` |
| H.4 | Inbox UI: `ConnectorInboxPanel.xaml` + `ConnectorInboxViewModel.cs` — recent activity, mark-as-read, re-send failed | App files |
| H.5 | Settings validation: valid URL, valid directory, valid host:port | ViewModel |
| H.6 | Edge cases: vault deleted → error toast, webhook 401 → suggest checking URL, disk full → graceful | Connectors |
| H.7 | Unit tests: inbox CRUD, retention cleanup, preset runner LLM re-process, fan-out, error isolation | Test files |

**Commit**: `feat: add connector notifications, inbox, and polish [SPEC_015-H]`

---

## 4. Module 2: Meetings / Scribe (SPEC_001)

> Full specification: `SPEC_001_MEETINGS.md`

### Phase D: Core Session Engine [SPEC_015-D]

> `Session` data model, `SessionManager`, long-form audio recording, batch transcription, LLM synthesis.

| Task | Description | Files |
|------|-------------|-------|
| D.1 | Create `Session` data model — Id, Title, StartedAt, EndedAt, State, AudioPath, TranscriptPath, UserNotesMarkdown, ArtifactMarkdown, TemplateName, Participants, WordCount, ModelUsed | `Core/Meetings/Session.cs` |
| D.2 | Create `SessionState` enum: Recording, Processing, Complete, Failed | `Core/Meetings/SessionState.cs` |
| D.3 | Create `SessionManager` — CRUD, SQLite storage in `history.db` (new `meeting_sessions` table), `ActiveSession` property, state transitions | `Core/Meetings/SessionManager.cs` |
| D.4 | Create `MeetingRecorder` — `WasapiLoopbackCapture` (system audio) + `WasapiCapture` (mic), mixed into single WAV, disk-streaming (not RAM) for 1hr+ meetings | `Core/Meetings/MeetingRecorder.cs` |
| D.5 | Disk streaming: write directly to temp `.wav` file, ring buffer for level meter data only. Auto-create `%APPDATA%/DiktaMe/sessions/{session_id}/` directory | Same |
| D.6 | Post-recording compression: convert WAV → Opus via `OpusEncoder` (or shell out to ffmpeg if easier). Delete WAV after successful compression. | `Core/Meetings/AudioCompressor.cs` |
| D.7 | Create `MeetingTranscriber` — batch transcription: send full audio to Deepgram with `diarize=true&utterances=true&smart_format=true`, parse JSON response into `TranscriptSegment[]` | `Core/Meetings/MeetingTranscriber.cs` |
| D.8 | Create `TranscriptSegment` record: Speaker, Text, StartMs, EndMs, Confidence | `Core/Meetings/TranscriptSegment.cs` |
| D.9 | Create `MeetingSynthesizer` — `(transcript + typed_notes + template_prompt) → artifact` via `LLMRouter`. Notes as intent signals (see SPEC_001 Section 4.3) | `Core/Meetings/MeetingSynthesizer.cs` |
| D.10 | Hotkey suppression during active session: voice hotkeys silently disabled (Dictate, Ask, Translate, Note, Refine Voice). Refine Auto + Chat text-only remain available | `ControlPanelViewModel.cs` |
| D.11 | Unit tests: SessionManager CRUD + state transitions, TranscriptSegment parsing, Synthesizer prompt construction, hotkey suppression logic | `Tests/Meetings/` |

**Key decision**: Audio capture uses NAudio `WasapiLoopbackCapture` for system audio and `WasapiCapture` for mic. Both streams are mixed into a single stereo WAV (system audio = left, mic = right) for diarization accuracy.

**Commit**: `feat: add Session model, SessionManager, and MeetingRecorder [SPEC_015-D]`

---

### Phase E: Scribe Window [SPEC_015-E]

> WinUI 3 window: notepad (left pane) + AI output (right pane) + recording controls.

| Task | Description | Files |
|------|-------------|-------|
| E.1 | Create `ScribeWindow.xaml` — split-pane layout: left (user notes editor), right (AI output / chat), top bar (title + timer + stop), status bar (recording indicator + template selector + audio level) | `App/Views/ScribeWindow.xaml` + `.cs` |
| E.2 | Create `ScribeViewModel` — `ActiveSession`, `UserNotes` (two-way binding), `ArtifactMarkdown` (read-only), `IsRecording`, `ElapsedTime`, timer `DispatcherTimer`, start/stop commands | `App/ViewModels/ScribeViewModel.cs` |
| E.3 | Left pane: basic markdown editor — plain `TextBox` with monospace font, auto-save every 5 seconds to `SessionManager.UpdateNotesAsync()`, persist on crash (write-ahead) | ScribeWindow |
| E.4 | Right pane: during recording → placeholder instructions; after synthesis → rendered Markdown artifact (use `WebView2` or `RichTextBlock` with manual formatting) | ScribeWindow |
| E.5 | Recording controls: Start Session button (opens window + begins recording), Stop button (in title bar), timer showing `HH:MM:SS` | ScribeWindow |
| E.6 | Audio level meter: visualize mic + system audio levels from `MeetingRecorder.LevelChanged` event | ScribeWindow status bar |
| E.7 | Template selector: ComboBox with 6 built-in templates (Meeting Minutes, Interview, Lecture, Brainstorm, Sales Call, Custom) | ScribeWindow status bar |
| E.8 | End-session flow: Stop → show "Processing..." → run transcription → run synthesis → show artifact in right pane → toast "Meeting processed" | ScribeViewModel |
| E.9 | Session list view: history of past meetings, launched from tray menu "Session History". Click session → re-opens ScribeWindow with saved notes + artifact | `App/Views/SessionListWindow.xaml` or `SessionListPage.xaml` |
| E.10 | Register ScribeWindow, ScribeViewModel in DI. Add tray menu items: "Start Session" (opens ScribeWindow + begins recording) + "Session History" (opens session list). Optional: minimal Control Panel indicator showing active session timer + stop button when recording | `App.xaml.cs`, tray menu, `ControlPanel.xaml` |
| E.11 | Create 6 template prompts in `PromptRepository` (reuse existing pattern): meeting_minutes, interview, lecture, brainstorm, sales_call, custom | `Core/Pipeline/PromptRepository.cs` or `Core/Meetings/MeetingTemplates.cs` |

**Commit**: `feat: add Scribe window with notepad and AI synthesis [SPEC_015-E]`

---

### Phase G: Post-Meeting Experience [SPEC_015-G]

> "Ask this meeting" chat, copy/export, audio playback linked to transcript.

| Task | Description | Files |
|------|-------------|-------|
| G.1 | "Ask this meeting" chat — text input at bottom of right pane, pass full transcript + question to LLM with system prompt "Answer based only on this meeting transcript" | ScribeWindow, ScribeViewModel |
| G.2 | Chat history within session — show previous Q&A pairs in scrollable list above input | ScribeWindow |
| G.3 | Copy artifact: Markdown → clipboard button | ScribeWindow |
| G.4 | Export artifact: Save as `.md` file via file picker | ScribeWindow |
| G.5 | Audio playback: play Opus recording, show waveform/progress, click-to-seek linked to transcript timestamps | ScribeWindow |
| G.6 | Auto-title generation: after synthesis, LLM generates a short title from the first 500 words of transcript | `MeetingSynthesizer.cs` |
| G.7 | Retention policy: configurable (default 90 days), auto-delete old sessions + audio files in `SessionManager.CleanupAsync()` | `SessionManager.cs` |
| G.8 | Unit tests: chat prompt construction, export format, retention cleanup | Test files |

**Commit**: `feat: add post-meeting chat, export, and audio playback [SPEC_015-G]`

---

### Phase I: Meetings Polish [SPEC_015-I]

> Speaker naming, search, hotkeys, notifications, settings.

| Task | Description | Files |
|------|-------------|-------|
| I.1 | Speaker naming UI: post-synthesis, show "Speaker 0 → ?" assignment panel. LLM inference: parse "Hey Alice" patterns to suggest names | ScribeWindow |
| I.2 | Session search: full-text search across all past meetings (artifacts + transcripts) | SessionListPage |
| I.3 | Global hotkey: start/stop session (configurable, like existing dictation hotkeys) | `HotkeySettings`, `ControlPanelViewModel` |
| I.4 | `AudioDucker` integration: auto-duck other apps when session recording starts | `MeetingRecorder.cs` |
| I.5 | Meeting Settings: add `MeetingSettings` sub-object to `AppSettings` — default template, audio format, retention days, default STT/LLM provider, auto-duck toggle | `AppSettings.cs`, `SettingsManager.cs` |
| I.6 | Meeting Settings page **in the existing Settings window** (not a separate window — Meetings settings are simple enough for a page). Accessible from Settings nav + tray "Connectors..." equivalent is not needed since ScribeWindow IS the module's primary window | `App/Views/Settings/MeetingSettingsPage.xaml` |
| I.7 | Toast: "Meeting processed — click to view" with action button | `NotificationService` |
| I.8 | Unit tests: speaker name inference, search, hotkey state management | Test files |

**Commit**: `feat: add speaker naming, search, hotkeys, and meeting polish [SPEC_015-I]`

---

## 5. Module 3: Vision / See (SPEC_002)

> Full specification: `SPEC_002_VISION.md`
> **Core concept**: "You talk, dIKta.me looks." Hotkey → screenshot → optional voice query → multimodal LLM → response injected at cursor.
> **Two layers**: Core Vision (shared infrastructure: `ScreenCapture`, multimodal LLM) and Standalone Vision Pipeline (`Ctrl+Alt+S` hotkey flow). Meeting captures use Core Vision directly.

### Phase L: Core Vision Infrastructure [SPEC_015-L]

> `ScreenCapture`, `ILLMProvider.ProcessWithImageAsync()`, multimodal support in all 4 LLM providers.
> This is **shared infrastructure** — used by both standalone Vision and Meeting captures.

| Task | Description | Files |
|------|-------------|-------|
| L.1 | Create `ScreenCapture` class — Win32 `PrintWindow()`/`BitBlt` for active window capture, `BitBlt` on virtual screen for region capture. Returns `byte[]` PNG. | `Core/Vision/ScreenCapture.cs` |
| L.2 | Image preprocessing: resize if longest side > 2048px, compress to JPEG (quality 85) if PNG > 1MB, base64 encode | `Core/Vision/ImageProcessor.cs` |
| L.3 | Extend `ILLMProvider` with `ProcessWithImageAsync(byte[] imageData, string mimeType, string text, string systemPrompt, string mode, CancellationToken)` — default throws `NotSupportedException` | `Core/LLM/ILLMProvider.cs` |
| L.4 | Implement multimodal in `GeminiProvider` — `inlineData` with `mimeType` + `data` (base64) in `parts[]` | `Core/LLM/GeminiProvider.cs` |
| L.5 | Implement multimodal in `AnthropicProvider` — `image` content block with `source.type = "base64"` | `Core/LLM/AnthropicProvider.cs` |
| L.6 | Implement multimodal in `OpenAICompatibleProvider` — `image_url` content with `data:image/png;base64,...` (covers GPT-4o + Ollama vision models: LLaVA, Moondream) | `Core/LLM/OpenAICompatibleProvider.cs` |
| L.7 | Create `VisionOptions` record: CaptureMode (ActiveWindow/Region), DefaultQuery, MaxImageDimension, AutoRecordQuery, QueryTimeoutSeconds | `Core/Vision/VisionOptions.cs` |
| L.8 | Add `VisionSettings` sub-object to `AppSettings` (Enabled, DefaultQuery, MaxImageDimensionPx, AutoRecordQuery, QueryTimeoutSeconds), add to `SanitizeNulls()` | `AppSettings.cs`, `SettingsManager.cs` |
| L.9 | Unit tests: ScreenCapture mocked (test PNG output format), ImageProcessor (resize/compress thresholds), multimodal provider request format validation (JSON structure for each provider) | `Tests/Vision/`, `Tests/LLM/` (extend existing provider tests) |

**Key decision**: `ProcessWithImageAsync()` is a default interface method that throws `NotSupportedException`. Providers opt in by overriding. This means existing non-vision providers (e.g., a text-only Ollama model) gracefully fail with a clear error.

**VRAM note**: Vision tasks don't require concurrent STT. Ollama auto-swaps models — when a vision model loads, the text model may be evicted from VRAM. Acceptable because vision is a discrete action, not a continuous pipeline.

**Commit**: `feat: add ScreenCapture, multimodal LLM providers, and Vision infrastructure [SPEC_015-L]`

---

### Phase M: Standalone Vision Pipeline [SPEC_015-M]

> Snipping overlay window, `VisionPipeline`, hotkey dispatch, settings UI.

| Task | Description | Files |
|------|-------------|-------|
| M.1 | Create `SnippingOverlayWindow.xaml` — transparent fullscreen always-on-top window covering all monitors. Semi-transparent dark fill, click = capture active window, drag = capture region, Esc = cancel. Crosshair cursor. Bottom hint text. | `App/Views/SnippingOverlayWindow.xaml` + `.cs` |
| M.2 | Region selection: mouse down → start rect, mouse move → update rect (clear cutout in overlay), mouse up → capture region coordinates → `ScreenCapture.CaptureRegion(rect)` | Same |
| M.3 | Create `VisionPipeline` — orchestrates: `ScreenCapture` → optional voice query (reuse `AudioRecorder` + STT for short recording) → `ILLMProvider.ProcessWithImageAsync()` → `TextInjector.InjectText()` → return `PipelineResult` (mode = "vision") | `Core/Pipeline/VisionPipeline.cs` |
| M.4 | Voice query flow: after screenshot selection, auto-record for up to `QueryTimeoutSeconds`. User speaks query ("What does this error mean?") → STT → text. Silence/skip → use `DefaultQuery` from settings. | Same |
| M.5 | Register hotkey `Vision = 8` → `Ctrl+Alt+S` ("See") in `HotkeySettings` | `Core/Config/AppSettings.cs` (HotkeySettings) |
| M.6 | Dispatch in `LoadingViewModel`: `case HotkeyId.Vision: _ = RunVisionPipelineAsync()` — show overlay → await capture → run pipeline → show toast | `App/ViewModels/LoadingViewModel.cs` |
| M.7 | Output modes: Inject (default, paste at cursor), Clipboard (copy, toast), Toast-only (show response). Configurable in `VisionSettings`. | `VisionPipeline.cs` |
| M.8 | Vision Settings page in existing Settings window: enable/disable, default query, auto-record toggle, cloud model selector (filtered to vision-capable), local model selector (Ollama vision), output mode | `App/Views/Settings/VisionSettingsPage.xaml` + VM |
| M.9 | History integration: store vision results in SQLite if privacy allows (mode = "vision", text = response, attach screenshot path) | `HistoryManager.cs` |
| M.10 | Error handling: no vision model selected → toast "Configure a vision model", API error → toast with message, Ollama non-vision model → toast "Try llava or moondream", capture fails → toast "Check display permissions" | Pipeline + providers |
| M.11 | Unit tests: VisionPipeline orchestration, output mode routing, error handling paths, hotkey registration | `Tests/Vision/VisionPipelineTests.cs` |

**Commit**: `feat: add snipping overlay, VisionPipeline, and Ctrl+Alt+S hotkey [SPEC_015-M]`

---

### Phase N: Meeting Captures (Vision + Meetings Integration) [SPEC_015-N]

> Scribe can capture screenshots during meetings. Captures are attached to the session and fed to synthesis.

| Task | Description | Files |
|------|-------------|-------|
| N.1 | Add `CapturedImages: List<SessionCapture>` to `Session` model. `SessionCapture` record: `Id`, `Timestamp`, `ImagePath`, `Query?`, `AiDescription?` | `Core/Meetings/Session.cs` |
| N.2 | Add "Capture" button to ScribeWindow status bar (camera icon). Click → `ScreenCapture.CaptureActiveWindow()` or region select → save PNG to `sessions/{id}/captures/` → add to session | `App/Views/ScribeWindow.xaml`, `ScribeViewModel.cs` |
| N.3 | Optional: quick voice query after capture ("What's on this whiteboard?") → `ProcessWithImageAsync()` → store `AiDescription` in `SessionCapture` | `ScribeViewModel.cs` |
| N.4 | Synthesis enrichment: when `Session.CapturedImages` is non-empty, include image descriptions in synthesis prompt: "During the meeting, the following was captured: [timestamp] [description]..." | `MeetingSynthesizer.cs` |
| N.5 | Display captures in ScribeWindow: thumbnail strip below notes pane, click to expand. Post-synthesis: image descriptions appear inline in artifact where relevant | `ScribeWindow.xaml` |
| N.6 | Unit tests: session capture storage, synthesis prompt construction with images, capture cleanup on retention | `Tests/Meetings/` |

**Note**: This phase does NOT depend on the Vision Module (Phase M). It uses `ScreenCapture` directly (shared Core infrastructure from Phase L). The standalone `Ctrl+Alt+S` pipeline and the Scribe capture button are independent features that share the same capture class.

**Commit**: `feat: add meeting screenshot captures with synthesis enrichment [SPEC_015-N]`

---

## 6. Module 4: Memory (SPEC_014)

> Full specification: [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md)
> **Core concept**: Semantic vector memory that stores embeddings of every pipeline result and injects relevant context before LLM processing. Invisible to the user — the AI just "remembers."
> **Module pattern**: No window, no tray menu, fully automatic. Settings page for enable/disable + stats + clear. Same isolation as all other modules — depends only on Core contracts.

### Phase O: Core Memory Infrastructure [SPEC_015-O]

> `IMemoryLayer` interface, SQLite+VSS vector store, local embedding model, privacy gating.
> Full design details: SPEC_014 §2 (Architecture), §3 (Implementation), §5 (Technical Details).

| Task | Description | Files |
|------|-------------|-------|
| O.1 | Create `IMemoryLayer` interface — `StoreAsync()`, `SearchAsync()`, `GetByMetadataAsync()`, `DeleteAsync()`, `ClearAllAsync()`, `GetStatsAsync()` | `Core/Memory/IMemoryLayer.cs` |
| O.2 | Create `MemoryEntryId`, `MemoryResult`, `MemoryMetadata`, `MemoryStats` records — as defined in SPEC_014 §2.2 | `Core/Memory/MemoryModels.cs` |
| O.3 | Evaluate and integrate SQLite VSS extension (`sqlite-vss`) — native extension loading alongside existing SQLite. See SPEC_014 §3.1 Option A. | `Core/Memory/SqliteMemoryStore.cs` |
| O.4 | Implement `SqliteMemoryStore : IMemoryLayer` — vector storage schema (SPEC_014 §5.2), CRUD operations, similarity search via VSS | Same |
| O.5 | Local embedding model integration — ONNX `all-MiniLM-L6-v2` (384 dimensions) for generating embeddings. See SPEC_014 §5.1. | `Core/Memory/EmbeddingGenerator.cs` |
| O.6 | Privacy gating — Ghost: disabled entirely, Stats: metadata only (no content), Balanced: encrypted vectors, Full: full storage. See SPEC_014 §3.3. | `SqliteMemoryStore.cs` |
| O.7 | Create `MemorySettings` sealed record, add to `AppSettings`, add to `SanitizeNulls()` | `Core/Config/MemorySettings.cs`, `AppSettings.cs`, `SettingsManager.cs` |
| O.8 | Wire `IMemoryLayer` / `SqliteMemoryStore` as singleton in DI | `App.xaml.cs` |
| O.9 | Unit tests: store/search/delete operations, privacy level compliance (Ghost blocks all), similarity scoring, metadata filtering | `Tests/Memory/SqliteMemoryStoreTests.cs` |

**Key decisions** (from SPEC_014):
- SQLite+VSS chosen over ML.NET or FAISS for single-file simplicity and alignment with existing `HistoryManager` pattern
- `all-MiniLM-L6-v2` chosen for small size + good quality — runs entirely locally via ONNX Runtime (already a dependency)
- Encryption at rest via DPAPI-backed keys from `SecureStorage` (SPEC_014 §6.1)

**Commit**: `feat: add IMemoryLayer with SQLite+VSS vector store and embedding model [SPEC_015-O]`

---

### Phase P: Pipeline Integration [SPEC_015-P]

> Auto-store on pipeline completion, context injection before LLM processing.
> Full design details: SPEC_014 §3.2 (Integration Points), §4.1–4.2 (Workflows).

| Task | Description | Files |
|------|-------------|-------|
| P.1 | Add `_ = _memoryLayer.StoreAsync(result)` in `OnPipelineCompleted()` — same fire-and-forget pattern as ConnectorManager dispatch. Store text + mode + provider metadata as embedding. | `ControlPanelViewModel.cs` |
| P.2 | Context injection: before LLM processing in `DictationPipeline` / `LLMRouter`, call `_memoryLayer.SearchAsync(userText, limit: 5)` → format top results as context block → prepend to system prompt | `Core/Pipeline/DictationPipeline.cs` or `Core/LLM/LLMRouter.cs` |
| P.3 | Context injection for Chat: `ChatPipeline` queries memory for conversation-relevant context → enhances system prompt with "You previously discussed: ..." | `Core/Pipeline/ChatPipeline.cs` |
| P.4 | Context injection for Ask: `AskPipeline` queries memory → provides relevant past Q&A pairs as examples | `Core/Pipeline/AskPipeline.cs` (or equivalent) |
| P.5 | Meeting synthesis enrichment: when Scribe synthesizes, query memory for "what was discussed about [topic]" from past meetings → adds historical context to synthesis prompt | `MeetingSynthesizer.cs` (Phase I prerequisite) |
| P.6 | Embedding generation throttling — don't block the pipeline. Generate embeddings async after pipeline completion, queue if multiple arrive quickly. | `Core/Memory/EmbeddingGenerator.cs` |
| P.7 | Unit tests: auto-store on pipeline completion (mock IMemoryLayer), context injection formatting, throttling behavior, memory-enhanced prompt structure | `Tests/Memory/MemoryIntegrationTests.cs` |

**The pattern**: Same as Connectors — one line in `OnPipelineCompleted()` stores. One line before LLM call retrieves. No module imports another's namespace. Memory is a Core service that pipelines consume through DI.

**Note**: P.5 (meeting enrichment) only applies after Meetings module (Phases D-I) is implemented. It can be deferred or skipped if Memory ships before Meetings.

**Commit**: `feat: integrate Memory Layer with pipeline store and context injection [SPEC_015-P]`

---

### Phase Q: Memory Settings & Management UI [SPEC_015-Q]

> Settings page, statistics, retention, search UI, clear all.
> Full design details: SPEC_014 §4.3 (User-Facing Features).

| Task | Description | Files |
|------|-------------|-------|
| Q.1 | Create `MemorySettingsViewModel` — enable/disable toggle, retention days, storage stats (entry count, DB size, oldest/newest), clear all command | `App/ViewModels/Settings/MemorySettingsViewModel.cs` |
| Q.2 | Create `MemorySettingsPage.xaml` — master toggle, stats display (entries count, storage size), retention slider, embedding model info, "Clear All Memories" button with confirmation | `App/Views/Settings/MemorySettingsPage.xaml` + `.cs` |
| Q.3 | Optional: simple memory search textbox in settings — type a query, see top-5 semantically similar past interactions. Useful for debugging and trust-building ("the AI remembers this"). | Same |
| Q.4 | Register page in SettingsWindow navigation + DI | `SettingsWindow.xaml`, `App.xaml.cs` |
| Q.5 | Retention enforcement: background task that purges memories older than `RetentionDays` on app startup | `SqliteMemoryStore.cs` |
| Q.6 | Unit tests: settings round-trip, retention purge, stats calculation | `Tests/Memory/MemorySettingsTests.cs` |

**Commit**: `feat: add Memory settings page with stats, retention, and search [SPEC_015-Q]`

---

## 7. Cross-Module Integration

### Phase J: Cross-Module Bridge [SPEC_015-J]

> The payoff: all three modules talk through `PipelineResult`. Meeting → Connectors. Vision → Connectors. Vision → Meetings.

| Task | Description | Files |
|------|-------------|-------|
| J.1 | When Scribe synthesis completes, create a `PipelineResult` from the session artifact — `Mode = "meeting"`, `Text = artifactMarkdown`, `RawTranscript = fullTranscript` | `MeetingSynthesizer.cs` or `ScribeViewModel.cs` |
| J.2 | Call `_ = _connectorManager.DispatchPresetsAsync(meetingResult, _activeConnectorPresetIds)` — same dispatch path as regular dictation | `ScribeViewModel.cs` |
| J.3 | When VisionPipeline completes, its `PipelineResult` (mode = "vision") is already returned to `OnPipelineCompleted()` → connector dispatch works automatically. Verify this path. | `LoadingViewModel.cs`, `ControlPanelViewModel.cs` |
| J.4 | Add `"meeting"` and `"vision"` to the mode filter options in Connector Preset config — presets can opt in/out of each mode | `ConnectorSettings.cs` validation |
| J.5 | Add built-in example Connector Presets: "Meeting → Obsidian", "Meeting → Slack Webhook", "Screenshot → Obsidian" (saves vision result + image path) | Default config or wizard |
| J.6 | E2E integration tests: mock session → synthesis → connector dispatch; mock vision → connector dispatch; verify payloads reach connectors | Integration test file |

**The contract**: All modules produce `PipelineResult`. Connectors consume `PipelineResult`. Memory stores `PipelineResult` as embeddings and injects context before LLM calls. No module imports another's namespace. The bridge is 1-3 lines of code per source.

**Commit**: `feat: bridge Meetings and Vision to Connector Presets [SPEC_015-J]`

---

## 7. New Files Summary

### DiktaMe.Core

```
src/DiktaMe.Core/
├── Connectors/                          ← NEW directory
│   ├── IConnector.cs                    # Interface
│   ├── ConnectorPayload.cs              # Payload record
│   ├── ConnectorResult.cs               # Result record
│   ├── ConnectorType.cs                 # Enum
│   ├── ConnectorInputType.cs            # Flags enum
│   ├── ConnectorNotifyMode.cs           # Enum
│   ├── ConnectorManager.cs              # Orchestrator
│   ├── ConnectorPresetRunner.cs         # Single preset executor
│   ├── ObsidianConnector.cs             # File: .md with YAML frontmatter
│   ├── FolderConnector.cs               # File: generic .md
│   ├── WebhookConnector.cs              # HTTP POST
│   ├── DiscordWebhookConnector.cs       # Discord embeds
│   └── StreamerBotConnector.cs          # WebSocket
├── Meetings/                            ← NEW directory
│   ├── Session.cs                       # Data model (+SessionCapture record for Phase N)
│   ├── SessionState.cs                  # Enum
│   ├── SessionManager.cs                # CRUD + SQLite
│   ├── MeetingRecorder.cs               # WasapiLoopback + Wasapi capture
│   ├── AudioCompressor.cs               # WAV → Opus
│   ├── MeetingTranscriber.cs            # Batch STT
│   ├── TranscriptSegment.cs             # Record
│   ├── MeetingSynthesizer.cs            # LLM synthesis (+image-enriched prompts Phase N)
│   └── MeetingTemplates.cs              # 6 built-in templates
├── Vision/                              ← NEW directory
│   ├── ScreenCapture.cs                 # Win32 capture: active window + region
│   ├── ImageProcessor.cs                # Resize, compress, base64 encode
│   └── VisionOptions.cs                 # Options record
├── Memory/                              ← NEW directory
│   ├── IMemoryLayer.cs                  # Interface: Store, Search, Delete, Clear, Stats
│   ├── MemoryModels.cs                  # MemoryEntryId, MemoryResult, MemoryMetadata, MemoryStats
│   ├── SqliteMemoryStore.cs             # SQLite+VSS vector store implementation
│   └── EmbeddingGenerator.cs            # ONNX all-MiniLM-L6-v2 local embeddings
├── Pipeline/
│   └── VisionPipeline.cs               # Screenshot → STT → multimodal LLM → inject
├── Config/
│   ├── ConnectorSettings.cs             # ConnectorSettings + ConnectorConfig + ConnectorPreset
│   ├── MemorySettings.cs                # MemorySettings sealed record
│   └── (AppSettings.cs modified)        # +Connectors, +Meetings, +Vision, +Memory sub-objects
└── Data/
    └── ConnectorInboxManager.cs         # SQLite CRUD for connector_inbox
```

### DiktaMe.App

```
src/DiktaMe.App/
├── Views/
│   ├── ConnectorSettingsWindow.xaml      # Separate window
│   ├── ConnectorInboxPanel.xaml          # Inbox flyout
│   ├── ScribeWindow.xaml                 # Meeting notepad + AI output + capture button
│   ├── SessionListPage.xaml             # Meeting history list
│   ├── SnippingOverlayWindow.xaml       # Transparent fullscreen capture overlay
│   └── Settings/
│       ├── MeetingSettingsPage.xaml      # Meeting settings
│       ├── VisionSettingsPage.xaml       # Vision settings (model, default query)
│       └── MemorySettingsPage.xaml       # Memory settings (stats, retention, clear)
├── ViewModels/
│   ├── ConnectorSettingsViewModel.cs     # Connector CRUD
│   ├── ConnectorInboxViewModel.cs        # Inbox list
│   ├── ScribeViewModel.cs               # Meeting session (+capture commands Phase N)
│   └── Settings/
│       ├── MeetingSettingsViewModel.cs   # Meeting settings
│       ├── VisionSettingsViewModel.cs    # Vision settings
│       └── MemorySettingsViewModel.cs    # Memory settings
```

### DiktaMe.Core.Tests

```
tests/DiktaMe.Core.Tests/
├── Connectors/                          ← NEW directory
│   ├── ConnectorManagerTests.cs
│   ├── ConnectorPresetRunnerTests.cs
│   ├── ObsidianConnectorTests.cs
│   ├── FolderConnectorTests.cs
│   ├── WebhookConnectorTests.cs
│   ├── DiscordWebhookConnectorTests.cs
│   └── StreamerBotConnectorTests.cs
├── Meetings/                            ← NEW directory
│   ├── SessionManagerTests.cs
│   ├── MeetingRecorderTests.cs
│   ├── MeetingTranscriberTests.cs
│   ├── MeetingSynthesizerTests.cs
│   └── MeetingTemplatesTests.cs
├── Vision/                              ← NEW directory
│   ├── ScreenCaptureTests.cs
│   ├── ImageProcessorTests.cs
│   └── VisionPipelineTests.cs
├── Memory/                              ← NEW directory
│   ├── SqliteMemoryStoreTests.cs
│   ├── EmbeddingGeneratorTests.cs
│   ├── MemoryIntegrationTests.cs
│   └── MemorySettingsTests.cs
└── Data/
    └── ConnectorInboxManagerTests.cs
```

---

## 8. Modified Files

| File | Change | Phase |
|------|--------|-------|
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `ConnectorSettings Connectors` + `MeetingSettings Meetings` + `VisionSettings Vision` + `MemorySettings Memory` properties, add `Vision = 8` to `HotkeyId` enum, add `Vision` hotkey to `HotkeySettings` | A, I, L, M, O |
| `src/DiktaMe.Core/Config/SettingsManager.cs` | Add `Connectors` + `Meetings` + `Vision` + `Memory` to `SanitizeNulls()` | A, I, L, O |
| `src/DiktaMe.Core/LLM/ILLMProvider.cs` | Add `ProcessWithImageAsync()` default interface method | L |
| `src/DiktaMe.Core/LLM/GeminiProvider.cs` | Override `ProcessWithImageAsync()` — `inlineData` multimodal format | L |
| `src/DiktaMe.Core/LLM/AnthropicProvider.cs` | Override `ProcessWithImageAsync()` — `image` content block format | L |
| `src/DiktaMe.Core/LLM/OpenAICompatibleProvider.cs` | Override `ProcessWithImageAsync()` — `image_url` format (covers GPT-4o + Ollama LLaVA/Moondream) | L |
| `src/DiktaMe.App/App.xaml.cs` | Register all new singletons + ViewModels in DI | A, D, F, H, M, O |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | Add `DispatchPresetsAsync()` call + `MemoryLayer.StoreAsync()` call in `OnPipelineCompleted`, add Connector Presets row state, add hotkey suppression for active sessions | A, D, F, P |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Add `case HotkeyId.Vision: _ = RunVisionPipelineAsync()` dispatch | M |
| `src/DiktaMe.App/Views/ControlPanel.xaml` | Add Connector Presets row widget + Inbox badge + "Start Session" button | F, E |
| `src/DiktaMe.Core/Data/HistoryManager.cs` | Add `connector_inbox` + `meeting_sessions` table creation in DB migration, store vision results | H, D, M |
| `src/DiktaMe.Core/Pipeline/PromptRepository.cs` | Add 6 meeting template prompts (or use separate `MeetingTemplates.cs`) | E |
| `src/DiktaMe.App/Views/SettingsWindow.xaml` | Add navigation items for Meeting Settings + Vision Settings + Memory Settings pages | I, M, Q |
| Tray menu (TrayIconManager or equivalent) | Add "Start Session" + "Session History" menu items | E |

---

## 9. Shared Infrastructure Reuse

All four modules leverage existing infrastructure:

| Infrastructure | Connector Module | Meetings Module | Vision Module | Memory Module |
|----------------|-----------------|----------------|---------------|---------------|
| `HistoryManager` (SQLite) | `connector_inbox` table | `meeting_sessions` table | Vision results in `dictation_history` | Links via HistoryId |
| `LLMRouter` / `ILLMProvider` | Preset LLM re-processing | Transcript → artifact synthesis | `ProcessWithImageAsync()` multimodal | Context injection before LLM |
| `SettingsManager` / `AppSettings` | `ConnectorSettings` sub-object | `MeetingSettings` sub-object | `VisionSettings` sub-object | `MemorySettings` sub-object |
| `NotificationService` | Success/failure toasts, TTS | "Meeting processed" toast | Vision result preview toast | — |
| `AudioRecorder` (NAudio) | — | `MeetingRecorder` (extends) | Short voice query recording | — |
| `PipelineResult` | Input contract for dispatch | Output contract from synthesis | Output from VisionPipeline | Input for embedding storage |
| `SecureStorage` | Future: OAuth tokens (K) | Future: calendar OAuth | — | Encryption keys (DPAPI) |
| `PromptRepository` | Preset system prompts | Meeting template prompts | Default vision query | Context injection prompt template |
| `AudioDucker` | — | Auto-duck during recording | — | — |
| `TextInjector` | — | — | Inject vision response at cursor | — |
| `ScreenCapture` (NEW) | — | Session captures (Phase N) | Standalone capture (Phase M) | — |
| `HotkeyManager` | — | Session start/stop hotkey | `Ctrl+Alt+S` (Vision) | — |
| ONNX Runtime (existing) | — | — | — | `all-MiniLM-L6-v2` embeddings |

---

## 10. Settings Architecture

```
AppSettings
├── ... (existing 11 sub-objects, unchanged)
│
├── Connectors: ConnectorSettings (NEW — Phase A)
│   ├── Enabled: bool (default: false — opt-in)
│   ├── InboxRetentionDays: int (default: 30)
│   ├── Destinations: List<ConnectorConfig>
│   └── Presets: List<ConnectorPreset>
│
├── Meetings: MeetingSettings (NEW — Phase I)
│   ├── Enabled: bool (default: true)
│   ├── DefaultTemplate: string (default: "meeting_minutes")
│   ├── AudioFormat: string (default: "opus")
│   ├── RetentionDays: int (default: 90)
│   ├── DefaultSttProvider: string? (null = use global)
│   ├── DefaultLlmProvider: string? (null = use global)
│   ├── DefaultLlmModel: string? (null = use global)
│   ├── AutoDuck: bool (default: true)
│   └── AutoCompress: bool (default: true)
│
├── Vision: VisionSettings (NEW — Phase L)
│   ├── Enabled: bool (default: true)
│   ├── DefaultQuery: string (default: "Describe what you see and extract any visible text.")
│   ├── MaxImageDimensionPx: int (default: 2048)
│   ├── AutoRecordQuery: bool (default: true)
│   ├── QueryTimeoutSeconds: int (default: 10)
│   └── OutputMode: string (default: "inject") — "inject" | "clipboard" | "toast"
│
└── Memory: MemorySettings (NEW — Phase O)
    ├── Enabled: bool (default: false — opt-in, see SPEC_014 §6.3)
    ├── RetentionDays: int (default: 365)
    ├── MaxEntries: int (default: 10000)
    ├── ContextInjectionEnabled: bool (default: true — when Memory is enabled)
    ├── ContextResultLimit: int (default: 5 — top-K results injected)
    ├── MinSimilarity: double (default: 0.7)
    └── EmbeddingModel: string (default: "all-MiniLM-L6-v2")
```

**HotkeySettings addition:**
```
HotkeySettings
├── ... (existing hotkeys)
└── Vision: string (default: "Ctrl+Alt+S")
```

---

## 11. Test Targets

| Phase | New Tests | Cumulative |
|-------|-----------|------------|
| A | ~10 (ConnectorManager) | 960+ |
| B | ~8 (ObsidianConnector) | 968+ |
| C | ~20 (4 connectors) | 988+ |
| D | ~15 (SessionManager, Recorder, Transcriber) | 1003+ |
| E | ~5 (Synthesizer, Templates) | 1008+ |
| F | ~5 (ViewModel validation) | 1013+ |
| G | ~8 (Chat, export, retention) | 1021+ |
| H | ~12 (Inbox, PresetRunner) | 1033+ |
| I | ~8 (Speaker naming, search, hotkeys) | 1041+ |
| J | ~6 (E2E cross-module integration) | 1047+ |
| L | ~12 (ScreenCapture, ImageProcessor, multimodal providers) | 1059+ |
| M | ~8 (VisionPipeline, overlay, error handling) | 1067+ |
| N | ~6 (Meeting captures, synthesis enrichment) | 1073+ |
| O | ~10 (SqliteMemoryStore, EmbeddingGenerator, privacy gating) | 1083+ |
| P | ~8 (Auto-store, context injection, throttling) | 1091+ |
| Q | ~5 (Settings round-trip, retention purge, stats) | 1096+ |
| **Total** | **~146 new tests** | **1096+** |

---

## 12. Commit Strategy

Trunk-based, one commit per phase:

```
feat: add IConnector framework and ConnectorManager [SPEC_015-A]
feat: add Obsidian vault connector [SPEC_015-B]
feat: add Folder, Webhook, Discord, and Streamer.bot connectors [SPEC_015-C]
feat: add Session model, SessionManager, and MeetingRecorder [SPEC_015-D]
feat: add Scribe window with notepad and AI synthesis [SPEC_015-E]
feat: add Connector Settings window and Presets UI [SPEC_015-F]
feat: add post-meeting chat, export, and audio playback [SPEC_015-G]
feat: add connector notifications, inbox, and polish [SPEC_015-H]
feat: add speaker naming, search, hotkeys, and meeting polish [SPEC_015-I]
feat: bridge Meetings and Vision to Connector Presets [SPEC_015-J]
feat: add Google Calendar and Gmail connectors [SPEC_015-K]
feat: add ScreenCapture, multimodal LLM providers, and Vision infrastructure [SPEC_015-L]
feat: add snipping overlay, VisionPipeline, and Ctrl+Alt+S hotkey [SPEC_015-M]
feat: add meeting screenshot captures with synthesis enrichment [SPEC_015-N]
feat: add IMemoryLayer with SQLite+VSS vector store and embedding model [SPEC_015-O]
feat: integrate Memory Layer with pipeline store and context injection [SPEC_015-P]
feat: add Memory settings page with stats, retention, and search [SPEC_015-Q]
```

---

## 13. Progress Tracker

| Phase | Module | Status | Commit | Tests |
|-------|--------|--------|--------|-------|
| A: Core Framework | Connectors | `PENDING` | — | — |
| B: Obsidian Connector | Connectors | `PENDING` | — | — |
| C: Folder/Webhook/Discord/SB | Connectors | `PENDING` | — | — |
| D: Core Session Engine | Meetings | `PENDING` | — | — |
| E: Scribe Window | Meetings | `PENDING` | — | — |
| F: Connector Settings + Presets UI | Connectors | `PENDING` | — | — |
| G: Post-Meeting Experience | Meetings | `PENDING` | — | — |
| H: Notifications + Inbox + Polish | Connectors | `PENDING` | — | — |
| I: Meetings Polish | Meetings | `PENDING` | — | — |
| J: Cross-Module Bridge | All | `PENDING` | — | — |
| K: Google OAuth (Release 2) | Connectors | `PENDING` | — | — |
| L: Core Vision Infrastructure | Vision | `PENDING` | — | — |
| M: Standalone Vision Pipeline | Vision | `PENDING` | — | — |
| N: Meeting Captures | Vision+Meetings | `PENDING` | — | — |
| O: Core Memory Infrastructure | Memory | `PENDING` | — | — |
| P: Pipeline Integration | Memory | `PENDING` | — | — |
| Q: Memory Settings & Management | Memory | `PENDING` | — | — |

---

## 14. Multi-Session Instructions

### Session Workflow

1. **Start of session**: Read this spec. Check `git log --oneline -10` for last `[SPEC_015-*]` commit.
2. **Pick the next uncompleted phase** from the Progress Tracker above.
3. **Check prerequisites**: Follow the dependency graph (Section 2).
4. **Implement all tasks in the phase**: Follow the task table row by row.
5. **Run tests**: `dotnet test DiktaMe.sln` — ALL tests must pass.
6. **Build check**: `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors.
7. **Commit**: Use the commit message from Section 11.
8. **Update Progress Tracker**: Mark phase as `COMPLETE`.

### Key Patterns

- **Settings records**: `sealed record`, `= new()` defaults, add to `SanitizeNulls()`
- **DI registration**: Singleton for managers, Transient for ViewModels
- **HTTP clients**: Cached `HttpClient` with `ConnectionClose = false` (LLMProviderFactory pattern)
- **File I/O**: Async, create dirs, validate paths
- **Logging**: Structured Serilog: `Log.Information("ConnectorManager: ...")`, `Log.Information("SessionManager: ...")`
- **Error handling**: Never let module failure crash core pipeline. Catch all, log, toast, continue.
- **Test mocking**: Moq. Always pass `It.IsAny<CancellationToken>()` explicitly (CS0854).
- **XAML**: No `x:Bind` on `Run.Text`. Use computed ViewModel properties instead of converters in `Window`.
- **Namespaces**: `DiktaMe.Core.Connectors`, `DiktaMe.Core.Meetings`, `DiktaMe.Core.Vision`, `DiktaMe.Core.Memory` — never `DiktaMe.Core.System.*`

### Critical Gotchas

- `SanitizeNulls()` — MUST add `Connectors`, `Meetings`, `Vision`, and `Memory` or JSON null will crash
- Cross-thread `ObservableCollection` — `DispatcherQueue.TryEnqueue()` for all UI-bound updates from background tasks
- Moq optional params — always explicit `It.IsAny<CancellationToken>()`
- `x:Bind` converters in `Window` — use ViewModel computed properties
- NRE in UI thread = silent crash (exit 127) — guard ALL property change paths
- NAudio `WasapiLoopbackCapture` requires WASAPI shared mode; exclusive mode will fail if another app has exclusive access
- Long recordings: stream to disk, never buffer in RAM. 1hr WAV = ~660MB.
- `ProcessWithImageAsync()` default interface method — providers that don't override it throw `NotSupportedException`. Catch and show "Model X doesn't support images" toast.
- `SnippingOverlayWindow` must be `AppWindow` with `Presenter = FullScreen` and transparent background — WinUI 3 transparent windows need `DesktopAcrylicController` or `MicaController` disabled
- Screenshot `byte[]` can be large (4K monitors = ~8MB PNG) — always run `ImageProcessor` before sending to API
- Ollama vision models (LLaVA, Moondream) use the OpenAI-compatible format but with `images` parameter in native API — prefer OpenAI-compat endpoint for consistency
- Multi-monitor snipping: overlay must cover all displays using `DisplayArea.GetWatcherForDisplayId()` or union of all screen bounds
- SQLite VSS extension (`sqlite-vss`) requires native library loading — test on both x64 and arm64. May need separate native packages per architecture.
- Embedding generation is CPU-intensive (~50ms per embedding) — never block the pipeline. Fire-and-forget from `OnPipelineCompleted()`, queue if multiple arrive.
- ONNX Runtime is already a dependency (Kokoro TTS) — reuse for embedding model. Watch for version conflicts with different ONNX model requirements.
- Memory `Enabled = false` by default (opt-in). Users must explicitly enable it — privacy-first.

---

## 15. Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Audio capture fails on some machines (driver issues) | Meeting recording broken | Medium | Graceful error on start, fallback to mic-only |
| Deepgram batch transcription cost for long meetings | User complaints | Low | Show estimated cost before processing, local Whisper fallback |
| Scribe window XAML complexity (split pane, editor, Markdown render) | Long Phase E | High | Use simple TextBox for notes, WebView2 for Markdown render |
| Opus compression dependency | Build complexity | Low | Use managed NuGet (Concentus) or shell to ffmpeg |
| Streamer.bot WebSocket protocol changes | Connector breaks | Low | Version check on connect, graceful degradation |
| Google OAuth verification delays | Phase K blocked | Medium | Ship without Google initially, use testing mode (100 users) |
| Snipping overlay rendering on multi-monitor / mixed DPI | Visual glitches | Medium | Union of all display bounds, per-monitor DPI awareness via WinUI 3 built-in support |
| Vision API costs for large screenshots | User surprise bills | Low | Resize to 2048px max + show token estimate toast before sending |
| Ollama vision model VRAM pressure (LLaVA = ~4GB) | Model swap latency | Medium | Vision is discrete (not continuous) — model swap is acceptable. Warn if <8GB VRAM. |
| SQLite VSS native extension loading fails on some machines | Memory module broken | Medium | Graceful fallback to disabled state with clear error message. Consider pure-managed cosine similarity on small datasets (<1000 entries) as fallback. |
| Embedding model download size (~90MB for MiniLM) | First-run delay | Low | Download on first enable with progress bar. Bundle in installer if size budget allows. |
| ONNX Runtime version conflict with Kokoro TTS | Build failure | Medium | Pin shared ONNX Runtime version. Test both embedding + TTS in same process. |
| Memory DB grows unbounded on heavy use | Disk space | Low | `RetentionDays` + `MaxEntries` caps enforce automatic pruning on startup |

---

## 16. Market Impact

### What This Sprint Delivers

| Capability | Competitive Standing |
|------------|---------------------|
| Voice → Obsidian vault (daily note + standalone) | **Only tool on any platform** that does this natively |
| Voice → Webhook → Zapier/n8n → 1000+ apps | Matches Fireflies/Otter pricing tier at $0 |
| Meeting recording + AI synthesis (local-first) | Matches Granola ($14/mo) at $0, with privacy |
| Meeting → Obsidian + Webhook auto-dispatch | **No competitor does this** (meetings + integrations + local) |
| Streamer.bot voice control | **Unique** — voice-to-automation bridge |
| Discord community updates by voice | Saves 5-10 min per update for community managers |
| "Ask this meeting" with local LLM | Matches Granola/Fellow chat, with privacy |
| Composable Connector Presets | **Novel architecture** — no competitor has per-preset routing |
| System-wide screenshot → AI analysis at cursor | **No competitor** has hotkey → capture → LLM → inject flow |
| Meeting whiteboard capture → synthesis enrichment | Granola/Fellow have zero visual capture capability |
| Local multimodal (Ollama LLaVA) | Screenshot analysis without cloud — unique |
| Semantic memory with local embeddings | **No desktop dictation tool** has persistent AI memory |
| "Remember what I said about X last week" | ChatGPT memory but local-first, private, and tied to voice |
| Context-aware dictation (memory-enriched prompts) | AI that improves with use — unique in voice tools |

### Four Modules, Four Markets, One Sprint

1. **Privacy market** (attorneys, clinicians, R&D): Meeting intelligence + Vision analysis + Memory — all with zero cloud → addresses the #1 objection to Granola/Fellow
2. **Productivity market** (knowledge workers, managers): Voice → structured notes + email + calendar + screenshot analysis + AI that remembers context → time savings
3. **Automation market** (streamers, DevOps, power users): Voice → webhooks → multi-system orchestration + screenshot → connector presets → new capability
4. **AI-first market** (early adopters, power users): Memory-enriched voice assistant that gets smarter over time — locally. No cloud AI has your personal voice history.

### V2 Completion Scope

After this sprint, V2's feature set is **locked**:

| Feature | Status |
|---------|--------|
| Core dictation pipeline (8 modes) | Shipped |
| Cloud + Local STT | Shipped |
| Cloud + Local LLM | Shipped |
| TTS (Kokoro) | Shipped |
| CRUD Dictation Modes | Shipped |
| OAuth & Trial Credits | Shipped |
| Deepgram Streaming | Shipped |
| Chat (QuickChat) | Shipped |
| **Connectors Module** | **This sprint** |
| **Meetings Module (Scribe)** | **This sprint** |
| **Vision Module (See)** | **This sprint** |
| **Memory Layer** | **This sprint** |
| Google OAuth (Calendar + Gmail) | Release 2 (Phase K) |
| Notion / Slack / CRM integrations | Release 2-3 |
| Stream Deck (SPEC_005) | Post-V2 |
| Internationalization (SPEC_004) | Post-V2 |

---

*End of SPEC_015*
