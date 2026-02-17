# Developer Handoff

## Session Summary: 2026-02-16 (Session 3)

### ✅ Completed This Session

#### Work Stream B — Race Condition Fix
- **HotkeyManager race condition** (`HotkeyManagerTests.UnregisterAll_DoesNotThrow` was flaky)
  - Root cause: `Start()` launched pump thread and returned before HWND was created; 100ms sleep was non-deterministic
  - Fix: `ManualResetEventSlim _hwndReady` — pump thread signals after HWND assignment; `Start()` waits with 5s timeout; failure path also signals to prevent hang
  - All 102 B-stream tests now pass cleanly

#### Work Stream C — STT & LLM Providers ✅
- **C.1** `STTRouter` — primary/fallback routing implementing `ISTTProvider`
- **C.2** `DeepgramProvider` — POST nova-2 listen API, auto language detection, token auth
- **C.3** `GeminiAudioProvider` — multimodal inline-base64 WAV, direct audio understanding (no separate STT pipeline)
- **C.4** `WhisperProvider` — Whisper.net 1.9.0 local GGML inference; model auto-download with progress events
  - Key API: `WhisperFactory.FromPath(path)`, `WhisperGgmlDownloader.Default.GetGgmlModelAsync(type)`, `processor.ProcessAsync(Stream)`
- **C.5** `ILLMProvider` updated to return `LlmResult` record (text + provider + latency + token counts)
- **C.6** `OpenAICompatibleProvider` — one class for OpenAI, DeepSeek, OpenRouter, Groq, Together, Fireworks, Perplexity, Azure OpenAI, LM Studio, vLLM; named constructors `ForOpenAI()`, `ForDeepSeek()`, `ForOpenRouter()`, etc.
- **C.6** `GeminiProvider` — generateContent API; API key (query param) + OAuth Bearer (`ya29.*`)
- **C.6** `AnthropicProvider` — Messages API; `x-api-key` + `anthropic-version: 2023-06-01`
- **C.7** `OllamaProvider` — `/api/generate` stream:false; `WarmUpAsync()`; `LastTokensPerSec`; warns < 20 tok/s

#### Work Stream D — Pipeline Orchestration ✅
- **D.1** `DictationPipeline` — STT → optional LLM → `TextInjector.InjectText`
  - `RawMode = true` skips LLM (true passthrough)
  - LLM failure falls back to raw transcript (never drops output)
  - `PipelineState` events: `Transcribing → Processing → Injecting → Idle`
- **D.2** `RefinePipeline` — dual mode:
  - Autopilot (`stt = null`): `CaptureSelection → LLM → InjectText`
  - Instruction: `TranscribeAudio → CaptureSelection → LLM(selection+instruction) → InjectText`
  - Fallback: no selection in instruction mode → treat as Ask (answer returned, not injected)
- **D.3** `AskPipeline` — STT → LLM Q&A → answer in `PipelineResult.Text` (not injected)
- **D.3** `TranslatePipeline` — STT (language="auto") → LLM → inject; falls back to raw on LLM failure
- **D.3** `NotePipeline` — STT → optional LLM formatting → `File.AppendAllTextAsync` with `## timestamp` header
- **D.4** Oops re-inject — `TextInjector.LastInjectedText` + `ReInjectLast()` (volatile, matches V1)

**Shared pipeline infrastructure:**
- `PipelineResult` — text, raw transcript, IsSuccess, ErrorMessage, per-stage latency ms, SttProvider, LlmProvider, WordCount
- `PipelineState` — enum: Idle, Recording, Transcribing, Processing, Injecting, Error
- `PipelineOptions.cs` — typed options per pipeline: `DictationOptions`, `RefineOptions`, `AskOptions`, `TranslateOptions`, `NoteOptions`, `PipelineInjectionOptions`
- All pipelines: no-throw contract, `StateChanged` event, `Completed` event, `CancellationToken` accepted

### 📊 Current State

| Metric | Value |
|--------|-------|
| **Tests** | 224 passing, 0 failing |
| **Build** | 0 errors, 0 warnings (Debug + Release) |
| **Commits** | `ef03974` C.1-C.3 · `8846484` C.4 · `be16c19` C.5-C.7 · `4dd4182` D.1-D.4 |
| **Branch** | main (9 commits ahead of origin) |

### 📋 Next Steps: Work Stream E — Data & Security

**Priority order (blocked path: E.1 → E.3 → E.2 → DI wiring):**

