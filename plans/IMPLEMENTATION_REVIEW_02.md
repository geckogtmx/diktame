# dIKta.me V2 — Implementation Review 02

**Review Date:** 2026-02-16
**Updated:** 2026-02-16 — Stream E complete
**Focus:** Architecture Gaps vs Roadmap
**Reference:** `DEVELOPMENT_ROADMAP.md`

---

## ✅ Update: Stream E Complete (2026-02-16)

All Stream E blockers identified in this review have been resolved:

| Finding | Resolution |
|---------|------------|
| DI container not wired | `App.xaml.cs:ConfigureServices()` fully wired; all core services registered |
| Config module missing | `AppSettings`, `SettingsManager`, `ProfileManager`, `PromptRepository`, `STTProviderFactory`, `LLMProviderFactory`, `PipelineFactory` created |
| Security module missing | `SecureStorage` (DPAPI), `ApiKeyValidator`, `PIIScrubber` created with tests |
| Data module missing | `HistoryManager` (SQLite), `MetricsCollector`, `NoteWriter` created with tests |
| CancellationToken missing from interfaces | Added to `ISTTProvider` and `ILLMProvider`; propagated through all providers and pipelines |
| No provider factory pattern | `ISTTProviderFactory` and `ILLMProviderFactory` implemented |

**Remaining open items:** OopsPipeline (D.4), ChatPipeline (I.2), System module (CapabilityDetector, SystemMonitor, StartupManager, OllamaManager), Stream F UI, Stream I promoted features.

**Next priority:** Stream F (UI — SettingsView, ControlPanelView, WizardView).

---

## Executive Summary (Original)

This review focuses on **architecture gaps** — components defined in the roadmap that are missing from the codebase. While Streams A-D are substantially complete, several key components remain unimplemented, creating dependencies that will block future work.

**Key Findings (at time of review):**
- 2 Pipelines missing (Oops, Chat) — ⚠️ Still open
- 3 entire module directories missing (Config, Security, Data) — ✅ Resolved
- 4+ system components not implemented — ⚠️ Still open (System module)
- DI container still not wired — ✅ Resolved

---

## 1. Pipeline Gaps (Stream D)

### 1.1 D.4: Oops Pipeline — 🔴 NOT IMPLEMENTED

**Status:** Missing  
**Roadmap Reference:** §4, Task D.4

The "Oops" mode (Ctrl+Alt+V) allows re-injecting the last transcribed text. While the [`TextInjector`](src/DiktaMe.Core/Input/TextInjector.cs) has the `LastInjectedText` property, there is **no `OopsPipeline`** class.

**Current State:**
```
TextInjector.cs:42 — public string? LastInjectedText { get; private set; }
```

**Expected:**
- `OopsPipeline.cs` — Re-injects `LastInjectedText` with same injection options

**Recommendation:**
```csharp
// Quick implementation — this is just re-injection, no STT/LLM needed
public sealed class OopsPipeline
{
    private readonly TextInjector _injector;

    public OopsPipeline(TextInjector injector) => _injector = injector;

    public PipelineResult Run(InjectionOptions options)
    {
        var text = _injector.LastInjectedText;
        if (string.IsNullOrEmpty(text))
            return PipelineResult.Failure("oops", "No text to re-inject");

        _injector.InjectText(text, options.TrailingSpace, options.AdditionalKey);
        return new PipelineResult { Mode = "oops", IsSuccess = true };
    }
}
```

### 1.2 Chat Pipeline — 🔴 NOT IMPLEMENTED

**Status:** Missing  
**Roadmap Reference:** §4, Task D.5 / SPEC_042d

Quick Chat Overlay (floating LLM chat window) requires `ChatPipeline.cs`. This is part of the promoted deferred features (Stream I.2) but listed in Stream D.5.

**Expected:**
- `ChatPipeline.cs` — Stateless MVP for voice/text Q&A with floating overlay
- No injection — returns answer to clipboard or overlay

---

## 2. Missing Module Directories (Stream E)

The entire Stream E (Data & Security) is **not started**. Three directories are completely empty:

