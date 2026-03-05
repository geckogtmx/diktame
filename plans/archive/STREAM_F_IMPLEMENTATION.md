# Stream F: WinUI 3 UI Layer — Implementation Plan

**Date:** 2026-02-17
**Predecessor:** All core streams complete (A–E, I.1/I.2-core/I.4/I.5-core)
**Build baseline:** 0 errors, 0 warnings, 343/343 tests passing

---

## Execution Order

| # | Task ID | Description | Files Created | Files Modified |
|---|---------|-------------|:---:|:---:|
| 1 | F.0a | Add `ControlPanelSettings` to `AppSettings` | 0 | 1 |
| 2 | F.0b | Shared converters + `SharedResources.xaml` | 9 | 1 |
| 3 | F.5 | `NotificationService` (toast + sound) | 1 | 1 |
| 4 | F.2 | Control Panel dashboard (replace MainWindow placeholder) | 3 | 2 |
| 5 | F.1a | Settings window shell (NavigationView + tab routing) | 2 | 2 |
| 6 | F.1b | General, AI Engine, Audio settings tabs | 6 | 0 |
| 7 | F.1c | Modes tab (master-detail, dual-profile) | 2 | 0 |
| 8 | F.1d | Privacy, API Keys tabs | 4 | 0 |
| 9 | F.1e | Ollama, Snippets, CP Config, About tabs | 8 | 0 |
| 10 | F.3 | Configuration Wizard (5-step first-run) | 8 | 1 |
| 11 | F.4 | Loading Screen | 2 | 1 |
| 12 | I.2-UI | Quick Chat overlay window | 2 | 1 |
| **Totals** | | | **~47** | **~10** |

---

## Task Details

### 1. F.0a — Add `ControlPanelSettings` to AppSettings

**Modify:** `src/DiktaMe.Core/Config/AppSettings.cs`

- New sealed record `ControlPanelSettings` with 4 bool properties (ShowModesRow, ShowActionsRow, ShowSessionStats, ShowPerformanceStats — all default `true`)
- Add `ControlPanelSettings ControlPanel` property to `AppSettings`
- Add `[JsonSerializable(typeof(ControlPanelSettings))]` to `AppSettingsContext`

**Commit:** `chore(config): add ControlPanelSettings to AppSettings [F.0]`

---

### 2. F.0b — Shared Converters + Theme Resources

**Create in `src/DiktaMe.App/Converters/`:**

| File | Converts |
|------|----------|
| `BoolToVisibilityConverter.cs` | `true` → `Visible`, `false` → `Collapsed` |
| `InverseBoolToVisibilityConverter.cs` | Inverted visibility |
| `BoolNegationConverter.cs` | `!bool` for `IsEnabled` bindings |
| `PipelineStateToColorConverter.cs` | `PipelineState` → `SolidColorBrush` |
| `PipelineStateToTextConverter.cs` | `PipelineState` → "READY"/"LISTENING"/etc. |
| `PrivacyLevelToStringConverter.cs` | Enum → display label |
| `NullToVisibilityConverter.cs` | `null` → `Collapsed` |
| `ApiKeyMaskConverter.cs` | "sk-abc...xyz" masking |

**Create:** `src/DiktaMe.App/Themes/SharedResources.xaml` — registers all converters as `StaticResource`

**Modify:** `src/DiktaMe.App/App.xaml` — merge `SharedResources.xaml`

**Commit:** `feat(ui): add shared converters and theme resources [F.0]`

---

### 3. F.5 — Notification Service

**Create:** `src/DiktaMe.App/Services/NotificationService.cs`
- `ShowToast(title, message, type)` via `Microsoft.Toolkit.Uwp.Notifications`
- `PlaySound(type)` for audio feedback
- `NotificationType` enum: Info, Success, Error, ModeChange

**Modify:** `src/DiktaMe.App/App.xaml.cs` — register singleton

**Commit:** `feat(ui): add NotificationService with toast and sound [F.5]`

---

### 4. F.2 — Control Panel Dashboard

**Create:**
- `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` — observable state, session/perf stats, quick toggles, mode commands
- `src/DiktaMe.App/Views/ControlPanelPage.xaml` + `.xaml.cs` — 6-row grid layout

**Modify:**
- `src/DiktaMe.App/MainWindow.xaml` — replace placeholder with `<ControlPanelPage/>`
- `src/DiktaMe.App/MainWindow.xaml.cs` — wire ViewModel from DI

**Layout:**
```
Row 0: Status indicator (ellipse + "READY"/"LISTENING"/etc.)
Row 1: Modes row — Standard/Prompt/Professional/RAW buttons
Row 2: Actions row — Sound/Cloud/+Key/Refine toggles
Row 3: Session stats — SESS / WORDS / WPM / TOK
Row 4: Performance stats — REC / TRANS / PROC / INJ
Row 5: Provider badges (STT name + LLM name)
```

All rows visibility-bound to `ControlPanelSettings` toggles.

**Commit:** `feat(ui): implement Control Panel dashboard [F.2]`

---

### 5. F.1a — Settings Window Shell

**Create:**
- `src/DiktaMe.App/Views/SettingsWindow.xaml` + `.xaml.cs`

NavigationView (left pane), 10 tabs, Frame for page content. 900×700 window. Singleton tracking in App.

**Modify:**
- `src/DiktaMe.App/Views/TrayIconViewModel.cs` — `OpenSettings()` → `App.Current.ShowSettings()`
- `src/DiktaMe.App/App.xaml.cs` — add `ShowSettings()`, add ViewModel DI registrations

**Commit:** `feat(ui): implement Settings window shell with NavigationView [F.1]`

