# SPEC_009_FIXES: Post-Testing Issues from Scenario 1

> **Source**: Manual testing of SPEC_009 Scenario 1 (Full Cloud, no Ollama)
> **Date**: 2026-03-09
> **Status**: 15/16 complete (FIX-1 deferred to SPEC_008, FIX-16 API Keys skip needs manual verification)

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

**Resolution**: Fixed in commit `02a9280`. Root cause was `TotalMs` only covering pipeline processing (STT + LLM + injection) — it excluded recording time. The comment on line 480 falsely claimed recording was included. RAW mode exposed this because pipeline-only time was ~500ms, giving absurd 1000+ WPM. Fix: `wallClockMs = RecordingMs + TotalMs` for the WPM denominator. Verified with same phrase dictated in both modes: LLM=123.8 WPM, RAW=154.2 WPM (avg 139 WPM — realistic dictation speed).

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

**Problem**: The single Cloud/Local toggle only wrote `ActiveProfileName` — it never touched `ModeProfiles.SttProvider`, which is what `PipelineFactory` actually reads. Users couldn't switch STT between Whisper and Deepgram from the Control Panel.

**Resolution**: Replaced the single LOCAL/CLOUD toggle with two independent toggles (STT + LLM). Each writes directly to all 12 ModeProfiles slots (6 modes x 2 profiles). UI restructured from 5 to 6 columns with label/toggle/state vertical layout. Auth badge shows LOC (both local), API (both cloud), or MIX (hybrid). Window width 369→420.

**Files modified**:
- `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` — `IsLocalStt`/`IsLocalLlm` replace `IsLocalMode`, new handlers write to ModeProfiles
- `src/DiktaMe.App/Views/ControlPanelPage.xaml` — 6-column grid, label/toggle/state layout
- `src/DiktaMe.App/MainWindow.xaml.cs` — Width 369→420
- `src/DiktaMe.App/Strings/en/Resources.resw` — 14 new keys (6 static labels + 8 state values), 10 old combined keys removed
- `src/DiktaMe.App/Strings/es-MX/Resources.resw` — Same changes in Spanish

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

## FIX-13: Wizard LLM Step — Ollama Validation + Model Pull

**Problem**: `WizardLlmPage` recorded the user's "Local (Ollama)" choice but did zero validation. No check if Ollama was installed, no model pull. Users finished the wizard thinking everything was configured, then got runtime failures when Ollama was offline or the model wasn't pulled.

**Resolution**: Same pattern as FIX-7/FIX-9 (Whisper download). Added:
1. **`OllamaManager.PullModelAsync()`** — streams `POST /api/pull` NDJSON response with `IProgress<OllamaPullProgress>` for real-time download progress
2. **`WizardLlmPage` status panel** — ProgressBar + status text, shown when "Local" radio selected
3. **`BeforeLeaveStep` callback** — on Next click:
   - Calls `OllamaManager.CheckAsync()` to detect status
   - **Offline**: Shows error, blocks Next
   - **VersionTooOld**: Shows version warning, blocks Next
   - **ModelNotPulled**: Starts `PullModelAsync()` with progress bar, blocks Next until complete
   - **Ready**: Proceeds to next step
4. **Cancellation**: Pull cancelled on page unload or radio switch to Cloud

**Files modified**:
- `src/DiktaMe.Core/System/OllamaManager.cs` — Added `PullModelAsync()` + `OllamaPullProgress` record
- `src/DiktaMe.App/Views/Wizard/WizardLlmPage.xaml` — Added `OllamaPanel` with ProgressBar + status
- `src/DiktaMe.App/Views/Wizard/WizardLlmPage.xaml.cs` — Full rewrite with BeforeLeaveStep pattern
- `src/DiktaMe.App/Strings/en/Resources.resw` — 7 new keys (`Wizard_Llm_Ollama*`, `Wizard_Llm_Pull*`)
- `src/DiktaMe.App/Strings/es-MX/Resources.resw` — Same 7 keys in Spanish
- `tests/DiktaMe.Core.Tests/System/OllamaManagerTests.cs` — 6 new tests for PullModelAsync

---

## FIX-14: Wizard LLM Step — Ollama Auto-Install via winget + Default Model gemma3:4b

**Problem**: FIX-13 added Ollama detection and model pull, but when Ollama is **not installed**, the wizard just shows "Ollama is not running" and blocks the user with no actionable path forward. User is stuck.

**Also**: Default Ollama model was `gemma3` (latest, ~3.9GB). Changed to `gemma3:4b` (~3.3GB) for faster download and lower VRAM.

