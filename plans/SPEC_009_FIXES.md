# SPEC_009_FIXES: Post-Testing Issues from Scenario 1

> **Source**: Manual testing of SPEC_009 Scenario 1 (Full Cloud, no Ollama)
> **Date**: 2026-03-09
> **Status**: 9/12 complete (FIX-1 deferred to SPEC_008, FIX-10 open, FIX-11 done, FIX-12 done)

---

## FIX-1: Wizard Step 0 — Replace "Trial" with "Wallet" Terminology

**Problem**: Wizard mentions "Free 15,000 words" tied to the old Trial system. Trial has been removed in favor of a unified Wallet (SPEC_008). All users get a preset credit balance they can spend.

**Required Changes**:
- Rename the 3 onboarding options:
  1. **"Test with free Wallet credits"** — Limited features, no API keys needed
  2. **"Bring Your Own Keys (BYOK)"** — Full features, user provides Deepgram/Gemini/etc. API keys
  3. **"Local"** — Whisper + Ollama, fully offline
- Remove all "Trial" / "15,000 words" wording from the wizard
- Add links/guidance for obtaining API keys — link to dIKta.me docs site (not directly to Deepgram/Gemini, since provider options may change)
- Update localization strings (en + es-MX)

**Files to investigate**:
- `src/DiktaMe.App/ViewModels/WizardViewModel.cs`
- `src/DiktaMe.App/Views/Wizard/WizardAuthPage.xaml` (or equivalent Step 0 page)
- `src/DiktaMe.App/Strings/en/Resources.resw`
- `src/DiktaMe.App/Strings/es-MX/Resources.resw`

---

## FIX-2: Add Language Selection as Wizard Step 1

**Problem**: No language step exists in the wizard. Users need to select their dictation language early so the rest of the wizard can be shown in their preferred language.

**Resolution**: Implemented. `WizardLanguagePage.xaml` + `.xaml.cs` added as Step 0 (bilingual EN/ES). `WizardViewModel.ApplyLanguageAsync()` switches UI via `Localizer.Get().SetLanguage()` and persists to `AppSettings.General.Language/UiLanguage`. Wizard is now 7 steps total.

---

## FIX-3: Remove "Skip" Option from Final Wizard Step

**Problem**: The last wizard step (Step 6, Cloud-only path) shows a Skip option. The final step should only have "Back" and "Finish" — there's nothing to skip to.

**Resolution**: Non-issue. `WizardWindow.xaml` only has Back + Next/Finish buttons. No Skip button exists in the current implementation. The wizard uses `UpdateNavState()` to toggle button text between "Next" and "Finish" based on step position.

---

## FIX-4: Default Refine Mode Should Be "Auto", Not "Voice"

**Problem**: On fresh installs, Refine mode defaults to Voice. It should default to Auto (text selection only, no audio recording required).

**Resolution**: Already fixed. `AppSettings.GeneralSettings.RefineVoiceMode` defaults to `false` (Auto mode). `ControlPanelViewModel.LoadFromSettings()` reads this value and applies it at line 497. The field initializer `_isRefineVoice = true` is overridden by `LoadFromSettings` before the UI renders.

---

## FIX-5: Preload Default System Prompts for Dictation Modes

