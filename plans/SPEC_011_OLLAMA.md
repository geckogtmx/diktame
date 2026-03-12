# SPEC_011: Ollama Management Hub

**Status:** IMPLEMENTED  
**Created:** 2026-03-11  
**Priority:** High  
**V1 Reference:** `src/settings/ollama.ts`, `src/ipc/ollamaHandlers.ts`, `python/check_vram_models.py`, `SPEC_031`

---

## 1. Executive Summary

The current V2 Ollama Settings page is **functional but minimal** — it checks health, shows installed model tags (names only), and handles the 412 rescue flow. Meanwhile, V1 had a richer experience: model management (pull with progress, delete), a curated "Safe Model Library" filtered by VRAM, service restart, warmup, and real-time hardware diagnostics.

This spec defines the **Ollama Management Hub** — a comprehensive, in-app control center that eliminates the need for users to ever open a terminal or the Ollama CLI. The goal is to make dIKta.me the single pane of glass for managing the user's local AI infrastructure.

---

## 2. Current State (V2 — Verified from Codebase)

### What We Already Have

| Feature | Status | Where in Code |
|---------|--------|---------------|
| Health check (version + model compatibility + version-change detection) | ✅ Done | `OllamaManager.CheckAsync()` |
| Installed model tags list (names only, no size/family) | ✅ Done | `OllamaManager.GetInstalledModelTagsAsync()` |
| Model pull with streaming progress (Core only — no UI progress bar) | ✅ Core Done | `OllamaManager.PullModelAsync()` with `IProgress<OllamaPullProgress>` |
| Compatibility manifest (`models.json` — version checks only) | ✅ Done | Embedded resource, `OllamaManager.LoadManifestAsync()` |
| Install Ollama via winget (with progress phases) | ✅ Done | `OllamaManager.InstallViaWingetAsync()` |
| Auto-start Ollama service (locate exe, spawn, wait for ready) | ✅ Done | `OllamaManager.StartOllamaAsync()` + `FindOllamaExe()` |
| 412 Rescue UI (version too old → fallback model) | ✅ Done | `OllamaSettingsViewModel.ShowRescue` + XAML InfoBar |
| Keep-alive duration config (5m, 10m, 30m, 1h, 2h) | ✅ Done | `OllamaSettingsViewModel.KeepAliveIndex` |
| Model warmup (preload into VRAM — Core only) | ✅ Core Done | `OllamaProvider.WarmUpAsync()` — but no settings UI to trigger it |
| Live model discovery across all providers | ✅ Done | `ModelListService.QueryOllamaModelsAsync()` (parallel with other providers) |
| Per-mode model/provider selection | ✅ Done | `ModeSettings.LlmProvider` + `LlmModel` in `AppSettings` |
| STT/LLM mode switching (Cloud / Local / Skip) | ✅ Done | `AIEngineSettingsViewModel` |
| GPU presence + VRAM detection (partial) | ✅ Partial | `CapabilityReport.HasGpu` + `GpuVramMb` (DTO exists, usage in `LoadingViewModel`) |
| Tokens/sec monitoring with GPU vs CPU inference detection | ✅ Done | `OllamaProvider.LastTokensPerSec` + slow inference warnings |
| First-run Wizard with Ollama install + pull + health check | ✅ Done | `WizardLlmPage.xaml.cs` |
| Fallback model discovery | ✅ Done | `OllamaManager.FindBestFallbackAsync()` |
| DI registration as singleton | ✅ Done | `App.xaml.cs` line 505 |

### What's Missing (Gap Analysis)

