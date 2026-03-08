# SPEC_005: Elgato Stream Deck Integration

> **Status:** DRAFT
> **Date:** 2026-03-07
> **Parent Specs:** `DEVELOPMENT_ROADMAP.md`

---

## 1. Overview & Value Proposition

A Stream Deck is not merely a collection of external hotkeys; it is a **dynamic physical dashboard** capable of two-way communication, state tracking, and telemetry display. 

Mapping dIKta.me's global hotkeys to a Stream Deck natively via the "System: Hotkey" action is already possible, but incredibly limiting. A native, value-centered Stream Deck plugin elevates the user experience by leveraging the hardware's full capabilities:

1. **Granular, Mode-Specific Triggers:** Bypass the globally selected dictation mode. Map physical buttons to specific custom modes (e.g., Button 1 = "Standard Dictation", Button 2 = "Developer Refactor", Button 3 = "Email Drafter").
2. **Visual State & Telemetry Display:** The LCD buttons act as a live monitor. They can dynamically show the pipeline state (Idle, Recording, Processing), display a live recording timer, or briefly flash the transcribed word count upon completion.
3. **Hardware Settings Toggles:** Map buttons to act as active toggle switches for core `AppSettings` (e.g., turning `RawModeOverride`, `StreamingEnabled`, or `AudioDucking` on/off). The button explicitly lights up or dims based on the app's real-time state.
4. **Multi-Action Workflows:** By exposing distinct actions to the Stream Deck software, users can sequence dIKta.me triggers with other plugins (e.g., *Press Button* -> *Mute Discord* -> *Turn Philips Hue light RED* -> *Start dIKta.me Dictation*).

---

## 2. The Action Catalog (Plugin Features)

The C# plugin will expose the following distinct "Actions" to the Stream Deck software:

### 2.1 The "Dictation Runner" Action
A button dedicated to running the dictation pipeline.
*   **Property Inspector (Settings):** Dropdown populated dynamically via IPC with all user-defined Modes from `DictationModeManager`. User selects a specific Mode, or "Use App Default".
*   **Visual Behavior:** 
    *   State 1 (Idle): Custom icon for the mode.
    *   State 2 (Recording): Icon changes to a red microphone; LCD text shows a live `00:00` recording timer.
    *   State 3 (Processing): Icon changes to a spinner/gear.
    *   State 4 (Success): Briefly displays `[Checkmark] 150 chars` before reverting to Idle.

### 2.2 The "Utility Pipeline" Action
A button dedicated to running specific non-dictation pipelines.
*   **Property Inspector:** Dropdown to select the pipeline: *Ask, Refine Voice, Refine Auto, Translate, Note, Chat*.
*   **Overrides:** Checkbox to override the global `AskOutputMode` (e.g., Force "Inject" instead of "Toast").

### 2.3 The "Settings Toggle" Action
A button that acts as a binary physical switch.
*   **Property Inspector:** Dropdown to select the setting to bind to:
    *   `GeneralSettings.RawModeOverride` (Bypass LLM)
    *   `GeneralSettings.StreamingEnabled` (WebSocket vs Batch)
    *   `AudioDucking.Enabled`
    *   `ActiveProfileName` (Cloud vs Local Engine)
*   **Visual Behavior:** Tracks the real-time state of `AppSettings`. If the user clicks the button, the setting toggles and the icon changes (Lit / Dim). If the user changes the setting inside the dIKta.me WinUI app, the Stream Deck button instantly updates to match.

### 2.4 The "Telemetry Monitor" Action (Passive)
A passive display key (no click action).
*   **Visual Behavior:** Subscribes to `MetricsCollector` data over IPC. Constantly displays the stats of the *last* run (e.g., "750ms STT | 1.2s LLM | Deepgram/OpenAI").

---

## 3. Architecture & Bidirectional IPC

To support this deep integration without muddying the WinUI 3 application layer, we introduce a dedicated API boundary.

### 3.1 `DiktaMe.V2.Api` Named Pipe Server
`DiktaMe.App` hosts an asynchronous `NamedPipeServerStream` (`PipeDirection.InOut`). 

*   **Why Named Pipes?** Zero firewall configuration, native to Windows, extremely fast, and can be isolated (`PipeAccessRights`) to the current user making it highly secure.
*   **Protocol:** Newline-delimited JSON payloads.

### 3.2 The IPC Data Contract
The data contract must handle bidirectional sync:

**Commands (Plugin → App):**
*   `{"action": "trigger_dictate", "modeId": "guid-here"}`
*   `{"action": "toggle_setting", "setting": "RawModeOverride", "value": true}`
*   `{"action": "query_config", "target": "modes"}`

**Events (App → Plugin):**
*   `{"event": "state_changed", "pipeline": "dictate", "state": "Recording"}`
*   `{"event": "telemetry_tick", "durationMs": 1500}` (Sent every second during recording to update the Stream Deck timer).
*   `{"event": "settings_synced", "RawModeOverride": true, "StreamingEnabled": false}` (Broadcast whenever `SettingsManager.UpdateAsync` completes, keeping toggle buttons in sync).

### 3.3 Core Codebase Refactoring
Currently, `LoadingViewModel.cs` intensely couples hotkeys to pipeline execution. This must be refactored:

1.  Extract execution logic from `LoadingViewModel` into a new `DiktaMe.Core.Pipeline.PipelineOrchestrator` singleton.
2.  The `PipelineOrchestrator` accepts execution requests (with optional `modeId` or overrides).
3.  Both `HotkeyManager` (local global hotkeys) and the new `LocalApiServer` (IPC requests from Stream Deck) call into the `PipelineOrchestrator` to execute workflows.

---

## 4. Implementation Details

### Phase 1: Core IPC & Orchestration (Tasks J.7 - J.8)
1. Write `PipelineOrchestrator` and decouple `LoadingViewModel`.
2. Implement `LocalApiServer.cs` in `DiktaMe.App\Services`.
3. Wire `SettingsManager.SettingsChanged` events to broadcast config updates over the pipe.
4. Wire `DictationPipeline.StateChanged` to broadcast execution states over the pipe.

### Phase 2: The Stream Deck Plugin (`DiktaMe.StreamDeck`) (Task J.9)
1. Create a `net8.0-windows` Console App.
2. Integrate `streamdeck-client-csharp` for WebSocket communication with the Stream Deck Software.
3. Build the `NamedPipeClientStream` connection logic, implementing aggressive auto-reconnect with exponential backoff if `DiktaMe.App` is closed or crashes.
4. If the pipe is disconnected, all Stream Deck buttons must cleanly fallback to an "Offline" warning icon.

### Phase 3: The Property Inspector (UI)
1. Build `property_inspector/index.html` using Stream Deck's standard CSS library.
2. Write the JavaScript necessary to request configuration data (Modes list) from the plugin (which passes it through from the IPC pipe) and populate HTML dropdowns dynamically.

### Phase 4: CI/CD & Deployment
1. Use Elgato's `DistributionTool.exe` in the GitHub Actions pipeline to compile the plugin and bundle resources (icons, HTML).
2. Distribute the `.sdPlugin` file as a supplementary download alongside the standard `diktame-setup.exe` installer.