**Resolution**: Added Ollama auto-install via `winget` and model default change:
1. **`OllamaManager.InstallViaWingetAsync()`** — runs `winget install Ollama.Ollama --silent`, captures output, reports progress
2. **`OllamaManager.IsWingetAvailableAsync()`** — checks `winget --version` availability
3. **`OllamaManager.StartOllamaAsync()`** — starts `ollama serve` if installed but not running, waits for API readiness
4. **Wizard "Install Ollama" button** — shown when Offline, triggers winget install with progress
5. **Fallback**: If winget unavailable, opens browser to `ollama.com/download` + shows Retry button
6. **Default model**: `AppSettings.OllamaModel` changed from `"gemma3"` to `"gemma3:4b"`, added to `models.json`

**Complete wizard LLM flow**:
- Ollama running + model ready → "Ready" → Next proceeds
- Ollama running + model missing → Next → PullModelAsync with progress → complete
- Ollama installed but not running → "Not running, start it and click Retry" → Retry re-checks
- Ollama NOT installed → [Install Ollama] button → winget install → re-check → model pull
- winget unavailable → opens browser + Retry button

**Files modified**:
- `src/DiktaMe.Core/System/OllamaManager.cs` — Added install/start helpers
- `src/DiktaMe.Core/Config/AppSettings.cs` — Default `OllamaModel` → `"gemma3:4b"`
- `src/DiktaMe.Core/System/models.json` — Added `gemma3:4b` entry
- `src/DiktaMe.App/Views/Wizard/WizardLlmPage.xaml` — Install button + manual link UI
- `src/DiktaMe.App/Views/Wizard/WizardLlmPage.xaml.cs` — Install flow + Retry logic
- `src/DiktaMe.App/Strings/en/Resources.resw` — 7 new keys + 1 updated
- `src/DiktaMe.App/Strings/es-MX/Resources.resw` — Same in Spanish

---

## FIX-15: Local Mode Polish — Auto-Start, Keep-Alive, GPU Log, Settings Downloads

**Problem**: After FIX-14 (wizard auto-install), several friction points remained for local mode:
1. **Ollama doesn't auto-start on app launch** — if user reboots or closes Ollama, next launch fails for local LLM
2. **Keep-alive hardcoded to `"10m"`** — no user control over how long Ollama keeps models in VRAM
3. **No GPU confirmation log** — first inference doesn't clearly log whether GPU or CPU is being used
4. **Whisper model change in Settings doesn't download** — selecting a larger model without downloading it causes `FileNotFoundException`
5. **No Ollama install from Settings page** — users who skip the wizard can't install Ollama later

**Resolution**:
1. **Auto-start**: `LoadingViewModel` Step 4b now calls `OllamaManager.StartOllamaAsync()` when LLM=ollama but Ollama is offline
2. **Keep-alive setting**: Added `AppSettings.OllamaKeepAlive` property (default `"10m"`), parameterized in `OllamaProvider` constructor, ComboBox UI on `OllamaSettingsPage` (5m/10m/30m/1h/2h)
3. **First-inference GPU log**: `OllamaProvider` logs tok/s + GPU/CPU/BORDERLINE assessment on first inference
4. **Whisper download**: `AIEngineSettingsViewModel.OnWhisperModelIndexChanged()` now checks if model exists, triggers download with progress bar if missing
5. **Ollama install**: `OllamaSettingsPage` shows Install button when Offline, reuses FIX-14 winget + browser fallback flow

**Files modified**:
- `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` — Auto-start Ollama if offline + LLM is local
- `src/DiktaMe.Core/Config/AppSettings.cs` — Added `OllamaKeepAlive` property
- `src/DiktaMe.Core/LLM/OllamaProvider.cs` — `keepAlive` param, first-inference GPU log
- `src/DiktaMe.App/App.xaml.cs` — Keep-alive HttpClient for DI singleton
- `src/DiktaMe.App/Views/Settings/OllamaSettingsPage.xaml` — Keep-alive ComboBox + Install button
- `src/DiktaMe.App/ViewModels/Settings/OllamaSettingsViewModel.cs` — KeepAliveIndex + install flow
- `src/DiktaMe.App/Views/Settings/AIEngineSettingsPage.xaml` — Whisper download progress UI
- `src/DiktaMe.App/ViewModels/Settings/AIEngineSettingsViewModel.cs` — Download on model change
- `src/DiktaMe.App/Strings/en/Resources.resw` — 10 new localization keys
- `src/DiktaMe.App/Strings/es-MX/Resources.resw` — Same 10 keys in Spanish

---

## FIX-16: LLMProviderFactory Caching + Wizard Fixes (5x Ollama Latency Improvement)