| Feature | V1 Had It? | Ollama API | Status |
|---------|------------|------------|--------|
| **Model deletion** | ✅ Yes | `DELETE /api/delete` | ❌ Not implemented |
| **Pull progress bar in Settings UI** | ✅ Yes (basic) | `POST /api/pull` (streaming) | ❌ Core done, no UI binding |
| **Rich model list** (size, family, quantization, age) | ✅ Partial | `GET /api/tags` returns full details | ❌ We only parse `name` |
| **Model detail view** (capabilities, template, license) | ❌ No | `POST /api/show` | ❌ Not implemented |
| **Running/loaded models view** (which models are in VRAM) | ❌ No | `GET /api/ps` (VRAM per model + expiry) | ❌ Not implemented |
| **Model Library** (search + browse + one-click install) | ✅ Yes (curated) | `ollama.com/search` (live) | ❌ Not implemented |
| **VRAM diagnostics** (real-time used/total) | ✅ Yes | WMI + `/api/ps` | ❌ `GpuVramMb` exists but not exposed in Settings UI |
| **Service restart** | ✅ Yes | Process management | ❌ Not implemented |
| **Warmup trigger in Settings** | ✅ Yes | Via existing `WarmUpAsync()` | ❌ Core exists, no button in Settings |
| **Disk usage stats** (total model storage) | ❌ No | Calculated from `/api/tags` sizes | ❌ Not implemented |
| **Custom Ollama base URL** | ❌ No | Constructor param | ❌ Hardcoded `localhost:11434` |
| **Configurable `num_ctx`** | ❌ No | Request option | ❌ Hardcoded `2048` in `OllamaProvider` |
| **Model selector ComboBox** (instead of free-text TextBox) | ✅ Yes | `/api/tags` | ❌ Currently a TextBox |

---

## 3. Proposed Architecture

### 3.1 Core Layer Changes (`DiktaMe.Core`)

**Extend `OllamaManager`** (methods that wrap Ollama local API):

```
OllamaManager (existing)
├── CheckAsync()                        — ✅ exists
├── GetInstalledModelTagsAsync()        — ✅ exists (upgrade to rich DTOs)
├── PullModelAsync()                    — ✅ exists
├── StartOllamaAsync()                  — ✅ exists
├── InstallViaWingetAsync()             — ✅ exists
│
├── [NEW] GetInstalledModelsAsync()     → List<OllamaModelDetail>   (parse /api/tags fully)
├── [NEW] GetModelInfoAsync(tag)        → OllamaModelInfo           (via /api/show)
├── [NEW] GetRunningModelsAsync()       → List<OllamaRunningModel>  (via /api/ps)
├── [NEW] DeleteModelAsync(tag)         → bool                      (via DELETE /api/delete)
└── [NEW] RestartServiceAsync()         → bool                      (kill + restart process)
```

**New service: `OllamaSearchService`** (queries Ollama registry):

```
OllamaSearchService [NEW]
├── SearchModelsAsync(query, ct)         → List<OllamaSearchResult>
│   └── GET https://ollama.com/search?q={query} → parse HTML
├── GetPopularModelsAsync(ct)            → List<OllamaSearchResult>
│   └── GET https://ollama.com/search (empty = popular)
└── Cross-reference with OllamaManager.GetInstalledModelTagsAsync()
```

**New service: `HardwareInfoService`** (enriches existing `CapabilityReport`):

```
HardwareInfoService [NEW]
└── GetInfoAsync() → HardwareInfo
    ├── WMI Win32_VideoController (GPU name, total VRAM — works with all GPUs)
    ├── GlobalMemoryStatusEx (RAM total/available)
    └── /api/ps for real-time VRAM usage (Ollama-specific)
```

> [!NOTE]
> `CapabilityReport` already has `HasGpu` and `GpuVramMb`. `HardwareInfoService` enriches this with GPU name, real-time used VRAM, and RAM stats.

### 3.2 New DTOs

