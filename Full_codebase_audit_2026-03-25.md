# Engineering Review: dIKta.me V2

**Date**: 2026-03-25 | **Scope**: Full codebase audit (architecture, testing, CI/CD, performance, security, concurrency)

## Overall Grade: A-

The codebase demonstrates production-grade engineering: 1,014 tests, zero TODOs/HACKs, strict CI with 12 validation stages, proper DPAPI security, and clean async patterns. Issues found are typical growth-stage concerns, not fundamental design flaws.

---

## CRITICAL (Fix Soon)

### 1. ~~HistoryManager missing concurrency control~~ ✅ FIXED (commit `818f940`, 2026-03-26)
`src/DiktaMe.Core/Data/HistoryManager.cs` — No `SemaphoreSlim` protecting `_connection`. WalletManager does this correctly. Concurrent `LogSessionAsync()` calls will throw `InvalidOperationException` or corrupt data.

**Fix**: Add `private readonly SemaphoreSlim _lock = new(1, 1);` and wrap all `_connection` operations (same pattern as WalletManager).

### 2. ~~HistoryManager missing Dispose~~ ✅ ALREADY IMPLEMENTED (audit was wrong — Dispose exists at line 332)

### 3. No retry logic on cloud providers
`GeminiProvider.cs`, `AnthropicProvider.cs`, `OpenAICompatibleProvider.cs`, `DeepgramProvider.cs`, `DeepgramTtsProvider.cs` — A single network hiccup fails the entire dictation. Only `OllamaProvider.ProcessAsync()` and `GeminiTtsProvider` (429 only) have retry.

**Fix**: Add Polly `WaitAndRetryAsync` for transient errors (5xx, 429, timeouts) across all cloud providers. 3 retries with exponential backoff + jitter.

---

## HIGH (Should Fix)

### 4. ~~API key in URL query parameter (Security)~~ ✅ FIXED (commit `818f940`, 2026-03-26) — All 6 call sites migrated to x-goog-api-key header
`WizardApiKeysPage.xaml.cs:149` — Gemini key validation sends API key as `?key=` URL parameter. Exposed in proxy logs, referrer headers.

**Fix**: Use `Authorization: Bearer` header instead (Gemini API supports it).

### 5. LoadingViewModel god class (2,310 lines, 31 dependencies)
`src/DiktaMe.App/ViewModels/LoadingViewModel.cs` — Orchestrates dictation, chat, vision, notes, and more. High fan-in makes it fragile and hard to test.

**Fix** (future refactor): Extract `DictationPipelineHandler`, `ChatPipelineHandler`, `VisionPipelineHandler` classes.

### 6. Fire-and-forget tasks swallow exceptions
Multiple files use `_ = Task.Run(async () => ...)` without exception handling. Silent failures in background operations.

**Fix**: Add `.ContinueWith(t => Log.Error(t.Exception, "..."), TaskContinuationOptions.OnlyOnFaulted)`.

---

## MODERATE

### 7. LocalApiServer client list race condition
`src/DiktaMe.App/Services/LocalApiServer.cs:120` — Client added under lock, but `WriteSafe()` called outside lock. Client could be removed by another thread mid-write.

### 8. Inconsistent HttpClient timeouts
Wizard pages create ephemeral `new HttpClient()` with no timeout. Ollama uses 180s, TokenRefreshService uses 15s. No central timeout constants.

### 9. No code coverage threshold in CI
CI collects Coverlet coverage but doesn't enforce a minimum %. Test count threshold (470) is a proxy but doesn't catch line-level regressions.

### 10. Settings validation missing
No range checks on numeric settings (MaxDurationSeconds, HistoryRetentionDays). UI likely constrains input, but manual JSON editing can break things.

### 11. No schema versioning for AppSettings
V1→V2 migration exists, but no V2→V3 pattern. Future schema changes could silently lose user settings.

---

## LOW / INFORMATIONAL

### 12. Verbose logging
Heavy `Log.Information()` for routine operations (every provider call, every hotkey registration). Should demote to `Log.Debug()`.

### 13. Two bare `catch (Exception)` in OllamaManager (lines 591, 790)
Silent failure on install check and version detection. Should log at Debug level.

### 14. No SAST or SBOM generation
Gitleaks + NuGet audit cover secrets and CVEs, but no SonarQube or CycloneDX SBOM.

### 15. `ConfigureAwait(true)` in some ViewModel background work
`LoadingViewModel.cs`, `DictationModesSettingsViewModel.cs` — unnecessary UI thread marshaling for non-UI operations.

---

## POSITIVE FINDINGS

- **Zero TODOs/HACKs** in 20k+ line codebase — exceptional discipline
- **AI-proofing in CI** — test count threshold (470 min), publish size guard (130-250MB), explicit comments warning about AI deletion
- **Security posture strong** — DPAPI encryption, parameterized SQL everywhere, Gitleaks in CI, NuGet audit at moderate level, plaintext zeroing after decryption
- **Async patterns correct** — 397 `ConfigureAwait(false)` in library code, no `.Result`/`.Wait()`, proper CancellationToken propagation
- **WalletManager concurrency** — correct `SemaphoreSlim(1,1)` pattern for SQLite
- **Atomic file writes** — settings saved to `.tmp` then renamed
- **Test infrastructure mature** — xUnit + Moq + FluentAssertions, trait-based categories, clipboard collection for flaky test isolation
- **Clean architecture** — no circular dependencies, 14 domain modules, proper DI via ServiceCollection
- **Build quality** — TreatWarningsAsErrors=true, nullable enabled, Meziantou.Analyzer active, 0 warnings

---

## RECOMMENDED PRIORITY ORDER

| # | Item | Effort | Impact |
|---|------|--------|--------|
| 1 | HistoryManager SemaphoreSlim + Dispose | 30 min | Prevents data corruption |
| 2 | Cloud provider retry policies (Polly) | 2-3 hrs | Major reliability improvement |
| 3 | Gemini API key → Authorization header | 15 min | Security hardening |
| 4 | Fire-and-forget exception logging | 30 min | Observability |
| 5 | LocalApiServer race condition | 20 min | Stability |
| 6 | Settings validation method | 1 hr | Robustness |
| 7 | Code coverage threshold in CI | 30 min | Regression prevention |
| 8 | LoadingViewModel decomposition | 4-6 hrs | Maintainability (defer) |

---

## FILES REFERENCED

**Issues**:
- `src/DiktaMe.Core/Data/HistoryManager.cs` — #1, #2
- `src/DiktaMe.Core/LLM/GeminiProvider.cs` — #3
- `src/DiktaMe.Core/LLM/AnthropicProvider.cs` — #3
- `src/DiktaMe.Core/LLM/OpenAICompatibleProvider.cs` — #3
- `src/DiktaMe.Core/STT/DeepgramProvider.cs` — #3
- `src/DiktaMe.App/Views/Wizard/WizardApiKeysPage.xaml.cs:149` — #4
- `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` — #5
- `src/DiktaMe.App/Services/LocalApiServer.cs` — #7
- `src/DiktaMe.Core/System/OllamaManager.cs:591,790` — #13

**Exemplary**:
- `src/DiktaMe.Core/Security/SecureStorage.cs` — DPAPI, plaintext zeroing, atomic writes
- `src/DiktaMe.Core/Data/WalletManager.cs` — correct concurrency pattern
- `.github/workflows/ci-v2.yml` — 12-stage CI with AI safeguards
