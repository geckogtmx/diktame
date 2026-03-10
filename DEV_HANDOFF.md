# Developer Handoff

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 689 passing locally (479 on CI — DPAPI/Clipboard/Audio tests skipped on runners) |
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
| **SPEC_009** | Local Mode E2E + Wizard Fixes — Phases A-F complete, Phase G in progress (GPU acceleration fix) |

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

## Recent Changes (SPEC_009 Wizard Fixes)

All fixes verified via manual testing on 2026-03-09. See `plans/SPEC_009_FIXES.md` for full details.

| Fix | Summary |
|-----|---------|
| FIX-2 | Language selection step added (bilingual EN/ES, Step 0) |
| FIX-4 | Default Refine mode = Auto (not Voice) |
| FIX-5 | Default system prompts preloaded for all dictation modes |
| FIX-6 | WPM telemetry guard for first-run garbage values |
| FIX-7 | Whisper model download UI in wizard STT step (progress bar, blocks Next) |
| FIX-8 | Hotkey double-subscription fix (singleton LoadingViewModel unsubscribes before re-subscribing) |
| FIX-9 | Download triggers on Next click, not radio selection (BeforeLeaveStep callback) |

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

**Full investigation details**: `plans/SPEC_009_LOCALFLOW.md` §12.8–12.10

## Remaining Work

### Tier 1 — STT/Whisper (complete before moving on)

| Task | Effort |
|------|--------|
| ~~G.7: Manual verify model cache~~ | ✅ Verified — 0-1ms gap, `loaded runtime` once per session |
| **SPEC_009 STT scenarios** | Wizard Local STT tests, Whisper model selection, hybrid STT (see `plans/SPEC_009_TESTING.md`) |
| **FIX-10: Cloud/Local toggle ignores STT** | Toggle should switch STT provider too (see `plans/SPEC_009_FIXES.md`) |

### Tier 2 — Ollama/Local LLM (only after Tier 1 passes)

| Task | Effort |
|------|--------|
| **G.3: Verify Ollama warmup log** | Code complete (`LoadingViewModel.cs:152-179`) — needs manual verify with Ollama running |
| **SPEC_009 LLM scenarios** | Full local E2E (Whisper + Ollama), hybrid Cloud STT + Local LLM |

### Tier 3 — Post-local-mode

| Task | Effort |
|------|--------|
| **FIX-1** | Wizard: Trial → Wallet terminology (deferred, depends on SPEC_008) |
| **H.1** | Installer (MSIX or Inno Setup) |
| **LemonSqueezy** | License integration, device binding, trial abuse prevention |
| Latency tuning | Cloud inference profiling |
| Control Panel wiring | RAW toggle→pipeline, REFINE toggle→pipeline (see `plans/CONTROL_PANEL_REWORK.md`) |
| ~~L.6-L.7~~ | Deferred — Flux (revisit when Chat gets voice input) |

## Reference Docs

- `DEVELOPMENT_ROADMAP.md` — Full task breakdown
- `ARCHITECTURE.md` — Technical architecture
- `SECURITY.md` — GitHub security policy
- `plans/SPEC_009_LOCALFLOW.md` — Local mode E2E spec + GPU investigation (§12)
- `plans/SPEC_009_FIXES.md` — Wizard fix tracker (9/12 complete)
- `plans/SPEC_009_TESTING.md` — Manual test scenarios
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` / `SPEC_003_TTS.md` — Post-launch feature specs
- `plans/archive/` — Completed implementation plans (Stream F, K, OAuth Restructure, etc.)