```csharp
// Rich model info from /api/tags (we currently only parse "name")
public sealed record OllamaModelDetail(
    string Name,
    long Size,               // bytes on disk
    DateTime ModifiedAt,
    string Family,
    string ParameterSize,    // e.g. "7.6B"
    string QuantizationLevel // e.g. "Q4_K_M"
);

// Running model from /api/ps
public sealed record OllamaRunningModel(
    string Name,
    long SizeVram,           // bytes in VRAM
    DateTime ExpiresAt,      // when model will be unloaded
    string ParameterSize,
    string QuantizationLevel
);

// Model detail from /api/show
public sealed record OllamaModelInfo(
    string Family,
    string ParameterSize,
    string QuantizationLevel,
    int ContextLength,
    string[] Capabilities,   // ["completion", "vision", "tools"]
    string Template,
    string License
);

// Hardware info (enriches existing CapabilityReport)
public sealed record HardwareInfo(
    string GpuName,
    double VramTotalGB,
    double VramUsedGB,       // real-time from /api/ps
    double RamTotalGB,
    double RamAvailableGB
);

// Search result from ollama.com/search (real-time)
public sealed record OllamaSearchResult(
    string Name,             // e.g. "gemma3"
    string Description,      // e.g. "The current, most capable model..."
    string[] SizeLabels,     // e.g. ["1b", "4b", "12b", "27b"]
    string[] Capabilities,   // e.g. ["vision", "tools"]
    string PullCount,        // e.g. "33M"
    int TagCount,
    string LastUpdated,      // e.g. "3 months ago"
    string Url,              // e.g. "https://ollama.com/library/gemma3"
    bool IsInstalled         // cross-referenced with local /api/tags
);
```

---

## 4. UI Design — Ollama Settings Page

The revamped page is organized into **collapsible sections** using WinUI 3 `Expander` controls.

### 4.1 Section: Service Status (Always Visible)

```
┌─────────────────────────────────────────────────────┐
│  🟢 Ollama v0.6.2                                   │
│  Status: Running · 2 models loaded · 3.2 GB VRAM    │
│                                                      │
│  [Check Health]  [Restart Service]                   │
│                                                      │
│  ⚠️ Update available: v0.7.0  [Update]  [Website]   │
└─────────────────────────────────────────────────────┘
```

**Data:** `GET /api/version` + `GET /api/ps` (running model count + VRAM)

### 4.2 Section: Default Model

```
┌─ Default Model ─────────────────────────────────────┐
│  Model used by all local processing modes.           │
│                                                      │
│  [▼ llama3.2:latest          ]  [🔥 Warmup]         │
│  Family: llama · 3.2B · Q4_K_M · 1.9 GB             │
│  Capabilities: completion                            │
│                                                      │
│  Keep-alive: [▼ 10 minutes ]                         │
│  How long the model stays loaded in VRAM after use.  │
└─────────────────────────────────────────────────────┘
```

**Key change:** Model selector becomes a `ComboBox` populated from `GetInstalledModelsAsync()` with rich display (name + size + family). Currently it's a free-text `TextBox`.

**Warmup button:** Calls existing `OllamaProvider.WarmUpAsync()` — already implemented in Core, just needs a UI trigger.

### 4.3 Section: Installed Models

```
┌─ Installed Models (5) ── Total: 12.4 GB ────────────┐
│                                                      │
│  ┌──────────────────────────────────────────────┐    │
│  │ llama3.2:latest                         1.9 GB│    │
│  │ 3.2B · Q4_K_M · llama · Modified: 3d ago     │    │
│  │                              [ℹ️ Info] [🗑️]  │    │
│  ├──────────────────────────────────────────────┤    │
│  │ gemma3:4b                               2.6 GB│    │
│  │ 4B · Q4_K_M · gemma · Modified: 1w ago        │    │
│  │                              [ℹ️ Info] [🗑️]  │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  Pull New Model: [model name...        ] [Pull]      │
│  ══════════════════════════════════ 45% downloading   │
└─────────────────────────────────────────────────────┘
```

**Features:**
- **Rich model cards** with family, parameter count, quantization, size, age
- **Delete button** with confirmation dialog (prevents deleting the active model)
- **Info button** opens flyout via `/api/show` showing template, license, capabilities
- **Total disk usage** summed from model sizes
- **Pull with progress bar** — wires existing `PullModelAsync()` to UI `ProgressBar`
- **Pull input** with autocomplete powered by live search (see §4.5)

### 4.4 Section: Live VRAM Monitor

