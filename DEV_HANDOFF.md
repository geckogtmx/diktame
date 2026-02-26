# Developer Handoff

## Session Summary: 2026-02-25 (Session 7)

### Project Status: Stream J Complete, UI Fix Required

Stream J (CRUD Dictation Modes) was completed but the UI implementation (J.6) introduced architectural issues that need immediate correction. The build currently FAILS due to missing references in DictationModesSettingsViewModel.

### ⚠️ IMMEDIATE ACTION REQUIRED

**Build Status:** ❌ FAILING (3 CS0103 errors - `_pipelineManager` does not exist)

**Critical Issue:** The J.6 implementation incorrectly replaced the old ModesSettingsPage (utility pipeline configuration) with DictationModesSettingsPage, causing:
1. Lost access to Ask/Refine/Translate/Note/Chat configuration
2. Build failures due to incomplete refactoring
3. Duplicate hotkey configuration UI
4. Corrupted settings.json (missing built-in modes)

**Fix Plan Available:** See [MODES_PAGE_FIX.md](MODES_PAGE_FIX.md) for detailed step-by-step fix plan.

**Quick Summary:**
- Revert SettingsWindow navigation "modes" tag back to ModesSettingsPage
- Add new "Dictation Modes" navigation item
- Remove utility pipeline code from DictationModesSettingsViewModel (fix build)
- Remove hotkey fields from DictationModesSettingsPage
- Update ModesSettingsPage to show only Ask/Refine/Translate (remove Note/Chat)
- Delete corrupted settings.json to trigger fresh migration
- Note and Chat will get their own pages in Phase 2 (future task)

### ✅ Completed (All Sessions)

| Stream | Scope | Sessions |
|--------|-------|----------|
| **A** — Scaffolding | Solution scaffold, build config, publish pipeline | 1 |
| **B** — Core Engine | Audio recording, device management, hotkeys, text injection, mute detection | 1–2 |
| **C** — STT & LLM Providers | Deepgram, Gemini Audio, Whisper.net, OpenAI-compatible, Anthropic, Ollama | 3 |
| **D** — Pipeline Orchestration | Dictation, Refine, Ask, Translate, Note, Oops pipelines | 3 |
| **E** — Data & Security | SettingsManager, ProfileManager, PromptRepository, HistoryManager, MetricsCollector, NoteWriter, SecureStorage, PIIScrubber, ApiKeyValidator, DI wiring | 4 |
| **F** — UI (WinUI 3) | Settings (10 tabs), Control Panel, Wizard, Loading Screen, Quick Chat overlay, Notifications, Tray icon | 5 |
| **G** — Testing & CI/CD | 521 unit tests, GitHub Actions CI (12-step pipeline), coverage tracking | 5–6 |
| **I** — Promoted Features | SnippetManager, AudioDucker, ChatPipeline, OllamaManager | 4–5 |
| **J** — CRUD Dictation Modes | DictationMode/PipelineConfig models, Managers, Migration, Pipeline integration, Per-mode model selection, UI (needs fix) | 7 |

### 📊 Current Metrics

| Metric | Value |
|--------|-------|
| **Tests** | 521 passing locally (before UI break) |
| **Coverage** | ~74% line, ~52% branch (DiktaMe.Core) |
| **Build** | ❌ FAILING (3 errors in DictationModesSettingsViewModel) |
| **CI** | Not pushed (build broken) |
| **Publish size** | ~173MB uncompressed, ~70MB compressed (win-x64, self-contained, trimmed) |
| **Branch** | main (trunk-based, uncommitted changes) |

### 🔧 Stream J: CRUD Dictation Modes (Status: 95% Complete, UI Fix Required)

#### ✅ J.1: Core Data Models
- `DictationMode` (id, title, cloud/local profiles, sort order, IsBuiltIn flag)
- `DictationProfile` (system prompt, UseLlm, ModelName, Hotkey)
- `PipelineConfig` (for Ask/Refine/Translate/Note/Chat utility pipelines)
- `UtilityProfile` (system prompt, ModelName)
- `DictationModeDefaults` (4 built-in modes: Standard, Prompt, Professional, Raw)

#### ✅ J.2: CRUD Services
- `DictationModeManager` (GetAll, GetById, GetActiveProfile, Create, Update, Delete, Reorder)
- `PipelineConfigManager` (GetAll, GetByType, UpdatePipeline)
- Built-in modes can be edited but not deleted
- Custom modes fully CRUD-able

#### ✅ J.3: Settings Migration
- Auto-populate DictationModes and UtilityPipelines on first run
- Migrate `ActiveProfile` → `ActiveProfileName` ("Cloud" or "Local")
- `SettingsMigrationService.MigrateAsync()` handles defaults

#### ✅ J.4: Pipeline Integration
- `LoadingViewModel` uses `DictationModeManager.GetActiveProfile()` for mode-specific settings
- Pipelines receive mode-specific system prompts and model names
- Legacy `ProfileManager` marked deprecated but still used in old ModesSettingsPage

