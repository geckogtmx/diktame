# SPEC_005: Elgato Stream Deck Integration

> **Status:** DRAFT
> **Date:** 2026-03-08
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