---

### 6. F.1b — General, AI Engine, Audio Tabs

**Create (in `Views/Settings/` + `ViewModels/Settings/`):**

| Page | ViewModel | Key Controls |
|------|-----------|-------------|
| `GeneralSettingsPage` | `GeneralSettingsViewModel` | Language ComboBox, AutoStart toggle, SoundFeedback toggle, AdditionalKey ComboBox, TrailingSpace toggle |
| `AIEngineSettingsPage` | `AIEngineSettingsViewModel` | STT mode radio (cloud/local), LLM mode radio (cloud/local/skip), capability summary |
| `AudioSettingsPage` | `AudioSettingsViewModel` | Device ComboBox, MaxDuration radios, Ducking toggle + slider |

**Commit:** `feat(ui): add General, AI Engine, Audio settings tabs [F.1]`

---

### 7. F.1c — Modes Tab (Master-Detail)

**Create:**
- `src/DiktaMe.App/Views/Settings/ModesSettingsPage.xaml` + `.xaml.cs`
- `src/DiktaMe.App/ViewModels/Settings/ModesSettingsViewModel.cs`

Left: ListView of 8 modes. Right: profile toggle (0/1), SttProvider dropdown, LlmProvider dropdown, LlmModel text, PromptSlot ComboBox, UseLlm toggle.

**Commit:** `feat(ui): add Modes tab with dual-profile master-detail layout [F.1]`

---

### 8. F.1d — Privacy, API Keys Tabs

**Create:**

| Page | ViewModel | Key Controls |
|------|-----------|-------------|
| `PrivacySettingsPage` | `PrivacySettingsViewModel` | Level slider (0-3), PII toggle, retention NumberBox, Wipe button + confirmation |
| `ApiKeysSettingsPage` | `ApiKeysSettingsViewModel` | Per-provider PasswordBox + Test/Save/Delete + status |

**Commit:** `feat(ui): add Privacy and API Keys settings tabs [F.1]`

---

### 9. F.1e — Ollama, Snippets, CP Config, About Tabs

**Create:**

| Page | ViewModel | Key Controls |
|------|-----------|-------------|
| `OllamaSettingsPage` | `OllamaSettingsViewModel` | Status badge, version, health check, model dropdown, installed list, 412 Rescue InfoBar |
| `SnippetsSettingsPage` | `SnippetsSettingsViewModel` | ListView + Add/Edit/Delete, edit panel, counter "N/100" |
| `ControlPanelConfigPage` | `ControlPanelConfigViewModel` | 4 ToggleSwitches for row visibility |
| `AboutPage` | (no VM) | Static version/links/tech stack |

**Commit:** `feat(ui): add Ollama, Snippets, Control Panel Config, About tabs [F.1]`

---

### 10. F.3 — Configuration Wizard

**Create:**
- `src/DiktaMe.App/Views/WizardWindow.xaml` + `.xaml.cs`
- `src/DiktaMe.App/ViewModels/WizardViewModel.cs`
- `src/DiktaMe.App/Views/Wizard/WizardWelcomePage.xaml` + `.xaml.cs`
- `src/DiktaMe.App/Views/Wizard/WizardSttPage.xaml` + `.xaml.cs`
- `src/DiktaMe.App/Views/Wizard/WizardLlmPage.xaml` + `.xaml.cs`
- `src/DiktaMe.App/Views/Wizard/WizardTestPage.xaml` + `.xaml.cs`
- `src/DiktaMe.App/Views/Wizard/WizardReadyPage.xaml` + `.xaml.cs`

**Modify:** `src/DiktaMe.App/App.xaml.cs` — startup gate on `WizardCompleted`

Steps: Welcome → Choose STT → Choose LLM → Test (3s record + transcribe) → Ready

**Commit:** `feat(ui): implement Configuration Wizard [F.3]`

---

### 11. F.4 — Loading Screen

**Create:**
- `src/DiktaMe.App/Views/LoadingWindow.xaml` + `.xaml.cs`
- `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

ProgressRing + status text. Async init: settings → DB → snippets → Ollama check. Dismissed when ready.

**Modify:** `src/DiktaMe.App/App.xaml.cs` — show loading before wizard/main window

**Commit:** `feat(ui): implement Loading Screen [F.4]`

---

### 12. I.2-UI — Quick Chat Overlay

**Create:**
- `src/DiktaMe.App/Views/QuickChatWindow.xaml` + `.xaml.cs`
- `src/DiktaMe.App/ViewModels/QuickChatViewModel.cs`

~400×300, always-on-top, Escape-to-close. TextBox + Send + Mic. Binds to `ChatPipeline`. Singleton tracked in App.

**Modify:** `src/DiktaMe.App/App.xaml.cs` — hotkey handler, window tracking

**Commit:** `feat(ui): implement Quick Chat overlay window [I.2]`

---

## Key Implementation Patterns

1. **Thread marshalling** — Capture `DispatcherQueue.GetForCurrentThread()` in VM constructor; `TryEnqueue()` in all event handlers
2. **Immutable settings** — `with { }` operator → `SettingsManager.UpdateAsync()`
3. **Compile-time binding** — `{x:Bind ViewModel.Prop, Mode=OneWay}` everywhere
4. **Page DI** — Pages resolve VMs: `App.Current.Services.GetRequiredService<T>()`
5. **Window singletons** — Nullable field in App.xaml.cs, null on close

## Verification (per commit)

1. `dotnet build DiktaMe.sln` — 0 errors, 0 warnings
2. `dotnet test DiktaMe.sln` — 343+ tests pass
3. Manual launch verification for UI commits