#### ✅ J.5: LLMRouter Per-Mode Model Support
- `ModelListService` queries all providers (OpenAI, Anthropic, Gemini, OpenRouter, Ollama)
- Returns `List<ModelInfo>` with DisplayName, ModelId, Provider
- `LLMRouter.ProcessWithModelAsync(modelName:)` overload for per-mode model selection
- Cloud profile can specify different model per dictation mode

#### ⚠️ J.6: UI — Dictation Modes Settings Tab (BROKEN - FIX REQUIRED)
- **Created:** DictationModesSettingsPage.xaml + DictationModesSettingsViewModel
- **Issue:** Replaced ModesSettingsPage entirely, losing Ask/Refine/Translate/Note/Chat config
- **Build Error:** References `_pipelineManager` field that doesn't exist (lines 240, 355)
- **Hotkey Duplication:** Included hotkey fields that are already in Hotkeys tab
- **Settings Corruption:** settings.json has wrong data (1 GUID mode instead of 4 built-ins)

#### ✅ J.7: Documentation
- DEVELOPMENT_ROADMAP.md updated (Stream J marked complete with caveat)
- MEMORY.md updated with Stream J completion and new gotchas
- **NEW:** MODES_PAGE_FIX.md created with detailed fix plan

### 📋 Next Steps (Priority Order)

#### 1. **IMMEDIATE: Fix J.6 UI Issues** (Phase 1 - This Must Be Done First)
See [MODES_PAGE_FIX.md](MODES_PAGE_FIX.md) for complete plan. Summary:

1. Fix build failures in DictationModesSettingsViewModel (remove `_pipelineManager` references)
2. Revert SettingsWindow navigation "modes" tag back to ModesSettingsPage
3. Add "Dictation Modes" navigation item to SettingsWindow.xaml
4. Remove hotkey fields from DictationModesSettingsPage (XAML + ViewModel)
5. Update ModesSettingsPage to show only Ask/Refine/Translate (remove Note/Chat from sidebar)
6. Delete `C:\Users\gecko\AppData\Roaming\DiktaMe\settings.json` to trigger migration
7. Fix model dropdown dispatcher issue
8. Verify 521 tests still pass

#### 2. **Create Note and Chat Settings Pages** (Phase 2 - Future Task)
- Create NotesSettingsPage + ViewModel (V1 had this)
  - File path with Browse button
  - LLM Processing toggle
  - Timestamp format input
  - Note System Prompt editor
  - Live preview
- Create ChatSettingsPage + ViewModel (new for V2)
  - Chat configuration options (TBD)
- Add navigation items for both
- Consider migrating Note/Chat out of UtilityPipelines array

#### 3. **Work Stream H — Distribution**
- H.1: Installer (MSIX or Inno Setup)
- H.2: V1 Migration

### 🔍 Key Context

#### Architecture
- **ARCHITECTURE.md** — Complete architectural spec (14 sections, up to date)
- **DEVELOPMENT_ROADMAP.md** — Full task breakdown with V1 "Port from" references
- **MODES_PAGE_FIX.md** — Fix plan for current UI issues
- **ci/DECISIONS.md** — All CI rule suppressions with rationale and revisit conditions

#### Settings Navigation Structure

**Current V2 (BROKEN):**
- General, Hotkeys, AI Engine, **Modes** (broken - shows DictationModesSettingsPage), Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

**After Phase 1 Fix:**
- General, Hotkeys, AI Engine, **Modes** (Ask/Refine/Translate only), **Dictation Modes** (Standard/Prompt/Professional/Raw + custom), Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

**After Phase 2 (Future):**
- General, Hotkeys, AI Engine, **Modes** (Ask/Refine/Translate), **Dictation Modes** (CRUD), **Notes** (file config + prompt), **Chat** (TBD), Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

**V1 Reference:**
- General, Audio, **Modes**, **Notes**, Control Panel, Ollama, API Keys, Privacy, About

#### Build & Test Commands
```bash
# WILL FAIL until Phase 1 fix is complete
dotnet build DiktaMe.sln -c Release          # Currently FAILS (3 errors)
dotnet test DiktaMe.sln                       # Cannot run (build broken)

# After fix:
dotnet build DiktaMe.sln -c Release          # Should: 0 errors, 0 warnings
dotnet test DiktaMe.sln                       # Should: 521 tests pass
publish-release.cmd                           # Trimmed self-contained win-x64
```

#### CI Pipeline (.github/workflows/ci-v2.yml)
12-step single-job pipeline on `windows-latest`:
Restore → Lint → Build → Test → Test-count threshold → Secret scan → Vuln audit → Deprecated packages → Publish → Publish size guard → Upload coverage → Upload publish artifact

