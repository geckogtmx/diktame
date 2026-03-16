# Developer Handoff

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 968 passing locally (479 on CI — DPAPI/Clipboard/Audio/Whisper tests skipped on runners) |
| **Build** | 0 errors, 0 warnings |
| **CI** | Passing on main |
| **Branch** | main |
| **Website** | Deployed on Vercel (dikta.me), Root Directory = `website` |

## Completed Streams

| Stream | Summary |
|--------|---------|
| **A-E** | Git repo, solution scaffold, publish config, CancellationToken, Config, Data, Security |
| **F** | WinUI 3 UI Layer — all 12 tasks |
| **G** | 689 unit tests + CI/CD pipeline |
| **I** | SnippetManager, AudioDucker, ChatPipeline, OllamaManager |
| **J** | CRUD Dictation Modes — all 7 tasks |
| **K** | OAuth & Trial Credits — K.1-K.7 (open bugs below) |
| **L** | Deepgram Streaming — L.1-L.5 committed. L.6-L.7 (Flux) deferred. |
| **SPEC_007** | Chat Feature Upgrade — 14/14 tasks complete (committed) |
| **SPEC_009** | Local Mode E2E + Wizard Fixes — Phases A-G complete, FIX-1 through FIX-16 (15/17 done; FIX-1 unblocked by SPEC_008, FIX-17 TTS wizard step pending) |
| **SPEC_011** | Ollama Management Hub — Core API, search service, Settings UI, E2E warmup, 22 new tests |
| **DOCS_V2** | Exhaustive User documentation (Features & Settings), integrated natively into the Next.js Website via Markdown |
| **SPEC_003 A–G** | TTS: Core infra, Kokoro local, Read Selection hotkey, pipeline hooks, cloud providers, Settings UI + Control Panel toggle, Phase G polish + E2E bugfixes. 282 new tests. **All 40 tasks complete. E2E verified.** |
| **SPEC_KOKORO_GPU** | **BLOCKED** — DirectML ConvTranspose incompatibility (ONNX Runtime 1.22.0). GPU variant + UI variant reorder kept. NuGet reverted to KokoroSharp.CPU. 5 new tests. |
| **Settings Rework** | Gemini TTS, per-preset trailing space, "When to Speak" relocation, local model selector removal, mute detection, conversational TTS notifications, note context capture. 8 features in one session. |

## Open Bugs (Stream K)

1. **App UI doesn't update after sign-in** — `StatusChanged` may not fire if `/api/trial/status` fails for new users.
2. **Website "Sign Up" shows Coming Soon** — Vercel env var `NEXT_PUBLIC_COMING_SOON=true` still set. Delete it in Vercel dashboard.
3. **Trial counter page blank** — depends on Bug 1 + Supabase Edge Function returning proper trial records.

## Resolved Bugs (SPEC_011)

4. ~~**NullReferenceException on dictation — `LLMProviderFactory.CreateOllamaProvider`**~~ ✅ Fixed — null-coalesce defaults on `baseUrl`, `keepAlive`, `numCtx` in `LLMProviderFactory.CreateOllamaProvider`.
5. ~~**Free-text TextBox corrupted OllamaModel setting**~~ ✅ Fixed — removed "Or type model name" TextBox; model selection now exclusively via ComboBox dropdown of installed models. Added `OnSelectedModelIndexChanged` to sync ComboBox → SelectedModel → settings.
6. ~~**Model Library Install button too risky**~~ ✅ Fixed — replaced Install button with "View" link opening `ollama.com/library/{model}` in browser.
7. ~~**Ollama Settings page empty on open**~~ ✅ Fixed — auto-check health on `Page.Loaded` to populate model list and status.

## Resolved: Startup Crash (SPEC_003 Phase F)

**Root cause**: `settings.json` had `"Tts":null` — the JSON deserializer overwrites the `= new()` default initializer with `null`. Then `ControlPanelViewModel.LoadFromSettings()` accessed `settings.Tts.Enabled`, throwing a `NullReferenceException` during a WinUI UI-thread property change notification. WinUI's native XAML binding system intercepts such exceptions and crashes the process (exit code 127), bypassing ALL managed exception handlers including `UnhandledException`.