```
┌─ Live VRAM ─────────────────────────────────────────┐
│                                                      │
│  GPU: NVIDIA GeForce RTX 3060 Ti                     │
│  VRAM: 3.2 GB / 8.0 GB (40%)                        │
│  ████████████████░░░░░░░░░░░░░░░░░░░░░░░  40%       │
│                                                      │
│  Loaded Models:                                      │
│  • llama3.2:latest — 1.9 GB — expires in 8m          │
│  • (none other)                                      │
│                                                      │
│  System RAM: 12.4 / 32.0 GB                          │
│  Last inference: 85.2 tok/s (GPU)                    │
└─────────────────────────────────────────────────────┘
```

**Data:**
- GPU name + total VRAM: `HardwareInfoService` (WMI `Win32_VideoController`)
- Used VRAM + loaded models: `GET /api/ps` (`size_vram` per model + `expires_at`)
- RAM: `GlobalMemoryStatusEx`
- Inference speed: existing `OllamaProvider.LastTokensPerSec` + GPU/CPU detection

> [!NOTE]
> `CapabilityReport` already detects `HasGpu` and `GpuVramMb` during startup. This section enriches that data with real-time usage from `/api/ps` and adds GPU name. `OllamaProvider` already tracks tokens/sec — we just surface it.

> [!IMPORTANT]
> Refresh on page load + on demand via "Refresh" button. Optionally auto-refresh every 30 seconds while the page is active.

### 4.5 Section: Model Library (Live Search & Install)

```
┌─ Model Library ─────────────────────────────────────┐
│                                                      │
│  Search Ollama Models:                               │
│  [🔍 Search models...               ]               │
│                                                      │
│  ┌──────────────────────────────────────────────┐    │
│  │ gemma3                                  33M ↓│    │
│  │ The current, most capable model that runs     │    │
│  │ on a single GPU.                              │    │
│  │ 🏷️ vision  ·  Sizes: 1b 4b 12b 27b           │    │
│  │ Updated: 3 months ago  ·  29 tags             │    │
│  │                    [▼ Select size] [Install]  │    │
│  │                              ✅ 4b installed   │    │
│  ├──────────────────────────────────────────────┤    │
│  │ deepseek-r1                             22M ↓│    │
│  │ DeepSeek's first-generation reasoning model.  │    │
│  │ 🏷️ thinking  ·  Sizes: 1.5b 7b 8b 14b 32b   │    │
│  │ Updated: 2 months ago  ·  45 tags             │    │
│  │                    [▼ Select size] [Install]  │    │
│  └──────────────────────────────────────────────┘    │
│                                                      │
│  Showing 20 results · [Load more]                    │
│                                                      │
│  💡 Popular: gemma3, llama3.2, deepseek-r1,          │
│             phi4, qwen2.5, mistral                   │
└─────────────────────────────────────────────────────┘
```

**Logic:**
1. User types in search box → debounced query (300ms) to `OllamaSearchService`
2. Results from `ollama.com/search?q=<query>` (HTML parsed to DTOs)
3. Each result: name, description, available sizes, capabilities, pull count
4. Cross-reference with installed models (`/api/tags`) to show ✅ badges
5. User selects size variant → triggers `PullModelAsync()` with progress bar
6. On empty search, show popular/trending models (default page)
7. **Quick-tag buttons**: Popular, Vision, Reasoning, Embedding, Code

> [!IMPORTANT]
> The search hits ollama.com directly — no third-party APIs, no hardcoded model lists. If ollama.com is unreachable, section shows "Search unavailable — you can still pull models by name above."

### 4.6 Section: Advanced Settings (Collapsed by Default)

```
┌─ Advanced ──────────────────────────────────────────┐
│                                                      │
│  Ollama Base URL:  [http://localhost:11434    ]       │
│  For remote Ollama instances or non-default ports.   │
│                                                      │
│  □ Auto-warmup default model on startup              │
│                                                      │
│  Context Window (num_ctx):  [▼ 2048 ]                │
│  2048 | 4096 | 8192 | 16384                          │
│                                                      │
│  [Open Ollama Website]                               │
└─────────────────────────────────────────────────────┘
```