| Directory | Status | Contains |
|-----------|--------|----------|
| `src/DiktaMe.Core/Config/` | ❌ Empty | AppSettings, SettingsManager, PromptRepository, ProfileManager, SnippetManager |
| `src/DiktaMe.Core/Security/` | ❌ Empty | SecureStorage, PIIScrubber, ApiKeyValidator |
| `src/DiktaMe.Core/Data/` | ❌ Empty | HistoryManager, MetricsCollector, NoteWriter |
| `src/DiktaMe.Core/System/` | ❌ Empty | CapabilityDetector, SystemMonitor, StartupManager, OllamaManager |

### 2.1 Config Module — 🔴 CRITICAL BLOCKER

The Config module is the **most critical gap**. Without it:

- No settings persistence
- No user preferences storage
- No API key storage
- Cannot configure providers
- Blocks ALL UI work (Stream F)

**Required Files:**
| File | Purpose |
|------|---------|
| `AppSettings.cs` | Settings model (16 prompt slots, dual profiles, privacy levels) |
| `SettingsManager.cs` | JSON file persistence |
| `PromptRepository.cs` | System prompts CRUD |
| `ProfileManager.cs` | Dual-profile system |
| `SnippetManager.cs` | Voice snippets (SPEC_026) |

**Priority:** This MUST be implemented before any UI work can proceed.

### 2.2 Security Module — 🔴 CRITICAL BLOCKER

No secure storage for API keys. Users cannot configure cloud providers.

**Required Files:**
| File | Purpose |
|------|---------|
| `SecureStorage.cs` | DPAPI encryption for API keys |
| `PIIScrubber.cs` | PII regex patterns for privacy levels |
| `ApiKeyValidator.cs` | Key format validation |

### 2.3 Data Module — 🟡 HIGH PRIORITY

SQLite history and metrics.

**Required Files:**
| File | Purpose |
|------|---------|
| `HistoryManager.cs` | SQLite session logging |
| `MetricsCollector.cs` | Performance tracking |
| `NoteWriter.cs` | File-based note appending |

### 2.4 System Module — 🟡 MEDIUM PRIORITY

Runtime capability detection and system integration.

**Required Files:**
| File | Purpose |
|------|---------|
| `CapabilityDetector.cs` | GPU detection, provider availability |
| `SystemMonitor.cs` | CPU/GPU/memory metrics |
| `StartupManager.cs` | Auto-start registration |
| `OllamaManager.cs` | Version sensing, model library (SPEC_031) |

**Note:** [`CapabilityReport`](src/DiktaMe.Core/Capabilities.cs) model exists but no detector implementation.

---

## 3. DI Container Status — 🔴 STILL NOT WIRED

**Location:** [`App.xaml.cs:82-90`](src/DiktaMe.App/App.xaml.cs:82)

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // Core services will be registered here as they are implemented
    // Example:
    // services.AddSingleton<ISTTProvider, DeepgramProvider>();
}
```

**Impact:** Even if all components existed, they cannot be used. App shows tray icon and empty window only.

**This was flagged in Review 01 and remains unaddressed.**

---

## 4. Test Coverage Gaps

### Missing Test Files (per roadmap G.1)

| Component | Test File | Status |
|-----------|-----------|--------|
| Settings | `SettingsManagerTests.cs` | ❌ Missing |
| History | `HistoryManagerTests.cs` | ❌ Missing |
| PII Scrubber | `PIIScrubberTests.cs` | ❌ Missing |
| Capability | `CapabilityDetectorTests.cs` | ❌ Missing |
| Snippets | `SnippetManagerTests.cs` | ❌ Missing |
| Chat Pipeline | `ChatPipelineTests.cs` | ❌ Missing |
| Audio Ducking | `AudioDuckerTests.cs` | ❌ Missing |
| Ollama Manager | `OllamaManagerTests.cs` | ❌ Missing |

---

## 5. Architecture Recommendations

### 5.1 Immediate Actions (Before Proceeding)

1. **Implement Config Module First**
   - Create directory: `src/DiktaMe.Core/Config/`
   - Start with `AppSettings.cs` and `SettingsManager.cs`
   - This unblocks all subsequent work

2. **Wire DI Container**
   - Don't wait for full Config — wire existing services now
   - Use placeholder/null implementations where needed

3. **Create OopsPipeline**
   - Trivial implementation (re-inject only)
   - Estimated: 1-2 hours

### 5.2 Component Dependency Graph

```
Config (E.1)
    ├── SettingsManager → JSON persistence
    ├── PromptRepository → 16 prompt slots
    └── ProfileManager → dual profiles
         │
         ▼