#### E.1: Settings Manager (CRITICAL — blocks everything downstream)
**Create:** `DiktaMe.Core/Config/AppSettings.cs`, `SettingsManager.cs`
- `AppSettings` record — strongly typed, all settings with defaults
- Persist to `%APPDATA%/DiktaMe/settings.json` via `System.Text.Json`
- `SettingsManager` — load, save, merge defaults, schema migration
- Use `[JsonSerializable(typeof(AppSettings))]` source generator for trim-safe serialization
- Observable properties for eventual MVVM binding (see §4.1 of ARCHITECTURE.md)
- `ProfileManager` — dual-profile (8 modes × 2 profiles: local/cloud)
- `PromptRepository` — 16 custom system prompt slots
- Migration from V1 `electron-store` JSON (read `%APPDATA%/diktate/settings.json`, convert)
- **Port from:** V1 `src/services/settingsManager.ts` + `src/services/profileManager.ts`

#### E.3: Security (do this before E.2 to get API key storage working)
**Create:** `DiktaMe.Core/Security/SecureStorage.cs`, `ApiKeyValidator.cs`, `PIIScrubber.cs`
- `SecureStorage` — DPAPI `ProtectedData.Protect/Unprotect` (`DataProtectionScope.CurrentUser`)
  - Store encrypted keys to `%APPDATA%/DiktaMe/keys.dat`
  - Never log key values — log provider name only
  - Zero byte arrays after encrypt/decrypt
- `ApiKeyValidator` — format validation per provider (OpenAI `sk-*`, Anthropic `sk-ant-*`, etc.)
- `PIIScrubber` — regex patterns for emails, phones, SSN, card numbers, accidental API key paste
- **Port from:** V1 `src/services/securityManager.ts`

