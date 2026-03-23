# SPEC_009: Local Mode End-to-End

> **Status:** DRAFT
> **Date:** 2026-03-09
> **Priority:** Critical — Local mode is non-functional despite all building blocks being implemented
> **Parent Specs:** `DEVELOPMENT_ROADMAP.md`

---

## 1. Problem Statement

Local mode (Whisper STT + Ollama LLM, with GPU acceleration) does not work end-to-end. A user who deletes their settings and runs the wizard choosing "Local" will hit multiple hard failures before they can dictate a single word.

All the heavy infrastructure exists:

| Component | File | Status |
|-----------|------|--------|
| WhisperProvider (Whisper.net 1.9.0 GGML) | `DiktaMe.Core/STT/WhisperProvider.cs` | ✅ Implemented |
| Whisper.net.Runtime.Cuda NuGet | `DiktaMe.Core/DiktaMe.Core.csproj` | ✅ Referenced |
| Model download with progress events | `WhisperProvider.DownloadModelAsync()` | ✅ Implemented, **never called** |
| OllamaProvider with warmup & tok/s monitoring | `DiktaMe.Core/LLM/OllamaProvider.cs` | ✅ Implemented |
| OllamaProvider.WarmUpAsync() | `OllamaProvider.cs:83` | ✅ Implemented, **never called** |
| OllamaManager health check & version compat | `DiktaMe.Core/System/OllamaManager.cs` | ✅ Implemented |
| STT/LLM routers with fallback | `STTRouter.cs`, `LLMRouter.cs` | ✅ Implemented |
| STT/LLM provider factories | `STTProviderFactory.cs`, `LLMProviderFactory.cs` | ✅ Implemented |
| Wizard with Local STT/LLM choices | `WizardViewModel.cs` | ✅ UI works, **wiring broken** |
| AI Engine settings page | `AIEngineSettingsPage.xaml` | ✅ Cloud settings only |
| Ollama settings page | `OllamaSettingsViewModel.cs` | ✅ Health check only |

The pieces are all there. They just aren't connected.

---

## 2. Failure Analysis: What Breaks Today

### Scenario: Delete settings → Wizard → Local → Dictate

**Step-by-step trace through the code:**

#### Failure 1: Whisper model not downloaded → `FileNotFoundException`

1. User selects "Local" for STT in wizard
2. `CompleteWizardAsync()` sets `ModeProfiles[*].SttProvider = "whisper"` — correct
3. User finishes wizard, tries to dictate (Ctrl+Alt+D)
4. `PipelineFactory.GetProviders()` → `STTProviderFactory.CreateProvider("whisper")` → `new WhisperProvider("turbo")`
5. `WhisperProvider.TranscribeAsync()` → `File.Exists(_modelPath)` → **false**
6. **Throws `FileNotFoundException`**: "Whisper model 'turbo' not found at '%APPDATA%\DiktaMe\models\ggml-large-v3-turbo.bin'"

**Root cause**: `DownloadModelAsync()` is never called. Not during wizard, not during startup, not anywhere.

#### Failure 2: Wrong profile → LLM routes to cloud provider → no API key

1. User selects "Local" for LLM in wizard
2. `CompleteWizardAsync()` sets `ModeProfiles[*].LlmProvider = "ollama"` — correct
3. But `ActiveProfileName` stays `"Cloud"` (wizard never sets it)
4. User presses Ctrl+Alt+D → `RunBatchDictationAsync()` runs
5. `_dictationModes.GetActiveProfile(modeId)` → checks `ActiveProfileName == "Cloud"` → returns **`CloudProfile`**
6. `CloudProfile.ModelName = "gpt-4o-mini"` → set as `options.ModelName`
7. `ProcessWithModelAsync(text, prompt, "gpt-4o-mini")` → model name is non-null
8. Router calls `ResolveProviderForModel("gpt-4o-mini")` → resolves to `"openai"`
9. Factory tries `CreateProvider("openai", "gpt-4o-mini")` → **no OpenAI API key → failure**

**Root cause**: Wizard writes to old `ModeProfiles` system but doesn't set `ActiveProfileName` for the new Stream J CRUD system. The two profile systems are out of sync.

#### Failure 3: Ollama not warmed up → first dictation painfully slow

Even if failures 1 & 2 were fixed:

1. `OllamaProvider.WarmUpAsync()` is never called during startup
2. First dictation sends a full prompt to cold Ollama
3. Ollama must load model into VRAM first → 3-5s on GPU, 30+ seconds on CPU
4. User perceives the app as broken/frozen

**Root cause**: `WarmUpAsync()` exists but is never called from `LoadingViewModel` or anywhere else.

#### Failure 4: No Whisper model selection → stuck on turbo

1. `STTProviderFactory` hardcodes `new WhisperProvider("turbo")` when provider is `"whisper"`
2. No `WhisperModel` setting exists in `AppSettings`
3. No UI to change model size in Settings > AI Engine
4. Users with limited VRAM or slow CPUs can't downgrade to `small`/`base`/`tiny`

#### Failure 5: AI Engine toggles don't sync `ActiveProfileName`

1. User goes to Settings > AI Engine
2. Changes STT mode from Cloud to Local (index 0 → 1)
3. `AIEngineSettingsViewModel` writes to `ModeProfiles["dictate_0"]` — old system
4. `ActiveProfileName` stays `"Cloud"` — new system unchanged
5. Same model-name-override failure as Failure 2

---

## 3. Design Constraint: Hybrid Configurations

STT and LLM choices are **independent**. Users may legitimately run:

| Configuration | STT | LLM | Use case |
|---------------|-----|-----|----------|
| Full Local | Whisper | Ollama | Privacy-first, offline capable |
| Full Cloud | Deepgram | Gemini/OpenAI | Best quality, requires API keys |
| **Hybrid: Local STT + Cloud LLM** | Whisper | Gemini/OpenAI | Free STT, cloud LLM quality |
| **Hybrid: Cloud STT + Local LLM** | Deepgram | Ollama | Streaming STT + local processing |

The implementation must NOT treat "Local" as a single binary toggle. Whisper model download must trigger whenever STT is local, regardless of LLM choice. Ollama warmup must trigger whenever LLM is local, regardless of STT choice.

## 4. Current Architecture (Two Profile Systems)

Understanding this is key to the fix:

| System | Era | Controls | Key | Used by |
|--------|-----|----------|-----|---------|
| **ModeProfiles** | Pre-Stream-J | STT/LLM *provider* name (`"whisper"`, `"ollama"`) | `ModeProfiles["dictate_0"]` + `ActiveProfile` (int 0/1) | `ProfileManager` → `PipelineFactory.GetProviders()` |
| **DictationModes / UtilityPipelines** | Stream J | Model name, system prompt, hotkey | `DictationModes[].CloudProfile/LocalProfile` + `ActiveProfileName` (string `"Cloud"`/`"Local"`) | `DictationModeManager` / `PipelineConfigManager` → `LoadingViewModel` pipeline handlers |

The wizard writes to system 1. The pipeline handlers read model names from system 2. They must agree.

**The `ActiveProfileName` problem**: This string controls which `ModelName` is used. `CloudProfile.ModelName` = `"gpt-4o-mini"`, `LocalProfile.ModelName` = `null`. When LLM is local, `ActiveProfileName` must be `"Local"` so the `null` model name passes through to the Ollama primary provider. When LLM is cloud, `ActiveProfileName` must be `"Cloud"` so the specific cloud model name is used. This is correct regardless of STT choice — STT provider is determined by `ModeProfiles`, not by `ActiveProfileName`.

---

## 5. Existing Code to Reuse

These methods are already implemented and tested — they just need to be called:

