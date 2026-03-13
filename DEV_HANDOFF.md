# Developer Handoff

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 944 passing locally (479 on CI — DPAPI/Clipboard/Audio/Whisper tests skipped on runners) |
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
| **SPEC_009** | Local Mode E2E + Wizard Fixes — Phases A-G complete, FIX-1 through FIX-16 (15/16 done, FIX-1 deferred to SPEC_008) |
| **SPEC_011** | Ollama Management Hub — Core API, search service, Settings UI, E2E warmup, 22 new tests |
| **DOCS_V2** | Exhaustive User documentation (Features & Settings), integrated natively into the Next.js Website via Markdown |
| **SPEC_003 A–G** | TTS: Core infra, Kokoro local, Read Selection hotkey, pipeline hooks, cloud providers, Settings UI + Control Panel toggle, Phase G polish + E2E bugfixes. 282 new tests. **All 40 tasks complete. E2E verified.** |

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

## Known Issues (SPEC_011)

- **Settings corruption from TextBox bug may persist** — users who typed in the old TextBox may have `OllamaModel` set to a partial/invalid string in `settings.json`. Fix: open Ollama Settings → select correct model from dropdown.

## Current Work

**Active: SPEC_KOKORO_GPU — Kokoro TTS DirectML GPU Acceleration**

| Detail | Value |
|--------|-------|
| **Spec** | `plans/SPEC_KOKORO_GPU.md` |
| **Goal** | Sub-250ms Kokoro TTS synthesis via DirectML GPU (currently 1,800–5,000ms on CPU) |
| **NuGet swap** | `KokoroSharp.CPU` 0.6.5 → `KokoroSharp.DirectML` 0.6.5 |
| **No Whisper conflict** | Whisper.net uses whisper.cpp (P/Invoke), KokoroSharp uses ONNX Runtime — different native stacks |
| **Status** | Plan written, ready to implement |

### What to Do (3 Phases)

**Phase 1 — NuGet + SessionOptions wiring:**
1. Swap `KokoroSharp.CPU` → `KokoroSharp.DirectML` in `DiktaMe.Core.csproj`
2. Add `useGpu` param to `KokoroTtsProvider` constructor (default `true`)
3. Add `CreateSessionOptions()` — `AppendExecutionProvider_DML()` in try/catch (CPU fallback on failure)
4. Add `KokoroUseGpu` bool to `TtsSettings` in `AppSettings.cs` (default `true`)
5. Wire `useGpu` through `TTSProviderFactory.CreateProviderCore()`

**Phase 2 — GPU model variant + Settings UI:**
1. Add `"gpu"` variant (`kokoro-quant-gpu.onnx`, 169MB) to `KokoroModelManager.ModelMap`
2. Update `TtsSettingsViewModel` variant labels/keys (reorder: gpu, fp32, fp16, int8)
3. Add GPU toggle CheckBox to `TtsSettingsPage.xaml`
4. Add int8+GPU InfoBar warning
5. Change default variant from `"int8"` to `"gpu"` for new installs
6. Add `ClearCache()` to `TTSProviderFactory`, call on variant/GPU toggle change
7. Include GPU state in cache key: `"kokoro:gpu:gpu"` vs `"kokoro:fp32:cpu"`

**Phase 3 — Tests + verification:**
1. Update `KokoroTtsProviderTests` for `useGpu` parameter
2. Add `"gpu"` variant tests to `KokoroModelManagerTests`
3. Build (0 warnings), test (944+ passing)
4. Manual E2E: Test Voice → check log for `runtime=DirectML`, measure latency

### Key Files to Modify

| File | Change |
|------|--------|
| `src/DiktaMe.Core/DiktaMe.Core.csproj` | `KokoroSharp.CPU` → `KokoroSharp.DirectML` |
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `KokoroUseGpu`, change default variant to `"gpu"` |
| `src/DiktaMe.Core/TTS/KokoroTtsProvider.cs` | Add `useGpu` param, `CreateSessionOptions()` with DirectML EP |
| `src/DiktaMe.Core/TTS/KokoroModelManager.cs` | Add `"gpu"` variant to `ModelMap` |
| `src/DiktaMe.Core/Config/TTSProviderFactory.cs` | GPU-aware cache key, `ClearCache()` method |
| `src/DiktaMe.App/ViewModels/Settings/TtsSettingsViewModel.cs` | GPU toggle, updated variant labels, cache clear |
| `src/DiktaMe.App/Views/Settings/TtsSettingsPage.xaml` | GPU toggle CheckBox, int8+GPU InfoBar warning |
| `tests/DiktaMe.Core.Tests/TTS/KokoroTtsProviderTests.cs` | Tests for `useGpu` parameter |
| `tests/DiktaMe.Core.Tests/TTS/KokoroModelManagerTests.cs` | Test `"gpu"` variant |