#### E.2: History & Metrics (SQLite)
**Create:** `DiktaMe.Core/Data/HistoryManager.cs`, `MetricsCollector.cs`
- SQLite at `%APPDATA%/DiktaMe/history.db` (same path prefix as V1's `~/.diktate/history.db`)
- Schema: `history` table + `system_metrics` table (match V1 schema for migration)
- 90-day auto-pruning on startup
- `PrivacyLevel` enum gates what gets stored: Ghost(0) / Stats(1) / Balanced(2) / Full(3)
- PII scrubbing applied at level ≤ 2
- **Port from:** V1 `python/utils/history_manager.py`

#### DI Container Wiring (do after E.1 + E.3)
**Modify:** `DiktaMe.App/App.xaml.cs` — `ConfigureServices` is currently empty

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // Core infrastructure
    services.AddSingleton<SettingsManager>();
    services.AddSingleton<SecureStorage>();
    services.AddSingleton<AudioRecorder>();
    services.AddSingleton<TextInjector>();
    services.AddSingleton<HotkeyManager>();
    services.AddSingleton<MuteDetector>();

    // STT (driven by settings)
    services.AddSingleton<ISTTProvider>(sp => {
        var s = sp.GetRequiredService<SettingsManager>();
        return new STTRouter(/* primary from settings */, /* fallback */);
    });

    // LLM (driven by settings)
    services.AddSingleton<ILLMProvider>(sp => {
        var s = sp.GetRequiredService<SettingsManager>();
        return new LLMRouter(/* primary from settings */, /* fallback */);
    });

    // Pipelines (transient — new instance per hotkey press)
    services.AddTransient<DictationPipeline>();
    services.AddTransient<RefinePipeline>();
    services.AddTransient<AskPipeline>();
    services.AddTransient<TranslatePipeline>();
    services.AddTransient<NotePipeline>();
}
```

### 🔍 Key Context for Next Session

#### Files Modified / Created This Session

| File | Change |
|------|--------|
| `src/DiktaMe.Core/Input/HotkeyManager.cs` | Race condition fix (`ManualResetEventSlim`) |
| `src/DiktaMe.Core/Input/TextInjector.cs` | Added `LastInjectedText` + `ReInjectLast()` (D.4) |
| `src/DiktaMe.Core/STT/STTRouter.cs` | New |
| `src/DiktaMe.Core/STT/DeepgramProvider.cs` | New |
| `src/DiktaMe.Core/STT/GeminiAudioProvider.cs` | New |
| `src/DiktaMe.Core/STT/WhisperProvider.cs` | New |
| `src/DiktaMe.Core/LLM/ILLMProvider.cs` | Updated — `ProcessAsync` returns `LlmResult` |
| `src/DiktaMe.Core/LLM/OpenAICompatibleProvider.cs` | New |
| `src/DiktaMe.Core/LLM/GeminiProvider.cs` | New |
| `src/DiktaMe.Core/LLM/AnthropicProvider.cs` | New |
| `src/DiktaMe.Core/LLM/OllamaProvider.cs` | New |
| `src/DiktaMe.Core/LLM/LLMRouter.cs` | New |
| `src/DiktaMe.Core/Pipeline/DictationPipeline.cs` | New |
| `src/DiktaMe.Core/Pipeline/RefinePipeline.cs` | New |
| `src/DiktaMe.Core/Pipeline/AskPipeline.cs` | New |
| `src/DiktaMe.Core/Pipeline/TranslatePipeline.cs` | New |
| `src/DiktaMe.Core/Pipeline/NotePipeline.cs` | New |
| `src/DiktaMe.Core/Pipeline/PipelineResult.cs` | New |
| `src/DiktaMe.Core/Pipeline/PipelineState.cs` | New |
| `src/DiktaMe.Core/Pipeline/PipelineOptions.cs` | New |
| `src/DiktaMe.Core/DiktaMe.Core.csproj` | Whisper.net 1.9.0 packages |
| `tests/.../Pipeline/PipelineTests.cs` | New — 27 tests |

#### Architecture Decisions Made

1. **No `CancellationToken` on `ISTTProvider`/`ILLMProvider`** — interfaces are clean; if cancellation is needed, wrap at the call site or revisit in a later refactor (noted as tech debt in `IMPLEMENTATION_REVIEW.md`)
2. **Trim-safe JSON** — all providers use manual string escaping instead of `JsonSerializer.Serialize<T>()` to avoid IL2026 trim warnings in Release builds
3. **`OpenAICompatibleProvider` covers 10+ services** — single class with configurable base URL and named constructors; no per-service subclasses
4. **Pipeline no-throw contract** — all exceptions caught internally; errors returned as failed `PipelineResult`. This simplifies hotkey handlers: `var result = await pipeline.RunAsync(...); if (!result.IsSuccess) ShowNotification(result.ErrorMessage);`
5. **`NotePipeline` writes directly** — no `NoteWriter.cs` class yet (roadmap lists it but it's trivial enough to inline); `NoteWriter.cs` can be extracted when the settings system provides configurable file paths

#### External Review Notes (see `plans/IMPLEMENTATION_REVIEW.md`)
An external AI review was conducted. Key actionable items for **Stream E**:
- DI container is unwired — highest priority, do as part of E.1 wiring
- No `AppSettings` means API keys can't persist — E.3 (`SecureStorage`) unblocks cloud providers
- Review also flagged: streaming LLM responses and VAD as high-value V2.1 features (not in current roadmap scope)

#### Known Issues / Tech Debt
- `CancellationToken` not propagated to `ISTTProvider.TranscribeAsync` / `ILLMProvider.ProcessAsync` — low priority until UI exists
- `PipelineFactory` not yet created — pipelines are constructed directly; mode-aware provider selection (dual-profile) will need a factory layer as part of E.1/DI wiring
- `ChatPipeline.cs` in the file tree is a forward declaration only — actual implementation is Task I.2
- `NoteWriter.cs` listed in ARCHITECTURE.md under `Data/` is not yet built (not needed until E.2)

### 💡 Notes for Next Session

- **App path convention:** `%APPDATA%/DiktaMe/` for all user data (settings, history, keys, models, notes)
  - Whisper models: `%APPDATA%/DiktaMe/models/`
  - Notes file default: `%APPDATA%/DiktaMe/notes.md`
  - History DB: `%APPDATA%/DiktaMe/history.db`
  - Settings: `%APPDATA%/DiktaMe/settings.json`
  - Encrypted keys: `%APPDATA%/DiktaMe/keys.dat`
- **V1 settings reference:** `%APPDATA%/diktate/settings.json` — note lowercase `diktate` (V1 app name)
- **Trim-safe pattern:** any new serialization in `DiktaMe.Core` must use either manual string building or a `[JsonSerializable]` source-generated context — NOT `JsonSerializer.Serialize<T>()` without attributes
- **Test pattern:** see `tests/DiktaMe.Core.Tests/STT/DeepgramProviderTests.cs` for the `FakeHttpHandler` pattern used by all HTTP-based providers
- **All providers use exponential backoff:** 3 retries at 1s, 2s, 4s delays — this is already in the providers; no changes needed
- `publish-release.cmd` still works — run it to verify trimmed publish still builds after adding new code

### 🏷️ Alpha Tag Due
Per roadmap §9.4, `v2.0.0-alpha.2` should be tagged after Stream D completion ("full dictation pipeline works"). Tag it before starting Stream E:
```
git tag -a v2.0.0-alpha.2 -m "alpha.2: full pipeline layer complete (D.1-D.4)"
```