**Fix**: Added `SanitizeNulls()` in `SettingsManager.LoadAsync()` — null-coalesces all 11 settings sub-objects with `?? new()` after deserialization. Also added `UnhandledException` handler in `App.xaml.cs` as defensive measure.

**Key lesson**: Any new `AppSettings` sub-object property is vulnerable to this if a user's existing `settings.json` has the property set to `null` (or doesn't have it at all and a migration writes `null`). The `SanitizeNulls` method now covers all sub-objects.

## Resolved: Audio Ducking Not Finding App Sessions

**Root cause**: `GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)` returned only ONE endpoint — Chrome/Edge/Spotify sessions on other endpoints were invisible. **Fix**: Replaced with `EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)` to iterate ALL active render devices and their sessions. Ducking now works for both recording and TTS playback.

## Resolved: TTS ReadSelection Text Capture

**Root cause**: Two bugs — (1) `CaptureSelection` sent Ctrl+C while Alt was still held from Ctrl+Alt+Q hotkey, OS combined into Ctrl+Alt+C firing the Chat hotkey instead; (2) HWND was captured on UI thread (after dispatch delay) instead of hotkey thread. **Fix**: Added `WaitForModifierRelease()` before Ctrl+C in `CaptureSelection`, moved `GetCurrentForegroundWindow()` to `OnHotkeyPressed` (hotkey thread), reordered sound/capture in ReadSelection. Ducking restore race also fixed with `didDuck` flag.

## Resolved: Audio Ducking Fade Duration

**Implemented**: `DuckAsync()` and `RestoreAsync()` with linear volume interpolation over configurable `RampDownMs` (default 500ms, 0 = instant). New "Fade Duration" slider in Audio Settings (0–2000ms, step 100ms). All recording and TTS ducking paths use ramped transitions. Instant `Duck()`/`Restore()` kept for event handlers and `finally` safety nets.

## Known Issues (SPEC_011)

- **Settings corruption from TextBox bug may persist** — users who typed in the old TextBox may have `OllamaModel` set to a partial/invalid string in `settings.json`. Fix: open Ollama Settings → select correct model from dropdown.

## Open Issues

- **App quit stalling after TTS**: `AppWindow.Closing` handler cancels close unconditionally — `Application.Current.Exit()` gets blocked. Needs `_isExiting` flag to bypass cancellation during shutdown.
- **Test beep ducks live audio**: One test instantiates a real AudioDucker and ducks live audio sessions (YouTube volume drops during test run). Needs mocking or environment guard.

## Current Work

**Next 10 Steps** defined in `DEVELOPMENT_ROADMAP.md` (top of file). Priority order:
1. **FIX-17**: TTS wizard step (spec + test matrix ready in `SPEC_009_WIZARD_FLOW.md`)
2. **FIX-1**: Wallet terminology (unblocked — SPEC_008 COMPLETE)
3. **L.5**: Streaming UI toggle (manual test only)
4. **H.1**: Installer — only hard blocker before V2.0 ships
5–10. **SPEC_015 Modules Sprint** — plugin infra → Vision → Connectors → Memory → Meetings

After step 4, V2.0 is releasable. Modules ship as incremental updates.

## Recent Session: Settings Rework + Gemini TTS + UX Improvements (2026-03-15)

### Feature 1: Gemini TTS Cloud Provider

Added Google Gemini as a 5th TTS provider (alongside Kokoro, Deepgram, Inworld, OpenAI).

**New file:** `src/DiktaMe.Core/TTS/GeminiTtsProvider.cs` (271 lines)
- Endpoint: `generativelanguage.googleapis.com/v1beta/models/{model}:generateContent` with `responseModalities: ["AUDIO"]`
- Output: base64-encoded PCM (s16le, 24kHz, mono) — identical format to all other providers
- Auth: API key via `?key=` query param, or OAuth Bearer token detection (`ya29.*`)
- Models: `gemini-2.5-flash-preview-tts` (default), `gemini-2.5-pro-preview-tts`
- 30 voices: Kore (default), Zephyr, Puck, Charon, Fenrir, Leda, Orus, Aoede, etc.
- 60s timeout, 3 retries with exponential backoff on 429/network errors
- Reuses existing Gemini API key from SecureStorage (no new key setup needed)