**Problem**: The 3 base system prompts (for the default dictation preset's Cloud and Local profiles) are missing on fresh install. Users should get working presets out of the box that they can customize later.

**Resolution**: Already fixed. `DictationModeDefaults.CreateDefaultPresets()` populates all `SystemPrompt` fields from `PromptDefaults` constants (Dictate, DictatePrompt, DictateProfessional). `CreateBuiltInUtilityPipelines()` does the same for all 7 utility pipelines (ask, refine_auto, refine_instruction, refine, translate, note, chat). All cloud profiles use `ModelName = "gemini-2.5-flash"`, all local profiles use `ModelName = null`.

---

## FIX-6: Words/Min Telemetry Shows Garbage on First Dictation

**Problem**: The WORD/MIN reading in the Control Panel shows absurd 5-digit numbers on the first dictation. Corrects itself on the second run.

**Resolution**: Already fixed. `ControlPanelViewModel.RefreshSessionStats()` at line 458 has guard: `if (lastResult is not null && lastResult.TotalMs > 0 && lastResult.WordCount > 0)`. Needs manual re-verification — if the bug persists despite the guard, the issue may be in how `TotalMs` is computed (not a zero-guard issue but a value correctness issue).

---

## FIX-7: Whisper Model Download in Wizard STT Step

**Problem**: When user selects "Local (Whisper)" for STT in wizard, the ~466MB model only downloads after the wizard on the loading screen. User has no feedback during the wizard that their choice requires a large download.

**Resolution**: Added download progress UI directly into `WizardSttPage`. When user selects "Local":
1. Progress panel appears with `ProgressBar` + status `TextBlock`
2. Next button disabled until download completes (via `CanGoNext` binding on `WizardWindow.xaml`)
3. `WhisperProvider.DownloadModelAsync()` called with `CancellationToken` (cancels if user switches to Cloud)
4. On success: "Whisper model ready" shown, Next re-enabled
5. On failure: Error shown in red, Next re-enabled (user can switch to Cloud)
6. If model already downloaded: Immediately shows "already downloaded", Next stays enabled
7. `LoadingViewModel` Step 4a kept as safety net (will detect "already downloaded" and skip)

**Files modified**:
- `src/DiktaMe.App/Views/Wizard/WizardSttPage.xaml` — Added `DownloadPanel` StackPanel
- `src/DiktaMe.App/Views/Wizard/WizardSttPage.xaml.cs` — Download logic with CancellationToken + DispatcherQueue
- `src/DiktaMe.App/Views/WizardWindow.xaml` — Added `IsEnabled="{x:Bind ViewModel.CanGoNext}"` on Next button
- `src/DiktaMe.App/Strings/en/Resources.resw` — 4 new keys: `Wizard_Stt_Downloading/DownloadComplete/DownloadFailed/ModelReady`
- `src/DiktaMe.App/Strings/es-MX/Resources.resw` — Same 4 keys in Spanish

---

## FIX-8: Hotkey Double-Subscription — Recording Immediately Stops

**Problem**: After wizard completes, pressing Ctrl+Alt+D starts recording but it immediately stops (~30ms). Whisper transcribes empty audio `""`, pipeline aborts on empty transcription. LLM never runs.

**Root Cause**: `LoadingViewModel` is a DI **singleton**. When `WizardWindow.OnWizardCompleted()` creates a new `LoadingWindow`, it resolves the same `LoadingViewModel` instance. `InitializeHotkeys()` runs again and calls `_hotkeyManager.HotkeyPressed += OnHotkeyPressed` a **second** time without unsubscribing first. Result: one keypress fires `OnHotkeyPressed` twice. The first invocation starts recording, the second finds `_isRecording == true` and calls `StopRecordingAsync()`.

**Evidence** (from `diktame_20260309.log`):
```
[DBG] HotkeyManager: "Dictate" pressed           ← ONE WM_HOTKEY message
[INF] Hotkey pressed: "Dictate"                   ← First handler invocation → starts recording
[INF] Hotkey pressed: "Dictate"                   ← Second handler invocation → stops recording
[INF] AudioRecorder: started ...
[INF] Hotkey "Dictate": stopping active recording ← 30ms after start
[INF] WhisperProvider (small): transcribed in 19ms — ""
[INF] DictationPipeline: empty transcription — aborting
```

**Fix**: Unsubscribe before subscribing in `InitializeHotkeys()`:
```csharp
_hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
_hotkeyManager.RegistrationFailed -= OnHotkeyRegistrationFailed;
_hotkeyManager.HotkeyPressed += OnHotkeyPressed;
_hotkeyManager.RegistrationFailed += OnHotkeyRegistrationFailed;
```

**File**: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` line 217

---

## FIX-9: Whisper Download Should Start on Next Click, Not Radio Selection

**Problem**: Selecting the "Local (Whisper)" radio button immediately started downloading the ~466MB model. User had no way to cancel or reconsider — accidental clicks triggered a large download.

**Fix**: Moved download trigger from `SttRadio_SelectionChanged` to `BeforeLeaveStep` callback (new `WizardViewModel.BeforeLeaveStep` property). Download now starts when user clicks Next, not when radio is selected. Panel shows "Model will be downloaded when you click Next" as preview.

**Files**:
- `src/DiktaMe.App/ViewModels/WizardViewModel.cs` — Added `BeforeLeaveStep` callback, called from `GoNextAsync()`
- `src/DiktaMe.App/Views/Wizard/WizardSttPage.xaml.cs` — Rewrote to use `BeforeLeaveStep`
- `src/DiktaMe.App/Strings/en/Resources.resw` — Added `Wizard_Stt_DownloadPending` key
- `src/DiktaMe.App/Strings/es-MX/Resources.resw` — Same key in Spanish

---

## FIX-10: Cloud/Local Toggle Does Not Affect STT Provider

**Problem**: The Cloud/Local toggle switch in the Control Panel does not affect the STT provider. When toggled to "Cloud", Whisper (local) is still used for transcription instead of switching to Deepgram. The toggle currently only switches the LLM between Gemini (cloud) and Ollama (local).

**Impact**: Users who select "Cloud" mode expect both STT and LLM to use cloud providers. Currently only LLM switches.

**Potential Fix**: Consider separate Cloud/Local toggles for STT and LLM independently, or make the single toggle affect both subsystems. Needs design decision.

**Status**: Open — noted for future fix.

---

## FIX-11: Whisper Download Auto-Advances Without Showing Completion

**Problem**: When selecting Local (Whisper) STT and clicking Next, the download runs from 0% and then immediately auto-advances to the next step without showing the user that the download completed. User has no confirmation that the model is ready.

**Root Cause**: `OnBeforeLeaveStepAsync()` returned `true` after download completed, which told `GoNextAsync()` to advance immediately. The "Download Complete" status text was set via `DispatcherQueue.TryEnqueue()` but the page was already navigating away.

**Fix**: Changed `return true` → `return false` after successful download. The user now sees the "Download Complete" message and clicks Next again to proceed. The second click hits `whisper.IsModelDownloaded` → `return true` → advances normally.

**File**: `src/DiktaMe.App/Views/Wizard/WizardSttPage.xaml.cs` — line 132

---

## FIX-12: Wizard Won't Show on Fresh Install

**Problem**: Deleting `%APPDATA%\DiktaMe` and launching the app did not trigger the first-run wizard. Instead, the app went straight to the main window.

**Root Cause**: `ControlPanelViewModel` constructor ran before `LoadingViewModel.InitializeAsync()`. Its `LoadFromSettings()` set `IsRefineVoice` from `true` (field initializer) to `false` (default), triggering `OnIsRefineVoiceChanged` → `UpdateAsync()` → wrote `settings.json` prematurely. When `LoadAsync()` ran, it found the file, entered the existing-file branch, and Migration 8 set `WizardCompleted = true`.

**Fix**: Added `_suppressSave` field to `ControlPanelViewModel`. Set to `true` around both `LoadFromSettings()` call sites (constructor + `OnSettingsChanged`). All 5 `On*Changed` handlers guard their `UpdateAsync()` call with `if (!_suppressSave)`. UI updates (label text, RefineMode, etc.) still run during load.

**File**: `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`

**Verified**: Wizard shows on fresh install (no folder), does not show on subsequent launches.

---

## Task Log

| # | Fix | Status | Notes |
|---|-----|--------|-------|
| 1 | Wizard: Trial → Wallet terminology | Deferred | Depends on SPEC_008 Wallet design |
| 2 | Wizard: Add Language step | Done | Bilingual step 0 (EN+ES), `WizardLanguagePage` |
| 3 | Wizard: Remove Skip on final step | N/A | No Skip button exists — only Back + Next/Finish |
| 4 | Default Refine = Auto | Done | `RefineVoiceMode = false` in AppSettings, loaded by ControlPanelVM |
| 5 | Preload default prompts | Done | All prompts populated from `PromptDefaults` constants |
| 6 | WPM telemetry garbage on first run | Done | Guard at `ControlPanelViewModel.cs:458` — re-verify manually |
| 7 | Whisper download in wizard STT step | Done | Download UI in `WizardSttPage`, blocks Next until complete |
| 8 | Hotkey double-subscription | Done | Unsubscribe before re-subscribing in `InitializeHotkeys()` |
| 9 | Download on Next click, not radio | Done | `BeforeLeaveStep` callback pattern in WizardViewModel |
| 10 | Cloud/Local toggle ignores STT | Open | Toggle doesn't affect Whisper — always uses Whisper regardless of position. May need separate Cloud/Local toggles for STT and LLM independently. |
| 11 | Whisper download auto-advances | Done | Changed `return true` → `return false` after download; user sees completion, clicks Next again |
| 12 | Wizard won't show on fresh install | Done | `_suppressSave` guard in `ControlPanelViewModel` prevents premature `settings.json` creation |