| Method | File | What it does |
|--------|------|-------------|
| `WhisperProvider.DownloadModelAsync()` | `STT/WhisperProvider.cs:162` | Downloads GGML model with chunked streaming + progress events |
| `WhisperProvider.IsModelDownloaded` | `STT/WhisperProvider.cs:223` | Property: checks if model file exists on disk |
| `WhisperProvider.DownloadProgress` event | `STT/WhisperProvider.cs:48` | EventHandler with `(Percent, BytesReceived, TotalBytes)` |
| `OllamaProvider.WarmUpAsync()` | `LLM/OllamaProvider.cs:83` | Loads model into VRAM with single-token request, fire-and-forget |
| `OllamaManager.CheckAsync()` | `System/OllamaManager.cs:147` | Full preflight: version, compatibility, model pulled check |
| `OllamaManager.GetInstalledModelTagsAsync()` | `System/OllamaManager.cs:230` | Lists installed models via `/api/tags` |
| `WhisperProvider` model map | `STT/WhisperProvider.cs:24-33` | Maps `tiny/base/small/medium/large/turbo` → GgmlType + filename |

---

## 6. Implementation Plan

### Task 0: Set Sensible Wizard Defaults for Local Mode

**Problem**: Current defaults are wrong for local mode:
- `AppSettings.OllamaModel` defaults to `"llama3.2"` — should be `"gemma3"` (V1 proved `gemma3:4b` is the best balance)
- No `WhisperModel` setting exists yet — Task 4 adds it, and its default must be `"small"` (not `"turbo"`)

**Why `small` Whisper + `gemma3` Ollama**: On an 8GB GPU (the typical consumer card), `gemma3:4b` uses ~3-4GB VRAM. The Whisper `small` model (~466MB on disk, modest VRAM footprint) leaves enough headroom for both models plus the OS. V1 users confirmed this combination as the best quality-vs-resources balance. The `turbo` model (1.6GB) works but competes with Ollama for VRAM on 8GB cards.

**Files**:
- `src/DiktaMe.Core/Config/AppSettings.cs` — Change `OllamaModel` default from `"llama3.2"` to `"gemma3"`
- `src/DiktaMe.Core/Config/AppSettings.cs` — When adding `WhisperModel` (Task 4), default to `"small"` not `"turbo"`

**AppSettings.cs** change:
```csharp
// Before:
public string OllamaModel { get; init; } = "llama3.2";

// After:
public string OllamaModel { get; init; } = "gemma3";
```

**Wizard behavior**: `CompleteWizardAsync()` does not need to explicitly set model names — it relies on these defaults. Users can later change models in Settings > Ollama (model dropdown) and Settings > AI Engine (Whisper model dropdown, added in Task 4).

**Note**: Existing users who already have `"OllamaModel": "llama3.2"` in their settings.json won't be affected — the default only applies to new installs or deleted settings.

### Task 1: Fix Wizard `ActiveProfileName` Wiring

**File**: `src/DiktaMe.App/ViewModels/WizardViewModel.cs`

In `CompleteWizardAsync()` (~line 131), set `ActiveProfileName` based on `LlmChoice`. The LLM choice drives `ActiveProfileName` because that's what controls which `ModelName` gets used (Cloud → `"gpt-4o-mini"`, Local → `null`). STT provider selection comes from `ModeProfiles` independently.

```csharp
// LLM choice determines ActiveProfileName (controls model name resolution)
// STT choice is independent — handled by ModeProfiles provider field
string profileName = string.Equals(LlmChoice, "local", StringComparison.Ordinal)
    ? "Local" : "Cloud";

var updated = _settings.Current with
{
    WizardCompleted = true,
    ActiveProfileName = profileName,
};
```

**Hybrid example**: User picks Whisper (local STT) + Gemini (cloud LLM):
- `ActiveProfileName = "Cloud"` → `CloudProfile.ModelName = "gpt-4o-mini"` → correct cloud model
- `ModeProfiles["dictate_0"].SttProvider = "whisper"` → `PipelineFactory` creates `WhisperProvider` → correct local STT
- Both work independently. No conflict.

### Task 2: Auto-Download Whisper Model During Startup

**File**: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

After settings load (step 1, ~line 89), add a Whisper model download step. This triggers whenever STT is local, **regardless of LLM choice** (supports hybrid: local STT + cloud LLM):

1. Check if active STT provider is `"whisper"` (read from `ModeProfiles["dictate_{ActiveProfile}"]`)
2. Resolve `WhisperProvider` from DI
3. If `!IsModelDownloaded`:
   - Update `StatusText` to "Downloading Whisper model..."
   - Wire `DownloadProgress` event → update `StatusText` with `$"Downloading Whisper model... {percent}%"`
   - Call `await provider.DownloadModelAsync()`
   - Log completion

Non-fatal: if download fails (no internet), log warning and continue. User will get `FileNotFoundException` at dictation time with a clear error message.

### Task 3: Ollama Warmup During Startup

**File**: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

After the existing Ollama health check (step 4, ~line 107), if Ollama status is `Ready` and local LLM is configured. This triggers whenever LLM is local, **regardless of STT choice** (supports hybrid: cloud STT + local LLM):

1. Check if active LLM provider is `"ollama"` (read from `ModeProfiles["dictate_{ActiveProfile}"]`)
2. Update `StatusText` to "Warming up Ollama model..."
3. Resolve `OllamaProvider` from DI
4. Call `await provider.WarmUpAsync()`
5. Non-fatal: already handled inside `WarmUpAsync()` (catches + logs)

### Task 4: Add `WhisperModel` Setting + Model Selection UI

**Files**:
- `src/DiktaMe.Core/Config/AppSettings.cs` — Add `WhisperModel` property
- `src/DiktaMe.Core/Config/STTProviderFactory.cs` — Use `settings.WhisperModel` instead of hardcoded `"turbo"`
- `src/DiktaMe.App/ViewModels/Settings/AIEngineSettingsViewModel.cs` — Add Whisper model picker properties
- `src/DiktaMe.App/Views/Settings/AIEngineSettingsPage.xaml` — Add Whisper model ComboBox

**AppSettings.cs**:
```csharp
/// <summary>Whisper model size for local STT (tiny, base, small, medium, large, turbo).</summary>
public string WhisperModel { get; init; } = "small";
```

**STTProviderFactory.cs**: Change the `"whisper"` case to read model from settings:
```csharp
"whisper" => new WhisperProvider(_settings.Current.WhisperModel),
```

**AIEngineSettingsViewModel.cs**: Add:
- `WhisperModelIndex` observable property
- `WhisperModels` array: `["Tiny (~75MB)", "Base (~142MB)", "Small (~466MB, recommended)", "Medium (~1.5GB)", "Large (~3GB)", "Turbo (~1.6GB)"]`
- `WhisperModelCodes` array: `["tiny", "base", "small", "medium", "large", "turbo"]`
- `OnWhisperModelIndexChanged` handler → save to `AppSettings.WhisperModel`
- `IsWhisperSectionVisible` computed from `SttModeIndex == 1`

**AIEngineSettingsPage.xaml**: Add a Whisper section (conditionally visible when STT = Local):
- Section header: "Whisper (Local STT)"
- Model ComboBox with the 6 size options
- Note: "Models are downloaded automatically on first use"

### Task 5: Sync AI Engine Mode Toggles with `ActiveProfileName`

**File**: `src/DiktaMe.App/ViewModels/Settings/AIEngineSettingsViewModel.cs`

Add `OnSttModeIndexChanged` and `OnLlmModeIndexChanged` partial methods that:

1. Write to `ModeProfiles` (existing behavior — already happens in some form)
2. **Also** set `ActiveProfileName` to match:
   - LLM Local (index 1) → `ActiveProfileName = "Local"`
   - LLM Cloud (index 0) → `ActiveProfileName = "Cloud"`
3. Update `CapabilitySummary` text to reflect changes

Currently these handlers don't exist — the toggles save nothing when changed. This also needs fixing.

---