**Modified files:**
- `TTSProviderFactory.cs` — added `"gemini"` to `ResolveVariant()` and `CreateProviderCore()` switches
- `TtsSettingsViewModel.cs` — added `"gemini"` to `ProviderKeys`, `CloudProviderKeys`, `VoiceLists`, labels
- `AppSettings.cs` — added `SpeechPrompt` property to `TtsSettings`
- `TtsSpeaker.cs` — prepends speech prompt for Gemini before synthesis
- Resource strings: `Settings_Tts_Provider_Gemini` in en + es-MX

### Feature 2: Per-Provider TTS Controls

Each TTS provider now only shows controls it actually supports:
- **Speed slider**: visible only for OpenAI (Cloud tab) and Kokoro (Local tab)
- **Speech Style prompt**: visible only for Gemini — free-text field for tone/pace/style instructions (max 200 chars)
- **Voice/Volume/MaxWords/TestVoice**: shown for all providers

**Implementation:** Added `ShowSpeed`, `ShowSpeechPrompt`, and `CurrentProviderKey` computed properties to `TtsSettingsViewModel`. XAML uses `BoolToVisibilityConverter` on these properties.

**How Gemini speech prompts work:** The prompt is prepended to the text content (e.g. `"Say cheerfully: Hello world"`), not a separate API field. This means no changes to `ITTSProvider` interface. Prepending happens in both `TtsSpeaker.SpeakAsync()` (production) and `TtsSettingsViewModel.TestVoiceAsync()` (test voice button).

### Feature 3: Move "When to Speak" to Pipelines > Speak (TTS)

The "When to Speak" toggles (Speak Ask Responses, Speak Chat, Speak Translations, Speak Notifications, Duck Other Apps, Read Selection hotkey) moved from **AI Engine > TTS > "When to Speak" tab** to **Pipelines > "Speak (TTS)"** sub-item.

**Rationale:** TTS behavior toggles are workflow config, not engine config. TTS engine page now has only Cloud/Local tabs.

**Changes:**
- `AIEngineSettingsPage.xaml` — removed "When to Speak" tab, simplified TTS to Cloud/Local bool toggle (`IsTtsCloudTab`)
- `AIEngineSettingsViewModel.cs` — replaced 3-tab `TtsTabIndex` int with `IsTtsCloudTab` bool
- `WorkflowsSettingsPage.xaml` — added Speak (TTS) section with all 6 toggles
- `WorkflowsSettingsViewModel.cs` — removed "Dictation Behaviors" section, added `Tts` property, replaced `IsDictationBehaviorsSelected` with `IsSpeakSelected`

### Feature 4: Per-Preset Trailing Space + Remove Use LLM Toggle

Trailing space moved from global `GeneralSettings.TrailingSpace` to per-preset `DictationProfile.TrailingSpace`.

**Changes:**
- `DictationMode.cs` — added `TrailingSpace` property to `DictationProfile` (default `true`)
- `DictationModesSettingsViewModel.cs` — replaced `CloudUseLlm`/`LocalUseLlm` with `CloudTrailingSpace`/`LocalTrailingSpace`
- `DictationPresetsSettingsPage.xaml` — replaced "Use LLM" toggle with "Trailing Space" toggle (with description)
- `LoadingViewModel.cs` — dictation pipeline now reads `profile.TrailingSpace` instead of `_settings.Current.General.TrailingSpace`
- `WorkflowsSettingsViewModel.cs` — removed all "Dictation Behaviors" fields (TrailingSpace, AdditionalKeyEnabled, RawModeOverride, RefineVoiceMode) since they were either moved or removed

**Note:** `UseLlm` is preserved in `DictationProfile` for pipeline use but no longer editable from UI — existing values are carried forward via `existing?.CloudProfile.UseLlm ?? true`.

### Feature 5: Remove Per-Pipeline Local Model Selector

Removed the Local Model ComboBox from Pipelines settings to prevent GPU overload from multiple Ollama models loading simultaneously.

**Changes:**
- `WorkflowsSettingsPage.xaml` — removed Local Model ComboBox + Refresh button from Local tab
- `ModesSettingsViewModel.cs` — removed `SelectedLocalModelIndex`, `LocalModelNames`, `_localModelIds`, local model population, and sync. `SaveAsync()` now always sets `LocalProfile.ModelName = null` (uses global Ollama model).