Security (E.3)          Data (E.2)           System
    │                        │                    │
    ├── SecureStorage        ├── HistoryManager   ├── CapabilityDetector
    ├── PIIScrubber         ├── MetricsCollector  ├── SystemMonitor
    └── ApiKeyValidator     └── NoteWriter        └── OllamaManager
         │                        │                    │
         └────────────────────────┴────────────────────┘
                                  │
                                  ▼
                         UI Views (Stream F)
```

### 5.3 Suggested Implementation Order

1. **Config (E.1)** — 2 days
   - AppSettings, SettingsManager, PromptRepository

2. **DI Wiring** — 0.5 day
   - Register existing services

3. **Security (E.3)** — 1 day
   - SecureStorage, ApiKeyValidator

4. **OopsPipeline** — 0.25 day
   - Simple re-inject

5. **Data (E.2)** — 2 days
   - HistoryManager, MetricsCollector

6. **System** — 2 days
   - CapabilityDetector, StartupManager

7. **UI (Stream F)** — 5+ days
   - Settings window, Control panel, Wizard

---

## 6. Warnings

### ⚠️ Warning 1: Scope Creep Risk

The promoted features (Stream I) are listed in the roadmap but are **not started**:
- Voice Snippets (SPEC_026)
- Quick Chat Overlay (SPEC_042d)
- Audio Ducking (SPEC_043d)
- Ollama Management (SPEC_031)

**Recommendation:** Complete Stream E (Config/Data/Security) and Stream F (UI) before tackling Stream I. These are "Phase 2" features.

### ⚠️ Warning 2: No Provider Factory Pattern

The roadmap mentions per-mode provider selection (dual-profile system). Current routers accept single primary/fallback at construction.

**Recommendation:** Design `IProviderFactory` now to avoid refactoring later:
```csharp
public interface ISTTProviderFactory
{
    ISTTProvider Create(ProviderConfig config);
    ISTTProvider GetForMode(string mode, Profile profile);
}
```

### ⚠️ Warning 3: Missing CancellationToken Propagation

From Review 01 — still unaddressed. The `ISTTProvider` and `ILLMProvider` interfaces don't accept `CancellationToken`.

**Recommendation:** Add to interfaces before too much code depends on them:
```csharp
Task<TranscriptionResult> TranscribeAsync(
    string audioFilePath, 
    string language = "en",
    CancellationToken cancellationToken = default);