> [!NOTE]
> `num_ctx` is currently **hardcoded to 2048** in `OllamaProvider.BuildRequestJson()`. This setting would make it configurable. `OllamaBaseUrl` is currently hardcoded to `localhost:11434` in both `OllamaManager` and `OllamaProvider`.

---

## 5. Real-Time Model Search

### 5.1 Why Not a Hardcoded Manifest?

The Ollama ecosystem moves fast — new models are released weekly. A hardcoded list of "verified models" that only refreshes with app updates is unacceptable. Users would always be behind, and we'd be doing maintenance busywork keeping a manifest in sync.

### 5.2 Search Architecture

Ollama has **no official search API**. However, `ollama.com/search?q=<query>` returns a server-rendered HTML page with structured model data. We parse this to extract live search results.

```
OllamaSearchService [NEW]
├── SearchModelsAsync(query, cancellationToken)
│   └── GET https://ollama.com/search?q={query}
│       └── Parse HTML → List<OllamaSearchResult>
├── GetPopularModelsAsync(cancellationToken)
│   └── GET https://ollama.com/search  (empty query = popular/featured)
│       └── Parse HTML → List<OllamaSearchResult>
└── Cross-reference with OllamaManager.GetInstalledModelTagsAsync()
    └── Mark results as IsInstalled
```

### 5.3 HTML Parsing Strategy

The `ollama.com/search` page returns structured HTML with consistent patterns:
- Model name, description, pull count, tag count, last updated
- Capability badges (vision, tools, embedding, etc.)
- Available size labels (1b, 4b, 7b, 12b, etc.)
- Link to model detail page

Use **`HtmlAgilityPack`** (or `AngleSharp`) to parse HTML. The parser is isolated in `OllamaSearchService` so it's easy to swap if Ollama ever ships an official JSON search API.

### 5.4 Resilience

| Scenario | Behavior |
|----------|----------|
| ollama.com unreachable | Show "Search unavailable" message. User can still pull by name. |
| HTML structure changes | Log parsing errors, degrade gracefully (partial results or fallback). |
| Slow response | 5-second timeout, loading spinner, cancellable. |
| Rate limiting | Debounce search input (300ms). Cache results for 5 minutes. |

### 5.5 The Existing `models.json` Stays — Compatibility Only

The embedded `models.json` is **NOT used** for the Model Library. It stays exclusively for its original purpose: **version compatibility checks** (`minOllamaVersion` per model tag) during `CheckAsync()`. It doesn't need to list all models — just the ones with known Ollama version requirements.

> [!IMPORTANT]
> `models.json` = compatibility manifest for 412-rescue flows only.
> `OllamaSearchService` = real-time model discovery for the Model Library.
> These are completely separate concerns.

---

## 6. Implementation Plan

### Phase 1: Core API Extensions [A]
Extend `OllamaManager.cs` with new methods that wrap Ollama's existing REST API.

| Task ID | Description | Files |
|---------|-------------|-------|
| A.1 | `GetInstalledModelsAsync()` — parse `/api/tags` into rich `OllamaModelDetail` DTOs (name, size, family, params, quantization, modified date) | `OllamaManager.cs` |
| A.2 | `GetModelInfoAsync(tag)` — call `POST /api/show` for detail view | `OllamaManager.cs` |
| A.3 | `GetRunningModelsAsync()` — call `GET /api/ps` for loaded models + VRAM usage | `OllamaManager.cs` |
| A.4 | `DeleteModelAsync(tag)` — call `DELETE /api/delete` | `OllamaManager.cs` |
| A.5 | `RestartServiceAsync()` — kill existing process + restart via `StartOllamaAsync()` | `OllamaManager.cs` |
| A.6 | Create `HardwareInfoService` — WMI query for GPU name/VRAM/RAM (enriches existing `CapabilityReport`) | `HardwareInfoService.cs` [NEW] |