### Feature 6: Conversational Spoken TTS Variants

Notification TTS now speaks natural, conversational phrases instead of raw UI text.

**How it works:**
- `NotificationService.ShowToast()` accepts optional `spokenKey` and `spokenArgs` parameters
- `ResolveSpokenText()` looks up `{spokenKey}_Spoken` from resources for a natural phrasing
- Falls back to generic `Spoken_Error_Generic` / `Spoken_Warning_Generic` for unkeyed error/warning toasts
- Final fallback: original `"{title}. {message}"` concatenation

**Spoken variants added (en + es-MX):**
- `Loading_WhisperFailed_Spoken` — "The speech recognition model couldn't be downloaded..."
- `Loading_HotkeyConflict_Spoken` — "The {0} hotkey is already being used..."
- `Loading_RecordingFailed_Spoken` — "The recording didn't work..."
- `Loading_NoModesConfigured_Spoken` — "No dictation modes are set up yet..."
- `Loading_NoteSaved_Spoken` — "Your note has been saved."
- `ReadSelection_NoSelection_Spoken` — "No text is selected..."
- `Spoken_Error_Generic` — "Something went wrong."
- `Spoken_Warning_Generic` — "Just a heads up."

### Feature 7: Microphone Mute Detection

Detects when the user's microphone is muted during recording and shows a toast + spoken notification.

**Changes:**
- `LoadingViewModel.cs` — added `MuteDetector` dependency, `OnMuteStateChanged` handler
- Both `RecordAudioAsync` (utility pipelines) and streaming dictation: call `_muteDetector.UpdateDeviceLabel()`, check immediately with `CheckMuteState()`, subscribe to `MuteStateChanged`, start monitoring. Cleanup on stop.
- Resource strings: `Recording_MicMuted_Title`, `Recording_MicMuted_Message`, `Recording_MicMuted_Spoken` (en + es-MX)

### Feature 8: Note Pipeline Context Capture

Notes now capture the currently selected text as context before recording, embedding it as a blockquote in the saved note.

**Changes:**
- `PipelineOptions.cs` — added `PreCapturedContext` property to `NoteOptions`
- `NotePipeline.cs` — builds note entry with optional `> {context}` blockquote between timestamp and note text
- `LoadingViewModel.cs` — `RunNotePipelineAsync` now receives `sourceWindow` HWND, calls `CaptureSelection()` before recording, passes `PreCapturedContext` to pipeline options

**Output format:**
```
## 2026-03-15 19:30:00

> Selected text from the active window

Transcribed note content here
```

### Files Changed Summary

| File | Change Type |
|------|------------|
| `GeminiTtsProvider.cs` | **NEW** — Gemini TTS provider (271 lines) |
| `TTSProviderFactory.cs` | Register Gemini provider |
| `AppSettings.cs` | Add `SpeechPrompt` to TtsSettings |
| `DictationMode.cs` | Add `TrailingSpace` to DictationProfile |
| `TtsSpeaker.cs` | Gemini speech prompt prepending |
| `PipelineOptions.cs` | Add `PreCapturedContext` to NoteOptions |
| `NotePipeline.cs` | Context blockquote in saved notes |
| `NotificationService.cs` | Conversational spoken TTS with resource keys |
| `LoadingViewModel.cs` | Mute detection, per-preset trailing space, note context capture, spoken keys |
| `TtsSettingsViewModel.cs` | Gemini provider + per-provider visibility + SpeechPrompt |
| `AIEngineSettingsViewModel.cs` | Simplify TTS tabs to Cloud/Local bool |
| `ModesSettingsViewModel.cs` | Remove local model selector |
| `DictationModesSettingsViewModel.cs` | TrailingSpace replaces UseLlm toggle |
| `WorkflowsSettingsViewModel.cs` | Remove Dictation Behaviors, add Speak (TTS) + Tts VM |
| `AIEngineSettingsPage.xaml` | Remove "When to Speak" tab, add Speech Prompt + ShowSpeed visibility |
| `WorkflowsSettingsPage.xaml` | Add Speak (TTS) section, remove Local Model selector |
| `DictationPresetsSettingsPage.xaml` | Replace UseLlm with TrailingSpace toggle |
| `en/Resources.resw` | Gemini label, SpeechPrompt, spoken variants, mute detection strings |
| `es-MX/Resources.resw` | Same as en (translated) |

