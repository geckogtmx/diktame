# SPEC_005: Elgato Stream Deck Integration

> **Status:** IMPLEMENTED (Phase 1 + Phase 2)
> **Date:** 2026-03-08 (spec) / 2026-03-23 (implementation)
> **Priority:** Low — personal quality-of-life feature, not user-facing roadmap
> **Parent Specs:** `DEVELOPMENT_ROADMAP.md`

---

## 1. Overview & Value Proposition

Mapping dIKta.me's global hotkeys to a Stream Deck via the built-in "System: Hotkey" action already works — but it's a dumb one-way trigger with no feedback. A native plugin adds two things that matter:

1. **Mode-Specific Buttons:** Bypass the globally selected dictation mode. Dedicate physical buttons to specific modes (e.g., "Standard", "Developer", "Email") without cycling through the UI.
2. **Visual Feedback:** Buttons reflect pipeline state (Idle/Recording/Processing) so you know what's happening without looking at the screen.

That's the 80/20. Everything else is gravy.

---

## 2. The Action Catalog

Two actions. Keep it simple.

### 2.1 "Pipeline Trigger" Action (Unified)

A single action type that can trigger **any** pipeline — dictation modes or utility pipelines — configured per-button via the Property Inspector.

**Property Inspector:**
- **Pipeline type** dropdown: *Dictate, Ask, Refine Auto, Refine Voice, Translate, Note*.
- **Mode selector** (visible when Dictate is selected): Dropdown populated from `DictationModeManager.GetAllModes()`, plus "Use App Default".
- Chat is excluded — it opens a window, which doesn't make sense from a Stream Deck button.

**Visual Behavior (2 states only):**
- **Idle:** Static icon (customizable per pipeline type).
- **Active:** Icon changes to indicate recording/processing. Reverts to Idle on completion or error.

No live timers, no character count flashes, no multi-state animation sequences. Two states. If you want to know details, glance at the Control Panel.

### 2.2 "Settings Toggle" Action

A button that toggles a binary setting and reflects its current state.

**Property Inspector:** Dropdown to select the setting:
- Raw Mode (bypass LLM)
- Streaming (WebSocket vs Batch)
- Audio Ducking
- Engine (Cloud vs Local)

**Visual Behavior:** Lit = on, dim = off. Bidirectional — toggling in the WinUI app updates the button, and vice versa.

### What's Cut (and Why)

| Dropped | Reason |
|---------|--------|
| Telemetry Monitor action | Gold-plating. The data is in the Control Panel. A passive LCD readout adds dev cost for zero workflow improvement. |
| Live recording timer | Requires `telemetry_tick` events every second over IPC. Complexity for a glanceable number you can see on screen. |
| Per-completion char count flash | Cute, but adds a state machine to the button for a 2-second visual. Not worth it. |
| AskOutputMode override per button | Over-engineering. Use the global setting. |

---

## 3. Architecture

### 3.1 IPC: Named Pipe Server in `DiktaMe.App`

`DiktaMe.App` hosts a `NamedPipeServerStream` (`PipeDirection.InOut`, newline-delimited JSON). Named pipes: zero firewall config, fast, user-scoped via `PipeAccessRights`.

Existing reference: `SingleInstanceManager.cs` already runs a named pipe for deeplinks. The API pipe follows the same pattern but bidirectional.

**Pipe name:** `DiktaMe.V2.Api`

### 3.2 IPC Contract

Keep it minimal. No message IDs, no request/response correlation, no schema versioning. Add those if they're ever needed.

**Commands (Plugin → App):**
```json
{"action": "trigger", "pipeline": "dictate", "modeId": "guid-or-null"}
{"action": "trigger", "pipeline": "refine_auto"}
{"action": "trigger", "pipeline": "ask"}
{"action": "toggle", "setting": "RawModeOverride"}
{"action": "query", "target": "modes"}
{"action": "query", "target": "settings"}
```

**Events (App → Plugin):**
```json
{"event": "state", "state": "Recording"}
{"event": "state", "state": "Idle"}
{"event": "settings", "RawModeOverride": false, "StreamingEnabled": true, "AudioDucking": true, "ActiveProfile": "Cloud"}
{"event": "modes", "modes": [{"id": "...", "title": "Standard"}, ...]}
{"event": "busy"}
{"event": "error", "message": "No audio device found"}
```

Note: `busy` is sent when a pipeline trigger arrives while another is already running. The plugin shows a brief flash or ignores the press. No queueing.

### 3.3 Where Execution Logic Lives

**No PipelineOrchestrator refactor.** The spec originally called for extracting all pipeline execution from `LoadingViewModel` into a new orchestrator class. That's a major refactor (audio recording lifecycle, dispatcher marshaling, toggle-stop logic, toast notifications, Ask output routing — all tangled together in 600+ lines). It would touch every pipeline flow and risk breaking the working hotkey system, all for the sake of architectural purity.

Instead: **add a thin `LocalApiServer` in `DiktaMe.App.Services` that translates IPC commands into the same calls `LoadingViewModel` already makes.**

```
Stream Deck Plugin
    ↓ Named Pipe (JSON)
LocalApiServer (DiktaMe.App.Services)
    ↓ Dispatches to UI thread
LoadingViewModel (existing methods, unchanged)
    ↓
PipelineFactory → Pipeline → Result
    ↓ Events
LocalApiServer listens, broadcasts state/settings over pipe
```

`LocalApiServer` responsibilities:
- Host the named pipe, accept connections, parse JSON commands.
- On `trigger` command: dispatch to `LoadingViewModel.TriggerPipelineAsync(pipelineType, modeId)` via `DispatcherQueue`. This is a **single new public method** on the VM that calls the existing private `RunXxxPipelineAsync` methods.
- On `toggle` command: update the setting via `SettingsManager.UpdateAsync()`.
- On `query` command: serialize modes/settings from `DictationModeManager` / `SettingsManager` and write to pipe.
- Subscribe to `SettingsManager.SettingsChanged` → broadcast `settings` event.
- Subscribe to pipeline `StateChanged` events (via a simple event on `LoadingViewModel`) → broadcast `state` event.
- Handle pipe disconnection: log it, wait for reconnection. No crash, no drama.

**What changes in LoadingViewModel:** One new public method (~15 lines) that maps a pipeline type string to the existing private `RunXxxPipelineAsync` calls. Plus exposing `StateChanged` as a public event (it already fires `_controlPanel.OnPipelineStateChanged` — just add a parallel event). That's it.

### 3.4 Risk: SDK Support

Elgato's official Stream Deck SDK 2.0 is now JavaScript/Node.js only. C# plugins still work because the underlying protocol is WebSocket-based, but Elgato no longer documents or officially supports native executables.