```

---

## 7. Summary: What's Missing

| Category | Component | Status |
|----------|-----------|--------|
| **Pipeline** | OopsPipeline | ⚠️ Pending (D.4) |
| **Pipeline** | ChatPipeline | ⚠️ Pending (I.2) |
| **Config** | AppSettings.cs | ✅ Complete (E.1) |
| **Config** | SettingsManager.cs | ✅ Complete (E.1) |
| **Config** | PromptRepository.cs | ✅ Complete (E.1) |
| **Config** | ProfileManager.cs | ✅ Complete (E.1) |
| **Config** | SnippetManager.cs | ⚠️ Pending (I.1) |
| **Security** | SecureStorage.cs | ✅ Complete (E.3) |
| **Security** | PIIScrubber.cs | ✅ Complete (E.3) |
| **Security** | ApiKeyValidator.cs | ✅ Complete (E.3) |
| **Data** | HistoryManager.cs | ✅ Complete (E.2) |
| **Data** | MetricsCollector.cs | ✅ Complete (E.2) |
| **Data** | NoteWriter.cs | ✅ Complete (E.2) |
| **System** | CapabilityDetector.cs | ⚠️ Not started |
| **System** | SystemMonitor.cs | ⚠️ Not started |
| **System** | StartupManager.cs | ⚠️ Not started |
| **System** | OllamaManager.cs | ⚠️ Pending (I.5) |
| **Infrastructure** | DI Wiring | ✅ Complete (E.0) |

---

## 9. Strategic Assessment: Value, Opportunities & Recommendations

### 9.1 App Value Proposition

**Current Strengths:**
- **Single-process architecture** — Massive improvement over V1's Electron+Python dual-process model
- **Provider flexibility** — OpenAI-compatible router covers 10+ providers with single implementation
- **Clean interfaces** — ISTTProvider/ILLMProvider allow hot-swappable providers
- **Native Windows** — WinUI 3 provides modern Fluent Design, better performance
- **Memory efficiency** — Target: 50-80MB vs V1's 300MB
- **Startup time** — Target: <3s vs V1's 10-12s

### 9.2 Missed Opportunities

| Opportunity | Current State | Recommendation |
|-------------|---------------|----------------|
| **Cross-platform** | Windows only | Consider MAUI for future Mac/Linux support |
| **Plugin architecture** | Hardcoded pipelines | Add IPlugin interface for 3rd-party extensions |
| **Real-time transcription** | Batch processing only | Add streaming STT for live dictation feedback |
| **Voice activity detection** | Push-to-talk only | Add VAD for hands-free dictation |
| **Multi-language UI** | EN/ES only | Add resource file infrastructure early |
| **Mobile companion** | None | Consider companion app for mobile dictation |
| **Cloud sync** | Local only | Settings/profile sync across machines |

### 9.3 What to Add

**High Priority Additions:**
1. **Streaming STT Support** — Real-time transcription with interim results (like Google Speech)
2. **Voice Activity Detection** — Auto-start/stop recording based on speech detection
3. **Plugin System** — Allow 3rd-party providers and custom prompts
4. **Command aliases** — Shortcut phrases that expand to longer text

**Medium Priority:**
5. **Macro system** — Trigger complex multi-step actions with single hotkey
6. **Context awareness** — Detect active app and apply context-specific prompts
7. **Keyboard shortcuts for settings** — Power user controls without UI

### 9.4 What to Change

1. **Configuration Storage** — Consider SQLite for settings instead of JSON
   - Better schema validation
   - Easier migration path
   - Single file in AppData

2. **Provider Initialization** — Lazy initialization vs eager
   - Currently: providers initialized at startup
   - Better: initialize on first use, fail fast

3. **Error Recovery** — Add exponential backoff with jitter
   - Current: simple retry
   - Better: circuit breaker pattern

4. **Logging** — Structured logging with correlation IDs
   - Currently: basic Serilog
   - Better: Add request IDs for tracing across pipelines

### 9.5 What to Remove (or Deprecate)

1. **Local Whisper as sidecar** — Whisper.net is sufficient
   - Keep the code, but don't market as separate feature

2. **Legacy hotkey conflicts** — Document common conflicts
   - Don't try to auto-resolve — just notify user

3. **V1 compatibility layer** — Complete rewrite means no migration
   - Don't carry forward Python code patterns

### 9.6 Long-term Vision

The app's true value is **invisible infrastructure** — the user speaks, text appears. The V2 rewrite positions it for:

| Horizon | Goal |
|---------|------|
| **Near (V2)** | Ship with all 6 modes working, cloud-first |
| **Medium** | Add plugin ecosystem, real-time STT |
| **Long** | Cross-platform, AI agent integration |

**Recommendation:** Stay focused on the core 6 modes first. The promoted features (Snippets, Quick Chat, Audio Ducking) are nice-to-haves but can delay. Ship the solid foundation first.

---

## 10. Security-Specific Assessment

### 10.1 Current Security Posture

**Status:** ✅ IMPLEMENTED — Security module complete (E.3)

`SecureStorage`, `ApiKeyValidator`, and `PIIScrubber` are all implemented with tests. See section 7 summary table for details. The checklist in §10.6 can now be verified against real code.

### 10.2 Security Gap Analysis

| Security Concern | Current State | Risk Level | Required Action |
|------------------|----------------|------------|------------------|
| **API Key Storage** | Keys in memory only | 🔴 Critical | Implement SecureStorage.cs with DPAPI |
| **API Key Validation** | None | 🟡 Medium | Implement ApiKeyValidator.cs |
| **PII Handling** | None | 🔴 Critical | Implement PIIScrubber.cs |
| **Temp Audio Files** | System temp folder | 🟡 Medium | Use AppData temp + auto-delete |
| **Crash Recovery** | Orphaned temp files | 🟡 Medium | Cleanup on startup |
| **Logging** | May leak sensitive data | 🟡 Medium | Audit all log statements |
| **HTTPS** | Default validation | 🟢 Good | Keep default — do NOT bypass |
| **Telemetry** | None by design | 🟢 Good | Maintain no-telemetry policy |

### 10.3 Critical Security Implementation Requirements

#### 10.3.1 SecureStorage (DPAPI)

**Must implement:**
```csharp
// %APPDATA%/DiktaMe/keys.dat — encrypted blob
public sealed class SecureStorage
{
    // Store: ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)
    // Retrieve: ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser)
    // Delete: File.Delete(path)
    
