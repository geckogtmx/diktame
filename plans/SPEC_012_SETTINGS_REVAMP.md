# SPEC_012: Settings UI Revamp

> **Status:** Proposed
> **Component:** `DiktaMe.App` — Views/ViewModels/Settings
> **Goal:** Consolidate 13 disconnected navigation items into 8 structured, context-aware categories using a master-detail layout. Every existing setting field must survive migration; nothing is deleted from the data model.

---

## 1. Problem Statement

`SettingsWindow.xaml` has a left-side `NavigationView` with exactly **13 navigation items** (verified in XAML):

| Tag | Page class routed to |
|---|---|
| `general` | `GeneralSettingsPage` |
| `hotkeys` | `HotkeysSettingsPage` |
| `aiengine` | `AIEngineSettingsPage` |
| `dictationmodes` | `DictationModesSettingsPage` |
| `modes` | `ModesSettingsPage` |
| `audio` | `AudioSettingsPage` |
| `tts` | `TtsSettingsPage` |
| `privacy` | `PrivacySettingsPage` |
| `apikeys` | `ApiKeysSettingsPage` |
| `ollama` | `OllamaSettingsPage` |
| `snippets` | `SnippetsSettingsPage` |
| `controlpanel` | `ControlPanelConfigPage` |
| `about` | `AboutPage` |

**Additionally**, `AccountSettingsPage` exists but is NOT a nav item — it is accessed exclusively via a `UserPaneFooter` click in `SettingsWindow.xaml.cs:26-31`. It must be elevated to a nav item.

**Verified UX problems:**

1. **Overwhelm & Discoverability:** 13 top-level items for a settings window is too many.
2. **Scattered AI domains:** AI capabilities are split across *AI Engine*, *TTS*, *Ollama*, and *API Keys* — four separate nav items.
3. **Sound Feedback redundancy (confirmed in code):** `GeneralSettingsViewModel._soundFeedback` and `AudioSettingsViewModel._soundEnabled` both read and write the same field: `AppSettings.General.SoundFeedback`. Two pages control the same toggle.
4. **Inconsistent layout:** Most pages are flat single-column scroll lists. `ModesSettingsPage` and `DictationModesSettingsPage` already use a proven left-list + right-detail pattern that the other pages should adopt.

---

## 2. Current Files — Disposition Table

| Source file | Current nav tag | Disposition |
|---|---|---|
| `Views/Settings/GeneralSettingsPage.xaml` | `general` | **Refactor** — absorb ControlPanel; split into Application + Behavior sub-items |
| `ViewModels/Settings/GeneralSettingsViewModel.cs` | — | **Refactor** — remove SoundFeedback (moved to Hardware); add RawModeOverride, RefineVoiceMode |
| `Views/Settings/HotkeysSettingsPage.xaml` | `hotkeys` | **Delete** — content merged into Hardware & Hotkeys |
| `ViewModels/Settings/HotkeysSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into new `HardwareSettingsViewModel` |
| `Views/Settings/AIEngineSettingsPage.xaml` | `aiengine` | **Refactor** — convert to inner master-detail; absorb ApiKeys, Ollama, TTS |
| `ViewModels/Settings/AIEngineSettingsViewModel.cs` | — | **Refactor** — becomes sub-VM for STT section only |
| `Views/Settings/DictationModesSettingsPage.xaml` | `dictationmodes` | **Delete** — content becomes "Dictation Presets" sub-item under Workflows |
| `ViewModels/Settings/DictationModesSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into new `WorkflowsSettingsViewModel` |
| `Views/Settings/ModesSettingsPage.xaml` | `modes` | **Delete** — content becomes utility pipeline sub-items under Workflows |
| `ViewModels/Settings/ModesSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into new `WorkflowsSettingsViewModel` |
| `Views/Settings/AudioSettingsPage.xaml` | `audio` | **Delete** — content merged into Hardware & Hotkeys |
| `ViewModels/Settings/AudioSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into new `HardwareSettingsViewModel` |
| `Views/Settings/TtsSettingsPage.xaml` | `tts` | **Delete** — content merged into AI Engine |
| `ViewModels/Settings/TtsSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into refactored `AIEngineSettingsViewModel` |
| `Views/Settings/PrivacySettingsPage.xaml` | `privacy` | **Keep** — single column, no change needed |
| `ViewModels/Settings/PrivacySettingsViewModel.cs` | — | **Keep** |
| `Views/Settings/ApiKeysSettingsPage.xaml` | `apikeys` | **Delete** — content merged into AI Engine |
| `ViewModels/Settings/ApiKeysSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into refactored `AIEngineSettingsViewModel` |
| `Views/Settings/OllamaSettingsPage.xaml` | `ollama` | **Delete** — content merged into AI Engine |
| `ViewModels/Settings/OllamaSettingsViewModel.cs` | — | **Keep as sub-VM** — inject into refactored `AIEngineSettingsViewModel` |
| `Views/Settings/SnippetsSettingsPage.xaml` | `snippets` | **Keep** — grid/list layout, no structural change needed |
| `ViewModels/Settings/SnippetsSettingsViewModel.cs` | — | **Keep** |
| `Views/Settings/ControlPanelConfigPage.xaml` | `controlpanel` | **Delete** — content moved into General > Application |
| `Views/Settings/AccountSettingsPage.xaml` | *(footer only)* | **Keep** — add to nav items, remove footer-only access |
| `ViewModels/Settings/AccountSettingsViewModel.cs` | — | **Keep** |
| `Views/Settings/AboutPage.xaml` | `about` | **Keep** — single column, no change needed |
| `Views/SettingsWindow.xaml` | — | **Refactor** — reduce to 8 nav items, add `account` tag |
| `Views/SettingsWindow.xaml.cs` | — | **Refactor** — update routing switch, remove footer navigation handler |