The community library **[StreamDeck-Tools](https://www.nuget.org/packages/StreamDeck-Tools/)** (by BarRaider) fills this gap:
- Version 6.4.0, updated **March 2026**, targets **.NET 8.0**
- Supports Stream Deck Plus XL and latest hardware
- 7.0.0-beta.3 available (active development)
- Wraps all WebSocket communication, action registration, and Property Inspector plumbing

This is the right library. It's battle-tested and actively maintained. The risk is that a future Stream Deck software update could break compatibility, but BarRaider has tracked SDK changes for years. Acceptable risk for a personal-use feature.

---

## 4. Implementation Plan

### Phase 1: IPC Server (~8 hours)

1. Create `LocalApiServer.cs` in `DiktaMe.App\Services`.
2. Host `NamedPipeServerStream` with async read/write loop (follow `SingleInstanceManager` pattern).
3. Parse incoming JSON commands, dispatch `trigger` to LoadingViewModel, handle `toggle`/`query` directly.
4. Wire `SettingsManager.SettingsChanged` → broadcast settings JSON over pipe.
5. Add `PipelineStateChanged` public event on `LoadingViewModel`, wire to broadcast.
6. Add `LoadingViewModel.TriggerPipelineAsync(string pipelineType, string? modeId)` — public entry point that maps to existing private methods.

### Phase 2: Stream Deck Plugin (~12 hours)

1. Create `DiktaMe.StreamDeck` project — `net8.0-windows` console app.
2. Add `StreamDeck-Tools` 6.4.0 NuGet package.
3. Implement `PipelineTriggerAction` — on keyDown, send `trigger` command over named pipe. On `state` event, update button icon (Idle/Active).
4. Implement `SettingsToggleAction` — on keyDown, send `toggle` command. On `settings` event, update button icon (Lit/Dim).
5. Named pipe client with auto-reconnect (3s interval, not exponential — keep it simple). On disconnect, set all buttons to "Offline" icon.

### Phase 3: Property Inspector (~4 hours)

1. Build `property_inspector/index.html` — standard Stream Deck CSS, minimal JS.
2. Pipeline type dropdown (static list: Dictate, Ask, Refine Auto, Refine Voice, Translate, Note).
3. Mode selector dropdown (populated dynamically: plugin queries modes via pipe on PI open, forwards JSON to the HTML via `sendToPropertyInspector`).
4. Settings toggle dropdown (static list: Raw Mode, Streaming, Audio Ducking, Engine).

### Phase 4: Packaging & Distribution (~2 hours)

1. Create `manifest.json` with action definitions and icons.
2. Use `DistributionTool.exe` to package `.streamDeckPlugin` file.
3. Distribute as a separate download on the website/GitHub releases. Not bundled with the main installer — Stream Deck is niche.
4. No CI/CD automation for this. Manual build-and-package is fine for a low-frequency release.

**Total estimate: ~26 hours.**

---

## 5. What Good Looks Like

A user with a Stream Deck and 6 spare buttons gets:

| Button | Action | Idle Icon | Active Icon |
|--------|--------|-----------|-------------|
| 1 | Dictate (Standard) | Mic | Red Mic |
| 2 | Dictate (Developer) | Code icon | Red Code |
| 3 | Refine Auto | Wand | Spinning Wand |
| 4 | Ask | Question mark | Spinning Q |
| 5 | Toggle: Raw Mode | "RAW" dim | "RAW" lit |
| 6 | Toggle: Cloud/Local | Cloud icon | Gear icon |

Press button 2 → recording starts → icon goes red → processing → icon reverts. Meanwhile button 5 lets you flip raw mode without touching the app. That's it. No dashboards, no telemetry readouts, no live timers. Just buttons that do things and show their state.

---

## Appendix: Key Code References

| Component | Path | Role in Integration |
|-----------|------|-------------------|
| SingleInstanceManager | `src/DiktaMe.App/Services/SingleInstanceManager.cs` | Named pipe pattern to follow |
| LoadingViewModel | `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Pipeline execution entry point; hotkey dispatch at lines 335-422 |
| PipelineFactory | `src/DiktaMe.Core/Config/PipelineFactory.cs` | Creates all 8 pipeline types |
| SettingsManager | `src/DiktaMe.Core/Config/SettingsManager.cs` | `SettingsChanged` event (line 49) |
| DictationModeManager | `src/DiktaMe.Core/Config/DictationModeManager.cs` | `GetAllModes()` for Property Inspector |
| PipelineState | `src/DiktaMe.Core/Pipeline/PipelineState.cs` | State enum (Idle, Recording, Transcribing, etc.) |
| StreamDeck-Tools | [NuGet](https://www.nuget.org/packages/StreamDeck-Tools/) | v6.4.0, .NET 8, maintained by BarRaider |

---

## Appendix B: Feasibility Review (2026-03-23)

> Deep-dive research performed against current SDK docs, NuGet packages, and codebase state.

### Blockers

**None found.** The feature is buildable with current tooling.

### SDK & Library Status

| Item | Status | Detail |
|------|--------|--------|
| Elgato official SDK | JS-only | `@elgato/streamdeck` v2.0.4. No C#/.NET SDK planned. |
| StreamDeck-Tools (BarRaider) | Active | v6.4.0 stable (.NET 8), v7.0.0-beta.3 (.NET 8/10 + SkiaSharp). 535 stars, 39.9K NuGet downloads. |
| Stream Deck SW requirement | v6.9+ | Required for latest plugin features. |
| `dialPress` event | Removed | Gone in SW v6.5. Must use `dialDown`/`dialUp`. **No impact** — spec doesn't use dials. |
| New devices | Supported | Stream Deck Neo, Plus XL, SCUF, Galleon 100 SD — all handled by StreamDeck-Tools v6.4. |

### Alternative C# Libraries (for reference)

| Library | Notes |
|---------|-------|
| `streamdeck-client-csharp` (TyrenDe) | v4.3.0, minimal wrapper, Windows-only. |
| `Stream-Deck-CSharp-Client` (Aeroverra) | Cross-platform, DI-friendly. Less adoption. |
| `StreamDeckSharp` (OpenMacroBoard) | Low-level hardware control. Different use case. |
| `StreamDeckToolkit` (FritzAndFriends) | Template has known build failures ([Issue #82](https://github.com/FritzAndFriends/StreamDeckToolkit/issues/82)). Avoid. |

**Verdict:** StreamDeck-Tools remains the best choice. No reason to switch.

### Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Community library dependency (not Elgato-backed) | Medium | Underlying protocol is just WebSocket + JSON — could implement directly if BarRaider disappears. |
| v7.0 migration required (~12-18 months) | Medium | v7.0 replaces `System.Drawing` with SkiaSharp, drops .NET Standard 2.0. ~4h migration. Pin v6.4 for now. |
| Named pipes = Windows-only | Low | DiktaMe is WinUI 3 / Windows-only. Not a real concern. |
| Sideloaded plugins don't auto-update | Low | Fine for personal/niche feature. Marketplace submission possible later. |

### Spec Gaps Found

1. **Foreground window capture**: `OnHotkeyPressed()` captures `sourceWindow` via `TextInjector.GetCurrentForegroundWindow()` *before* dispatching to UI thread (line 339). A Stream Deck trigger arriving via named pipe would miss this — the foreground window would be wrong. `TriggerPipelineAsync` must capture HWND on the pipe-reader thread before dispatching.

2. **Concurrent pipe connections**: `LocalApiServer` should use `MaxAllowedServerInstances` (like `SingleInstanceManager` does) to handle overlapping reconnects gracefully.

3. **Oops action omitted**: `Oops` (undo last injection) is a one-liner and a natural Stream Deck button. Consider adding it as a low-cost third action type.

### Broader Value of Phase 1

The `LocalApiServer` + named pipe IPC is useful beyond Stream Deck. Any external tool that can write JSON to a named pipe could trigger pipelines: Talon (voice coding), Touch Portal, AutoHotkey, macro keyboards, custom scripts. Building Phase 1 first creates a general-purpose local API.

---

## 6. Implementation Record (2026-03-23)

> Phase 1 (IPC Server) and Phase 2 (Stream Deck Plugin) implemented in a single session.
> All spec goals from §1–§5 achieved. Zero pipeline files modified. 1039 tests passing (1014 existing + 25 new).

### 6.1 Key Design Decisions Made During Implementation

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Refine command naming | Two explicit variants: `refine_auto` and `refine_voice` | No ambiguous `"refine"` command. The two refine modes have different UX (auto applies to selection, voice re-records). Explicit naming prevents misconfiguration. |
| ExternalStateChanged location | On `ControlPanelViewModel`, not `LoadingViewModel` | All pipeline state transitions already route through `ControlPanelViewModel.OnPipelineStateChanged()`. Adding the event there requires 2 lines (declaration + invoke), zero pipeline changes. |
| Plugin ↔ DiktaMe.Core dependency | **None** — plugin is fully standalone | Keeps the plugin at ~2–3 MB. The IPC protocol (JSON over named pipe) is the only contract. No NAudio, Whisper, Kokoro, etc. pulled in. |
| Plugin project in DiktaMe.sln | **Not added** — builds independently | Different lifecycle, different dependencies (Newtonsoft.Json via StreamDeck-Tools vs System.Text.Json in main app). `dotnet build src/DiktaMe.StreamDeck/DiktaMe.StreamDeck.csproj` builds it standalone. |
| JSON serialization (server side) | `Utf8JsonWriter` | Trim-safe in `PublishTrimmed` builds. No anonymous types, no reflection-based serializers. |
| JSON serialization (plugin side) | `Newtonsoft.Json` (JObject) | Transitive dependency of StreamDeck-Tools. No extra package needed. |
| Manifest SDKVersion | 2 (not 3) | StreamDeck-Tools 6.4.0 uses SDKVersion 2. Version 3 is for Elgato's official JS SDK. |
| Oops and ReadSelection | Added to pipeline catalog | Low cost (1 case each in dispatch table). Oops = undo last injection. ReadSelection = TTS of selected text. Both are natural Stream Deck buttons. |
| Icons | Solid-color placeholders | 16 PNGs at 1x/2x resolutions. Functional but not final art. Replace with branded icons in a polish pass. |

### 6.2 Deviations from Original Spec

| Spec Section | Original | Actual | Reason |
|--------------|----------|--------|--------|
| §2.1 Pipeline list | 6 types (Dictate, Ask, Refine Auto, Refine Voice, Translate, Note) | 8 types (+Oops, +Read Selection) | Per Appendix B §3: Oops is a natural button. ReadSelection follows the same pattern. |
| §3.3 Method signature | `TriggerPipelineAsync(pipelineType, modeId)` | `TriggerPipeline(pipelineType, modeId, sourceWindow)` | Per Appendix B §1: HWND must be captured on pipe reader thread, passed through to pipeline methods. Synchronous dispatch to `DispatcherQueue.TryEnqueue()`. |
| §3.3 State event source | "via a simple event on `LoadingViewModel`" | Via `ExternalStateChanged` on `ControlPanelViewModel` | All state transitions already converge to `ControlPanelViewModel.OnPipelineStateChanged()`. Adding the event there is less intrusive. |
| §4.3 Property Inspector | Single `index.html` per action | `trigger-pi.html` and `toggle-pi.html` (separate files) | Stream Deck manifest requires one PI per action UUID. Separate files are cleaner. |

---

## 7. File Inventory

### 7.1 Phase 1 — IPC Server (inside DiktaMe.App / DiktaMe.Core)

| File | Type | Lines | Description |
|------|------|-------|-------------|
| `src/DiktaMe.Core/Config/ApiCommand.cs` | NEW | 82 | `ApiCommand` record + `ApiCommandParser.TryParse()` — testable command parser using `JsonDocument` |
| `src/DiktaMe.App/Services/LocalApiServer.cs` | NEW | 407 | Named pipe server: accept loop, per-client handler, command dispatch, event broadcasting |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | MOD | +7 | `ExternalStateChanged` event declaration + invoke in `OnPipelineStateChanged()` |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | MOD | +85, ~2 changed | `_modeIdOverride` field, `TriggerPipeline()` public method, 2 lines changed for mode override consumption |
| `src/DiktaMe.App/App.xaml.cs` | MOD | +2 | DI registration (`AddSingleton<LocalApiServer>`) + `Start()` call in `OnLaunched` |
| `tests/DiktaMe.Core.Tests/Config/ApiCommandParserTests.cs` | NEW | 175 | 25 unit tests — all command types, error cases, edge cases |
| `test-helpers/test-ipc-pipe.ps1` | NEW | 91 | PowerShell E2E test script — connects to pipe, sends queries/toggles, listens for events |

### 7.2 Phase 2 — Stream Deck Plugin (standalone project)

| File | Type | Lines | Description |
|------|------|-------|-------------|
| `src/DiktaMe.StreamDeck/DiktaMe.StreamDeck.csproj` | NEW | 33 | `net8.0-windows` console app, StreamDeck-Tools 6.4.0, flat output layout |
| `src/DiktaMe.StreamDeck/Program.cs` | NEW | 13 | Entry point: `SDWrapper.Run(args)` |
| `src/DiktaMe.StreamDeck/manifest.json` | NEW | 53 | Plugin manifest: 2 actions, SDKVersion 2, plugin/category icons |
| `src/DiktaMe.StreamDeck/Services/ApiPipeClient.cs` | NEW | 276 | Singleton named pipe client: auto-reconnect (3s), state cache, event dispatch |
| `src/DiktaMe.StreamDeck/Actions/PipelineTriggerAction.cs` | NEW | 165 | `KeypadBase` action: trigger commands, state icon updates, mode forwarding to PI |
| `src/DiktaMe.StreamDeck/Actions/SettingsToggleAction.cs` | NEW | 131 | `KeypadBase` action: toggle commands, settings icon updates, bidirectional sync |
| `src/DiktaMe.StreamDeck/Models/TriggerActionSettings.cs` | NEW | 18 | Settings DTO: `PipelineType` + `ModeId` |
| `src/DiktaMe.StreamDeck/Models/ToggleActionSettings.cs` | NEW | 12 | Settings DTO: `SettingName` |
| `src/DiktaMe.StreamDeck/Models/ModeInfo.cs` | NEW | 12 | DTO: `{Id, Title}` for dictation mode dropdown |
| `src/DiktaMe.StreamDeck/PropertyInspectors/trigger-pi.html` | NEW | 128 | Property Inspector: pipeline dropdown, dynamic mode selector, connection status |
| `src/DiktaMe.StreamDeck/PropertyInspectors/toggle-pi.html` | NEW | 23 | Property Inspector: setting dropdown (4 options) |
| `src/DiktaMe.StreamDeck/install-plugin.cmd` | NEW | 75 | Build → kill Stream Deck → copy to Plugins dir → restart |
| `src/DiktaMe.StreamDeck/Images/` | NEW | 16 files | Placeholder icons at 1x and 2x (see §7.3) |

### 7.3 Icon Assets

All icons are solid-color placeholders. Replace with branded art in a future polish pass.

| File | Size | Color | Usage |
|------|------|-------|-------|
| `plugin-icon.png` | 256×256 | Teal #00607a | Plugin list in Stream Deck app |
| `plugin-icon@2x.png` | 512×512 | Teal #00607a | HiDPI plugin list |
| `category-icon.png` | 28×28 | Teal #00607a | Category sidebar |
| `category-icon@2x.png` | 56×56 | Teal #00607a | HiDPI category sidebar |
| `trigger-idle.png` | 72×72 | Dark #1a1a2e | Pipeline button: idle state |
| `trigger-idle@2x.png` | 144×144 | Dark #1a1a2e | HiDPI idle |
| `trigger-active.png` | 72×72 | Red #e74c3c | Pipeline button: recording/processing |
| `trigger-active@2x.png` | 144×144 | Red #e74c3c | HiDPI active |
| `trigger-offline.png` | 72×72 | Grey #555555 | Pipeline button: app not running |
| `trigger-offline@2x.png` | 144×144 | Grey #555555 | HiDPI offline |
| `toggle-on.png` | 72×72 | Teal #00607a | Setting toggle: enabled |
| `toggle-on@2x.png` | 144×144 | Teal #00607a | HiDPI enabled |
| `toggle-off.png` | 72×72 | Dark #1a1a2e | Setting toggle: disabled |
| `toggle-off@2x.png` | 144×144 | Dark #1a1a2e | HiDPI disabled |
| `toggle-offline.png` | 72×72 | Grey #555555 | Setting toggle: app not running |
| `toggle-offline@2x.png` | 144×144 | Grey #555555 | HiDPI offline |

---

## 8. Architecture Details (As Built)

### 8.1 Data Flow

```
┌─────────────────────────┐      Named Pipe       ┌──────────────────────────────┐
│  DiktaMe.StreamDeck     │  ← JSON events ──────  │  DiktaMe.App                 │
│  (Stream Deck Plugin)   │  ── JSON commands ──→  │  (WinUI 3 Application)       │
│                         │                        │                              │
│  ApiPipeClient          │◄────── pipe ─────────► │  LocalApiServer              │
│    ├─ ConnectLoopAsync  │      DiktaMe.V2.Api    │    ├─ AcceptLoopAsync         │
│    ├─ ReadLoopAsync     │                        │    ├─ HandleClientAsync       │
│    └─ SendCommandAsync  │                        │    ├─ HandleTrigger           │
│                         │                        │    ├─ HandleToggleAsync       │
│  PipelineTriggerAction  │                        │    └─ HandleQueryAsync        │
│  SettingsToggleAction   │                        │                              │
└─────────────────────────┘                        │  LoadingViewModel            │
                                                   │    └─ TriggerPipeline()      │
                                                   │  ControlPanelViewModel       │
                                                   │    └─ ExternalStateChanged   │
                                                   │  SettingsManager             │
                                                   │    └─ SettingsChanged         │
                                                   └──────────────────────────────┘
```

### 8.2 Connection Lifecycle

1. **DiktaMe.App starts** → `LocalApiServer.Start()` called from `App.OnLaunched()` → begins `AcceptLoopAsync` on threadpool
2. **Stream Deck loads plugin** → `SDWrapper.Run(args)` → actions instantiate → `ApiPipeClient.Instance.EnsureStarted()` → begins `ConnectLoopAsync`
3. **Connection established** → server sends initial snapshot (state + settings + modes) → client caches values
4. **User presses Stream Deck button** → action's `KeyPressed` → `ApiPipeClient.SendCommandAsync(json)` → server's `ProcessCommandAsync`
5. **Server dispatches command** → captures foreground HWND on pipe thread → `DispatcherQueue.TryEnqueue()` → `LoadingViewModel.TriggerPipeline()`
6. **Pipeline runs** → state changes flow through `ControlPanelViewModel.OnPipelineStateChanged()` → `ExternalStateChanged` fires → `LocalApiServer.OnPipelineStateChanged()` → `BroadcastJson()` → all connected clients receive state event
7. **Client updates icon** → `PipelineTriggerAction.OnStateChanged(state)` → `UpdateIconAsync(state)` → button reflects current state
8. **Disconnection** → pipe breaks → client's `SetDisconnected()` fires → buttons go to "offline" icon → 3-second reconnect loop begins

### 8.3 Threading Model

| Thread | Phase 1 (Server) | Phase 2 (Plugin) |
|--------|-------------------|-------------------|
| UI thread | `Start()` captures `DispatcherQueue`. `TriggerPipeline()` runs here via `TryEnqueue()`. | N/A — plugin has no UI thread. |
| Pipe reader thread | `HandleClientAsync` reads commands. `HandleTrigger` captures HWND here (critical — must be before UI dispatch). | `ReadLoopAsync` receives events, dispatches to action event handlers. |
| Pipe writer thread | `BroadcastJson()` writes to all clients (synchronized via `_clientsLock`). | `SendCommandAsync()` writes (synchronized via `SemaphoreSlim`). |
| StreamDeck-Tools thread | N/A | `SDWrapper.Run(args)` manages WebSocket to Stream Deck app. Action methods called from this thread. |

### 8.4 Foreground Window Capture

**Problem:** When a Stream Deck button triggers a pipeline, the foreground window must be the user's target app (e.g., VS Code, Word), not the Stream Deck application.

**Solution:** Stream Deck Plus communicates via USB HID — pressing a button does NOT steal focus. The user's active window remains focused. `TextInjector.GetCurrentForegroundWindow()` is called on the pipe reader thread *before* dispatching to the UI thread. The captured HWND is passed through to `TriggerPipeline(pipelineType, modeId, sourceWindow)`.

This matches the existing hotkey handler pattern at `LoadingViewModel` line 339 where `sourceWindow` is captured before `DispatcherQueue.TryEnqueue()`.

**Edge case:** iPad Stream Deck app — the iPad is a separate device, so the desktop's foreground window is unaffected.

### 8.5 Mode Override Pattern

**Problem:** Each Stream Deck button can target a specific dictation mode (e.g., "Developer", "Email"), but the pipeline methods read the active mode from `ControlPanelViewModel.ActiveDictationModeId` or `AppSettings.ActiveDictationModeId`.

**Solution:** Transient `_modeIdOverride` field on `LoadingViewModel`:
1. `TriggerPipeline()` sets `_modeIdOverride` before dispatching to the appropriate `RunXxxPipelineAsync` method
2. `RunStreamingDictationAsync` (line 951): `string? activeModeId = _modeIdOverride ?? _controlPanel.ActiveDictationModeId;`
3. `RunBatchDictationAsync` (line 1067): `string? activeModeId = _modeIdOverride ?? _settings.Current.ActiveDictationModeId;`
4. `_modeIdOverride` is reset to `null` in the `finally` block of `TriggerPipeline()`

**No race condition:** The toggle-stop guard prevents starting a new pipeline while one is recording. Since all triggers are serialized through this guard, `_modeIdOverride` is always consumed before the next trigger can set it.

---

## 9. IPC Protocol Reference (As Implemented)

### 9.1 Wire Format

- **Transport:** Named pipe `DiktaMe.V2.Api`, `PipeDirection.InOut`, `PipeTransmissionMode.Byte`, `PipeOptions.Asynchronous`
- **Framing:** Newline-delimited JSON — one JSON object per `\n` (StreamReader/StreamWriter handle framing)
- **Encoding:** UTF-8
- **Concurrency:** `MaxAllowedServerInstances` — handles overlapping reconnects

### 9.2 Commands (Plugin → App)

```json
{"action":"trigger","pipeline":"dictate"}
{"action":"trigger","pipeline":"dictate","modeId":"abc123-def456-..."}
{"action":"trigger","pipeline":"ask"}
{"action":"trigger","pipeline":"refine_auto"}
{"action":"trigger","pipeline":"refine_voice"}
{"action":"trigger","pipeline":"translate"}
{"action":"trigger","pipeline":"note"}
{"action":"trigger","pipeline":"oops"}
{"action":"trigger","pipeline":"read_selection"}
{"action":"toggle","setting":"RawModeOverride"}
{"action":"toggle","setting":"StreamingEnabled"}
{"action":"toggle","setting":"AudioDucking"}
{"action":"toggle","setting":"Engine"}
{"action":"query","target":"modes"}
{"action":"query","target":"settings"}
```

### 9.3 Events (App → Plugin)

```json
{"event":"state","state":"Idle"}
{"event":"state","state":"Recording"}
{"event":"state","state":"Transcribing"}
{"event":"state","state":"Streaming"}
{"event":"state","state":"Processing"}
{"event":"state","state":"Injecting"}
{"event":"state","state":"Speaking"}
{"event":"state","state":"Error"}
{"event":"settings","RawModeOverride":false,"StreamingEnabled":true,"AudioDucking":true,"ActiveProfile":"Cloud"}
{"event":"modes","modes":[{"id":"guid-1","title":"Standard"},{"id":"guid-2","title":"Developer"}]}
{"event":"busy"}
{"event":"error","message":"No audio device found"}
```

### 9.4 Connection Handshake

On connection, the server sends three events in order:
1. `{"event":"state","state":"Idle"}` — current pipeline state
2. `{"event":"settings",...}` — current settings snapshot
3. `{"event":"modes","modes":[...]}` — available dictation modes

The plugin also sends two query commands after connecting to ensure fresh data:
1. `{"action":"query","target":"settings"}`
2. `{"action":"query","target":"modes"}`

---

## 10. Build & Install

### 10.1 Building the Plugin

```bash
# From repo root — build the standalone plugin project
dotnet build src/DiktaMe.StreamDeck/DiktaMe.StreamDeck.csproj -c Release

# Output: src/DiktaMe.StreamDeck/bin/Release/me.dikta.streamdeck.sdPlugin/
#   ├── DiktaMe.StreamDeck.exe
#   ├── manifest.json
#   ├── Images/
#   ├── PropertyInspectors/
#   └── (runtime DLLs)
```

The build output directory name (`me.dikta.streamdeck.sdPlugin`) matches the Stream Deck plugin folder naming convention. The entire folder is ready to copy to the Plugins directory.

### 10.2 Installing the Plugin

**Automated (recommended):**
```cmd
cd src\DiktaMe.StreamDeck
install-plugin.cmd
```

The script performs:
1. Validates Release build exists
2. Kills `StreamDeck.exe` process
3. Waits 2 seconds for process cleanup
4. Removes old plugin folder from `%APPDATA%\Elgato\StreamDeck\Plugins\me.dikta.streamdeck.sdPlugin\`
5. Copies new build to plugin folder
6. Restarts Stream Deck

**Manual:**
1. Build with `dotnet build -c Release`
2. Close Stream Deck
3. Copy `src\DiktaMe.StreamDeck\bin\Release\me.dikta.streamdeck.sdPlugin\` to `%APPDATA%\Elgato\StreamDeck\Plugins\`
4. Restart Stream Deck

### 10.3 Building the Main App

The IPC server is part of the normal DiktaMe.App build — no separate steps:
```bash
dotnet build DiktaMe.sln -c Release
# or: dotnet build DiktaMe.sln -c Debug
```

### 10.4 Running Tests

```bash
dotnet test DiktaMe.sln
# 1039 tests: 1014 existing + 25 new ApiCommandParser tests
```

---

## 11. Testing

### 11.1 Manual E2E Test Script

`test-helpers/test-ipc-pipe.ps1` — validates the Phase 1 IPC server without needing a Stream Deck:

```powershell
# Prerequisites: DiktaMe app must be running
powershell -ExecutionPolicy Bypass -File test-helpers\test-ipc-pipe.ps1
```

The script:
1. Connects to `DiktaMe.V2.Api` named pipe
2. Reads initial snapshot events (state, settings, modes)
3. Sends `query:modes` and `query:settings`, prints responses
4. Sends `toggle:RawModeOverride` twice (toggle + restore), prints settings events
5. Enters interactive listen mode (Ctrl+C to exit) — shows all events in real-time with timestamps

### 11.2 Stream Deck Testing Procedure

**Prerequisites:**
- DiktaMe app running
- Stream Deck software v6.5+ running
- Plugin installed (§10.2)

**Pipeline Trigger Tests:**
1. Add a "Pipeline Trigger" action to a Stream Deck button
2. In Property Inspector, select "Dictate" pipeline
3. Press the button → verify recording starts → icon changes to active (red)
4. Wait for pipeline to complete → icon reverts to idle (dark)
5. Repeat with each pipeline type: Ask, Refine Auto, Refine Voice, Translate, Note, Oops, Read Selection

**Mode-Specific Dictation:**
1. Add a "Pipeline Trigger" action, select "Dictate"
2. In the Mode dropdown, select a specific mode (e.g., "Developer")
3. Press the button → dictate → verify the dictation uses the selected mode's prompt, not the app's active mode

**Settings Toggle Tests:**
1. Add a "Settings Toggle" action, select "Raw Mode"
2. Press the button → verify icon changes (on/off) + verify setting changes in the DiktaMe app UI
3. Change the setting in the app UI → verify the Stream Deck button icon updates bidirectionally
4. Repeat for: Streaming, Audio Ducking, Engine

**Connection/Disconnection:**
1. Close the DiktaMe app → verify all buttons show "offline" icon
2. Reopen the app → verify buttons reconnect within ~3 seconds and show current state
3. Verify the initial snapshot (correct state, correct settings values) after reconnect

**Busy Handling:**
1. Start a dictation pipeline (press trigger button)
2. While recording/processing, press another trigger button → verify a brief alert flash (no crash, no queue)

### 11.3 Unit Tests

25 tests in `tests/DiktaMe.Core.Tests/Config/ApiCommandParserTests.cs`:

| Category | Count | Tests |
|----------|-------|-------|
| Trigger commands | 7 | dictate, dictate+modeId, ask, oops, refine_auto, refine_voice, translate, note, read_selection (Theory) |
| Toggle commands | 4 | RawModeOverride, StreamingEnabled, AudioDucking, Engine (Theory) |
| Query commands | 2 | modes, settings (Theory) |
| Error handling | 7 | null, empty, whitespace, malformed JSON, missing action, empty action, action is number |
| Edge cases | 3 | unknown action (still parses), extra fields (ignored), null modeId (treated as absent) |

---

## 12. Existing Code Changes (Detailed)

This section documents the exact modifications to existing files. No files under `src/DiktaMe.Core/Pipeline/` were modified.

### 12.1 ControlPanelViewModel.cs

**Location:** `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`

**Change 1 — Event declaration** (after line 63, in pipeline state section):
```csharp
/// <summary>
/// Raised when the pipeline state changes. Used by LocalApiServer to broadcast state over IPC.
/// Fires on the UI thread (inside the DispatcherQueue callback).
/// </summary>
public event EventHandler<PipelineState>? ExternalStateChanged;
```

**Change 2 — Event invocation** (inside `OnPipelineStateChanged()`, after setting `StateText`):
```csharp
ExternalStateChanged?.Invoke(this, state);
```

**Impact:** 7 lines added. Zero behavioral change to existing code.

### 12.2 LoadingViewModel.cs

**Location:** `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

**Change 1 — `_modeIdOverride` field** (after line 54):
```csharp
/// <summary>
/// Transient per-trigger mode override for Stream Deck per-button modes.
/// Null = use app's active mode. Set by TriggerPipeline, consumed by pipeline methods.
/// </summary>
private string? _modeIdOverride;
```

**Change 2 — `TriggerPipeline()` public method** (~80 lines, after `OnHotkeyPressed`):

Public method that maps pipeline type strings to existing private `RunXxxPipelineAsync` methods. Includes the same toggle-stop and TTS-stop guards as `OnHotkeyPressed`. Dispatch table:

| Pipeline Type | Delegates To |
|---------------|-------------|
| `dictate` | Sets `_modeIdOverride`, runs dictation pipeline (streaming or batch per settings) |
| `refine_auto` | `RunRefineAutoAsync(sourceWindow)` |
| `refine_voice` | `RunRefineVoiceAsync(sourceWindow)` |
| `ask` | `RunAskPipelineAsync()` |
| `translate` | `RunTranslatePipelineAsync()` |
| `note` | `RunNotePipelineAsync(sourceWindow)` |
| `oops` | `_textInjector.ReInjectLast()` + stop sound |
| `read_selection` | `RunReadSelectionPipelineAsync(sourceWindow)` |

**Change 3 — Mode override consumption** (2 existing lines changed):
- Line 951 (`RunStreamingDictationAsync`): `string? activeModeId = _modeIdOverride ?? _controlPanel.ActiveDictationModeId;`
- Line 1067 (`RunBatchDictationAsync`): `string? activeModeId = _modeIdOverride ?? _settings.Current.ActiveDictationModeId;`

**Impact:** +85 lines, 2 lines changed. No existing method signatures altered. No pipeline files touched.

### 12.3 App.xaml.cs

**Location:** `src/DiktaMe.App/App.xaml.cs`

**Change 1 — DI registration** (in `ConfigureServices`, after existing UI services):
```csharp
services.AddSingleton<Services.LocalApiServer>();
```

**Change 2 — Server start** (in `OnLaunched`, after theme apply):
```csharp
Services.GetRequiredService<Services.LocalApiServer>().Start();
```

**Impact:** 2 lines added.

---

## 13. Plugin Project Structure

```
src/DiktaMe.StreamDeck/
├── DiktaMe.StreamDeck.csproj    # net8.0-windows, StreamDeck-Tools 6.4.0
├── Program.cs                    # SDWrapper.Run(args) entry point
├── manifest.json                 # Plugin manifest (SDKVersion 2, 2 actions)
├── install-plugin.cmd            # Build → install → restart Stream Deck
│
├── Services/
│   └── ApiPipeClient.cs          # Singleton pipe client, auto-reconnect, state cache
│
├── Actions/
│   ├── PipelineTriggerAction.cs  # [PluginActionId("me.dikta.streamdeck.trigger")]
│   └── SettingsToggleAction.cs   # [PluginActionId("me.dikta.streamdeck.toggle")]
│
├── Models/
│   ├── TriggerActionSettings.cs  # PipelineType + ModeId
│   ├── ToggleActionSettings.cs   # SettingName
│   └── ModeInfo.cs               # {Id, Title} DTO
│
├── PropertyInspectors/
│   ├── trigger-pi.html           # Pipeline dropdown + dynamic mode selector
│   └── toggle-pi.html            # Setting dropdown
│
└── Images/                       # 16 PNGs (1x + @2x variants)
    ├── plugin-icon.png / @2x     # 256×256 / 512×512
    ├── category-icon.png / @2x   # 28×28 / 56×56
    ├── trigger-idle.png / @2x    # 72×72 / 144×144
    ├── trigger-active.png / @2x  # 72×72 / 144×144
    ├── trigger-offline.png / @2x # 72×72 / 144×144
    ├── toggle-on.png / @2x       # 72×72 / 144×144
    ├── toggle-off.png / @2x      # 72×72 / 144×144
    └── toggle-offline.png / @2x  # 72×72 / 144×144
```

### 13.1 Key Plugin Components

**`ApiPipeClient`** (singleton, lazy-init):
- `EnsureStarted()` — double-checked lock, starts background `ConnectLoopAsync`
- `ConnectLoopAsync()` — connects to `DiktaMe.V2.Api`, sends initial queries, enters `ReadLoopAsync`. On disconnect, waits 3 seconds and retries.
- `ReadLoopAsync()` — reads newline-delimited JSON, parses with `JObject.Parse`, dispatches via `DispatchEvent` switch
- `SendCommandAsync()` — thread-safe writes via `SemaphoreSlim`, swallows IOException (read loop handles reconnect)
- **State cache:** `CurrentPipelineState`, `CurrentSettingsJson`, `CurrentModes` — actions read these for initial state on construction

**`PipelineTriggerAction`** (`KeypadBase`):
- **KeyPressed:** Builds JSON trigger command (includes `modeId` if pipeline is "dictate" and a mode is selected). Sends via `ApiPipeClient`.
- **State tracking:** Subscribes to `StateChanged`, `BusyReceived`, `ConnectionChanged`, `ModesReceived` events
- **Icon states:** `trigger-idle` (dark), `trigger-active` (red), `trigger-offline` (grey). Active states: Recording, Transcribing, Streaming, Processing, Injecting, Speaking.
- **Title labels:** Dictate, Ask, Refine, Refine V, Translate, Note, Oops, Read
- **Mode forwarding:** On `ModesReceived`, sends mode list to Property Inspector via `SendToPropertyInspectorAsync`

**`SettingsToggleAction`** (`KeypadBase`):
- **KeyPressed:** Sends toggle command. Does NOT optimistically update icon — waits for settings event from app (source of truth).
- **Settings tracking:** Subscribes to `SettingsReceived`, `ConnectionChanged`. `ApplySettingsValue()` extracts boolean from JObject.
- **Engine special case:** `"Engine"` maps to `ActiveProfile == "Local"` (not a boolean toggle — it's Cloud↔Local)
- **Icon states:** `toggle-on` (teal), `toggle-off` (dark), `toggle-offline` (grey)
- **Title labels:** RAW ON/RAW, STREAM/BATCH, DUCK ON/DUCK, LOCAL/CLOUD

---

## 14. Known Limitations & Future Work

### 14.1 Known Limitations

| Limitation | Impact | Workaround |
|------------|--------|------------|
| Placeholder icons | Functional but ugly — solid color squares | Replace with branded SVG→PNG exports |
| No per-pipeline custom icons | All trigger buttons share the same 3 state icons | Could map pipeline type to different icon sets |
| No dial/touch screen support | Stream Deck Plus dials and LCD strip unused | Add dial actions for volume/speed control in a future phase |
| Plugin not in DiktaMe.sln | Must build separately | Acceptable — different lifecycle, prevents accidental coupling |
| No `.streamDeckPlugin` package | Manual folder copy install only | Use Elgato's `DistributionTool.exe` to create installable package |
| Button responsiveness | Single press sometimes requires "hold" or "double click" | Server-side 300ms debounce helps but root cause unclear — investigate pipe message queuing |
| Ask button triggers Dictate | PI shows "Ask" but command may send "dictate" | Verify `ReceivedSettings` / `AutoPopulateSettings` round-trip preserves `pipelineType` casing |

### 14.2 Future Phase 3: Touch Screen & Dials (Stream Deck Plus)

Not implemented. Potential additions:
- **LCD touch strip:** Show pipeline state text, last transcription Macro, or telemetry
- **Dials:** Audio ducking level, microphone gain, TTS speed/volume
- **Encoder actions:** Requires `EncoderBase` from StreamDeck-Tools (instead of `KeypadBase`)

### 14.3 Packaging for Distribution

When ready for wider distribution:
1. Replace placeholder icons with branded art
2. Package with `DistributionTool.exe -b -i me.dikta.streamdeck.sdPlugin -o release/`
3. Host `.streamDeckPlugin` file on website downloads page or GitHub Releases
4. Consider Elgato Marketplace submission (requires review process)

---

## 15. Bugfix Record (2026-03-23, Post-Implementation)

Five bugs found and fixed during first live testing session. All fixes committed together.

### Bug 1: BroadcastJson Exception Kills App During Dictation (CRITICAL)

**Symptom:** App crashes (exit 127) every time during hotkey dictation when Stream Deck plugin is connected.

**Root Cause:** `BroadcastJson()` in `LocalApiServer` only caught `IOException`. But `StreamWriter.WriteLine()` can also throw `InvalidOperationException` ("stream in use by a previous operation") when `HandleClientAsync` uses `WriteLineAsync` (async) for the initial snapshot while `BroadcastJson` uses `WriteLine` (sync) concurrently. The uncaught exception propagates through the `ExternalStateChanged` event into the `DispatcherQueue.TryEnqueue()` callback → native crash (no managed stack trace).

**Fix:**
1. Added `ConnectedClient` inner class with per-client `_writeLock` (object lock) to serialize all writes
2. Broadened catch to `Exception ex when (ex is IOException or InvalidOperationException or ObjectDisposedException)`
3. Wrapped `OnPipelineStateChanged` and `OnSettingsChanged` event handlers in `try/catch`
4. Initial snapshot in `HandleClientAsync` uses `WriteSafe()` (sync, under lock) instead of `WriteLineAsync`

### Bug 2: SetWindowLongPtr P/Invoke Crash

**Symptom:** `EntryPointNotFoundException: Unable to find 'SetWindowLongPtr' in user32.dll` at startup (once).

**Fix:** Wrapped `InstallDoubleClickHook(page)` in `MainWindow.xaml.cs` with `try/catch (EntryPointNotFoundException)`. The hook is cosmetic (double-click title bar toggle), not critical.

### Bug 3: UI Thread Deadlock from WriteSafe Lock

**Symptom:** App hangs on loading window after Bug 1 fix. `WriteSafe()` lock held during `BroadcastJson` → blocks UI thread when called from `OnPipelineStateChanged` inside `DispatcherQueue.TryEnqueue()`.

**Fix:** Offloaded `BroadcastJson` calls in event handlers to `Task.Run()`:
```csharp
private void OnPipelineStateChanged(object? sender, PipelineState state)
{
    _ = Task.Run(() => { try { BroadcastJson(...); } catch { ... } });
}
```

### Bug 4: FlushFileBuffers Deadlock (Pipe Data Not Flowing)

**Symptom:** Plugin connects but receives no data. Buttons change color (online/offline) but no state events flow.

**Root Cause:** On Windows named pipes, `StreamWriter.Flush()` → `PipeStream.Flush()` → Win32 `FlushFileBuffers()` which **blocks until the other side reads all pending data**. Both server (`AutoFlush = true`) and plugin (`FlushAsync()`) wrote before reading → mutual deadlock.

**Proven via PowerShell:** write+flush before read = deadlock; no-flush = works.

**Fix:** Changed both sides to `new StreamWriter(stream, Encoding.UTF8, bufferSize: 1, leaveOpen: true)`. `bufferSize: 1` disables internal StreamWriter buffering — each `WriteLine` goes directly to the pipe kernel buffer via `WriteFile()` without calling `FlushFileBuffers()`. Removed all `Flush()`/`FlushAsync()` calls.

### Bug 5: PI Settings Not Persisting + Status "Connecting..."

**Symptom:** Dropdown selections (e.g., "Ask") revert to "Dictate" when switching between buttons. Status shows "Connecting..." permanently.

**Root Cause (settings):** PI HTML files used CDN-hosted EasyPI library (`sdtools.common.js` from jsdelivr). CDN dependency unreliable + possibly incompatible with SD SDK v7.3.

**Root Cause (status):** `SendConnectionStatusAsync` fired in the action constructor, but PI isn't open yet. Connection status event was sent and missed.

**Fix:**
1. Rewrote both PI HTML files with self-contained native SD WebSocket protocol (no CDN)
2. Added `Connection.OnPropertyInspectorDidAppear` handler to resend connection status + modes when PI opens

### Bug 6: Duplicate Trigger Commands (Ongoing — Partially Fixed)

**Symptom:** Single button press sends 3-5 trigger commands in 1ms. For recording pipelines (Ask, Dictate), the first starts recording and the second immediately stops it. After this, `_isRecording` gets stuck `true` in `LoadingViewModel`, blocking all subsequent hotkey presses ("stopping active recording" on every press — app appears broken until restart).

**Root Cause:** Pipe message queuing delivers multiple commands for a single physical press. The SD SDK sends exactly one `keyDown`/`keyUp` per press (confirmed via research), so the duplication is in the transport layer.

**Partial Fix:**
1. Added 300ms server-side debounce in `HandleTrigger` (only when state is `Idle` — toggle-stop during `Recording` always passes through)
2. Removed the 500ms plugin-side debounce that was causing perceived unresponsiveness

**Remaining:** Button still feels less responsive than other SD plugins. Need to investigate if the pipe message queuing can be eliminated at the source (perhaps related to `SemaphoreSlim` write lock in `ApiPipeClient.SendCommandAsync`).

### Bug 7: Ask Button Triggers Dictate (Ongoing)

**Symptom:** User reports pressing Ask button triggers Dictation instead of Ask.

**Possible Causes:**
1. `ReceivedSettings` / `Tools.AutoPopulateSettings` may not preserve `pipelineType` correctly
2. The `_settings.PipelineType` default is "dictate" and may be overwriting the saved value
3. The `setSettings` WebSocket event may not be persisting the `pipelineType` field

**Status:** Investigated — see updated summary below.

### Bug 8: AutoFlush Deadlock Regression (Connection Dead)

**Symptom:** Plugin connects to pipe but shows "Offline" — never receives any data. Plugin log shows "connecting to pipe..." but no "connected" or subsequent messages. App and plugin must both be restarted.

**Root Cause:** Round 3 of fixes changed StreamWriter from `bufferSize: 1` to `bufferSize: 4096` + `AutoFlush = true` on both server (`LocalApiServer.cs`) and client (`ApiPipeClient.cs`). The intent was to fix alleged message fragmentation from byte-at-a-time writes.

However, `AutoFlush = true` calls `StreamWriter.Flush()` → `PipeStream.Flush()` → Win32 `FlushFileBuffers()`, which **blocks until the remote side reads pending data**. This is the exact Bug 4 deadlock.

The deadlock occurs during initial handshake: server sends 3 snapshot messages (state + settings + modes) via `WriteSafe()`, each triggering `FlushFileBuffers()`. Client sends 2 queries (settings + modes) via `SendCommandAsync()`, each triggering `FlushFileBuffers()`. Both sides flush before either starts reading → mutual deadlock.

The comment in the code claiming `Stream.Flush()` was a "no-op on PipeStream" was **incorrect** — directly contradicted by Bug 4's PowerShell proof.

**Fix:** Revert both sides to `bufferSize: 1` (the proven Bug 4 fix). The alleged "fragmentation" from byte-at-a-time writes was a misdiagnosis — `StreamReader.ReadLineAsync()` internally buffers and correctly reassembles fragmented writes into complete lines. The truncation observed in plugin logs was the BarRaider logger truncating long strings, not pipe message fragmentation.

**Status:** Fixed.

---

### Investigation Summary: Bugs 6 & 7 (Updated after 3 rounds)

**Bug 6 (Duplicate Triggers / Sluggish Feel):**
Three rounds of investigation established:
1. Plugin logs confirm the SD SDK sends exactly ONE `keyDown` per physical press — no duplicates
2. `KeyPressed` was changed from `async void` to synchronous `void` (SDK defines it as `void`)
3. Server-side 300ms debounce was removed — it was eating legitimate presses (plugin proves no duplicates)
4. The "sluggish feel" may partially stem from the pipe transport latency itself
5. **Remaining:** Button responsiveness is closer to other plugins after debounce removal but user reports it still doesn't feel identical. Needs further investigation if this feature is resumed.

**Bug 7 (Ask Triggers Dictate):**
Plugin logs definitively show:
- Constructor receives `settings count=2, pipelineType=ask` — settings load correctly
- `KeyPressed` sends `pipelineType=ask` on every press — plugin side is correct
- The issue is server-side: the app receives the command and may route it incorrectly, OR the first command was lost/eaten by the (now-removed) debounce
- **Status:** Should be fixed by debounce removal (Bug 6 fix). Needs re-testing.

---

## 16. Feature Status: SHELVED (2026-03-24)

**Decision:** Stream Deck integration shelved after 3 sessions of bug fixing. Connection and data pipeline work correctly, but button press responsiveness remains unusable.

**What works:**
- Named pipe IPC connection (bidirectional, reconnects automatically)
- State events flow correctly (Idle → Recording → Transcribing → Processing → Injecting → Idle)
- Settings sync (Raw, Streaming, Ducking, Engine toggles reflected in real-time)
- Modes list delivery to PI dropdown
- Constructor settings persistence (Ask/Dictate/Refine saved across restarts)

**What doesn't work:**
- Button presses require multiple attempts to register (user must "wrestle" the button)
- Toggle commands sent by plugin are silently dropped by the server (settings events show no change)
- First press on Ask button often triggers Dictate (server-side command routing issue)
- Overall feel is "a world away" from other Stream Deck plugins

**Root cause (suspected):** The issue is NOT the IPC transport — commands arrive at the server. The server-side command processing (`HandleTrigger`, `HandleToggle`) appears to silently drop or misroute commands. Needs server-side Serilog tracing to diagnose.

**How to re-enable:**
1. Uncomment `Services.GetRequiredService<Services.LocalApiServer>().Start();` in `App.xaml.cs` (line 154)
2. Rebuild DiktaMe.App
3. Rebuild + install Stream Deck plugin via `install-plugin.cmd`

**App-side footprint (safe to leave compiled):**
- `LocalApiServer.cs` — IPC server (self-contained, never starts without the uncommented line)
- `ApiCommand.cs` — command parser (only used by LocalApiServer)
- `ControlPanelViewModel.cs:70` — `ExternalStateChanged` event (declared but never raised — dead code)
- `LoadingViewModel.cs:440-514` — `TriggerPipeline()` method (only called by LocalApiServer)
- `App.xaml.cs:591` — DI registration (harmless, never resolved when Start isn't called)