    // CRITICAL: Clear byte arrays after use
    // CRITICAL: Log operations, never values
}
```

**File location:** `%APPDATA%/DiktaMe/keys.dat`

#### 10.3.2 ApiKeyValidator

**Must implement format validation:**
```csharp
public static class ApiKeyValidator
{
    public static bool IsValidOpenAI(string key)  // sk-* pattern
    public static bool IsValidAnthropic(string key)  // sk-ant-* pattern
    public static bool IsValidGemini(string key)  // length check
    public static bool IsValidDeepgram(string key)  // length check
    
    // CRITICAL: Validate BEFORE storing
    // CRITICAL: Don't log key values
}
```

#### 10.3.3 PIIScrubber

**Must implement regex patterns for:**
- Email addresses
- Phone numbers (US/international)
- Credit card numbers
- SSN patterns
- API keys (accidental paste detection)
- Custom patterns (user-defined)

**Integration:** Apply based on privacy level:
| Level | Name | Behavior |
|-------|------|----------|
| 0 | Ghost | No logging, no history |
| 1 | Stats | Aggregate metrics only |
| 2 | Balanced | History with PII scrubbing |
| 3 | Full | Complete history |

### 10.4 Security Logging Requirements

**DO:**
```csharp
Log.Information("API key stored for provider {Provider}", providerName);
Log.Warning("Transcription failed for {File}", filePath);
```

**DON'T:**
```csharp
Log.Information("API key: {Key}", apiKey);  // NEVER
Log.Debug("Request body: {Body}", requestBody);  // NEVER
```

### 10.5 Temp File Security

**Current:** Audio saved to `%TEMP%/diktate_*.wav`

**Issues:**
- Shared machine risk
- Crash leaves orphaned files
- Contains potentially sensitive speech

**Required Changes:**
1. Use `%APPDATA%/DiktaMe/temp/` (user-only access)
2. Delete immediately after transcription
3. Implement crash recovery cleanup on startup
4. Optional: encrypt temp files with session key

### 10.6 Security Testing Checklist

Before first beta release, verify:

- [ ] API keys encrypted at rest (DPAPI)
- [ ] API keys never logged
- [ ] Temp files deleted after use
- [ ] Crash recovery cleans orphaned files
- [ ] PII scrubber catches common patterns
- [ ] No sensitive data in memory dumps
- [ ] No sensitive data in crash reports
- [ ] HTTPS certificate validation not bypassed

### 10.7 Privacy Compliance

**GDPR Considerations:**
- Data export functionality
- Data deletion (one-click wipe)
- No telemetry by default (correct)
- Clear privacy policy

**Recommendation:** Add export/delete UI before release.

---

## Conclusion

The core architecture is sound and demonstrates professional C# patterns. The gaps are in supporting infrastructure (Config/Data/Security), not in core logic. The strategic opportunity now is to:

1. **Ship Stream D.4 (Oops) quickly** — Low effort, high user value
2. **Prioritize Config module** — Critical path for everything else
3. **Implement Security module before beta** — Critical for user trust
4. **Add streaming STT** — Differentiator from competitors
5. **Resist feature bloat** — Complete V2 core before promoted features

The app has strong fundamentals. Execute on the blocking items, then ship.

---

## 8. Next Steps

~~1. Create Config directory and implement AppSettings.cs + SettingsManager.cs~~ ✅
~~2. Wire existing services in DI container~~ ✅
~~4. Implement Security module (SecureStorage first)~~ ✅
~~5. Implement Data module~~ ✅

**Remaining:**
1. **Implement OopsPipeline.cs** (D.4) — trivial, ~1 hour
2. **Begin Stream F UI work** — SettingsView, ControlPanelView, WizardView (high priority — blocks Config Wizard)
3. **System module** — CapabilityDetector, StartupManager needed before first beta
4. **Stream I promoted features** — after Stream F