**New files to create:**

| New file | Purpose |
|---|---|
| `Views/Settings/HardwareSettingsPage.xaml` | New host page for Hardware & Hotkeys master-detail |
| `ViewModels/Settings/HardwareSettingsViewModel.cs` | Aggregates `AudioSettingsViewModel` + `HotkeysSettingsViewModel` |
| `Views/Settings/WorkflowsSettingsPage.xaml` | New host page for Workflows & Modes master-detail |
| `ViewModels/Settings/WorkflowsSettingsViewModel.cs` | Aggregates `DictationModesSettingsViewModel` + `ModesSettingsViewModel` |

---

## 3. Proposed UI Architecture

**Left pane (main nav):** 8 items.

**Pattern for multi-section categories:** An inner `ListView` on the left half of the content area (the "master") drives a `ContentPresenter` or `Frame` on the right half (the "detail"). This is already working in `ModesSettingsPage` and `DictationModesSettingsPage` — reuse that pattern as the template.

**Pattern for single-section categories:** Plain `ScrollViewer` page, no inner list. Used by Snippets, Privacy, Account, About.

---

## 4. Complete Field Mapping

Every `AppSettings` field is placed exactly once. Fields marked **⚠ currently unplaced** are present in `AppSettings` but absent from the current UI — they must be added during this revamp.

---

### Nav Item 1: General (Double Column — Application | Behavior)

**Sub-item: Application**

| Field | `AppSettings` path | Notes |
|---|---|---|
| UI Language | `General.UiLanguage` | Show restart warning on change. Currently in `GeneralSettingsPage`. |
| Auto-start with Windows | `General.AutoStart` | Currently in `GeneralSettingsPage`. |
| Show Modes Row | `ControlPanel.ShowModesRow` | **Moved from** `ControlPanelConfigPage`. |
| Show Actions Row | `ControlPanel.ShowActionsRow` | **Moved from** `ControlPanelConfigPage`. |
| Show Session Stats | `ControlPanel.ShowSessionStats` | **Moved from** `ControlPanelConfigPage`. |
| Show Performance Stats | `ControlPanel.ShowPerformanceStats` | **Moved from** `ControlPanelConfigPage`. |

**Sub-item: Behavior**

| Field | `AppSettings` path | Notes |
|---|---|---|
| Transcription Language | `General.Language` | Currently in `GeneralSettingsPage`. Keep next to STT settings logically. |
| Additional Key after injection | `General.AdditionalKey` | Options: None / Enter / Tab / Space. Currently in `GeneralSettingsPage`. |
| Trailing Space after injection | `General.TrailingSpace` | Currently in `GeneralSettingsPage`. |
| Raw Mode Override | `General.RawModeOverride` | ⚠ **Currently unplaced in UI.** Global "skip LLM for all dictation" toggle. |
| Refine Voice Mode | `General.RefineVoiceMode` | ⚠ **Currently unplaced in UI.** When true, Refine uses voice instruction; when false, uses text-selection auto mode. |

---

### Nav Item 2: Hardware & Hotkeys (Double Column — 3 sub-items)

**Sub-item: Microphone & Recording**

| Field | `AppSettings` path | Notes |
|---|---|---|
| Input Device | `Audio.DeviceName` | Populated from `AudioDeviceManager.GetInputDevices()`. Moved from `AudioSettingsPage`. |
| Max Recording Duration | `Audio.MaxDurationSeconds` | ComboBox: 30s / 60s / 120s / Unlimited. Moved from `AudioSettingsPage`. |
| Enable Audio Ducking | `AudioDucking.Enabled` | Moved from `AudioSettingsPage`. |
| Duck Level % | `AudioDucking.DuckLevelPercent` | Slider 0–100. Shown only when ducking enabled. Moved from `AudioSettingsPage`. |

**Sub-item: Sound Feedback**

> **Important:** `General.SoundFeedback` is the master toggle. It is currently written by BOTH `GeneralSettingsViewModel` and `AudioSettingsViewModel`. After this revamp it must exist in exactly one place: this sub-item. Remove it from the refactored `GeneralSettingsViewModel`.