## 7. Key V1 Reference (What Worked)

For context on what V1 did differently (Python + Electron):

| Aspect | V1 | V2 (target) |
|--------|-----|-------------|
| **Whisper engine** | `faster-whisper` 1.2.1 (CTranslate2) | Whisper.net 1.9.0 (GGML) — already referenced |
| **GPU runtime** | `nvidia-cublas-cu12`, `nvidia-cudnn-cu12`, `torch` | `Whisper.net.Runtime.Cuda` — already referenced |
| **GPU detection** | `ctranslate2.get_cuda_device_count()` → torch → nvidia-smi | Automatic via Whisper.net runtime selection |
| **Compute type** | `int8_float16` (GPU) / `int8` (CPU) | Whisper.net handles internally |
| **Model download** | HuggingFace Hub + offline fallback | `WhisperGgmlDownloader.Default.GetGgmlModelAsync()` — already implemented |
| **Model variants** | tiny, base, small, medium, large, turbo | Same 6 — already mapped in `WhisperProvider.ModelMap` |
| **Ollama warmup** | Single-token `/api/generate` on startup | `OllamaProvider.WarmUpAsync()` — already implemented |
| **Ollama keep-alive** | `"keep_alive": "10m"` | Already set in `OllamaProvider.BuildRequestJson()` |
| **Inference monitoring** | `tokens/sec` logging, <20 tok/s alert | `OllamaProvider.LastTokensPerSec` + logging — already implemented |
| **Ollama model selection** | Settings UI with installed models dropdown | `OllamaSettingsViewModel` — already implemented |

---

## 8. Files to Modify

| File | Changes |
|------|---------|
| `src/DiktaMe.App/ViewModels/WizardViewModel.cs` | Set `ActiveProfileName` in `CompleteWizardAsync()` |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Auto-download Whisper model + Ollama warmup during startup |
| `src/DiktaMe.App/ViewModels/Settings/AIEngineSettingsViewModel.cs` | Add Whisper model picker, add mode change handlers, sync `ActiveProfileName` |
| `src/DiktaMe.App/Views/Settings/AIEngineSettingsPage.xaml` | Add Whisper model section (conditionally visible) |
| `src/DiktaMe.Core/Config/AppSettings.cs` | Change `OllamaModel` default to `"gemma3"`, add `WhisperModel` property (default `"small"`) |
| `src/DiktaMe.Core/Config/STTProviderFactory.cs` | Read `WhisperModel` from settings |

---

## 9. Verification

### Manual Test: Fresh Local Setup

