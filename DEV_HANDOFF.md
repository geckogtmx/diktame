# Developer Handoff

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 695+ passing locally (479 on CI — DPAPI/Clipboard/Audio/Whisper tests skipped on runners) |
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

## Open Bugs (Stream K)

1. **App UI doesn't update after sign-in** — `StatusChanged` may not fire if `/api/trial/status` fails for new users.
2. **Website "Sign Up" shows Coming Soon** — Vercel env var `NEXT_PUBLIC_COMING_SOON=true` still set. Delete it in Vercel dashboard.
3. **Trial counter page blank** — depends on Bug 1 + Supabase Edge Function returning proper trial records.

## CI/CD Notes

- **Gitleaks:** `.gitleaks.toml` allowlists `website/QUICKSTART.md` (historical fake JWTs in git history)
- **Test threshold:** `ci/test-threshold.json` set to 470 (local runs 689, CI runs ~479 due to skipped tests)
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
| **API Keys step skip** | FIX-16 auto-skips step 4 when both providers are local — needs manual verification |
| **SPEC_009 scenarios 3-8** | Scenarios 1-2 passed. Remaining: full local E2E, hybrid combos (see `plans/SPEC_009_TESTING.md`) |
| **Ollama auto-start** | FIX-15 — verify app launch with Ollama not running |
| **Keep-alive dropdown** | FIX-15 — change in Settings, restart app, verify in Ollama request logs |
| **Whisper model change download** | FIX-15 — switch model in Settings, verify download with progress |
| **Ollama install from Settings** | FIX-15 — verify Install button appears when Ollama is offline |
| **Refine on Antigravity** | `CaptureSelection` times out — app-specific accessibility issue, separate investigation |

### Tier 1 — Verification

| Task | Effort |
|------|--------|
| ~~G.7: Manual verify model cache~~ | ✅ Verified — 0-1ms gap, `loaded runtime` once per session |
| ~~G.8: Vulkan CPU-fallback warning~~ | ✅ Logs warning if Vulkan deployed but CPU fallback used |
| ~~FIX-10: Cloud/Local toggle ignores STT~~ | ✅ Split into independent STT + LLM toggles |
| ~~FIX-13: Wizard LLM step has no Ollama validation~~ | ✅ BeforeLeaveStep: check + pull + progress bar |
| ~~FIX-16: Ollama latency~~ | ✅ Provider caching — 3000ms → 550ms (5x improvement) |

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
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` / `SPEC_003_TTS.md` — Post-launch feature specs
- `plans/archive/` — Completed implementation plans (Stream F, K, OAuth Restructure, etc.)