| Field | `AppSettings` path | Notes |
|---|---|---|
| Enable Sound Feedback | `General.SoundFeedback` | Master toggle. **Remove from `GeneralSettingsPage` and `AudioSettingsPage` after migration.** |
| Start Sound | `Sound.StartSound` | ComboBox of available WAV stems + preview button. Moved from `AudioSettingsPage`. |
| Stop Sound | `Sound.StopSound` | ComboBox + preview button. Moved from `AudioSettingsPage`. |
| Utility Sound | `Sound.UtilitySound` | ComboBox + preview button. Moved from `AudioSettingsPage`. |

**Sub-item: Keyboard Shortcuts**

| Field | `AppSettings` path | Default |
|---|---|---|
| Dictate | `Hotkeys.Dictate` | Ctrl+Alt+D |
| Refine | `Hotkeys.Refine` | Ctrl+Alt+R |
| Ask | `Hotkeys.Ask` | Ctrl+Alt+A |
| Translate | `Hotkeys.Translate` | Ctrl+Alt+T |
| Oops (undo last) | `Hotkeys.Oops` | Ctrl+Alt+V |
| Note | `Hotkeys.Note` | Ctrl+Alt+N |
| Chat | `Hotkeys.Chat` | Ctrl+Alt+C |
| Read Selection | `Hotkeys.ReadSelection` | Ctrl+Alt+Q |

Recordable-binding UI already exists in `HotkeysSettingsPage` — keep the view/VM, just host it in the new page.

---

### Nav Item 3: AI Engine (Double Column — 4 sub-items)

**Sub-item: API Keys (Cloud Services)**

Five providers, each with: key input field, status label (has key / no key), Save button, Delete button. Logic lives in `ApiKeysSettingsViewModel` — reuse as-is.

| Provider | Storage |
|---|---|
| OpenAI | `SecureStorage` |
| Anthropic | `SecureStorage` |
| Gemini | `SecureStorage` |
| Deepgram | `SecureStorage` |
| Inworld | `SecureStorage` |

**Sub-item: Speech-to-Text**

| Field | `AppSettings` path | Notes |
|---|---|---|
| STT Mode | `ModeProfiles` (SttProvider across all profiles) | Cloud (Deepgram) vs Local (Whisper). Currently in `AIEngineSettingsPage`. |
| *Cloud Deepgram section* | | Visible when STT Mode = Cloud |
| Deepgram Model | `Deepgram.Model` | "nova-3" / "nova-2". |
| Punctuate | `Deepgram.Punctuate` | |
| Dictation | `Deepgram.Dictation` | ⚠ **Missing from original spec.** Spoken punctuation commands ("comma" → ,). Requires Punctuate or SmartFormat enabled. Disable toggle automatically when both are off. |
| Smart Format | `Deepgram.SmartFormat` | |
| Replacements | `Deepgram.Replacements` | Multi-line TextBox, one "find:replace" per line. |
| Streaming | `General.StreamingEnabled` | ⚠ **Stored in `General`, not `Deepgram`.** Displayed here logically. Existing `AIEngineSettingsViewModel` already handles this correctly (line 150: `DeepgramStreaming = s.General.StreamingEnabled`). |
| *Local Whisper section* | | Visible when STT Mode = Local |
| Whisper Model | `AppSettings.WhisperModel` | tiny / base / small (recommended) / medium / large / turbo. Show auto-download UI when selected model is not downloaded. |

**Sub-item: Local LLM (Ollama)**

Entire `OllamaSettingsViewModel` is reused as-is. Fields:

| Field | `AppSettings` path | Notes |
|---|---|---|
| Service Status | runtime | Version, status text, running model count. |
| 412 Rescue | runtime | Show rescue message + fallback model if Ollama returns 412. |
| VRAM Monitor | runtime | GPU name, VRAM total/used/%, RAM summary, last inference speed, running models list. |
| Default Model | `AppSettings.OllamaModel` | ComboBox populated from installed models. |
| Keep-alive | `AppSettings.OllamaKeepAlive` | Options: 5m / 10m / 30m / 1h / 2h. |
| Auto-warmup on startup | `AppSettings.OllamaAutoWarmup` | ⚠ **Missing from original spec.** Pre-loads default model into VRAM at startup. |
| Installed Models list | runtime | Disk usage total, per-model name/size. |
| Library Search & Pull | runtime | Search box → results list → pull with progress bar. |
| Base URL | `AppSettings.OllamaBaseUrl` | Advanced. Default: `http://localhost:11434`. |
| NumCtx | `AppSettings.OllamaNumCtx` | Advanced. Options: 2048 / 4096 / 8192 / 16384. |

**Sub-item: Text-to-Speech**

Entire `TtsSettingsViewModel` is reused as-is. Fields:

| Field | `AppSettings` path | Notes |
|---|---|---|
| Master TTS Enable | `Tts.Enabled` | Off by default (opt-in). |
| Provider | `Tts.Provider` | kokoro / deepgram / inworld / openai |
| Voice | `Tts.VoiceId` | Per-provider voice list |
| Speed | `Tts.Speed` | 0.5–2.0 |
| Volume % | `Tts.VolumePercent` | 0–100 |
| Max Speech Words | `Tts.MaxSpeechWords` | 0 = unlimited |
| Speak Ask Responses | `Tts.SpeakAskResponses` | |
| Speak Chat Responses | `Tts.SpeakChatResponses` | |
| Speak Translations | `Tts.SpeakTranslations` | |
| Speak Notifications | `Tts.SpeakNotifications` | |
| Duck During Playback | `Tts.DuckDuringPlayback` | Lowers system volume while TTS speaks |
| Kokoro Model Variant | `Tts.KokoroModelVariant` | gpu / fp32 / fp16 / int8. Shown only when provider = kokoro. |
| Kokoro Download UI | runtime | Download button + progress. Shown only when provider = kokoro. |
| Test Button | runtime | Plays sample phrase through selected provider + voice |

> **DirectML Note:** `Tts.KokoroUseGpu` exists in `AppSettings` but is **inert** — DirectML ConvTranspose is broken for Kokoro ONNX (SPEC_KOKORO_GPU blocked). Do not surface this toggle in the UI until that spec is unblocked.

---

### Nav Item 4: Workflows & Modes (Double Column — inner list)

The inner list has these items. Dictation Behaviors and Dictation Presets are new host entries; the pipeline items reuse `ModesSettingsViewModel` sections.

**Inner list items:** Dictation Behaviors | Dictation Presets | Ask | Refine (Auto) | Refine (Verbal) | Translate | Note | Chat

---

**Sub-item: Dictation Behaviors**

| Field | `AppSettings` path | Notes |
|---|---|---|
| Additional Key after injection | `General.AdditionalKey` | *(Also in General > Behavior — pick one canonical location, suggest here)* |
| Trailing Space | `General.TrailingSpace` | *(Same — pick one canonical location)* |
| Raw Mode Override | `General.RawModeOverride` | Global "skip LLM" override |
| Refine Voice Mode | `General.RefineVoiceMode` | Voice instruction vs text-selection auto mode |

> **Placement decision for developer:** `AdditionalKey` and `TrailingSpace` are currently in `GeneralSettingsPage`. You must pick one canonical home — either here or General > Behavior — and remove the duplicate. Suggest keeping them here since they are output behavior, not app configuration.

---

**Sub-item: Dictation Presets**

Reuse `DictationModesSettingsViewModel` entirely as-is. This is a CRUD list of user-defined presets.

> ⚠ **Correction from original spec:** There are no hardcoded "Fast", "Smart", or "Writer" presets. `AppSettings.DictationModes` is `List<DictationMode>` — fully user-defined. The app creates one preset named **"Standard"** on first run. Users can create, rename, and delete any preset.

Per-preset fields (right pane):

| Field | Source |
|---|---|
| Title | `DictationMode.Name` |
| Cloud Model | `DictationMode.CloudProfile.LlmModel` |
| Cloud System Prompt | `DictationMode.CloudProfile.SystemPrompt` |
| Use LLM (Cloud) | `DictationMode.CloudProfile.UseLlm` |
| Local Model | `DictationMode.LocalProfile.LlmModel` |
| Local System Prompt | `DictationMode.LocalProfile.SystemPrompt` |
| Use LLM (Local) | `DictationMode.LocalProfile.UseLlm` |

---

**Sub-items: Ask / Refine (Auto) / Refine (Verbal) / Translate / Note / Chat**

Reuse `ModesSettingsViewModel` sections as-is. This VM already uses the inner-list pattern. Fields per mode:

*All modes (common right-pane fields):*
- Cloud System Prompt override (`PipelineConfig.CloudProfile.SystemPrompt`)
- Cloud Model override (`PipelineConfig.CloudProfile.LlmModel`)
- Local System Prompt override (`PipelineConfig.LocalProfile.SystemPrompt`)
- Local Model override (`PipelineConfig.LocalProfile.LlmModel`)

*Ask — additional fields:*

| Field | `AppSettings` path |
|---|---|
| Output Mode | `General.AskOutput` (enum: ToastOnly / ClipboardOnly / InjectOnly / ClipboardAndToast) |

*Note — additional fields:*

| Field | `AppSettings` path |
|---|---|
| File Path | `Note.FilePath` |
| Use LLM Processing | `Note.UseLlmProcessing` |
| Timestamp Format | `Note.TimestampFormat` |
| Live Preview | Computed from `Note.TimestampFormat` (renders a sample timestamp — already in `ModesSettingsViewModel._notePreviewText`) |