1. Delete `%APPDATA%\DiktaMe\settings.json` and `%APPDATA%\DiktaMe\models\` folder
2. Ensure Ollama is running (`ollama serve`) with a pulled model (`ollama pull gemma3`)
3. Launch app → wizard appears
4. Step 0: Choose "Set up API Keys"
5. Step 1 (STT): Choose "Local" (Whisper)
6. Step 2 (LLM): Choose "Local" (Ollama)
7. Step 3 (API Keys): **Should be skipped** (both local — no keys needed)
8. Steps 4-5: Audio test → Finish
9. Loading screen shows: "Downloading Whisper model... 42%" → "Warming up Ollama model..."
10. Verify `%APPDATA%\DiktaMe\settings.json` contains `"ActiveProfileName": "Local"`, `"WhisperModel": "small"`, `"OllamaModel": "gemma3"`
11. Verify `%APPDATA%\DiktaMe\models\ggml-small.bin` exists (~466MB)
12. Press Ctrl+Alt+D → record → Whisper transcribes (GPU if CUDA available) → Ollama (gemma3) cleans up → text injected at cursor
13. Settings > AI Engine: Whisper model dropdown visible with "Small" selected, Deepgram section hidden
14. Change Whisper model to "Medium" → `settings.json` updates, next dictation downloads + uses medium model

### Manual Test: Hybrid (Local STT + Cloud LLM)

1. Delete `%APPDATA%\DiktaMe\settings.json`
2. Launch app → wizard
3. Step 1 (STT): Choose "Local" (Whisper)
4. Step 2 (LLM): Choose "Cloud" (Gemini)
5. Step 3 (API Keys): Gemini key page appears (STT = local → no Deepgram key needed)
6. Enter Gemini API key → Finish
7. Loading screen downloads Whisper model (local STT) but does NOT warmup Ollama (cloud LLM)
8. Verify `settings.json`: `"ActiveProfileName": "Cloud"`, `ModeProfiles` have `SttProvider: "whisper"`, `LlmProvider: "gemini"`
9. Press Ctrl+Alt+D → Whisper transcribes locally → Gemini cleans up via cloud → text injected

### Automated Tests

Run `dotnet test DiktaMe.sln` — all existing tests must pass (no regressions).

New tests to consider:
- `WizardViewModel` test: verify `ActiveProfileName = "Local"` when `LlmChoice = "local"`
- `WizardViewModel` test: verify `ActiveProfileName = "Cloud"` when `LlmChoice = "cloud"`
- `WizardViewModel` test: verify hybrid (STT local + LLM cloud) sets `ActiveProfileName = "Cloud"` and `SttProvider = "whisper"`

---

## 10. Out of Scope (Future)

These are intentionally **not** part of this spec:

- **GPU detection UI** — Whisper.net handles GPU selection automatically. No toggle needed.
- **Performance metrics dashboard** — `OllamaProvider.LastTokensPerSec` is logged but not surfaced in UI. Can be added later.
- **Ollama model pull from UI** — `OllamaSettingsViewModel` lists installed models but doesn't pull new ones. Users run `ollama pull` from terminal.
- **Ollama auto-install/auto-start** — User installs Ollama separately. V1 had same requirement.
- **System monitor (CPU/RAM/VRAM)** — V1 feature, not needed for "Local works" baseline.
- **Whisper model download from Settings** — Model downloads on first use during startup. Settings just selects which model. If user changes model, the new one downloads on next app launch.

---

## 11. Task Log

> Track progress across sessions. Mark subtasks with `[x]` as completed.
> Convention: `⬜ Phase` = not started, `🔶 Phase` = in progress, `✅ Phase` = complete

---

### Phase A: Core Config (Task 0 + Task 4 settings portion)

**Goal**: Get `AppSettings` and `STTProviderFactory` ready with correct defaults and new `WhisperModel` property.

| # | Subtask | File(s) | Status |
|---|---------|---------|--------|
| A.1 | Change `OllamaModel` default `"llama3.2"` → `"gemma3"` | `AppSettings.cs:336` | ✅ |
| A.2 | Add `WhisperModel` property (default `"small"`) | `AppSettings.cs` (near line 336) | ✅ |
| A.3 | Update `STTProviderFactory` to read `WhisperModel` from settings instead of hardcoded `"turbo"` | `STTProviderFactory.cs` | ✅ |
| A.4 | Build — verify 0 errors | `dotnet build DiktaMe.sln` | ✅ |
| A.5 | Run tests — verify 0 regressions | `dotnet test DiktaMe.sln` | ✅ (687 pass) |

---

### Phase B: Wizard Fix (Task 1)

**Goal**: `CompleteWizardAsync()` sets `ActiveProfileName` based on LLM choice. Fixes Failure 2.

| # | Subtask | File(s) | Status |
|---|---------|---------|--------|
| B.1 | In `CompleteWizardAsync()`, compute `profileName` from `LlmChoice` (`"local"` → `"Local"`, else → `"Cloud"`) | `WizardViewModel.cs` (~line 131) | ✅ |
| B.2 | Set `ActiveProfileName = profileName` on the `updated` settings record | `WizardViewModel.cs` | ✅ |
| B.3 | Add unit test: `LlmChoice="local"` → `ActiveProfileName="Local"` | `SettingsManagerTests.cs` | ✅ |
| B.4 | Add unit test: `LlmChoice="cloud"` → `ActiveProfileName="Cloud"` | `SettingsManagerTests.cs` | ✅ |
| B.5 | Add unit test: hybrid (STT local + LLM cloud) → `ActiveProfileName="Cloud"` AND `SttProvider="whisper"` | `SettingsManagerTests.cs` | ✅ |
| B.6 | Build + test pass | | ✅ (690 pass) |

---

### Phase C: Startup Wiring (Tasks 2 + 3)

**Goal**: `LoadingViewModel` downloads Whisper model (if STT=whisper) and warms up Ollama (if LLM=ollama) during startup. Fixes Failures 1 and 3.

| # | Subtask | File(s) | Status |
|---|---------|---------|--------|
| C.1 | Read active STT provider from `ModeProfiles` in `LoadingViewModel.InitializeAsync()` | `LoadingViewModel.cs` | ✅ |
| C.2 | If STT=whisper: use DI `WhisperProvider`, check `IsModelDownloaded` | `LoadingViewModel.cs` | ✅ |
| C.3 | If not downloaded: wire `DownloadProgress` → `StatusText`, call `DownloadModelAsync()` | `LoadingViewModel.cs` | ✅ |
| C.4 | Wrap download in try/catch — non-fatal, log warning on failure | `LoadingViewModel.cs` | ✅ |
| C.5 | Read active LLM provider from `ModeProfiles` | `LoadingViewModel.cs` | ✅ |
| C.6 | If LLM=ollama AND Ollama check passed (Ready): call `WarmUpAsync()` via DI `OllamaProvider` | `LoadingViewModel.cs` | ✅ |
| C.7 | Update `StatusText` with localized strings + progress events | `LoadingViewModel.cs` + `Resources.resw` | ✅ |
| C.8 | Fix DI: `WhisperProvider(settings.WhisperModel)`, `OllamaProvider(settings.OllamaModel)` | `App.xaml.cs` | ✅ |
| C.9 | Build + test pass | | ✅ (690 pass) |

---

### Phase D: Settings UI (Task 4 UI portion + Task 5)

**Goal**: Whisper model picker in Settings > AI Engine, mode toggle handlers that sync `ActiveProfileName`. Fixes Failures 4 and 5.

| # | Subtask | File(s) | Status |
|---|---------|---------|--------|
| D.1 | Add `WhisperModelIndex` observable property | `AIEngineSettingsViewModel.cs` | ✅ |
| D.2 | Add `WhisperModels` display array (6 entries with sizes) | `AIEngineSettingsViewModel.cs` | ✅ |
| D.3 | Add `WhisperModelCodes` string array | `AIEngineSettingsViewModel.cs` | ✅ |
| D.4 | Add `IsWhisperSectionVisible` + `IsDeepgramSectionVisible` properties | `AIEngineSettingsViewModel.cs` | ✅ |
| D.5 | Add `OnWhisperModelIndexChanged` handler → save to `AppSettings.WhisperModel` | `AIEngineSettingsViewModel.cs` | ✅ |
| D.6 | Load `WhisperModelIndex` from settings in `LoadFromSettings()` | `AIEngineSettingsViewModel.cs` | ✅ |
| D.7 | Add `OnSttModeIndexChanged` handler → write STT to all `ModeProfiles`, toggle section visibility | `AIEngineSettingsViewModel.cs` | ✅ |
| D.8 | Add `OnLlmModeIndexChanged` handler → write LLM to all `ModeProfiles` AND sync `ActiveProfileName` | `AIEngineSettingsViewModel.cs` | ✅ |
| D.9 | Update `CapabilitySummary` in both mode change handlers via `UpdateCapabilitySummary()` | `AIEngineSettingsViewModel.cs` | ✅ |
| D.10 | Add Whisper section to XAML, wrap Deepgram in conditional visibility | `AIEngineSettingsPage.xaml` | ✅ |
| D.11 | Add localization strings for Whisper section (EN + ES) | `Resources.resw` × 2 | ✅ |
| D.12 | Build + test pass | | ✅ (690 pass) |

---

### Phase E: Integration Verification

**Goal**: End-to-end manual testing of all scenarios.

| # | Subtask | Status |
|---|---------|--------|
| E.1 | Full local test: delete settings → wizard → Local/Local → loading downloads Whisper + warms Ollama → dictate works | ⬜ |
| E.2 | Hybrid test: wizard → Local STT + Cloud LLM → Whisper downloads, no Ollama warmup, cloud LLM works | ⬜ |
| E.3 | Hybrid test: wizard → Cloud STT + Local LLM → no Whisper download, Ollama warms up, Deepgram STT works | ⬜ |
| E.4 | Settings test: change Whisper model in Settings > AI Engine → verify settings.json updates | ⬜ |
| E.5 | Settings test: toggle STT/LLM modes in Settings > AI Engine → verify ModeProfiles + ActiveProfileName sync | ⬜ |
| E.6 | Existing cloud test: wizard → Cloud/Cloud → everything still works as before (no regressions) | ⬜ |
| E.7 | Full test suite: `dotnet test DiktaMe.sln` — 0 failures | ✅ (690 pass) |

---

### Phase F: Commit & Cleanup

| # | Subtask | Status |
|---|---------|--------|
| F.1 | `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors | ✅ |
| F.2 | Commit with message: `feat(local): implement SPEC_009 local mode end-to-end [SPEC_009]` | ⬜ |
| F.3 | Update `DEVELOPMENT_ROADMAP.md` with SPEC_009 completion status | ⬜ |
| F.4 | Archive spec: move `plans/SPEC_009_LOCALFLOW.md` → `plans/archive/` | ⬜ |

---

### Progress Summary

| Phase | Description | Status |
|-------|-------------|--------|
| **A** | Core Config (defaults + WhisperModel setting) | ✅ |
| **B** | Wizard Fix (ActiveProfileName) | ✅ |
| **C** | Startup Wiring (download + warmup) | ✅ |
| **D** | Settings UI (Whisper picker + toggle sync) | ✅ |
| **E** | Integration Verification | 🔶 (automated ✅, manual ⬜) |
| **F** | Commit & Cleanup | ✅ (committed a95bad8) |
| **G** | Local Inference Diagnostics (Whisper GPU + Ollama + DB) | 🔶 (G.1-G.7 ✅, G.3 Ollama verify deferred to Tier 2. Vulkan verified, model reload fix applied) |
| **H** | FIX-10: Split Cloud/Local toggle into separate STT + LLM toggles | ⬜ |

---

## 12. Whisper Latency Investigation — GPU Acceleration

> **Date**: 2026-03-09
> **Status**: Root cause identified — Vulkan swap pending (see §12.8)
> **Reported symptom**: Whisper `small` model transcribes 3.5s audio in ~2800ms (should be <200ms with GPU)
> **Root cause**: `Whisper.net.Runtime.Cuda` doesn't bundle CUDA runtime DLLs → falls back to CPU silently
> **Fix**: Replace with `Whisper.net.Runtime.Vulkan` (28MB, cross-vendor, self-contained)

### 12.1. Evidence from Logs

```
[INF] WhisperProvider (small): transcribed in 2781ms — "this is a simple test"
[INF] DictationPipeline: complete — total=4851ms stt=3184ms llm=1552ms inj=114ms words=5

[INF] WhisperProvider (small): transcribed in 2816ms — "All right, that was a cold model..."
[INF] DictationPipeline: complete — total=6246ms stt=3178ms llm=2977ms inj=90ms words=11
```

Both runs show ~2800ms for the `small` model. The second run was NOT a cold model load (factory already initialized from first run). This is consistent with **CPU inference** on the `small` model.

V1 with `faster-whisper` on the same hardware achieved <200ms GPU transcription for similar audio.

### 12.2. V1 vs V2 Comparison (Verified from Code)

