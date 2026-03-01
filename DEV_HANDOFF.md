# Developer Handoff

## Session Summary: 2026-03-01 (Session 13)

### Project Status: OAuth Implementation Next — Specs Complete

**Session 13 was spec-writing and audit.** Fixed CI lint failure, audited V1 OAuth/trial system (Stream K added to roadmap), audited V1 deferred specs, and created 3 new feature specs in `plans/`. No new C# code written — all documentation.

### Session 13 Accomplishments

**CI Fix:**
- Fixed 3 `IDE0011` (missing braces) lint errors in `MainWindow.xaml.cs:FindDescendant<T>` — commit `b2f73ac`

**V1 OAuth/Trial System Audit (Stream K):**
- Audited V1's complete OAuth + trial credits system (Supabase Auth, deeplink protocol, managed Gemini proxy, trial credits)
- Confirmed **none of it exists in V2** — V2 is BYOK-only
- Added Stream K (6 tasks, K.1–K.6, ~4.5 days) to `DEVELOPMENT_ROADMAP.md`
- Full 14-component gap analysis table in roadmap Section 12

**Feature Specs Created:**
- `plans/SPEC_001_MEETINGS.md` — Meeting Intelligence (Scribe). Competitive analysis vs Granola + Fellow.ai. Session-aware Notes integration (voice notes as weighted synthesis signals). Scribe window layout, 6 output templates, "Ask this meeting" chat.
- `plans/SPEC_002_VISION.md` — Screen capture + multimodal LLM vision. Hotkey: `Ctrl+Alt+S` ("See"). Cloud + local from day one. Snipping overlay, VisionPipeline.
- `plans/SPEC_003_TTS.md` — Text-to-speech with dual local+cloud strategy. 12 local models evaluated (<4B params, Orpheus family as top pick). 3 cloud providers: Inworld (recommended, $5-10/1M chars, #1 ranked), OpenAI, ElevenLabs. Voice cloning, NAudio playback.

**Commit:** `bf21244` — docs: add V2 feature specs (Meetings, Vision, TTS) and Stream K audit

---

## 🎯 Next Session: Stream K — OAuth & Trial Credits

### Start Here: K.1 Core Models & AppSettings (0.5 day)

1. Add trial-related fields to `AppSettings`:
   - `TrialSessionToken` (encrypted via `SecureStorage`)
   - `TrialEmail`, `TrialWordsUsed`, `TrialWordsQuota`
   - `TrialDaysRemaining`, `TrialExpiresAt`, `TrialActive`, `TrialLastSynced`
2. Add `AuthMode` enum: `None`, `Trial`, `ApiKey`
3. Add `TrialStatus` model class
4. Settings migration for new fields

**Port from:** `E:\git\diktate\src\types\settings.ts` (lines 124–132)

### Then: K.2 TrialAccountService (1 day)

1. `LoginAsync()` — opens browser to `https://dikta.me/login?mode=app`
2. `HandleAuthCallbackAsync(token)` — stores JWT, extracts email, triggers status sync
3. `RefreshStatusAsync()` — GET `/api/trial/status` with Bearer token
4. `RecordUsageAsync(provider, model, wordsUsed)` — POST `/api/trial/usage`
5. `LogoutAsync()` — clears token + trial fields
6. JWT decode helper (extract email, expiry from payload)

**Port from:** `E:\git\diktate\src\ipc\trialHandlers.ts`

### Then: K.3 Protocol Handler (0.5–1 day)

1. Register `diktame://` URL scheme (MSIX manifest or registry fallback)
2. Handle protocol activation in `App.xaml.cs` → route `diktame://auth?token=...`
3. Single-instance check — forward deeplink to existing instance
4. Update V1 website callback to use `diktame://` scheme

**Port from:** `E:\git\diktate\src\main.ts` (lines 57–61, 681–707)

### Then: K.4 Managed Gemini Integration (1 day)

1. `TrialGeminiProvider` — routes through Supabase Edge Function, Bearer JWT auth
2. Wire into `LLMRouter` — `AuthMode == Trial` → managed provider
3. Post-process: `TrialAccountService.RecordUsageAsync()` after each LLM call
4. Handle 403 quota-exceeded, 401 token expiry

**Port from:** `E:\git\diktate\supabase\functions\gemini-proxy\index.ts`

### Then: K.5 Trial Account UI (1 day)

1. Settings "Account" section: sign-in button, usage progress bar, days remaining
2. Control Panel badge: `AuthMode.Trial` → "Trial" badge
3. Configuration Wizard: "Try free" option alongside "Enter API key"
4. Quota exceeded notification → "Add your own API key" prompt

### Finally: K.6 Tests (0.5 day)

1. `TrialAccountServiceTests` — login, status sync, usage recording, JWT parsing
2. `TrialGeminiProviderTests` — routing, auth, quota handling
3. `LLMRouter` integration — delegates to trial provider when `AuthMode == Trial`

### K Dependencies

- **Website repo** (`E:\git\diktate\website`): Update OAuth callback `diktate://` → `diktame://`
- **Supabase**: No changes needed (existing Edge Function + DB schema)
- **H.1 (Installer)**: Protocol handler registration may depend on MSIX vs Inno Setup

---

## 📊 Current Metrics

| Metric | Value |
|--------|-------|
| **Tests** | 521 passing (CI filter: 376) |
| **Build** | 0 errors, 0 warnings |
| **CI** | Passing on main |
| **Publish size** | ~173MB uncompressed, ~70MB compressed (win-x64, self-contained, trimmed) |
| **Branch** | main |
| **Latest commit** | `bf21244` — docs: add V2 feature specs and Stream K audit |

## ✅ Completed Streams

| Stream | Scope | Status |
|--------|-------|--------|
| **A** — Scaffolding | Solution scaffold, build config, publish pipeline | ✅ Complete |
| **B** — Core Engine | Audio recording, device management, hotkeys, text injection, mute detection | ✅ Complete |
| **C** — STT & LLM Providers | Deepgram, Gemini Audio, Whisper.net, OpenAI-compatible, Anthropic, Ollama | ✅ Complete |
| **D** — Pipeline Orchestration | Dictation, Refine, Ask, Translate, Note, Oops pipelines | ✅ Complete |
| **E** — Data & Security | SettingsManager, ProfileManager, PromptRepository, HistoryManager, MetricsCollector, NoteWriter, SecureStorage, PIIScrubber, ApiKeyValidator, DI wiring | ✅ Complete |
| **F** — UI (WinUI 3) | Settings (12 tabs), Control Panel (V1-match), Wizard, Loading Screen, Quick Chat overlay, Notifications, Tray icon | ✅ Complete |
| **G** — Testing & CI/CD | 521 unit tests, GitHub Actions CI (12-step pipeline), coverage tracking | ✅ Complete |
| **H.2** — V1 Migration | N/A by design — Electron safeStorage keys can't be decrypted from C#; users re-enter via Wizard | ✅ N/A |
| **I** — Promoted Features | SnippetManager, AudioDucker, ChatPipeline, OllamaManager | ✅ Complete |
| **J** — CRUD Dictation Modes | DictationMode/PipelineConfig models, Managers, Migration, Pipeline integration, Per-mode model selection, UI | ✅ Complete |

## 📋 Remaining Work

| Task | Effort | Description |
|------|--------|-------------|
| **K.1–K.6** | ~4.5 days | **OAuth & Trial Credits** — next session focus |
| **H.1** | 1 day | Installer (MSIX or Inno Setup) |
| **I.6** | 0.5 day | Website rebrand for V2 launch (dikta.me) |
| **Latency tuning** | TBD | Cloud inference slower than V1 — needs profiling |
| **Settings polish** | TBD | Final UI pass, NavigationView spacing |

### Control Panel Remaining (from rework plan)
- [ ] RAW toggle → pipeline wiring (LoadingViewModel doesn't read `IsRawModeEnabled` yet)
- [ ] REFINE toggle → pipeline routing (`refine_auto` vs `refine_instruction`)
- [ ] Pipeline state granularity (Processing/Injecting states)
- [ ] Test with 1, 4, and 8 presets for UniformGridLayout wrapping

### Feature Specs Written (not yet implemented — post-launch)
- `plans/SPEC_001_MEETINGS.md` — Meeting Intelligence (Scribe)
- `plans/SPEC_002_VISION.md` — Screen Capture + Multimodal Vision
- `plans/SPEC_003_TTS.md` — Text-to-Speech (Local + Cloud)

---

## 🔧 Key Technical Context

### Build & Test Commands
```bash
dotnet build DiktaMe.sln -c Release          # 0 errors, 0 warnings
dotnet test DiktaMe.sln                       # 521 tests pass
dotnet format DiktaMe.sln --verify-no-changes # Lint check (CI uses this)
publish-release.cmd                           # Trimmed self-contained win-x64
```

### Key Files
| File | Purpose |
|------|---------|
| `DEVELOPMENT_ROADMAP.md` | Full task breakdown — Stream K tasks at Section 12 |
| `plans/SPEC_001_MEETINGS.md` | Meeting Intelligence spec (post-launch) |
| `plans/SPEC_002_VISION.md` | Vision module spec (post-launch) |
| `plans/SPEC_003_TTS.md` | TTS module spec (post-launch) |
| `plans/CONTROL_PANEL_REWORK.md` | Control Panel V2 rework plan (Phase 1+2 complete) |
| `ARCHITECTURE.md` | Technical architecture (14 sections) |
| `Directory.Build.props` | Shared build config (C# 12, nullable, TreatWarningsAsErrors) |
| `.editorconfig` | Code style rules (Meziantou.Analyzer + naming) |
| `ci/test-threshold.json` | Minimum test count + publish size bounds |

### V1 Reference Files for Stream K
| V1 File | What to port |
|---------|-------------|
| `src/types/settings.ts:124-132` | Trial fields in AppSettings |
| `src/ipc/trialHandlers.ts` | TrialAccountService logic |
| `src/main.ts:57-61,681-707` | Protocol handler + deeplink |
| `src/settings/trialAccount.ts` | Trial Account UI |
| `supabase/functions/gemini-proxy/index.ts` | Managed Gemini calling pattern |
| `src/services/configSync.ts` | Status sync pattern |

### Shell Gotchas (Windows + Bash)
- PowerShell `$_` gets mangled by bash — use `powershell -NoProfile -File -` with heredoc
- `/p:Platform=x64` needs quoting as `"-p:Platform=x64"` in bash

### WinUI 3 XAML Gotchas
- `x:Bind` is NOT supported on `Run.Text` — XAML compiler silently crashes
- `ExtendsContentIntoTitleBar` + `OverlappedPresenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)` = frameless window
- ToggleSwitch internal header column reserves space even with empty content — use `Margin="12,0,0,0"` to compensate
- Cross-thread ObservableCollection updates: Must use `DispatcherQueue.TryEnqueue()`
- JSON null overrides C# record defaults — always null-coalesce sub-records: `?? new()`

### Test Gotchas
- Moq expression trees cannot use optional parameters (CS0854) — pass `It.IsAny<CancellationToken>()` explicitly
- `LLMRouter.ProcessAsync(text, prompt, modelName)` ambiguous with `ProcessAsync(text, prompt, mode)` — use named parameter `modelName:`

---

**Next session:** Start K.1 (Core Models) → K.2 (TrialAccountService) → K.3 (Protocol Handler) → K.4 (Managed Gemini) → K.5 (UI) → K.6 (Tests)