### Phase 2: Real-Time Model Search [B]
Build `OllamaSearchService` for live model discovery from ollama.com.

| Task ID | Description | Files |
|---------|-------------|-------|
| B.1 | Add `HtmlAgilityPack` NuGet dependency to `DiktaMe.Core` | `DiktaMe.Core.csproj` |
| B.2 | Create `OllamaSearchService` with `SearchModelsAsync()` and `GetPopularModelsAsync()` | `OllamaSearchService.cs` [NEW] |
| B.3 | HTML parser for `ollama.com/search` results → `OllamaSearchResult` DTOs | `OllamaSearchService.cs` |
| B.4 | Cross-reference search results with installed models for ✅ badges | `OllamaSearchService.cs` |
| B.5 | In-memory cache (5-minute TTL) + debounce support | `OllamaSearchService.cs` |
| B.6 | Graceful degradation when ollama.com is unreachable | `OllamaSearchService.cs` |

### Phase 3: ViewModel Revamp [C]
Rewrite `OllamaSettingsViewModel` to surface all features.

| Task ID | Description | Files |
|---------|-------------|-------|
| C.1 | Rich installed models collection with `OllamaModelDetail` | `OllamaSettingsViewModel.cs` |
| C.2 | Delete model command with confirmation and active-model protection | `OllamaSettingsViewModel.cs` |
| C.3 | Pull model command with `IProgress<OllamaPullProgress>` bound to progress bar | `OllamaSettingsViewModel.cs` |
| C.4 | Running models / VRAM monitor properties | `OllamaSettingsViewModel.cs` |
| C.5 | Model library with live search, debounced input, and quick filters | `OllamaSettingsViewModel.cs` |
| C.6 | Hardware info properties (GPU name, VRAM bar, RAM, inference speed) | `OllamaSettingsViewModel.cs` |
| C.7 | Warmup command — wire existing `OllamaProvider.WarmUpAsync()` to UI button | `OllamaSettingsViewModel.cs` |
| C.8 | Restart service command | `OllamaSettingsViewModel.cs` |
| C.9 | Advanced settings (base URL, auto-warmup, num_ctx) | `OllamaSettingsViewModel.cs` |
| C.10 | Model detail flyout data from `/api/show` | `OllamaSettingsViewModel.cs` |
| C.11 | ComboBox model selector populated from installed models (replace TextBox) | `OllamaSettingsViewModel.cs` |

### Phase 4: XAML UI [D]
Rewrite `OllamaSettingsPage.xaml` with all sections.

| Task ID | Description | Files |
|---------|-------------|-------|
| D.1 | Service Status header section | `OllamaSettingsPage.xaml` |
| D.2 | Default Model section with ComboBox + warmup button | `OllamaSettingsPage.xaml` |
| D.3 | Installed Models section with model cards + delete + disk usage | `OllamaSettingsPage.xaml` |
| D.4 | Pull model with progress bar UI | `OllamaSettingsPage.xaml` |
| D.5 | Live VRAM Monitor section | `OllamaSettingsPage.xaml` |
| D.6 | Model Library section with search box and result cards | `OllamaSettingsPage.xaml` |
| D.7 | Advanced Settings expander | `OllamaSettingsPage.xaml` |
| D.8 | Model Info flyout/dialog | `OllamaSettingsPage.xaml` |

### Phase 5: Settings & Wiring [E]

| Task ID | Description | Files |
|---------|-------------|-------|
| E.1 | Add `OllamaBaseUrl`, `OllamaAutoWarmup`, `OllamaNumCtx` to `AppSettings` | `AppSettings.cs` |
| E.2 | Update `OllamaProvider` to use configurable `num_ctx` and base URL from settings | `OllamaProvider.cs` |
| E.3 | Update `OllamaManager` to use configurable base URL from settings | `OllamaManager.cs` |
| E.4 | Register `OllamaSearchService` + `HardwareInfoService` in DI | `App.xaml.cs` |
| E.5 | Add localization strings for all new UI elements | Strings `.resw` files |