> ⚠ **Legacy field:** `AppSettings.NotesFilePath` is a duplicate root-level property left over from before `NoteSettings` existed. Bind the UI to `Note.FilePath`, not the root `NotesFilePath`. Do not surface the root field.

*Chat — additional fields:*

| Field | `AppSettings` path | Notes |
|---|---|
| Font Size | `Chat.FontSize` | Currently in `ModesSettingsViewModel` |
| Window Opacity | `Chat.WindowOpacity` | Currently in `ModesSettingsViewModel` |
| Theme | `Chat.Theme` | System / Light / Dark. Currently in `ModesSettingsViewModel`. |
| Forget on Close | `Chat.ForgetOnClose` | Currently in `ModesSettingsViewModel` |
| Max History Messages | `Chat.MaxHistoryMessages` | Currently in `ModesSettingsViewModel` |
| Show Timestamps | `Chat.ShowTimestamps` | Currently in `ModesSettingsViewModel` |
| Enable Markdown | `Chat.EnableMarkdown` | Currently in `ModesSettingsViewModel` |
| Always on Top | `Chat.AlwaysOnTop` | ⚠ **Currently unplaced in UI.** |
| Default Model ID | `Chat.DefaultModelId` | ⚠ **Currently unplaced in UI.** Null = use profile default. |
| Default System Prompt | `Chat.DefaultSystemPrompt` | ⚠ **Currently unplaced in UI.** Null = use built-in default. |
| Web Search (Gemini grounding) | `Chat.WebSearchEnabled` | ⚠ **Currently unplaced in UI.** Only affects Gemini provider. |
| Window Width / Height | `Chat.WindowWidth` / `Chat.WindowHeight` | ⚠ **Currently unplaced in UI.** Consider whether to expose or keep as implicit resize-and-save. |

---

### Nav Item 5: Text Snippets (Single Column)

No structural change. `SnippetsSettingsPage` + `SnippetsSettingsViewModel` kept as-is. Remove the nav tag `snippets` from old nav and add it to new nav at this position.

---

### Nav Item 6: Privacy (Single Column)

No structural change. `PrivacySettingsPage` + `PrivacySettingsViewModel` kept as-is.

| Field | `AppSettings` path | Notes |
|---|---|---|
| Privacy Level | `Privacy.Level` | `PrivacyLevel` enum: Ghost / Stats / Balanced / Full. Implement as a 4-stop Slider or segmented control — it is an enum, not a continuous range. |
| PII Scrubber | `Privacy.PiiScrubEnabled` | |
| History Retention | `Privacy.HistoryRetentionDays` | |
| Wipe All Data | — | Destructive action button with confirmation dialog |

---

### Nav Item 7: Account (Single Column)

**Currently:** Accessed only via `UserPaneFooter` click — not a `NavigationView` menu item.
**Change required:** Add `<NavigationViewItem Tag="account">` to `SettingsWindow.xaml` and add `"account" => typeof(Settings.AccountSettingsPage)` to the routing switch in `SettingsWindow.xaml.cs`. Keep or remove the footer navigation — user preference, but the footer shortcut can remain as a convenience.

Content: `AccountSettingsPage` + `AccountSettingsViewModel` unchanged.

---

### Nav Item 8: About (Single Column)

No structural change. `AboutPage` kept as-is.

---

## 5. Implementation Details & Gotchas

### ViewModel Aggregation Pattern

New "host" ViewModels (`HardwareSettingsViewModel`, `WorkflowsSettingsViewModel`, and the refactored `AIEngineSettingsViewModel`) should not duplicate logic. They should **instantiate existing domain VMs as injected constructor parameters** and expose them as properties:

```csharp
// Example pattern — HardwareSettingsViewModel
public sealed partial class HardwareSettingsViewModel : ObservableObject
{
    public AudioSettingsViewModel Audio { get; }
    public HotkeysSettingsViewModel Hotkeys { get; }
    // ...
}
```

The XAML detail pane then binds directly to the sub-VM property. No data duplication.

### Inner List Navigation Pattern

Both `ModesSettingsPage` and `DictationModesSettingsPage` already implement the inner-list + detail-pane pattern. Use one of them as the XAML template for `HardwareSettingsPage`, `WorkflowsSettingsPage`, and the refactored `AIEngineSettingsPage`.

Do **not** use `DataTemplateSelector` or `ContentPresenter` type-switching for the detail pane — the existing pattern uses a single large detail area with `Visibility`-gated sections driven by `SelectedIndex`. This avoids the WinUI 3 `x:Bind` converter-in-Window gotcha (see MEMORY.md).

### WinUI 3 Gotchas (apply throughout)

- `x:Bind` is **not** supported on `Run.Text` — use separate `TextBlock` elements.
- `InfoBar.ActionButton` accepts only a single `ButtonBase`, not a panel with multiple buttons.
- `x:Bind` converters inside a `Window` (not Page) generate CS1503 — use computed ViewModel properties instead.
- Cross-thread `ObservableCollection` updates must use `DispatcherQueue.TryEnqueue()`.
- `NullReferenceException` in a property-change callback during load = silent native crash (exit 127). Always guard `LoadFromSettings()` with `_isLoading = true/false`.