### Critical Context

- `KokoroModel(string modelPath, SessionOptions options = null)` — already accepts optional SessionOptions
- DirectML auto-falls back to CPU for unsupported ops — safe default
- int8 model is **slower on GPU** (2,106ms) than CPU (1,887ms) — must warn users
- fp32 model (310MB) already downloaded on dev machine — can test immediately
- GPU-optimized model (`kokoro-quant-gpu.onnx`, 169MB) same performance as fp32 at half the size
- Rollback: one-line revert `KokoroSharp.DirectML` → `KokoroSharp.CPU`
- ~210MB publish size increase from `DirectML.dll` + `onnxruntime.dll` (compresses to ~60MB)

### Fallback Plan

1. **User-level:** GPU toggle in Settings → force CPU SessionOptions
2. **Code-level:** try/catch around `AppendExecutionProvider_DML()` → falls back to CPU automatically
3. **NuGet-level:** Revert to `KokoroSharp.CPU` in csproj — one-commit revert

---

### SPEC_003 TTS — Completed (for reference)

| Detail | Value |
|--------|-------|
| **Spec** | `plans/SPEC_003_TTS_V2.md` |
| **Phases** | A–G (40 tasks, all complete, E2E verified) |
| **Local TTS** | Kokoro-ONNX via `KokoroSharp.CPU` NuGet (82M params, 88MB int8 model) |
| **Cloud TTS** | Deepgram Aura-2, Inworld TTS-1.5, OpenAI (all working after variant routing fix) |
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

### Known Gap: TTS Not Persisted to DB

`PipelineResult.TtsPlayedMs` field exists but is never written to the SQLite history table (`DictationHistory`). TTS latency is logged to Serilog files only (`%APPDATA%\DiktaMe\logs\diktame_YYYY-MM-DD.log`). Adding a `TtsPlayedMs` column to the history schema is a future task.

### Tier 2 — Post-local-mode

| Task | Effort |
|------|--------|
| **FIX-1** | Wizard: Trial → Wallet terminology (deferred, depends on SPEC_008) |
| **H.1** | Installer (MSIX or Inno Setup) |
| **LemonSqueezy** | License integration, device binding, trial abuse prevention |
| Cloud latency tuning | Cloud inference profiling |
| Control Panel wiring | RAW toggle→pipeline, REFINE toggle→pipeline (see `plans/CONTROL_PANEL_REWORK.md`) |
| ~~L.6-L.7~~ | Deferred — Flux (revisit when Chat gets voice input) |

## Reference Docs

- `DEVELOPMENT_ROADMAP.md` — Full task breakdown
- `ARCHITECTURE.md` — Technical architecture
- `SECURITY.md` — GitHub security policy
- `plans/SPEC_009_LOCALFLOW.md` — Local mode E2E spec + GPU investigation (§12)
- `plans/SPEC_009_FIXES.md` — Wizard + local mode fix tracker (15/16 complete, FIX-1 deferred to SPEC_008)
- `plans/SPEC_009_TESTING.md` — Manual test scenarios
- `plans/SPEC_KOKORO_GPU.md` — **Active spec**: Kokoro DirectML GPU acceleration plan
- `plans/SPEC_003_TTS_V2.md` — TTS implementation plan (40 tasks, 7 phases, complete)
- `plans/SPEC_003_TTS.md` — TTS research reference (V1 draft, superseded by V2)
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` — Post-launch feature specs
- `plans/SPEC_011_OLLAMA.md` — Ollama Management Hub spec (implemented)
- `plans/archive/` — Completed implementation plans (Stream F, K, OAuth Restructure, etc.)