### Recently Blocked: SPEC_KOKORO_GPU — Kokoro TTS DirectML GPU Acceleration

| Detail | Value |
|--------|-------|
| **Spec** | `plans/SPEC_KOKORO_GPU.md` |
| **Goal** | Sub-250ms Kokoro TTS synthesis via DirectML GPU |
| **Status** | **BLOCKED** — ONNX Runtime 1.22.0 DirectML EP cannot handle Kokoro's `ConvTranspose` node |
| **Error** | `OnnxRuntimeException: ConvTranspose node '/encoder/F0.1/pool/ConvTranspose' — 80070057` |
| **Scope** | ALL model variants (gpu, fp32, fp16, int8) fail with DirectML EP active |
| **Unblock** | KokoroSharp or ONNX Runtime ships a version fixing DirectML ConvTranspose support |

**What was kept from this work (5 new tests, net-positive):**
- `"gpu"` model variant in `KokoroModelManager` (valid quantization, works on CPU, 169MB)
- Variant reorder in Settings UI: gpu → fp32 → fp16 → int8 (with descriptive labels)
- Default variant changed from `"int8"` to `"gpu"` for new installs
- `KokoroUseGpu` property in `AppSettings.TtsSettings` (inert, avoids settings.json compat issue)

**What was rolled back:**
- NuGet reverted: `KokoroSharp.DirectML` → `KokoroSharp.CPU`
- DirectML SessionOptions code, GPU toggle UI, GPU-aware cache key — all removed

---

### SPEC_003 TTS — Completed (for reference)

| Detail | Value |
|--------|-------|
| **Spec** | `plans/SPEC_003_TTS_V2.md` |
| **Phases** | A–G (40 tasks, all complete, E2E verified) |
| **Local TTS** | Kokoro-ONNX via `KokoroSharp.CPU` NuGet (82M params, 88MB int8 model) |
| **Cloud TTS** | Deepgram Aura-2, Inworld TTS-1.5, OpenAI, Gemini (all working after variant routing fix) |
| **Key hotkey** | `Ctrl+Alt+Q` = "Read Selection" (select text anywhere → hear it) |
| **Tests** | 282 new tests (944 total) |

### E2E Testing Still Needed

- **Cloud providers**: Retest Deepgram, OpenAI, Inworld after variant routing fix
- **Ask/Chat/Translate hooks**: Enable SpeakAskResponses etc. → use mode → verify audio
- **Control Panel toggle**: ON/OFF enables/disables all TTS output
- **Settings persistence**: Toggle states, provider, voice/speed survive restart

## CI/CD Notes

- **Gitleaks:** `.gitleaks.toml` allowlists `website/QUICKSTART.md` (historical fake JWTs in git history)
- **Test threshold:** `ci/test-threshold.json` set to 470 (local runs 944, CI runs ~479 due to skipped tests)
- **Vercel:** Connected to `geckogtmx/diktame`, Root Directory = `website`

## i18n Notes (SPEC_004)

- **WinUI3Localizer** adopted — `ApplicationLanguages.PrimaryLanguageOverride` does NOT work in unpackaged apps
- All 24 XAML files migrated from `x:Uid` to `l:Uids.Uid` (WinUI3Localizer namespace)
- en + es-MX `.resw` files (370+ keys each) + CoreStrings `.resx` (8 keys)
- **TODO:** Some labels and tooltips still need translation review — check all screens in es-MX locale for missing or untranslated strings

## Recent Changes (SPEC_009 Wizard Fixes + Telemetry + Local Mode Polish)

All fixes verified via manual testing on 2026-03-09/10. See `plans/SPEC_009_FIXES.md` for full details.