### SoundFeedback Deduplication

`AudioSettingsViewModel.Save()` already writes `General.SoundFeedback` (line 166). `GeneralSettingsViewModel` also manages it. After migration:
1. Remove `SoundFeedback` property and its `partial void OnSoundFeedbackChanged` from `GeneralSettingsViewModel`.
2. Remove the `SoundFeedback` control from `GeneralSettingsPage.xaml`.
3. The canonical owner is `HardwareSettingsViewModel` via `AudioSettingsViewModel`.

### Routing Update in SettingsWindow.xaml.cs

After the revamp, the routing switch reduces from 13 cases to 8:

```csharp
Type? pageType = tag switch
{
    "general"    => typeof(Settings.GeneralSettingsPage),
    "hardware"   => typeof(Settings.HardwareSettingsPage),   // NEW
    "aiengine"   => typeof(Settings.AIEngineSettingsPage),   // REFACTORED
    "workflows"  => typeof(Settings.WorkflowsSettingsPage),  // NEW
    "snippets"   => typeof(Settings.SnippetsSettingsPage),
    "privacy"    => typeof(Settings.PrivacySettingsPage),
    "account"    => typeof(Settings.AccountSettingsPage),    // ELEVATED
    "about"      => typeof(Settings.AboutPage),
    _ => typeof(Settings.GeneralSettingsPage),
};
```