#### Key Files
| File | Purpose |
|------|---------|
| `MODES_PAGE_FIX.md` | **NEW:** Step-by-step fix plan for J.6 UI issues |
| `Directory.Build.props` | Shared build config (C# 12, nullable, TreatWarningsAsErrors) |
| `.editorconfig` | Code style rules (Meziantou.Analyzer + naming) |
| `global.json` | SDK pin to 8.0.418 (dotnet format consistency) |
| `.gitleaks.toml` | Allowlist for test-fixture false positive |
| `ci/test-threshold.json` | Minimum test count (521 after J.1-J.5) + publish size bounds |
| `ci/DECISIONS.md` | CI suppression rationale |

#### Git Status (Uncommitted Changes)
**Modified files:**
- `DEVELOPMENT_ROADMAP.md` (Stream J marked complete)
- `src/DiktaMe.App/App.xaml.cs` (DictationModeManager DI registration)
- `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` (Uses CRUD system)
- `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` (Points to DictationModesSettingsPage - needs revert)
- `src/DiktaMe.Core/Config/AppSettings.cs` (DictationModes + UtilityPipelines arrays)
- `src/DiktaMe.Core/Config/SettingsManager.cs` (Migration logic)
- Multiple pipeline files (mode-specific options)
- Multiple test files (521 tests now)

**New files:**
- `src/DiktaMe.Core/Config/DictationMode.cs`
- `src/DiktaMe.Core/Config/PipelineConfig.cs`
- `src/DiktaMe.Core/Config/DictationModeDefaults.cs`
- `src/DiktaMe.Core/Config/DictationModeManager.cs`
- `src/DiktaMe.Core/Config/PipelineConfigManager.cs`
- `src/DiktaMe.Core/Config/PromptDefaults.cs`
- `src/DiktaMe.Core/LLM/ModelInfo.cs`
- `src/DiktaMe.Core/LLM/ModelListService.cs`
- `src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs` (BROKEN)
- `src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml` (BROKEN)
- `src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml.cs`
- `tests/DiktaMe.Core.Tests/Config/DictationModeDefaultsTests.cs`
- `tests/DiktaMe.Core.Tests/Config/DictationModeManagerTests.cs`
- `tests/DiktaMe.Core.Tests/Config/PromptDefaultsTests.cs`
- `tests/DiktaMe.Core.Tests/LLM/ModelListServiceTests.cs`
- `MODES_PAGE_FIX.md`

**DO NOT COMMIT** until Phase 1 fix is complete and build passes.

#### Known Issues / Tech Debt
- **CRITICAL:** Build currently fails (J.6 UI implementation incomplete)
- **CRITICAL:** settings.json corrupted (delete to trigger migration)
- `TextInjectorTests` uses real Win32 clipboard — tagged `[Trait("Category","Hardware")]`, excluded from CI
- No streaming LLM responses yet — `IAsyncEnumerable<string>` deferred to V2.1
- No Voice Activity Detection (VAD) — hands-free mode deferred to V2.1
- Legacy `ProfileManager` and `PromptRepository` marked deprecated but still used in old ModesSettingsPage

#### Shell Gotchas (Windows + Bash)
- PowerShell `$_` gets mangled by bash — use `powershell -NoProfile -File -` with heredoc
- `/p:Platform=x64` needs quoting as `"-p:Platform=x64"` in bash
- `global.json` pins SDK to 8.0.418 — `windows-latest` has .NET 10 pre-installed which has different `dotnet format` rules

#### Namespace Gotcha
- Never use `DiktaMe.Core.System` as a namespace — shadows BCL `System`. Actual namespace is `DiktaMe.Core.SystemManagement`, folder is `System/`.

#### WinUI 3 XAML Gotchas
- `x:Bind` is NOT supported on `Run.Text` — XAML compiler silently crashes
- `InfoBar.ActionButton` must be a single `ButtonBase`, not a StackPanel
- Converter keys in SharedResources.xaml: Use `BoolToVis`, `InverseBoolToVis`, `BoolNeg`, `NullToVis`
- Cross-thread ObservableCollection updates: Must use `DispatcherQueue.TryEnqueue()`

### 🚨 Critical Reminder for Next Developer

**DO NOT PROCEED with any new features until Phase 1 fix is complete:**

1. Read [MODES_PAGE_FIX.md](MODES_PAGE_FIX.md) thoroughly
2. Fix the build failures step-by-step
3. Verify all 521 tests pass
4. Delete corrupted settings.json and restart app to trigger migration
5. Manually test: Settings → Modes (Ask/Refine/Translate), Settings → Dictation Modes (CRUD)
6. Only then commit and push

**Estimated Time:** 2-3 hours for Phase 1 fix (straightforward but requires care)

### 🏷️ Tags Due
Tag v2.0.0-beta.1 AFTER Phase 1 fix is complete and CI is green:
```bash
git tag -a v2.0.0-beta.1 -m "beta.1: Stream J complete (CRUD Dictation Modes)"
git push origin v2.0.0-beta.1
```