| Fix | Summary |
|-----|---------|
| FIX-2 | Language selection step added (bilingual EN/ES, Step 0) |
| FIX-4 | Default Refine mode = Auto (not Voice) |
| FIX-5 | Default system prompts preloaded for all dictation modes |
| FIX-6 | WPM formula fixed — uses wall-clock time (RecordingMs + TotalMs). Verified: LLM=124 WPM, RAW=154 WPM |
| FIX-7 | Whisper model download UI in wizard STT step (progress bar, blocks Next) |
| FIX-8 | Hotkey double-subscription fix (singleton LoadingViewModel unsubscribes before re-subscribing) |
| FIX-9 | Download triggers on Next click, not radio selection (BeforeLeaveStep callback) |
| FIX-10 | Split Cloud/Local into independent STT + LLM toggles (6-col layout, auth badge LOC/API/MIX) |
| FIX-13 | Wizard LLM step: Ollama validation + model pull with progress (blocks Next when offline) |
| FIX-14 | Wizard LLM step: Ollama auto-install via winget, fallback to browser. Default model → `gemma3:4b`. |
| FIX-15 | Local mode polish: Ollama auto-start on launch, keep-alive setting (5m–2h), first-inference GPU log, Whisper download in Settings, Ollama install from Settings |
| FIX-16 | **LLMProviderFactory caching — 5x Ollama latency improvement** (3000ms→550ms). Wizard language Back bug fix. API Keys step auto-skip on local path. Phased winget install messages. |

## RESOLVED: Wizard Won't Show on Fresh Install

**Root cause**: `ControlPanelViewModel` constructor called `LoadFromSettings()` which triggered `OnIsRefineVoiceChanged` → `UpdateAsync()` → prematurely wrote `settings.json`. Then `LoadAsync()` found the file, Migration 8 set `WizardCompleted = true`, and the wizard was skipped.

**Fix**: Added `_suppressSave` guard in `ControlPanelViewModel`. All `On*Changed` handlers skip `UpdateAsync()` when `_suppressSave` is true. Guard is set around both `LoadFromSettings()` call sites (constructor + `OnSettingsChanged`). Manually verified: wizard shows on fresh install, does not show on subsequent launches.

**File**: `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`

## DONE: Whisper GPU Acceleration — CUDA → Vulkan Swap

**Root cause**: `Whisper.net.Runtime.Cuda` did NOT bundle CUDA runtime libraries → fell back to CPU silently (~2800ms for 11s audio).

**Fix applied**: NuGet swap in `src/DiktaMe.Core/DiktaMe.Core.csproj`:
```xml
<PackageReference Include="Whisper.net.Runtime.Vulkan" Version="1.9.0" />
```

**Why Vulkan**: Self-contained (28MB, all DLLs bundled), cross-vendor (NVIDIA + AMD + Intel Arc), no user setup needed. No code changes — runtime selection is automatic.

**Verified (G.6)**: `runtime="Vulkan"`, ratio 0.05x–0.09x (GPU). ~6-7x speedup over CPU. First dictation has cold-start penalty (Vulkan shader compile).

**G.7 fix**: `STTProviderFactory` was creating a new `WhisperProvider` per dictation, reloading the 466MB model each time (~800ms). Fixed by caching the instance. **Verified**: pipeline `transcription_ms` dropped from ~1250ms to ~440ms. Raw mode end-to-end: ~500ms.

**G.8**: Added CPU-fallback warning log — if Vulkan DLLs are deployed but `Cpu` runtime is loaded, logs a warning suggesting GPU driver update. Vulkan loader (`vulkan-1.dll`) comes from GPU drivers, not from us.

**Full investigation details**: `plans/SPEC_009_LOCALFLOW.md` §12.8–12.10

## Remaining Work

### Manual Testing Needed

| Item | Notes |
|------|-------|
| ~~**TTS Phase G gaps**~~ | ✅ All gaps fixed, E2E verified (see above) |
| **API Keys step skip** | FIX-16 auto-skips step 4 when both providers are local — needs manual verification |
| **SPEC_009 scenarios 3-8** | Scenarios 1-2 passed. Remaining: full local E2E, hybrid combos (see `plans/SPEC_009_TESTING.md`) |
| **Ollama auto-start** | FIX-15 — verify app launch with Ollama not running |
| **Keep-alive dropdown** | FIX-15 — change in Settings, restart app, verify in Ollama request logs |
| **Whisper model change download** | FIX-15 — switch model in Settings, verify download with progress |
| **Ollama install from Settings** | FIX-15 — verify Install button appears when Ollama is offline |
| **SPEC_011 Ollama Settings page** | Model list ✅, search/view ✅, pull ✅, delete (needs test), service restart (needs retest after fixes), VRAM display (needs test), warmup ✅ |
| **Refine on Antigravity** | `CaptureSelection` times out — app-specific accessibility issue, separate investigation |