---

## 7. New Settings Fields

Add to `AppSettings`:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `OllamaBaseUrl` | `string` | `"http://localhost:11434"` | Custom Ollama endpoint URL (currently hardcoded in `OllamaProvider` and `OllamaManager`) |
| `OllamaAutoWarmup` | `bool` | `false` | Pre-load default model into VRAM on startup (calls existing `OllamaProvider.WarmUpAsync()`) |
| `OllamaNumCtx` | `int` | `2048` | Context window size (currently hardcoded in `OllamaProvider.BuildRequestJson()`) |

> [!NOTE]
> `OllamaModel` and `OllamaKeepAlive` already exist in `AppSettings`. No need to add them.

---

## 8. Endpoints Used

### Ollama Local API (localhost:11434)
| Endpoint | Method | Purpose | V2 Status |
|----------|--------|---------|-----------|
| `/api/version` | GET | Service health + version | ✅ Used in `CheckAsync` |
| `/api/tags` | GET | List installed models (rich details) | ⚠️ Used but only `name` parsed |
| `/api/show` | POST | Model info (template, license, capabilities) | ❌ New |
| `/api/ps` | GET | Running models + VRAM usage + expiry | ❌ New |
| `/api/pull` | POST | Download model (streaming progress) | ✅ Used in `PullModelAsync` |
| `/api/delete` | DELETE | Remove model from disk | ❌ New |
| `/api/generate` | POST | Warmup (existing) + inference | ✅ Used in `OllamaProvider` |

### Ollama Registry (ollama.com)
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `ollama.com/search?q=<query>` | GET | Real-time model search (HTML, parsed client-side) |

---

## 9. UX Principles

1. **Progressive Disclosure** — Status header always visible. Details in collapsible `Expander` sections. Advanced settings hidden by default.
2. **No Terminal Required** — Every Ollama CLI operation has an in-app equivalent.
3. **VRAM-Aware** — The app knows your GPU and shows real-time VRAM usage + inference speed.
4. **Non-Blocking** — Pull, warmup, restart, and search operations show inline progress without freezing the UI.
5. **Safe Defaults** — Delete confirms. Can't delete the active model. VRAM warnings are informational, not blocking.
6. **Localized** — All user-visible strings go through `LocalizationService` / WinUI3Localizer.

---

## 10. Verification Plan

### Unit Tests
- `OllamaManager` new methods (`GetInstalledModelsAsync`, `DeleteModelAsync`, `GetRunningModelsAsync`, `GetModelInfoAsync`) with mocked `HttpClient`
- `OllamaSearchService` HTML parsing with sample HTML fixtures from `ollama.com/search`
- Search result cross-referencing with installed models
- `HardwareInfoService` (mock WMI responses)
- Run: `dotnet test DiktaMe.sln`

### Manual Testing
- Pull a model, observe progress bar, verify it appears in the installed list
- Delete a model, verify it disappears from the list
- Try to delete the active model — should be blocked with a dialog
- Warmup button → verify model appears in `/api/ps` via VRAM monitor
- Restart Service → service status goes yellow then green
- Search "gemma" → real-time results should show
- Search with ollama.com offline → "Search unavailable" message
- Test on a machine with no GPU — CPU-only fallback should still work
- Verify VRAM bar + inference tok/s match actual hardware

---

## 11. Production Pipeline Warmup (E2E Cold-Start Elimination)

### 11.1 Problem

The first dictation after app launch has significantly higher latency than subsequent ones:

| Component | Cold (1st) | Warm (2nd+) | Penalty |
|-----------|-----------|-------------|---------|
| Whisper STT | ~1337ms | ~370ms | ~1000ms (model load + Vulkan shader compile) |
| Ollama LLM | ~2573ms | ~385ms | ~2200ms (model context load into factory provider) |
| **Total pipeline** | **~4000ms** | **~770ms** | **~3200ms** |

**Root cause:** Two separate cold-start penalties:

1. **Whisper**: The `WhisperProvider` is cached (G.7 fix), but the first `TranscribeAsync()` call loads the 466MB model into GPU memory and compiles Vulkan shaders. No warmup exists for Whisper.

2. **Ollama**: The existing `OllamaProvider.WarmUpAsync()` runs during `LoadingViewModel` Step 4b, but it warms up the **DI singleton** `OllamaProvider` instance. Dictation calls go through `PipelineFactory` → `LLMProviderFactory.CreateProvider()`, which returns a **different cached instance** (FIX-16). The production HTTP connection + Ollama model context are cold on first use.

V1 solved this with `_quick_warmup()` (see `ipc_server.py:517-576`): it sent `"Hi"` through the **exact same pipeline** used by real dictations — same processor routing, same cached session — ensuring the first real dictation hit a fully primed connection.

### 11.2 Proposed Solution

Add a **silent E2E warmup** that runs after `LoadingViewModel` completes initialization (or as a background task after the control panel loads). The warmup:

1. **Whisper warmup**: Transcribe a tiny silent WAV (~0.5s) through the cached `WhisperProvider` to force model load + Vulkan shader compilation. Discard the result.

2. **LLM warmup via factory**: Call `LLMProviderFactory.CreateProvider("ollama", model: settings.OllamaModel)` to get-or-create the cached factory provider, then call `ProcessAsync("Hi", systemPrompt, "warmup")` with `numPredict: 1`. This primes the production HTTP connection + loads the model in Ollama's context. Discard the result.

3. **No injection, no telemetry**: The warmup produces no text injection and writes no `history` DB record. It's invisible to the user.

4. **Cloud skip**: Only warm up local providers (Whisper + Ollama). Skip if STT/LLM is cloud to avoid API charges.

5. **Non-blocking**: Run as a fire-and-forget background task after hotkeys are registered. The user can start dictating immediately — if they beat the warmup, they just get the cold-start penalty once.

### 11.3 Expected Impact

| Metric | Before | After Warmup |
|--------|--------|--------------|
| 1st dictation STT | ~1337ms | ~370ms |
| 1st dictation LLM | ~2573ms | ~385ms |
| 1st dictation total | ~4000ms | ~770ms |
| User-perceived startup | Loading screen only | Loading screen + ~3s background warmup (invisible) |

First dictation would be indistinguishable from subsequent ones.

### 11.4 Implementation Notes

- The `OllamaAutoWarmup` setting (§7) controls whether this runs. Default: `true` for local users, skipped for cloud-only.
- Replace the existing `_ollamaProvider.WarmUpAsync()` call in `LoadingViewModel` with the full E2E warmup to avoid warming up the wrong provider instance.
- Whisper warmup needs a tiny embedded WAV resource (~1KB of silence). Alternatively, generate silence bytes in memory via `NAudio`.
- Log warmup timing: `"E2E warmup: Whisper {stt_ms}ms, LLM {llm_ms}ms, total {total_ms}ms"`

### 11.5 Task Additions

| Task ID | Description | Files |
|---------|-------------|-------|
| E.6 | E2E production warmup — Whisper silent transcription + LLM factory provider warmup | `LoadingViewModel.cs`, `LLMProviderFactory.cs` |
| E.7 | Wire `OllamaAutoWarmup` setting to control E2E warmup (default: true for local) | `AppSettings.cs`, `LoadingViewModel.cs` |

---

## 12. Future Considerations (Out of Scope)

- **Official Ollama Search API** — If Ollama ships an official JSON search endpoint, swap `OllamaSearchService` from HTML parsing to JSON. The service abstraction makes this trivial.
- **ollamadb.dev Community API** — Third-party JSON API exists (`ollamadb.dev/api/v1/models`) but is unreliable. Could be a fallback alongside ollama.com scraping.
- **Bundled Ollama (Sidecar)** — Shipping Ollama inside dIKta.me's installer (V1's SPEC_017 concept).
- **Ollama model creation** (`POST /api/create`) — Custom Modelfile support. Power-user feature.