| Aspect | V1 (`E:\git\diktate\python\core\transcriber.py`) | V2 (`WhisperProvider.cs`) |
|--------|--------------------------------------------------|---------------------------|
| **Engine** | `faster-whisper` 1.2.1 → CTranslate2 | `Whisper.net` 1.9.0 → whisper.cpp (GGML) |
| **GPU package** | `nvidia-cublas-cu12==12.1.3.1`, `nvidia-cudnn-cu12==8.9.2.26` | `Whisper.net.Runtime.Cuda` 1.9.0 |
| **GPU detection** | Explicit: `ctranslate2.get_cuda_device_count()` → logs "CUDA detected" | Automatic: Whisper.net probes runtimes silently |
| **Compute type** | `int8_float16` (GPU) / `int8` (CPU) — explicitly set | Internal to GGML — not configurable |
| **Runtime logging** | Logs device, model name, device type on load | **None** — no log of which runtime was loaded |

### 12.3. V2 Runtime Architecture (Verified from NuGet XML docs)

Whisper.net 1.9.0 auto-selects runtime by priority order:

1. `Whisper.net.Runtime.Cuda` (CUDA 13 drivers)
2. `Whisper.net.Runtime.Cuda12` (CUDA 12 drivers) — **NOT referenced in V2**
3. `Whisper.net.Runtime.Vulkan` (Windows + Vulkan)
4. `Whisper.net.Runtime.CoreML` (Apple)
5. `Whisper.net.Runtime.OpenVino` (Intel)
6. `Whisper.net.Runtime` (CPU) — **referenced in V2**
7. `Whisper.net.Runtime.NoAvx` (CPU without AVX)

**Key risk**: V2 references `Whisper.net.Runtime.Cuda` (CUDA 13), but does NOT reference `Whisper.net.Runtime.Cuda12`. If the user's NVIDIA drivers support CUDA 12 but not CUDA 13, the CUDA probe fails silently and falls back to CPU.

### 12.4. Build Output Verification

Native DLLs present in build output (`bin/x64/Release/`):

```
runtimes/win-x64/           → ggml-base-whisper.dll, ggml-cpu-whisper.dll, ggml-whisper.dll, whisper.dll
runtimes/cuda/win-x64/      → ggml-base-whisper.dll, ggml-cpu-whisper.dll, ggml-cuda-whisper.dll, ggml-whisper.dll, whisper.dll
```

The CUDA DLLs exist. The question is whether the runtime loader successfully loads them.

### 12.5. Available Diagnostic APIs (Verified from NuGet XML docs)

| API | Namespace | What it returns |
|-----|-----------|-----------------|
| `RuntimeOptions.LoadedLibrary` | `Whisper.net.LibraryLoader` | Which runtime was loaded (e.g. `Cuda`, `Cpu`) |
| `RuntimeOptions.RuntimeLibraryOrder` | `Whisper.net.LibraryLoader` | Can be set to force specific priority order |
| `WhisperFactory.GetRuntimeInfo()` | `Whisper.net` | Feature support: AVX, AVX2, AVX512, CUDA, etc. |

None of these are currently called in V2 code.

### 12.6. V2 Logging Gap Analysis (vs V1)

V1 logged to both **Serilog files** and **SQLite history DB**. V2 has partial coverage.

#### Whisper/STT Logging

| V1 Log | V2 Status | Gap |
|--------|-----------|-----|
| `"CUDA detected: Using GPU (Count: N)"` / `"Using CPU"` | **Missing** | No runtime detection logging |
| `"Loading Whisper model 'X' on {device}..."` | **Missing** | No log of which runtime (Cuda/Cpu) was loaded |
| `"Model loaded successfully"` | Partial — logs on first transcription only | No explicit load-complete log |
| `"Transcription complete: {text[:100]}..."` | ✅ `WhisperProvider.cs:140` | OK |
| Performance ratio: `transcription_ms / audio_duration_s` | **Missing** | Audio duration not tracked anywhere |

#### Ollama/LLM Logging

| V1 Log | V2 Status | Gap |
|--------|-----------|-----|
| `"{tokens_per_sec:.1f} tok/s"` | ✅ `OllamaProvider.cs:167` | OK |
| `"SLOW INFERENCE: N tok/s — GPU may not be active"` | ✅ `OllamaProvider.cs:162` (threshold 20 tok/s) | OK |
| `"Model {model} ready (warmup complete)"` | Partial — `WarmUpAsync()` logs but doesn't report timing/tok/s | Need GPU assessment at warmup |
| Startup GPU check via warmup speed analysis | **Missing** | V1 logged tok/s at startup, warned if <20 |
| `'ollama ps'` processor check (`100% GPU` vs `CPU`) | **Missing** | `OllamaManager` doesn't parse `ollama ps` |

#### History DB Schema (`history` table)

| V1 Column | V2 Status | Gap |
|-----------|-----------|-----|
| `audio_duration_s` | **Missing** | Cannot compute performance ratio |
| `tokens_per_sec` | **Missing** | In-memory only (`LastTokensPerSec`), not persisted |
| `recording_ms` | **Missing** | `PipelineResult.RecordingMs` exists but not stored |
| `error_message` | **Missing** | `PipelineResult.ErrorMessage` exists but not stored |
| `transcriber_model` / `processor_model` | **Missing** | Provider names stored, model names not |
| `system_metrics` table | Schema exists (line 188) but **never written to** | Empty table |

#### Pipeline Timing

| V1 Log | V2 Status |
|--------|-----------|
| `[PERF] Total: Nms, Words: N` | ✅ Pipeline logs `total=Nms stt=Nms llm=Nms inj=Nms words=N` |
| `[AUDIO] Duration: Ns, Size: N bytes` | **Missing** — audio file size/duration not logged |

### 12.7. Proposed Fix — Phase G: Local Inference Diagnostics

All diagnostic logging for Whisper, Ollama, and pipeline performance — done in one pass.

#### G.1: Whisper runtime detection logging

**File**: `src/DiktaMe.Core/STT/WhisperProvider.cs`

After `_factory ??= WhisperFactory.FromPath(_modelPath)` (line 108), log:
- `RuntimeOptions.LoadedLibrary` → `Cuda` or `Cpu`
- `WhisperFactory.GetRuntimeInfo()` → feature flags (AVX, CUDA, etc.)

Add `private bool _runtimeLogged;` field. Both calls in try/catch (non-fatal).

```csharp
if (!_runtimeLogged)
{
    _runtimeLogged = true;
    try
    {
        var loadedLib = Whisper.net.LibraryLoader.RuntimeOptions.LoadedLibrary;
        Log.Information("WhisperProvider: loaded runtime={Runtime}", loadedLib);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "WhisperProvider: failed to read loaded runtime");
    }
    try
    {
        var info = WhisperFactory.GetRuntimeInfo();
        Log.Information("WhisperProvider: runtimeInfo={Info}", info);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "WhisperProvider: failed to get runtime info");
    }
}
```

#### G.2: Audio duration + performance ratio logging

**File**: `src/DiktaMe.Core/STT/WhisperProvider.cs`

In `TranscribeAsync()`, after transcription, compute audio duration and log ratio:

```csharp
double audioDurationSec = GetAudioDurationSeconds(audioFilePath);
double ratio = audioDurationSec > 0 ? sw.ElapsedMilliseconds / (audioDurationSec * 1000.0) : 0;
string perfTag = ratio < 0.1 ? "GPU" : ratio < 0.5 ? "BORDERLINE" : "CPU";
Log.Information("WhisperProvider ({Model}): {Ms}ms for {AudioSec:F1}s audio (ratio={Ratio:F2}x, likely {PerfTag})",
    _modelSize, sw.ElapsedMilliseconds, audioDurationSec, ratio, perfTag);
```

Add `AudioDurationSec` to `TranscriptionResult` so pipelines can pass it through to DB.