### ~~Known Gap: TTS Not Persisted to DB~~ ✅ Fixed

`tts_played_ms` column added to SQLite history table. Ask, Translate, and ReadSelection pipelines now persist TTS latency. Notification TTS wired via `ShowToast` → `SpeakIfEnabledAsync("notification")` with `suppressTts` to prevent double-speak on Ask answers.

### Tier 2 — Ship Blockers (Steps 1-4)

| Task | Effort | Status |
|------|--------|--------|
| **FIX-17** | TTS wizard step (Off / Local Kokoro / Cloud Deepgram) | Pending — spec ready (`SPEC_009_FIXES.md`, `SPEC_009_WIZARD_FLOW.md`) |
| **FIX-1** | Wizard: Trial → Wallet terminology | Unblocked — SPEC_008 now COMPLETE |
| **L.5** | Streaming UI toggle | Manual test pass only (~15 min) |
| **H.1** | Installer (Inno Setup) | Only hard blocker before V2.0 ships |

### Tier 3 — Modules (Steps 5-10, SPEC_015)

| Task | Effort |
|------|--------|
| **Phase 0A** | Factory tests (~28 tests, ~1 session) |
| **Phase 0B** | Plugin infrastructure: `IPlugin`, `PipelineEventBus`, `PluginManager`, `PluginUIRegistry` |
| **Phase 0C** | Vision core: screenshot → AI at cursor (new hotkey TBD — `Ctrl+Alt+S` conflicts with ReadSelection) |
| **Phases A-C** | Connectors: `IConnector` + Obsidian + Webhook/Discord/Streamer.bot |
| **Phases O-Q** | Memory: SQLite+VSS, embedding model, pipeline hooks |
| **Phases D-E** | Meetings: session engine + Scribe window (heaviest module, do last) |

Full spec: `plans/SPEC_015_MODULES_SPRINT.md` (17 phases, 18-23 sessions)

### Tier 4 — Deferred

| Task | Effort |
|------|--------|
| **LemonSqueezy** | License integration, device binding, trial abuse prevention |
| Cloud latency tuning | Cloud inference profiling |
| Control Panel wiring | RAW toggle→pipeline, REFINE toggle→pipeline (see `plans/CONTROL_PANEL_REWORK.md`) |
| ~~L.6-L.7~~ | Deferred — Flux (revisit when Chat gets voice input) |

## Reference Docs

- `DEVELOPMENT_ROADMAP.md` — Full task breakdown
- `ARCHITECTURE.md` — Technical architecture
- `SECURITY.md` — GitHub security policy
- `plans/SPEC_009_LOCALFLOW.md` — Local mode E2E spec + GPU investigation (§12)
- `plans/SPEC_009_FIXES.md` — Wizard + local mode fix tracker (15/17 complete; FIX-1 unblocked, FIX-17 pending)
- `plans/SPEC_009_TESTING.md` — Manual test scenarios
- `plans/SPEC_KOKORO_GPU.md` — Kokoro DirectML GPU acceleration plan (**BLOCKED** — ConvTranspose incompatibility)
- `plans/SPEC_003_TTS_V2.md` — TTS implementation plan (40 tasks, 7 phases, complete)
- `plans/SPEC_003_TTS.md` — TTS research reference (V1 draft, superseded by V2)
- `plans/SPEC_015_MODULES_SPRINT.md` — Modules Sprint: plugin infra, Vision, Connectors, Memory, Meetings (17 phases, DRAFT)
- `plans/SPEC_009_WIZARD_FLOW.md` — Complete wizard path test matrix (14 paths, target-state for FIX-17)
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` — Post-launch feature specs (superseded by SPEC_015)
- `plans/SPEC_011_OLLAMA.md` — Ollama Management Hub spec (implemented)
- `plans/archive/` — Completed implementation plans (Stream F, K, OAuth Restructure, etc.)