**Problem**: Three issues found during manual testing of the local wizard path:
1. **Ollama latency ~3000ms per call** — `PipelineFactory.GetProviders()` called `_llmFactory.CreateProvider()` on every dictation, creating a new `OllamaProvider` + `HttpClient` each time. New TCP connection to localhost:11434 added ~2500ms overhead (connection setup + Ollama context reload). V1 used `requests.Session()` with persistent keep-alive connections.
2. **Language step "Back" bug** — Going back after selecting a language and picking a different one didn't change — kept first selection. `ApplyLanguageAsync()` had an `if (!= "en")` guard.
3. **API Keys step shown on local path** — Step 4 showed empty panels with no input fields when both STT + LLM were set to local.

**Resolution**:
1. **Provider caching**: Added `ConcurrentDictionary<string, ILLMProvider>` cache to `LLMProviderFactory`. `CreateProvider()` returns cached instances via `GetOrAdd()` keyed by `"{type}:{effectiveModel}"`. Ollama providers get `ConnectionClose = false` for HTTP keep-alive. **Result: LLM latency dropped from ~3000ms to ~550ms (5x improvement). Total pipeline: ~3500ms → ~1100ms warm.**
2. **Language fix**: Removed `if (!= "en")` guard — `SetLanguage()` now always called, handles Back→re-select scenarios
3. **API Keys skip**: Added `NeedsApiKeys()` helper. Both `GoNextAsync()` and `GoBack()` auto-skip step 4 when no cloud providers selected.
4. **Phased install messages**: Changed `OllamaManager.InstallViaWingetAsync()` from buffered `ReadToEndAsync()` to line-by-line `ReadLineAsync()`. Parses winget output to report "Downloading..." → "Installing..." → "Starting..."

**Files modified**:
- `src/DiktaMe.Core/Config/LLMProviderFactory.cs` — ConcurrentDictionary cache, `ResolveModel()` extraction, `CreateOllamaProvider()` with keep-alive
- `src/DiktaMe.App/App.xaml.cs` — Keep-alive HttpClient for DI singleton OllamaProvider
- `src/DiktaMe.App/ViewModels/WizardViewModel.cs` — Language fix + `NeedsApiKeys()` + step skip logic
- `src/DiktaMe.Core/System/OllamaManager.cs` — Phased install messages via stdout line parsing

**Latency verification** (from logs):
- Before: LLM ~3000ms, total pipeline ~3500ms
- After: LLM ~550ms, total pipeline ~1100ms (warm calls)
- First "cold" call: ~3900ms (Ollama model loading), all subsequent < 1.2s
- Sub-1.2-second fully local dictation confirmed

---

## Task Log

| # | Fix | Status | Notes |
|---|-----|--------|-------|
| 1 | Wizard: Trial → Wallet terminology | Deferred | Depends on SPEC_008 Wallet design |
| 2 | Wizard: Add Language step | Done | Bilingual step 0 (EN+ES), `WizardLanguagePage` |
| 3 | Wizard: Remove Skip on final step | N/A | No Skip button exists — only Back + Next/Finish |
| 4 | Default Refine = Auto | Done | `RefineVoiceMode = false` in AppSettings, loaded by ControlPanelVM |
| 5 | Preload default prompts | Done | All prompts populated from `PromptDefaults` constants |
| 6 | WPM telemetry garbage on first run | Done | Guard at `ControlPanelViewModel.cs:481` + formula fix: `RecordingMs + TotalMs` (wall-clock) instead of `TotalMs` only (pipeline-only). Verified: LLM=124 WPM, RAW=154 WPM for same phrase. |
| 7 | Whisper download in wizard STT step | Done | Download UI in `WizardSttPage`, blocks Next until complete |
| 8 | Hotkey double-subscription | Done | Unsubscribe before re-subscribing in `InitializeHotkeys()` |
| 9 | Download on Next click, not radio | Done | `BeforeLeaveStep` callback pattern in WizardViewModel |
| 10 | Cloud/Local toggle ignores STT | Done | Split into 2 toggles (STT + LLM), each writes to ModeProfiles. 6-col layout, auth badge LOC/API/MIX. |
| 11 | Whisper download auto-advances | Done | Changed `return true` → `return false` after download; user sees completion, clicks Next again |
| 12 | Wizard won't show on fresh install | Done | `_suppressSave` guard in `ControlPanelViewModel` prevents premature `settings.json` creation |
| 13 | Wizard: Ollama validation + model pull | Done | `OllamaManager.PullModelAsync()` + `WizardLlmPage` BeforeLeaveStep: check→pull→progress. Blocks Next when offline. |
| 14 | Wizard: Ollama auto-install via winget | Done | winget install + fallback to browser. Default model → `gemma3:4b`. |
| 15 | Local mode polish (auto-start, keep-alive, GPU log, Settings downloads) | Done | Ollama auto-start on launch, keep-alive setting, first-inference GPU log, Whisper download + Ollama install in Settings |
| 16 | LLMProviderFactory caching + wizard fixes | Done | Provider caching (5x Ollama latency fix), language Back bug, API Keys skip, phased install messages |