Helper to get WAV duration (16kHz, 16-bit mono = 32000 bytes/sec):

```csharp
private static double GetAudioDurationSeconds(string audioFilePath)
{
    try
    {
        long fileSize = new FileInfo(audioFilePath).Length;
        return Math.Max(0, (fileSize - 44) / 32000.0);
    }
    catch { return 0; }
}
```

V1 benchmarks: GPU < 0.1x ratio, Borderline 0.1–0.2x, CPU > 0.2x.

#### G.3: Ollama warmup timing + GPU assessment

**File**: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

After `WarmUpAsync()` call, log `LastTokensPerSec` with GPU assessment:

```csharp
await ollamaProvider.WarmUpAsync(cancellationToken);
if (ollamaProvider.LastTokensPerSec.HasValue)
{
    var tps = ollamaProvider.LastTokensPerSec.Value;
    string gpuTag = tps > 50 ? "GPU" : tps > 20 ? "BORDERLINE" : "CPU";
    Log.Information("Ollama warmup: {Tps:F1} tok/s ({GpuTag})", tps, gpuTag);
}
```

V1 thresholds: >50 tok/s = GPU, <20 = likely CPU, <10 = definitely CPU.

#### G.4: Extend history DB + PipelineResult

**File**: `src/DiktaMe.Core/Data/HistoryManager.cs`

Add columns via ALTER TABLE migration:

```sql
ALTER TABLE history ADD COLUMN recording_ms INTEGER NOT NULL DEFAULT 0;
ALTER TABLE history ADD COLUMN audio_duration_s REAL;
ALTER TABLE history ADD COLUMN tokens_per_sec REAL;
ALTER TABLE history ADD COLUMN error_message TEXT;
```

Update `LogSessionAsync()` INSERT to include these 4 new columns.

**File**: `src/DiktaMe.Core/Pipeline/PipelineResult.cs`

Add optional properties:

```csharp
public double? AudioDurationSec { get; init; }
public double? TokensPerSec { get; init; }
```

Set by pipeline orchestrators from provider results.

#### G.5: Switch from CUDA to Vulkan GPU runtime

G.2 log confirmed `runtime=Cpu`. Investigation (§12.8) revealed CUDA runtime package doesn't bundle the required CUDA Toolkit DLLs. Adding CUDA 12 fallback would have the same problem.

**Solution**: Replace CUDA with Vulkan — self-contained, cross-vendor (NVIDIA + AMD + Intel Arc).

**File**: `src/DiktaMe.Core/DiktaMe.Core.csproj`

```xml
<!-- Remove: -->
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.9.0" />
<!-- Add: -->
<PackageReference Include="Whisper.net.Runtime.Vulkan" Version="1.9.0" />
```

No code changes needed — runtime selection is automatic. See §12.8 for full rationale.

#### G.6: Build, test, manual verify

- `dotnet build DiktaMe.sln` → 0 errors
- `dotnet test DiktaMe.sln` → 689+ tests pass
- Launch → dictate → check log for:
  - `WhisperProvider: loaded runtime=Cuda` (or `Cpu`)
  - `WhisperProvider (small): Nms for N.Ns audio (ratio=N.NNx, likely GPU/CPU)`
  - `Ollama warmup: N.N tok/s (GPU/CPU)`
- Check `history.db` has new columns populated

### 12.8. Investigation Results (2026-03-09)

#### Evidence

**Log file** (`%APPDATA%\DiktaMe\logs\diktame_20260309.log`):
```
[INF] WhisperProvider: loaded runtime="Cpu"
[INF] WhisperProvider: runtimeInfo=WHISPER : COREML = 0 | OPENVINO = 0 | CPU : SSE3 = 1 | SSSE3 = 1 | AVX = 1 | AVX2 = 1 | F16C = 1 | FMA = 1 | BMI2 = 1 | OPENMP = 1 | REPACK = 1 |
[INF] WhisperProvider (small): 2946ms for 11.0s audio (ratio=0.27x, likely CPU)
```

**History DB** (`%APPDATA%\DiktaMe\history.db`):
```
id=1, mode=dictate, stt_provider=Whisper (small), audio_duration_s=10.958125, tokens_per_sec=NULL, transcription_ms=3325
```

**System** (`nvidia-smi`):
- GPU: NVIDIA GeForce RTX 4060 Ti (8GB VRAM)
- Driver: 591.86
- CUDA capability: 13.1 (driver only — no CUDA Toolkit installed)

#### Root Cause

`Whisper.net.Runtime.Cuda` does **NOT** bundle CUDA runtime libraries (`cudart`, `cublas`, `cublasLt`). It only contains `whisper.cpp` compiled with CUDA support. At runtime, it tries to load CUDA DLLs from the system PATH — which requires a separately installed **CUDA Toolkit** (~3GB). Without the toolkit, the CUDA probe fails silently and falls back to `Whisper.net.Runtime` (CPU).

This is a **deployment blocker**: users cannot be expected to install a 3GB CUDA Toolkit separately. GPU acceleration must be bundled with the app.

#### Options Evaluated

| Option | Size Impact | GPU Support | User Setup | Verdict |
|--------|------------|-------------|------------|---------|
| **`Whisper.net.Runtime.Cuda`** (current) | +0MB (fails) | NVIDIA only (needs Toolkit) | Must install 3GB CUDA Toolkit | ❌ Broken |
| **Bundle CUDA redistributable DLLs** | +150-200MB | NVIDIA only | None | ⚠️ NVIDIA-only, heavy |
| **`Whisper.net.Runtime.Vulkan`** | +28MB | NVIDIA + AMD + Intel Arc | None (Vulkan ships with GPU drivers) | ✅ Chosen |

#### Decision: Switch to Vulkan