Remove the `UserFooter.NavigateToAccountRequested` handler (or keep as a convenience shortcut — just make sure it doesn't fight with the nav selection state).

---

## 6. Task Log

Tasks are ordered by dependency. Each can be a standalone commit.

---

### T1 — Reduce SettingsWindow nav items to 8
**Files:** `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`
**Work:**
- Replace the 13 `<NavigationViewItem>` blocks with 8 (general, hardware, aiengine, workflows, snippets, privacy, account, about).
- Update `NavView_SelectionChanged` routing switch to match.
- Add `account` tag. Remove the `UserPaneFooter.NavigateToAccountRequested` handler or keep it and just call `NavView.SelectedItem = accountItem` so nav state stays consistent.
- The app will crash on startup until the new pages exist (T2, T3) — do T1 last, or do it in a feature branch.

**Done when:** App launches, all 8 nav items are present, clicking each item navigates without exception (pages may be empty stubs at this point).

---

### T2 — Create HardwareSettingsPage + HardwareSettingsViewModel
**Files:** `HardwareSettingsPage.xaml`, `HardwareSettingsPage.xaml.cs`, `HardwareSettingsViewModel.cs`
**Work:**
- `HardwareSettingsViewModel` takes `AudioSettingsViewModel` and `HotkeysSettingsViewModel` as constructor-injected dependencies and exposes them as public properties.
- `HardwareSettingsPage.xaml` implements the inner-list + detail pattern (copy structure from `ModesSettingsPage.xaml`). Inner list: "Microphone & Recording", "Sound Feedback", "Keyboard Shortcuts".
- Microphone & Recording detail: device ComboBox, duration ComboBox, ducking toggle + level slider — bind to `Audio.*` properties.
- Sound Feedback detail: enable toggle, start/stop/utility ComboBoxes + preview buttons — bind to `Audio.*` properties. **Do not add a separate SoundFeedback toggle here** — it is already `Audio.SoundEnabled` in `AudioSettingsViewModel`.
- Keyboard Shortcuts detail: copy the existing `HotkeysSettingsPage.xaml` content verbatim, bind to `Hotkeys.*` properties.
- Register `HardwareSettingsViewModel` in DI container (`App.xaml.cs`).

**Done when:** Navigating to Hardware shows all three sub-items. Settings save and reload correctly.

---

### T3 — Create WorkflowsSettingsPage + WorkflowsSettingsViewModel
**Files:** `WorkflowsSettingsPage.xaml`, `WorkflowsSettingsPage.xaml.cs`, `WorkflowsSettingsViewModel.cs`
**Work:**
- `WorkflowsSettingsViewModel` takes `DictationModesSettingsViewModel` and `ModesSettingsViewModel` as constructor-injected dependencies.
- Inner list has 8 items: Dictation Behaviors, Dictation Presets, Ask, Refine (Auto), Refine (Verbal), Translate, Note, Chat.
- "Dictation Behaviors" detail pane: `General.AdditionalKey`, `General.TrailingSpace`, `General.RawModeOverride`, `General.RefineVoiceMode`. Add these 4 fields to the ViewModel (they can stay in `GeneralSettingsViewModel` and be wired here via the settings manager, or a new lightweight VM can own them — developer's choice).
- "Dictation Presets" detail pane: delegate entirely to `DictationModesSettingsViewModel` CRUD UI (copy from `DictationModesSettingsPage.xaml`).
- Utility pipeline detail panes (Ask–Chat): delegate entirely to `ModesSettingsViewModel` sections (copy from `ModesSettingsPage.xaml`).
- Add missing Chat fields to `ModesSettingsViewModel`: `AlwaysOnTop`, `DefaultModelId`, `DefaultSystemPrompt`, `WebSearchEnabled`. Wire to `AppSettings.Chat.*`. `WindowWidth`/`WindowHeight` may be left as implicit resize-and-save (no explicit UI control needed).
- Register `WorkflowsSettingsViewModel` in DI.

**Done when:** All 8 inner list items navigate correctly. Dictation preset CRUD works. Utility pipeline settings save and reload. New Chat fields are visible and persist.

---

### T4 — Refactor AIEngineSettingsPage to master-detail with Ollama + TTS + API Keys
**Files:** `AIEngineSettingsPage.xaml`, `AIEngineSettingsViewModel.cs`
**Work:**
- Convert `AIEngineSettingsPage.xaml` from a single-column scroll to the inner-list + detail pattern.
- Inner list: API Keys, Speech-to-Text, Local LLM, Text-to-Speech.
- `AIEngineSettingsViewModel` becomes a thin host that injects `ApiKeysSettingsViewModel`, `OllamaSettingsViewModel`, `TtsSettingsViewModel`, and retains STT logic itself (or extracts a `SttSettingsViewModel`).
- API Keys detail pane: copy content from `ApiKeysSettingsPage.xaml`.
- STT detail pane: existing `AIEngineSettingsPage` content (STT mode toggle, Whisper section, Deepgram section). Add `DeepgramSettings.Dictation` field (spoken punctuation commands toggle — currently bound in the VM but verify it is exposed in XAML).
- Ollama detail pane: copy content from `OllamaSettingsPage.xaml`.
- TTS detail pane: copy content from `TtsSettingsPage.xaml`.
- Do **not** add `Tts.KokoroUseGpu` to the UI (DirectML blocked — see MEMORY.md).

**Done when:** All 4 inner items navigate correctly. API key save/delete works. STT mode switch works. Ollama status + model pull works. TTS test play works.

---

### T5 — Refactor GeneralSettingsPage
**Files:** `GeneralSettingsPage.xaml`, `GeneralSettingsViewModel.cs`
**Work:**
- Convert to inner-list + detail: "Application" | "Behavior".
- Application detail: UiLanguage (+ restart warning), AutoStart, ControlPanel toggles (ShowModesRow, ShowActionsRow, ShowSessionStats, ShowPerformanceStats).
- Behavior detail: AdditionalKey, TrailingSpace, RawModeOverride, RefineVoiceMode.
- Remove `SoundFeedback` from this VM and this page (it lives in Hardware now — T2 must be done first).
- Remove `Language` (Transcription Language) from this page — it is now in Hardware > Microphone & Recording or AI Engine > STT depending on final placement decision. **Pick one and note it in a code comment.**
- Wire `ControlPanelSettings` fields to `GeneralSettingsViewModel` (they are currently in a separate `ControlPanelConfigPage` with its own VM — check if a `ControlPanelViewModel` exists and absorb it, or just add the 4 fields directly to `GeneralSettingsViewModel`).
- Add `RawModeOverride` and `RefineVoiceMode` observable properties + save logic.

**Done when:** General page has two sub-items. All 4 ControlPanel toggles persist. RawModeOverride and RefineVoiceMode are visible and persist. SoundFeedback is gone from this page.

---

### T6 — Delete obsolete pages
**Files to delete:**
- `Views/Settings/HotkeysSettingsPage.xaml` + `.cs`
- `Views/Settings/AudioSettingsPage.xaml` + `.cs`
- `Views/Settings/TtsSettingsPage.xaml` + `.cs`
- `Views/Settings/ApiKeysSettingsPage.xaml` + `.cs`
- `Views/Settings/OllamaSettingsPage.xaml` + `.cs`
- `Views/Settings/DictationModesSettingsPage.xaml` + `.cs`
- `Views/Settings/ModesSettingsPage.xaml` + `.cs`
- `Views/Settings/ControlPanelConfigPage.xaml` + `.cs`

**Work:**
- Before deleting, confirm each page's content has been fully migrated (T2–T5 done).
- Remove any DI registrations for deleted ViewModels that have been replaced (not the sub-VMs that were kept — just host VMs that are gone).
- Build must pass at 0 errors after deletion.

**Done when:** `dotnet build DiktaMe.sln` passes cleanly. No references to deleted types remain anywhere.

---

### T7 — Localization strings
**Files:** `Assets/Strings/en/Resources.resw` (and `es-MX` equivalent)
**Work:**
- Add string keys for all new nav items and sub-items: `Nav_Hardware`, `Nav_Workflows`, sub-item labels for inner lists.
- Add string keys for newly surfaced fields: `Settings_General_RawModeOverride`, `Settings_General_RefineVoiceMode`, `Settings_Chat_AlwaysOnTop`, `Settings_Chat_DefaultModel`, `Settings_Chat_DefaultSystemPrompt`, `Settings_Chat_WebSearch`, `Settings_Ollama_AutoWarmup`.
- Follow existing naming convention: `Settings_{Page}_{Field}` for labels, `Settings_{Page}_{Field}_Tip` for tooltips if needed.

**Done when:** App runs in both `en` and `es-MX` without missing-key placeholders in any new UI.

---

## 7. Success Criteria

### Structural
- [ ] `SettingsWindow.xaml` has exactly 8 `NavigationViewItem` elements.
- [ ] Account page is reachable via nav item (not only via footer).
- [ ] All 13 original nav tags have been removed from the nav; their content is accessible within the 8 new categories.

### Field coverage
- [ ] Every `AppSettings` property has a corresponding UI control. The following were previously unplaced — verify each:
  - [ ] `General.RawModeOverride`
  - [ ] `General.RefineVoiceMode`
  - [ ] `AppSettings.OllamaAutoWarmup`
  - [ ] `Chat.AlwaysOnTop`
  - [ ] `Chat.DefaultModelId`
  - [ ] `Chat.DefaultSystemPrompt`
  - [ ] `Chat.WebSearchEnabled`
  - [ ] `Deepgram.Dictation` (spoken punctuation — verify it is wired in XAML, not just in VM)
- [ ] `General.SoundFeedback` is controlled from exactly one location (Hardware > Sound Feedback).
- [ ] `AppSettings.NotesFilePath` (legacy root field) is not surfaced anywhere. UI binds to `Note.FilePath` only.
- [ ] `Tts.KokoroUseGpu` is not surfaced (blocked — see DirectML note).

### Functional
- [ ] All settings save immediately on change (no Save button required) and survive app restart.
- [ ] Dictation preset CRUD (create, rename, delete) works from within Workflows & Modes.
- [ ] Whisper model download progress is visible in AI Engine > STT.
- [ ] Kokoro model download progress is visible in AI Engine > TTS.
- [ ] Ollama model pull progress is visible in AI Engine > Local LLM.
- [ ] API key save/delete round-trips correctly for all 5 providers.
- [ ] Hotkey recording UI works within Hardware > Keyboard Shortcuts.

### Build & runtime
- [ ] `dotnet build DiktaMe.sln -c Release` — 0 errors, 0 warnings.
- [ ] `dotnet test DiktaMe.sln` — all existing tests pass (no regressions).
- [ ] Settings window opens without a crash. Navigating to each of the 8 items and all inner sub-items produces no unhandled exceptions in the log.

---

## 8. Testing Notes

The Settings layer is primarily UI + ViewModel with no network/audio I/O, so tests focus on ViewModel save/load logic.

### Existing test coverage
- `SettingsManagerTests` covers `LoadAsync`/`UpdateAsync`/`SanitizeNulls`. These tests must continue to pass unchanged — no data model changes in this spec.

### New VM tests to add

For each new "host" ViewModel (`HardwareSettingsViewModel`, `WorkflowsSettingsViewModel`) and any new fields added to existing VMs:

1. **Load round-trip:** Construct VM with a `SettingsManager` seeded with known values → assert observable properties match.
2. **Save on change:** Change a property → assert `SettingsManager.Current` reflects the new value.
3. **Deduplication guard:** After migrating `SoundFeedback` out of `GeneralSettingsViewModel`, add a test that confirms `GeneralSettingsViewModel` no longer exposes a `SoundFeedback` property (compilation failure is sufficient; no runtime test needed).
4. **New Chat fields:** `ChatSettingsLoad_MapsAllFields` — seed `Chat` with non-default values for all 10 properties; confirm `ModesSettingsViewModel` exposes them all correctly.
5. **New General fields:** `GeneralSettings_RawModeOverride_SavesAndReloads`, `GeneralSettings_RefineVoiceMode_SavesAndReloads`.

### Manual smoke test checklist (for each PR)

- [ ] Open Settings → navigate to all 8 items → no crash.
- [ ] Hardware > Sound Feedback: toggle enable, change start sound, click preview — sound plays.
- [ ] Hardware > Keyboard Shortcuts: click a binding, press a key combo, confirm it saves.
- [ ] AI Engine > API Keys: enter a fake key, save, reload settings window — key shows as "has key".
- [ ] AI Engine > STT: switch Cloud ↔ Local — correct section appears/hides.
- [ ] Workflows > Dictation Presets: create a new preset, rename it, delete it.
- [ ] Workflows > Chat: change AlwaysOnTop, restart app, reopen Settings — value persisted.
- [ ] General > Application: toggle a ControlPanel HUD checkbox, close settings, verify the HUD row appears/disappears on the main window.