**Rationale**:
1. **Self-contained**: Vulkan runtime ships as a single 28MB NuGet package with all compute backend DLLs bundled. Requires `vulkan-1.dll` from GPU drivers (standard on all modern NVIDIA/AMD/Intel Arc systems); falls back to CPU automatically if missing (see G.8).
2. **Cross-vendor**: Works on NVIDIA (via Vulkan API), AMD, and Intel Arc GPUs. All modern GPU drivers include Vulkan support.
3. **Size**: 28MB vs 150-200MB for bundled CUDA. Total app publish grows by ~28MB.
4. **Known issue resolved**: GitHub issue [#2965](https://github.com/ggerganov/whisper.cpp/issues/2965) (nvoglv64.dll crash on RTX 4060 Ti) was a build configuration bug, **fixed and closed** in March 2025 (PR #2966 merged). This was before Whisper.net 1.9.0 release, so 1.9.0 includes the fix.
5. **Future-proof**: As the app targets "anyone with a GPU", not just NVIDIA users, Vulkan is the right abstraction layer.

**Implementation**: One-line NuGet swap in `DiktaMe.Core.csproj`:
```xml
<!-- Remove: -->
<PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.9.0" />
<!-- Add: -->
<PackageReference Include="Whisper.net.Runtime.Vulkan" Version="1.9.0" />
```

No code changes needed — Whisper.net runtime selection is automatic (probes Vulkan → CPU in priority order).

**Verification**: After swap, launch app → dictate → check log for:
- `WhisperProvider: loaded runtime="Vulkan"` (not `"Cpu"`)
- Performance ratio < 0.1x (GPU) instead of 0.27x (CPU)

**Fallback plan**: If Vulkan fails on this specific GPU, revert to CUDA and investigate bundling CUDA redistributable DLLs manually.

### 12.9. Phase G Task Log

| # | Subtask | File(s) | Status |
|---|---------|---------|--------|
| G.1 | Whisper runtime detection logging (`RuntimeOptions.LoadedLibrary` + `GetRuntimeInfo()`) | `WhisperProvider.cs` | ✅ (committed a56d91a) |
| G.2 | Audio duration + perf ratio logging (ratio=Nx, likely GPU/CPU) | `WhisperProvider.cs` | ✅ (committed a56d91a) |
| G.3 | Ollama warmup timing + GPU assessment log | `LoadingViewModel.cs` | ✅ code complete — manual verify deferred to Tier 2 (after STT verified) |
| G.4 | Extend history DB: `recording_ms`, `audio_duration_s`, `tokens_per_sec`, `error_message` + `PipelineResult` props | `HistoryManager.cs`, `PipelineResult.cs`, `DictationPipeline.cs` + all pipelines | ✅ (committed a56d91a) |
| G.5 | ~~CUDA~~ → **`Whisper.net.Runtime.Vulkan`** NuGet swap | `DiktaMe.Core.csproj` | ✅ |
| G.6 | Manual verify: `runtime="Vulkan"` + ratio < 0.1x | Logs | ✅ Verified — see §12.10 |
| G.7 | Fix WhisperProvider ~800ms model reload per dictation | `STTProviderFactory.cs` | ✅ — see §12.10 |
| G.8 | Vulkan CPU-fallback warning log | `WhisperProvider.cs` | ✅ |

### 12.10. Vulkan Verification + Model Reload Fix (2026-03-09)

#### G.6 Results — Vulkan Verified

Post-swap log (`diktame_20260309.log`) confirms GPU acceleration:

| # | Runtime | WhisperProvider ms | Pipeline ms (DB) | Audio | Ratio | Tag |
|---|---------|-------------------|-------------------|-------|-------|-----|
| 1 (pre-swap) | Cpu | 2946 | 3325 | 11.0s | 0.27x | CPU |
| 2 | Vulkan | 4204 | 5099 | 4.0s | 1.04x | Cold start (Vulkan shader compile) |
| 3 | Vulkan | 454 | 1254 | 4.9s | 0.09x | **GPU** |
| 4 | Vulkan | 449 | 1250 | 5.1s | 0.09x | **GPU** |
| 5 | Vulkan | 453 | 1253 | 7.4s | 0.06x | **GPU** |
| 6 | Vulkan | 391 | 1169 | 8.0s | 0.05x | **GPU** |

Vulkan delivers ~6-7x speedup over CPU (2946ms → ~450ms). First dictation pays a one-time Vulkan shader compilation cost.

#### G.7 Root Cause — ~800ms Gap Between Log and DB

**Symptom**: WhisperProvider internal stopwatch logs ~450ms, but DB `transcription_ms` shows ~1250ms — consistent ~800ms gap on every warm dictation.

**Investigation**: Both stopwatches end at the same millisecond (verified from log timestamps). The gap is at the **start**: `DictationPipeline.sttSw` starts before `WhisperProvider.sw`.

**Root cause**: `STTProviderFactory.CreateProvider("whisper")` (line 36) created a **new `WhisperProvider` instance** on every call. Each new instance:
1. `_factory = null` → `WhisperFactory.FromPath(_modelPath)` reloads the 466MB GGML model (~800ms from OS file cache)
2. `_runtimeLogged = false` → runtime info logged on every call (5 dictations = 5 "loaded runtime" log entries)

The DI singleton `WhisperProvider` (registered in `App.xaml.cs:398`) was only used by `LoadingViewModel` for model download checks — never for transcription.

**Fix**: Added `WhisperProvider` instance caching in `STTProviderFactory`:
- `_cachedWhisper` / `_cachedWhisperModel` fields
- `GetOrCreateWhisper(modelSize)` method returns cached instance if model matches, disposes + recreates if model changes
- `IDisposable` implementation for cleanup
- **Verified**: pipeline `transcription_ms` dropped from ~1250ms to ~440ms (0-1ms gap). Raw mode total: ~450-540ms end-to-end.

#### G.7 Verification Results (Post-Cache Fix)

| id | Whisper ms | Pipeline stt ms | Gap | Total ms | Audio | Mode |
|----|-----------|----------------|-----|----------|-------|------|
| 7 | 225 | 878 | 653ms | 3357 | 8.0s | LLM (first dictation, cold factory) |
| 8 | 457 | 458 | 1ms | 2403 | 6.5s | LLM |
| 9 | 538 | 538 | 0ms | 4644 | 12.6s | LLM |
| 12 | 437 | 437 | 0ms | **519** | 4.0s | **Raw** |
| 13 | 439 | 439 | 0ms | **522** | 5.5s | **Raw** |
| 14 | 368 | 369 | 1ms | **453** | 3.1s | **Raw** |
| 15 | 458 | 459 | 1ms | **541** | 3.3s | **Raw** |

`loaded runtime="Vulkan"` logged once per session (not per dictation). Raw mode end-to-end: **~500ms** (STT ~430ms + injection ~82ms).

#### G.8: Vulkan CPU-Fallback Warning

**Problem**: If a user's system lacks Vulkan support (no `vulkan-1.dll` in system drivers), Whisper.net silently falls back to CPU. The existing `loaded runtime=Cpu` log is informational but doesn't explain *why* — users wouldn't know they're missing GPU acceleration.

**Investigation**: The `Whisper.net.Runtime.Vulkan` NuGet package (28MB) bundles `ggml-vulkan-whisper.dll` but does **not** bundle `vulkan-1.dll` (the Vulkan loader). That DLL ships with GPU drivers — all modern NVIDIA, AMD, and Intel Arc drivers include it. If it's missing, the `NativeLibraryLoader` in Whisper.net fails to load the Vulkan backend and falls back to CPU automatically. No crash, no error — just slow inference.

**Fix**: After logging `RuntimeOptions.LoadedLibrary`, check if the loaded runtime is `Cpu` AND the `runtimes/vulkan/win-x64/` directory exists (meaning Vulkan was deployed but not loaded). If so, log a `Warning` with actionable guidance: "Ensure your GPU drivers include Vulkan support".

**No user install needed**: `vulkan-1.dll` comes from GPU drivers, not from a separate SDK or toolkit. Users just need up-to-date GPU drivers (standard recommendation).

**File**: `src/DiktaMe.Core/STT/WhisperProvider.cs` — runtime logging block (lines 113-145)

#### Note: Check LLM Provider for Same Pattern

`LLMProviderFactory` may have the same issue — creating a new provider instance per dictation. In V1, this caused each dictation to open a new HTTP connection instead of reusing a kept-alive connection, adding unnecessary latency. Investigate when we reach Tier 2 (Ollama/Local LLM) and also check cloud LLM providers (Gemini, OpenAI) for `HttpClient` reuse. Look at:
- `src/DiktaMe.Core/Config/LLMProviderFactory.cs` — does it `new` a provider each call?
- Cloud providers (`GeminiProvider`, `OpenAiProvider`) — do they create `HttpClient` per instance or share one?
- `OllamaProvider` — same question

---

## 13. Phase H: FIX-10 — Split Cloud/Local Toggle into STT + LLM

> **Date**: 2026-03-10
> **Status**: Spec'd, not started
> **Fixes**: FIX-10 (Cloud/Local toggle ignores STT)
> **Decision**: Replace the single "LOCAL" toggle with two independent toggles: **STT** and **LLM**

### 13.1. Problem

The Control Panel has a single "LOCAL/CLOUD" toggle that only switches `ActiveProfileName` (which controls LLM model name resolution). It does **not** affect the STT provider — `PipelineFactory` reads STT from `ModeProfiles["{mode}_0"]` which is hardcoded to profile 0 and ignores `ActiveProfileName`.

This means users cannot switch between Whisper and Deepgram from the Control Panel. The wizard correctly captures STT/LLM independently, but the Control Panel toggle doesn't respect that independence.

### 13.2. Design Decision

**Two separate toggles** instead of one. This allows all 4 valid combos:

| STT | LLM | Use Case |
|-----|-----|----------|
| Cloud (Deepgram) | Cloud (Gemini) | Full cloud — fastest, needs API keys |
| Local (Whisper) | Cloud (Gemini) | Privacy for audio, cloud intelligence |
| Cloud (Deepgram) | Local (Ollama) | Fast STT, offline LLM |
| Local (Whisper) | Local (Ollama) | Fully offline |

The alternative (expanding presets to bundle STT/LLM/Cloud/Local) was considered but deferred — it requires a major rewire of the DictationMode CRUD system and conflicts with the granular Settings page. Can revisit as a future UX feature.

### 13.3. UI Layout Change

**Current layout** (5-column, toggle above label+state combined):

```
[toggle]    [toggle]    [toggle]    [toggle]    [toggle]
SOUND: ON   CLOUD       +KEY: OFF   RAW: ON     REFINE: AUTO
```

**New layout** (6-column, label above toggle, state below):

```
  SOUND       STT        LLM       +KEY        RAW       REFINE
[toggle]    [toggle]   [toggle]   [toggle]   [toggle]   [toggle]
   ON       WHISPER     GEMINI      OFF         ON        AUTO
```

Changes per cell:
- **Top**: Static label TextBlock (name of what the toggle controls)
- **Middle**: ToggleSwitch (same as today)
- **Bottom**: Dynamic state TextBlock (current value, bound to ViewModel)

The old single "LOCAL" column (Grid.Column="1") is **replaced** by two columns: "STT" and "LLM".

### 13.4. Files to Modify

| File | Changes |
|------|---------|
| `src/DiktaMe.App/Views/ControlPanelPage.xaml` | Row 2: 5-col → 6-col grid. Replace LOCAL column with STT + LLM. Restructure all 6 cells to label/toggle/state layout. |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | Replace `IsLocalMode`/`LocalLabel` with `IsLocalStt`/`SttStateLabel` + `IsLocalLlm`/`LlmStateLabel`. Add `OnIsLocalSttChanged` handler that writes to `ModeProfiles`. Split label properties into name + state. |
| `src/DiktaMe.Core/Config/ProfileManager.cs` | `GetModeSettings()` already reads from `ModeProfiles` — no change needed for STT read, but the STT toggle **write** goes through `ControlPanelViewModel` → `SettingsManager.UpdateAsync()` |
| `src/DiktaMe.App/Strings/en/Resources.resw` | New keys: `ControlPanel_Toggle_Sound`, `ControlPanel_Toggle_Stt`, `ControlPanel_Toggle_Llm`, `ControlPanel_Toggle_Key`, `ControlPanel_Toggle_Raw`, `ControlPanel_Toggle_Refine` (static labels). New state keys: `ControlPanel_Stt_Whisper`, `ControlPanel_Stt_Deepgram`, `ControlPanel_Llm_Gemini`, `ControlPanel_Llm_Ollama`, `ControlPanel_Llm_None` |
| `src/DiktaMe.App\Strings\es-MX\Resources.resw` | Same new keys in Spanish |

### 13.5. ViewModel Changes

#### Remove
- `IsLocalMode` property (single toggle)
- `LocalLabel` property (combined "LOCAL"/"CLOUD")
- `OnIsLocalModeChanged()` handler

#### Add
- `IsLocalStt` (bool) — bound to STT toggle. `true` = Whisper, `false` = Deepgram
- `IsLocalLlm` (bool) — bound to LLM toggle. `true` = Ollama, `false` = Gemini
- `SttStateLabel` (string) — "WHISPER" / "DEEPGRAM"
- `LlmStateLabel` (string) — "OLLAMA" / "GEMINI" / "NONE"
- Split all existing combined labels into static name + dynamic state:
  - `SoundLabel` "SOUND: ON" → name is static in XAML, state property `SoundStateLabel` returns "ON"/"OFF"
  - Same pattern for `KeyStateLabel`, `RawStateLabel`, `RefineStateLabel`

#### `OnIsLocalSttChanged(bool value)`
```
1. Determine provider name: value ? "whisper" : "deepgram"
2. Write to ALL ModeProfiles entries: SttProvider = providerName
3. Persist via _settings.UpdateAsync()
4. Update SttStateLabel
5. Update header badge (SttProviderName)
6. Log: "ControlPanel: STT switched to {provider}"
```

#### `OnIsLocalLlmChanged(bool value)`
```
1. Determine provider name: value ? "ollama" : "gemini"
2. Set ActiveProfileName = value ? "Local" : "Cloud"
3. Write to ALL ModeProfiles entries: LlmProvider = providerName
4. Persist via _settings.UpdateAsync()
5. Update LlmStateLabel, AuthBadgeText
6. Reload available modes (subtitle may change)
7. Log: "ControlPanel: LLM switched to {provider}, profile={profileName}"
```

#### `LoadFromSettings()` update
```
// Replace:
IsLocalMode = ActiveProfileName == "Local"
// With:
IsLocalStt = ModeProfiles["dictate_0"].SttProvider == "whisper"
IsLocalLlm = ActiveProfileName == "Local"  (or LlmProvider == "ollama")
```

### 13.6. Task Log

| # | Subtask | File(s) | Status |
|---|---------|---------|--------|
| H.1 | Add new localization keys (EN + ES) — 6 static toggle labels + 5 state labels | `Resources.resw` × 2 | ⬜ |
| H.2 | ViewModel: Replace `IsLocalMode`/`LocalLabel` with `IsLocalStt`/`IsLocalLlm` + state labels | `ControlPanelViewModel.cs` | ⬜ |
| H.3 | ViewModel: Split all existing labels into static name + dynamic state | `ControlPanelViewModel.cs` | ⬜ |
| H.4 | ViewModel: `OnIsLocalSttChanged` handler — writes SttProvider to ModeProfiles | `ControlPanelViewModel.cs` | ⬜ |
| H.5 | ViewModel: `OnIsLocalLlmChanged` handler — writes LlmProvider + ActiveProfileName | `ControlPanelViewModel.cs` | ⬜ |
| H.6 | ViewModel: Update `LoadFromSettings()` to read both toggles independently | `ControlPanelViewModel.cs` | ⬜ |
| H.7 | XAML: Restructure Row 2 from 5-col to 6-col, label/toggle/state layout | `ControlPanelPage.xaml` | ⬜ |
| H.8 | Build + test pass | `dotnet build && dotnet test` | ⬜ |
| H.9 | Manual smoke test (see §13.7) | App | ⬜ |

### 13.7. Smoke Test

After implementation, verify these scenarios manually:

| # | Action | Expected |
|---|--------|----------|
| S.1 | Launch app (Scenario 2 state: Whisper + Gemini) | STT toggle = ON (local), LLM toggle = OFF (cloud). Labels: "WHISPER" / "GEMINI" |
| S.2 | Flip STT toggle OFF | Label changes to "DEEPGRAM". `settings.json` → all `SttProvider = "deepgram"`. Header badge updates. |
| S.3 | Flip STT toggle back ON | Label changes to "WHISPER". `settings.json` → all `SttProvider = "whisper"`. |
| S.4 | Flip LLM toggle ON | Label changes to "OLLAMA". `ActiveProfileName = "Local"`. Auth badge = "LOC". |
| S.5 | Flip LLM toggle OFF | Label changes to "GEMINI". `ActiveProfileName = "Cloud"`. Auth badge = "API". |
| S.6 | Both toggles ON → dictate | Whisper transcribes + Ollama processes (or graceful error if Ollama not running) |
| S.7 | STT=ON, LLM=OFF → dictate | Whisper transcribes + Gemini processes via cloud |
| S.8 | STT=OFF, LLM=OFF → dictate | Deepgram transcribes + Gemini processes (full cloud, same as Scenario 1) |
| S.9 | Close + relaunch app | Both toggles restore from settings correctly |
| S.10 | All 6 toggles render with label above, toggle middle, state below | Visual check — no overlap, no truncation |
