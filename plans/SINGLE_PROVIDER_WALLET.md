# SINGLE_PROVIDER_WALLET — Unified Gemini Wallet (Single Model, Single Call)

## Context

The Wallet (Free Trial) pipeline currently uses **2 vendors** and **2 API calls** per dictation:
1. **STT**: `WalletDeepgramProxy` → Edge Function → **Deepgram Nova-3** → raw transcript
2. **LLM**: `WalletGeminiProxy` → Edge Function → **Gemini 2.0 Flash** → processed text

This plan replaces both with a **single Gemini call** that transcribes audio AND applies the system prompt in one shot. When no LLM processing is needed (raw mode), it falls back to a single transcription-only Gemini call. Either way: **one API call per dictation, one vendor, one model**.

### Why

- **~97% cost reduction**: Deepgram STT costs ~$0.004/30s. A single Gemini call with audio (~960 tokens at 32 tok/sec) + text output costs ~$0.0001. The $5 wallet credit goes from ~65K words to potentially 1M+ words.
- **Latency improvement**: Eliminates the round-trip between STT and LLM steps. One network hop instead of two.
- **Vendor simplification**: Drop Deepgram dependency entirely. One API key (`GEMINI_API_KEY`), one model.
- **Quality opportunity**: Gemini processes the original audio directly with the system prompt. It hears tone, emphasis, and pauses — better context than processing a lossy text transcript.

### Scope

Only wallet-specific code changes. No pipeline classes, no interfaces, no BYOK/local providers.

---

## Current Architecture (What We're Replacing)

### Current Flow: 2 Vendors, 2 Calls

```
User speaks → AudioRecorder → WAV file
  ↓
DictationPipeline.RunAsync()
  ├─ Stage 1: WalletDeepgramProxy.TranscribeAsync(audioFile, language)
  │   POST multipart/form-data → wallet-proxy Edge Function
  │   Edge Function → Deepgram Nova-3 API → raw transcript
  │   Cost: $0.0077/min (duration-based)
  │
  ├─ Stage 2: WalletGeminiProxy.ProcessAsync(rawText, systemPrompt, mode)
  │   POST JSON → wallet-proxy Edge Function
  │   Edge Function → Gemini 2.0 Flash API → processed text
  │   Cost: $0.075/1M input + $0.30/1M output (token-based)
  │
  └─ Stage 3: TextInjector.InjectText(finalText)
```

### Current Edge Function Handlers (`wallet-proxy/index.ts`)

- **`handleDeepgram(audio, language)`**: Sends raw WAV to `api.deepgram.com/v1/listen` with `model=nova-3`. Returns `{ transcript, duration_ms, confidence }`. Cost calculated from `metadata.duration`.
- **`handleGemini(body)`**: Sends text + systemPrompt to `generativelanguage.googleapis.com/.../gemini-2.0-flash:generateContent`. Returns `{ text, inputTokens, outputTokens }`. Cost calculated from `usageMetadata`.

### Current C# Wallet Proxies

- **`WalletDeepgramProxy`** (`src/DiktaMe.Core/Account/WalletDeepgramProxy.cs`): Implements `ISTTProvider`. Sends multipart form with `service="deepgram"`, `language`, `audio` (WAV bytes). Parses response `transcript` field. 60s timeout, 3 retries with exponential backoff.
- **`WalletGeminiProxy`** (`src/DiktaMe.Core/Account/WalletGeminiProxy.cs`): Implements `ILLMProvider`. Sends JSON with `service="gemini"`, `text`, `systemPrompt`, `mode`. Parses response `text` field. 30s timeout, 3 retries. Chat excluded (`ProcessConversationAsync` throws `NotSupportedException`).

Both are singletons registered in `App.xaml.cs` and injected into `PipelineFactory`.

### Current Cost Breakdown (per 10-second dictation)

| Step | Provider | Calculation | Cost |
|------|----------|-------------|------|
| STT | Deepgram Nova-3 | 10s / 60 * $0.0077/min | $0.00128 |
| LLM | Gemini 2.0 Flash | ~200 input + ~100 output tokens | $0.000045 |
| **Total** | | | **$0.001325** |

STT is **96.6%** of the cost. Deepgram is the bottleneck.

---

## Proposed Architecture: Single Model, Single Call

### Target Flow: 1 Vendor, 1 Call

```
User speaks → AudioRecorder → WAV file
  ↓
LoadingViewModel sets WalletPipelineContext:
  SystemPrompt = profile.SystemPrompt
  IsRawMode = controlPanel.IsLlmOff
  Mode = "dictate"
  ↓
DictationPipeline.RunAsync() [UNTOUCHED]
  ├─ Stage 1: WalletSTTProxy.TranscribeAsync(audioFile, language)
  │   Reads WalletPipelineContext
  │   prompt + !rawMode → COMBINED: audio + systemPrompt → Gemini
  │   no prompt or rawMode → TRANSCRIPTION-ONLY: audio → Gemini
  │   POST multipart/form-data → wallet-proxy Edge Function
  │   Edge Function → Gemini generateContent (audio + prompt) → final text
  │   Cost: token-based (audio + output tokens)
  │   Sets context.WasCombinedCall = true
  │
  ├─ Stage 2: WalletGeminiProxy.ProcessAsync(text, systemPrompt, mode)
  │   Reads WalletPipelineContext.WasCombinedCall
  │   WasCombinedCall=true → PASSTHROUGH (return input unchanged, zero cost)
  │   WasCombinedCall=false → normal LLM processing (Refine, Vision paths)
  │
  └─ Stage 3: TextInjector.InjectText(finalText)
```

### Design: WalletPipelineContext Pattern

The core challenge: `ISTTProvider.TranscribeAsync(audioFile, language)` has no system prompt parameter, and `ILLMProvider.ProcessAsync(text, prompt, mode)` has no audio parameter. A single Gemini call needs BOTH.

Solution: a shared context singleton that `LoadingViewModel` sets before each pipeline run.

```
┌─ LoadingViewModel ──────────────────────────────────────────┐
│  Before pipeline.RunAsync():                                │
│    walletContext.SystemPrompt = profile.SystemPrompt         │
│    walletContext.IsRawMode = controlPanel.IsLlmOff           │
│    walletContext.Mode = "dictate"                            │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
┌─ DictationPipeline (UNTOUCHED) ─────────────────────────────┐
│  Stage 1: stt.TranscribeAsync(audio, lang)                  │
│     ↓                                                       │
│  WalletSTTProxy reads context:                              │
│    prompt exists + !rawMode → COMBINED: audio+prompt→Gemini │
│    no prompt or rawMode → TRANSCRIPTION-ONLY: audio→Gemini  │
│  Sets context.WasCombinedCall = true/false                  │
│                                                             │
│  Stage 2: if (!rawMode && prompt): llm.ProcessAsync(text)   │
│     ↓                                                       │
│  WalletGeminiProxy reads context:                           │
│    WasCombinedCall=true → PASSTHROUGH (return input as-is)  │
│    WasCombinedCall=false → normal LLM processing            │
└─────────────────────────────────────────────────────────────┘
```

Thread safety: not needed. Dictations are strictly sequential (`LoadingViewModel` awaits each pipeline to completion via `_recordingCts` before starting the next). The context is set-before, read-during, cleared-after — no concurrent access.

---

## Pipeline Behavior Matrix

| Pipeline | Context Set By | STT Behavior | LLM Behavior | API Calls |
|----------|---------------|-------------|-------------|-----------|
| **Dictation (LLM on)** | RunBatchDictationAsync | Audio + prompt → processed text | Passthrough | **1** |
| **Dictation (raw mode)** | RunBatchDictationAsync | Audio → raw transcript | Skipped by pipeline | **1** |
| **Ask** | RunAskPipelineAsync | Audio + ask prompt → answer | Passthrough | **1** |
| **Translate** | RunTranslatePipelineAsync | Audio + translate prompt → translated | Passthrough | **1** |
| **Note (with LLM)** | RunNotePipelineAsync | Audio + note prompt → formatted | Passthrough | **1** |
| **Note (no LLM)** | RunNotePipelineAsync | Audio → raw transcript | Skipped by pipeline | **1** |
| **Refine Auto** | (no audio, STT never called) | N/A | Normal LLM call | **1** |
| **Refine Voice** | RunRefineVoiceAsync | Audio → raw instruction (rawMode=true) | Normal LLM with instruction | **2** (expected) |
| **Vision** | (uses ProcessWithImageAsync, different path) | N/A | Normal image+text call | **1** |

---

## Changes Required

### NEW: `src/DiktaMe.Core/Account/WalletPipelineContext.cs`

Simple internal singleton (~25 lines):

```csharp
internal sealed class WalletPipelineContext
{
    /// <summary>Master toggle. When false, STT always does transcription-only and LLM
    /// always processes normally (2 Gemini calls). 1-line killswitch for rollback.</summary>
    public bool UseCombinedCall { get; set; } = true;

    /// <summary>System prompt for combined STT+LLM call. Null = transcription only.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>When true, STT does transcription-only regardless of prompt.</summary>
    public bool IsRawMode { get; set; }

    /// <summary>Pipeline mode hint for edge function ("dictate", "ask", etc.).</summary>
    public string Mode { get; set; } = "dictate";

    /// <summary>Set by STT proxy after a combined call. Read and cleared by LLM proxy.</summary>
    public bool WasCombinedCall { get; set; }

    public void Reset()
    {
        SystemPrompt = null;
        IsRawMode = false;
        Mode = "dictate";
        WasCombinedCall = false;
        // Note: UseCombinedCall is NOT reset — it's a session-level toggle, not per-pipeline.
    }
}
```

---

### MODIFY: `src/DiktaMe.Core/Account/WalletDeepgramProxy.cs` → Rename to `WalletSTTProxy.cs`

**Constructor**: Add `WalletPipelineContext _context` parameter.

**`ProviderName`**: `"Wallet Proxy (Gemini STT)"`

**`TranscribeAsync()` decision logic** (replaces current Deepgram routing):

```
Read audio bytes from file
Determine call type:
  if (!_context.IsRawMode && !string.IsNullOrWhiteSpace(_context.SystemPrompt))
    → COMBINED mode
  else
    → TRANSCRIPTION-ONLY mode

Build multipart form:
  service = "gemini-audio"
  language = language
  audio = audio bytes (WAV)
  if COMBINED:
    systemPrompt = _context.SystemPrompt
    mode = _context.Mode

POST to edge function

On success:
  _context.WasCombinedCall = (was combined)
  Record usage from X-Wallet-Cost header
  Parse response { text, inputTokens, outputTokens }
  Return TranscriptionResult with Text = response.text
```

The response format: edge function returns `{ text: "...", inputTokens: N, outputTokens: N }` for both combined and transcription-only paths (unified format).

**Ledger metadata**: `{"service":"gemini-audio","combined":true}` or `{"service":"gemini-audio","combined":false}`

**Timeout**: Keep 60s (Gemini audio processing is comparable to Deepgram latency).

---

### MODIFY: `src/DiktaMe.Core/Account/WalletGeminiProxy.cs`

**Constructor**: Add `WalletPipelineContext _context` parameter.

**`ProcessAsync()` — add passthrough at top** (before existing token check):

```csharp
if (_context.WasCombinedCall)
{
    _context.WasCombinedCall = false;
    Log.Information("WalletGeminiProxy: passthrough — STT combined call already processed");
    return new LlmResult { Text = text, Provider = ProviderName };
}
// ... rest of existing method unchanged
```

Everything else stays the same. Refine Auto, Vision, and any path where `WasCombinedCall=false` uses the normal LLM flow.

---

### MODIFY: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

**Constructor**: Inject `WalletPipelineContext _walletContext`.

**Before each pipeline run**, if `AuthMode == Wallet`, set the context:

```csharp
if (_settings.Current.AuthMode == AuthMode.Wallet)
{
    _walletContext.Reset();
    _walletContext.SystemPrompt = <pipeline-specific prompt>;
    _walletContext.IsRawMode = <pipeline-specific raw mode>;
    _walletContext.Mode = <"dictate"|"ask"|"translate"|"note">;
}
try
{
    var result = await pipeline.RunAsync(...);
}
finally
{
    _walletContext.Reset();
}
```

Specific methods:
- **`RunBatchDictationAsync`** (~line 1201): `SystemPrompt = profile.UseLlm ? profile.SystemPrompt : null`, `IsRawMode = _controlPanel.IsLlmOff || !profile.UseLlm`, `Mode = "dictate"`
- **`RunAskPipelineAsync`** (~line 1440): `SystemPrompt = profile.SystemPrompt ?? PromptDefaults.Ask`, `IsRawMode = false`, `Mode = "ask"`
- **`RunTranslatePipelineAsync`** (~line 1553): `SystemPrompt = profile.SystemPrompt ?? PromptDefaults.Translate`, `IsRawMode = false`, `Mode = "translate"`
- **`RunNotePipelineAsync`** (~line 1644): `SystemPrompt = profile.SystemPrompt`, `IsRawMode = string.IsNullOrWhiteSpace(profile.SystemPrompt)`, `Mode = "note"`
- **`RunRefineVoiceAsync`**: `SystemPrompt = null`, `IsRawMode = true`, `Mode = "refine"` (raw transcription of spoken instruction; LLM step processes the captured selection separately)
- **`RunRefineAutoAsync`**: No audio, no context needed
- **Vision methods**: No audio through wallet STT path, no context needed

---

### MODIFY: `src/DiktaMe.App/App.xaml.cs`

DI registration changes (~line 517):
- `services.AddSingleton<WalletPipelineContext>();`
- Inject `WalletPipelineContext` into both `WalletSTTProxy` and `WalletGeminiProxy` constructors
- Inject `WalletPipelineContext` into `LoadingViewModel`
- Update class name from `WalletDeepgramProxy` to `WalletSTTProxy`

---

### MODIFY: `website/supabase/functions/wallet-proxy/index.ts`

**New handler: `handleGeminiAudio()`**

```typescript
async function handleGeminiAudio(
  audio: Uint8Array,
  language: string,
  systemPrompt?: string,
  mode?: string,
): Promise<GeminiAudioResult> {
  const apiKey = Deno.env.get("GEMINI_API_KEY")!;
  const audioBase64 = btoa(String.fromCharCode(...audio));

  // Build content parts
  const parts: any[] = [];

  if (systemPrompt) {
    // COMBINED: system instruction handles the prompt, audio goes in parts
  } else {
    // TRANSCRIPTION-ONLY: explicit transcription instruction
    parts.push({
      text: `Transcribe the following audio exactly in ${language}. Output only the transcribed text, nothing else.`
    });
  }

  parts.push({
    inlineData: { mimeType: "audio/wav", data: audioBase64 }
  });

  const body: any = {
    contents: [{ parts }],
    generationConfig: {
      temperature: systemPrompt ? (mode === "dictate" ? 0.3 : 0.7) : 0.1,
      maxOutputTokens: 8192,
    },
  };

  if (systemPrompt) {
    body.system_instruction = { parts: [{ text: systemPrompt }] };
  }

  const url = `https://generativelanguage.googleapis.com/v1beta/models/${WALLET_MODEL}:generateContent?key=${apiKey}`;
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  // Parse response, calculate cost from usageMetadata tokens
  // Return { text, inputTokens, outputTokens }
}
```

**Model constant**: `const WALLET_MODEL = "gemini-2.5-flash";` — single source of truth for both STT and LLM handlers. Easy to swap.

**Update existing LLM handler**: Change hardcoded `gemini-2.0-flash` in `handleGemini()` URL to use the same `WALLET_MODEL` constant.

**Routing update** (~line 158): Add `else if (service === "gemini-audio")` branch for audio requests.

**Keep `handleDeepgram` intact**: For rollback safety. Can be removed in a follow-up cleanup.

**Updated pricing constants**: Remove `DEEPGRAM_COST_PER_MINUTE`. Adjust `GEMINI_INPUT_COST_PER_TOKEN` / `GEMINI_OUTPUT_COST_PER_TOKEN` to match chosen model. Cost formula stays the same (token-based via `usageMetadata`).

**Multipart form parsing update** (~line 100): Extract optional `systemPrompt` and `mode` string fields from the form data alongside existing `service`, `language`, and `audio` fields.

---

### MODIFY: Tests

- Rename `WalletDeepgramProxyTests.cs` → `WalletSTTProxyTests.cs`
- Add tests for:
  - Combined call path (systemPrompt in context → sends prompt in form, sets WasCombinedCall)
  - Transcription-only path (no prompt or raw mode → no prompt in form)
  - WalletGeminiProxy passthrough when WasCombinedCall=true
  - WalletGeminiProxy normal processing when WasCombinedCall=false (Refine path)
  - Context reset in finally block

---

## Model Choice (Decision Deferred to Testing)

The model is a single constant in the edge function (`WALLET_MODEL`). Changing it is a one-line edit + deploy.

| Model | Status | Audio via generateContent | Text LLM | Pricing (input / output per 1M tokens) |
|-------|--------|--------------------------|----------|---------------------------------------|
| `gemini-2.5-flash` | GA (stable) | Proven in our `GeminiAudioProvider.cs` | Proven in current wallet LLM | ~$0.15 / $0.60 |
| `gemini-3.1-flash-lite-preview` | Preview | Documented by Google for audio | Designed for speed + scale | ~$0.25 / $1.50 |

### Proposed Cost (per 10-second dictation, combined call)

| Model | Input tokens | Output tokens | Total Cost | vs Deepgram+Gemini |
|-------|-------------|---------------|------------|-------------------|
| Gemini 2.5 Flash | ~320 audio + ~50 prompt | ~100 | ~$0.000115 | **~11x cheaper** |
| Gemini 3.1 Flash Lite | ~320 audio + ~50 prompt | ~100 | ~$0.000243 | **~5x cheaper** |
| Current (Deepgram + Gemini 2.0) | N/A | N/A | ~$0.001325 | baseline |

For the $5 wallet credit: current = ~65K words. Gemini 2.5 Flash = **~750K+ words**.

**Recommendation**: Start with `gemini-2.5-flash` (proven, GA, cheaper output). A/B test against `gemini-3.1-flash-lite-preview` for quality comparison on real dictations (English + Spanish).

---

## Files Summary

| File | Change Type | What Changes |
|------|------------|-------------|
| `src/DiktaMe.Core/Account/WalletPipelineContext.cs` | **NEW** | Shared context singleton (~25 lines) |
| `src/DiktaMe.Core/Account/WalletDeepgramProxy.cs` | **RENAME+MODIFY** | → `WalletSTTProxy.cs`. Add context, route to Gemini audio |
| `src/DiktaMe.Core/Account/WalletGeminiProxy.cs` | **MODIFY** | Add context, passthrough when WasCombinedCall |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | **MODIFY** | Set WalletPipelineContext before each pipeline run |
| `src/DiktaMe.App/App.xaml.cs` | **MODIFY** | DI registration for context + renamed proxy |
| `website/supabase/functions/wallet-proxy/index.ts` | **MODIFY** | Add `handleGeminiAudio`, unify model constant |
| `tests/DiktaMe.Core.Tests/Account/WalletDeepgramProxyTests.cs` | **RENAME+MODIFY** | New test cases for combined/passthrough paths |

### What Does NOT Change

- All pipeline classes (`DictationPipeline`, `AskPipeline`, `TranslatePipeline`, `NotePipeline`, `RefinePipeline`, etc.)
- `ISTTProvider` / `ILLMProvider` interfaces
- `STTRouter` / `LLMRouter` / `PipelineFactory`
- BYOK and local provider code
- Streaming dictation (still forced to batch for wallet)
- Non-wallet edge functions (`wallet-status`, `wallet-webhook`)
- Wallet ledger schema

---

## Rollback Strategy

Three levels, each independently deployable:

1. **Disable combined mode**: Add `UseCombinedCall` property to `WalletPipelineContext` (default: `true`). Set to `false` in DI → STT always does transcription-only, LLM always processes normally. Still uses Gemini for both (no Deepgram), but 2 API calls instead of 1.

2. **Revert to Deepgram for STT**: Change `service` field back to `"deepgram"`, remove systemPrompt/mode from multipart form. Edge function's `handleDeepgram` is kept intact for exactly this purpose.

3. **A/B by user**: Add `WalletSttBackend` to `AccountSettings` (`"gemini"` default, `"deepgram"` fallback). STT proxy reads this to choose edge function handler route. Enables per-user quality comparison.

---

## Verification Plan

1. **Unit tests**: `dotnet test DiktaMe.sln` — all 1134+ pass
2. **Format**: `dotnet format DiktaMe.sln --verify-no-changes --no-restore`
3. **Edge function deploy + curl test**:
   - Combined: multipart with audio + systemPrompt → verify processed text returned
   - Transcription-only: multipart with audio only → verify raw transcript
   - Verify `X-Wallet-Cost` header reflects token-based pricing
   - Verify `proxy_audit_log` records `service = "gemini-audio"`
4. **E2E on device**: Wallet account → Dictation (combined) → verify one API call in logs, text injected correctly
5. **E2E raw mode**: Toggle LLM off → Dictation → verify raw transcript (no processing)
6. **E2E Ask/Translate**: Verify combined call works with different system prompts
7. **Quality comparison**: Same audio through old (Deepgram+Gemini) vs new (Gemini combined) — compare output quality
8. **Latency comparison**: Measure wall-clock for complete pipeline (recording end → text injected)
9. **Cost audit**: Compare `X-Wallet-Cost` between old and new for same audio clips

---

## Implementation Sequence

Each step is independently deployable and backward-compatible until the final activation step:

1. **Edge Function** — Deploy `handleGeminiAudio` alongside existing handlers (backward-compatible addition)
2. **WalletPipelineContext** — Create new class (no behavior change, nothing reads it yet)
3. **WalletSTTProxy** — Rename + add context reading + Gemini routing (defaults to transcription-only until context is set)
4. **WalletGeminiProxy** — Add context reading + passthrough (defaults to normal processing until WasCombinedCall is set)
5. **App.xaml.cs** — Wire DI for context singleton
6. **LoadingViewModel** — Set context before pipeline runs (**activation point** — this is where combined mode turns on)
7. **Tests** — Unit tests for all paths

---

## Session 1 Results (2026-04-05) — Implementation + Testing

### What was built and deployed

All 7 implementation steps completed. Code changes are **uncommitted** — can be reverted cleanly.

**C# changes:**
- `WalletPipelineContext.cs` — NEW (shared context singleton with `UseCombinedCall` killswitch)
- `WalletSTTProxy.cs` — NEW (replaces `WalletDeepgramProxy.cs`, routes to `gemini-audio`)
- `WalletDeepgramProxy.cs` — DELETED
- `WalletGeminiProxy.cs` — MODIFIED (passthrough when `WasCombinedCall=true`)
- `App.xaml.cs` — MODIFIED (DI registration)
- `LoadingViewModel.cs` — MODIFIED (sets wallet context before each pipeline)
- `WalletSTTProxyTests.cs` — NEW (12 tests, replaces `WalletDeepgramProxyTests.cs`)
- `WalletGeminiProxyTests.cs` — MODIFIED (2 new passthrough tests)
- **Build**: 0 warnings, 0 errors. **Tests**: 1139 passed (was 1134, +5 new).

**Edge function** (`wallet-proxy`): Deployed v16 to Supabase project `volwljbiyzvvcqqdojyf`.
- `handleGeminiAudio()` handler with unified short prompt
- `WALLET_MODEL = "gemini-2.5-flash"` constant
- `handleDeepgram` kept intact for rollback
- Timing instrumentation via response headers (`X-Timing-*`)

### Latency findings (measured data)

**Old pipeline (Deepgram + Gemini 2.0 Flash, 2 calls):**
- Total: 1200-1966ms (avg ~1600ms)
- Deepgram STT: 700-1350ms
- Gemini LLM: 445-524ms

**New pipeline (Gemini 2.5 Flash audio, 1 call):**
- Total: 2500-4800ms typical, with unpredictable spikes to 10-28s
- Gemini fetch: 1300-3600ms typical
- Edge function overhead (JWT + checks + deduction): ~350ms consistent
- Base64 encoding: 2-34ms (negligible)
- **Verdict: ~2x slower than old pipeline. Latency regression confirmed.**

**Timing breakdown** (from `X-Timing-*` headers):
- JWT validation: ~80ms
- Freeze + rate limit + balance check: ~130ms
- Body parsing: ~8ms
- **Gemini API fetch: 1300-3600ms** (this is where all the time goes)
- Deduction + audit: ~120ms

### Prompt findings

- The original 325-char `PromptDefaults.Dictate` system prompt caused LLM ON to be ~2x slower than LLM OFF
- Replacing with short `"Transcribe in English, remove fillers, add punctuation."` eliminated the gap between LLM ON and OFF
- Edge function now uses this short prompt for ALL wallet audio calls, ignoring whatever C# sends
- Temperature set to 0.1 for all wallet audio calls (was 0.3 for dictate, 0.7 for other modes)

### Cost findings (from `proxy_audit_log`)

| Provider | Calls | Total Cost | Per Call |
|----------|-------|-----------|---------|
| Deepgram (old STT) | 57 | $0.0661 | $0.00116 |
| Gemini (old LLM) | 48 | $0.0006 | $0.000013 |
| Gemini-audio (new) | 80 | $0.0049 | $0.000061 |

**New pipeline is ~10-13x cheaper per dictation.**

$1 buys: ~1,575 dictations (old) vs ~16,400 dictations (new).

### Quality findings

- Gemini 2.5 Flash handles audio transcription + filler removal + punctuation in one call
- LLM ON vs OFF distinction is functionally meaningless for wallet dictation — both produce clean text
- The 3-way LLM toggle (Local/Cloud/Off) in wallet mode is irrelevant: Local and Cloud both hit the same wallet edge function, and the edge function uses its own prompt regardless

### Competitive latency benchmarks

| App | Latency | Architecture |
|-----|---------|-------------|
| Aqua Voice | 450ms-1s | Cloud streaming |
| Wispr Flow | 150-250ms | Cloud streaming (STT + AI cleanup) |
| Willow Voice | ~200ms | Local processing |
| **dIKta.me old wallet** | **~1.6s** | Batch (Deepgram + Gemini via edge function) |
| **dIKta.me new wallet** | **~3.5s** | Batch (Gemini audio via edge function) |

Competitors achieve sub-500ms via **streaming STT** — audio is processed while the user speaks. Our wallet uses batch (record → upload → wait). The latency difference is architectural.

### Key decision: Cost vs. Latency trade-off

The wallet is a **free trial test drive** — first impression determines purchase. At ~$0.001/dictation (old) vs ~$0.00006/dictation (new), cost was never the real problem. $5 credits last thousands of dictations either way. Latency IS the product experience.

**Decision**: Default to old pipeline (Deepgram + Gemini, fast) for best UX. Keep Gemini audio pipeline as a **server-side cost killswitch** if wallet spend exceeds a threshold — protecting against a scenario where thousands of users sign up and burn through credits simultaneously.

---

## Phase 2: Next Session — Decisions and Work Needed

### 1. Revert vs. Keep (decision needed)

The Gemini audio code is built and working but ~2x slower. Options:
- **Revert all C# changes**, keep only the edge function `handleGeminiAudio` handler for future use
- **Keep C# changes uncommitted** on the working tree, decide after Phase 2 research
- **Commit as-is** with `UseCombinedCall = false` (killswitch off by default, Gemini audio disabled but code ready)

### 2. Cost killswitch mechanism (to design)

Server-side toggle in Supabase `config` table:
- When wallet spend crosses threshold → flip all wallet users from Deepgram to Gemini audio
- No app update needed — edge function reads config and routes accordingly
- Needs: threshold value, monitoring, the actual routing logic in the edge function

### 3. Streaming wallet via Gemini Live API (to research)

The path to sub-1s wallet latency:
- Supabase edge functions **do support WebSocket** (both inbound and outbound)
- Gemini has a **Live API** — WebSocket-based real-time audio streaming with transcription
- Models: `gemini-2.5-flash-live`, `gemini-3.1-flash-live`
- Architecture: `C# app ←WS→ Edge Function ←WS→ Gemini Live API`
- This would match competitor latency while keeping Gemini as sole vendor
- Significant new work — different from batch approach, needs its own spec

### 4. Autoresearch investigation (separate agent)

Karpathy's [autoresearch](https://github.com/karpathy/autoresearch) pattern — autonomous improvement loop (modify → test → evaluate → keep/discard) — could be applied to optimize the wallet pipeline configuration (prompt wording, temperature, model selection, request structure). To be explored by a separate agent in a dedicated session.

---

## Phase 2: Streaming Wallet via Gemini Live API — Session 2 (2026-04-05)

### What was built

Full streaming pipeline implemented. Code is **uncommitted** — instant revert via `git checkout .`.

**Research findings:**
- Gemini Live API: `wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=API_KEY`
- Audio format: 16kHz PCM mono, base64-encoded chunks, MIME `audio/pcm;rate=16000`
- `systemInstruction` in setup message for filler removal / punctuation
- VAD can be disabled (C# app controls recording via hotkey, not Gemini)
- 15min audio-only session limit, 10min connection lifetime
- Audio input $1.00/1M tokens, output $2.00/1M tokens
- Supabase Edge Functions: Full WebSocket support (`Deno.upgradeWebSocket` inbound, `new WebSocket` outbound). 400s Pro wall-clock. 256MB memory. JWT via query param.
- Streaming cost per 10s dictation: ~$0.0005 (vs ~$0.00006 batch, ~$0.0013 old Deepgram)

**New C# files (4):**
- `src/DiktaMe.Core/Account/WalletStreamingSTTProxy.cs` — `IStreamingSTTProvider` via edge function WebSocket. Receive loop, 30s keepalive, cost recording from final message. `WalletStreamingFallbackException` for killswitch/error fail-fast. Default URL fallback for null-in-settings bug.
- `src/DiktaMe.Core/Account/WalletStreamingDictationPipeline.cs` — Pipeline orchestrator mirroring `StreamingDictationPipeline`. Wires audio chunks, transcript events, cost tracking. NOT always raw mode (edge function applies system instruction).
- `website/supabase/functions/wallet-stream/index.ts` — New edge function (v6 deployed): WS proxy to Gemini Live API. JWT auth (query param), balance pre-check, killswitch config read, rate limiting. Diagnostics array sent in every message for local C# logging.
- `tests/DiktaMe.Core.Tests/Account/WalletStreamingSTTProxyTests.cs` — 10 tests covering connect, session config, audio send, transcript events, cost recording, fallback, error handling.

**Modified C# files (4):**
- `src/DiktaMe.Core/Config/AccountSettings.cs` — Added `WalletStreamUrl` property (with `wss://` default)
- `src/DiktaMe.App/App.xaml.cs` — Registered `WalletStreamingSTTProxy` (transient — fresh WS per session)
- `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` — Wallet streaming routing: `AuthMode.Wallet` always tries streaming first (no `StreamingEnabled` toggle needed), with silent batch fallback. New `RunWalletStreamingDictationAsync()` method.
- `website/supabase/functions/wallet-proxy/index.ts` — Reads `wallet_pipeline_mode` config for batch killswitch routing. When `batch_fast`, overrides `gemini-audio` service to `deepgram`.

**New infra:**
- `website/supabase/migrations/015_wallet_streaming_config.sql` — `wallet_pipeline_mode = 'streaming'` in config table
- `wallet-stream` edge function deployed to Supabase (v6)
- `wallet-proxy` edge function updated (v17) with killswitch routing

**Build**: 0 warnings, 0 errors. **Tests**: 1149 passed (was 1139, +10 new).

### Architecture: C# app ←WS→ Edge Function ←WS→ Gemini Live API

```
User presses hotkey
  → LoadingViewModel detects AuthMode.Wallet (always tries streaming)
  → WalletStreamingSTTProxy.ConnectAsync()
    → Opens WS to wallet-stream edge function (JWT in query param)
    → Edge function: validates JWT, checks balance, reads killswitch config
    → Edge function: opens WS to Gemini Live API, sends setup message
    → Edge function: sends {"type":"ready"} to C#
  → Recording starts, audio chunks flow:
    NAudio → C# WS binary → Edge Function → base64 → Gemini realtimeInput
  → Gemini returns transcript via serverContent/inputTranscription
    → Edge function relays to C# as {"type":"transcript"}
  → User releases hotkey
    → C# sends {"type":"end_audio"} → Edge function sends turnComplete
    → Gemini returns final + usageMetadata
    → Edge function: calculate cost, deduct_wallet_balance, audit log
    → Sends {"type":"final","text":"...","cost":N,"balance":N} to C#
  → Pipeline injects text, records cost locally

On ANY failure → WalletStreamingFallbackException
  → Caught by RunDictationPipelineAsync → silent fallback to RunBatchDictationAsync
```

### Server-side cost killswitch

`config` table row `wallet_pipeline_mode` with 3 values:

| Value | Pipeline | Cost/10s | Latency | When |
|-------|----------|----------|---------|------|
| `"streaming"` | Gemini Live WS via `wallet-stream` | ~$0.0005 | <1s target | Default |
| `"batch_fast"` | Deepgram STT + Gemini LLM via `wallet-proxy` | ~$0.0013 | ~1.6s | Fallback |
| `"batch_cheap"` | Gemini batch audio via `wallet-proxy` | ~$0.00006 | ~3.5s | Cost emergency |

Switching (instant, no app update):
```sql
UPDATE config SET value = 'batch_cheap' WHERE key = 'wallet_pipeline_mode';
```

### Edge function diagnostics (for local debugging)

Edge function accumulates a `diagnostics: string[]` array with timestamped events. Every message sent to the C# client includes this array. The C# proxy logs each entry to the local Serilog file as `WalletStreaming [edge]: +Xms event_name`. This eliminates the need to check Supabase dashboard for edge function logs.

### Bugs found and fixed during testing

1. **Settings null override** — `WalletStreamUrl` in `AccountSettings` has a default `wss://` URL, but existing `settings.json` has `"WalletStreamUrl": null` which overrides the default (same pattern as the `SanitizeNulls` gotcha). Fix: fallback constant `DefaultStreamUrl` in the proxy.

2. **8-second freeze on streaming failure** — When the Gemini connection failed (wrong model), `ConnectAsync` blocked for 8 seconds waiting for a `{"type":"ready"}` that never came. During this time, hotkey presses were swallowed. After timeout, batch fallback started a NEW recording automatically — user experienced "starts on its own". Fix: `ProcessMessage` now signals `_readyTcs` with `TrySetException(WalletStreamingFallbackException)` on `final`, `error`, and `fallback` message types, so `ConnectAsync` fails immediately.

3. **Wallet streaming always-on** — Removed `StreamingEnabled` check for wallet streaming path. Wallet users always try streaming (server killswitch controls routing). No settings panel toggle needed.

### Gemini Live API model name investigation

**Problem**: The correct model ID for `bidiGenerateContent` was not obvious.

**What was tried and failed** (all rejected with code 1008 "model not found"):
- `gemini-2.5-flash-live` — does not exist
- `gemini-2.5-flash-live-preview` — does not exist in v1beta
- `gemini-2.5-flash` — exists but does not support `bidiGenerateContent`

**ListModels API query** (via `operation-liquidity` edge function calling `generativelanguage.googleapis.com/v1beta/models` with our API key) returned these models supporting `bidiGenerateContent`:
- `gemini-2.5-flash-native-audio-latest` ← alias for the latest preview
- `gemini-2.5-flash-native-audio-preview-09-2025`
- `gemini-2.5-flash-native-audio-preview-12-2025`
- `gemini-3.1-flash-live-preview`

**Current deploy**: `gemini-2.5-flash-native-audio-latest` (v6). Not yet E2E tested.

**Fallback**: `gemini-3.1-flash-live-preview` if 2.5 native audio has issues.

### Message protocol

**C# → Edge Function:**

| Type | Format | When |
|------|--------|------|
| Session config | `{"systemPrompt":"...","mode":"dictate","isRawMode":false}` | First text msg after connect |
| Audio chunk | Binary (raw PCM 16kHz mono) | During recording |
| End of audio | `{"type":"end_audio"}` | Hotkey release |
| Keepalive | `{"type":"keepalive"}` | Every 30s |

**Edge Function → C#:**

| Type | Format | When |
|------|--------|------|
| Ready | `{"type":"ready","diagnostics":[...]}` | Gemini session established |
| Partial | `{"type":"transcript","text":"...","isFinal":false,"diagnostics":[...]}` | During audio |
| Final | `{"type":"final","text":"...","cost":52,"balance":999948,"diagnostics":[...]}` | After cost deducted |
| Error | `{"type":"error","message":"...","code":402,"diagnostics":[...]}` | On any error |
| Fallback | `{"type":"fallback","reason":"cost_threshold","diagnostics":[...]}` | Killswitch active |

### Decisions (confirmed)

1. **No commits until E2E tested** — all Phase 1 + Phase 2 changes stay uncommitted. Instant revert via `git checkout .`.
2. **Dictation only** — streaming covers only the Dictation pipeline. Ask/Translate/Note continue using batch.
3. **Model**: Currently testing `gemini-3.1-flash-live-preview` (v8 deployed).

### Testing log (chronological)

All model attempts via edge function deploys. Diagnostics confirmed via local C# logs (`WalletStreaming [edge]:` entries).

| Deploy | Model | Result | Error |
|--------|-------|--------|-------|
| v1-v3 | `gemini-2.5-flash-live-preview` | Rejected | code=1008 "model not found for v1beta" |
| v5 | `gemini-2.5-flash` | Rejected | code=1008 "model not found, not supported for bidiGenerateContent" |
| v6 | `gemini-2.5-flash-native-audio-latest` | Rejected | code=1007 "Cannot extract voices from a non-audio request" — model requires audio output, TEXT-only `responseModalities` not supported |
| v7 | `gemini-3.1-flash-live-preview` (with `setup` key + `realtimeInputConfig`) | Rejected | code=1011 "Internal error encountered" |
| v8 | `gemini-3.1-flash-live-preview` (with `config` key, no `realtimeInputConfig`) | Rejected | code=1007 "Invalid JSON payload received. Unknown name \"config\": Cannot find field." |
| v9 | `gemini-3.1-flash-live-preview` (`setup` key, no `realtimeInputConfig`, with `systemInstruction`) | Rejected | code=1011 "Internal error encountered" — same as v7, so `realtimeInputConfig` was not the cause |
| v10 | `gemini-3.1-flash-live-preview` (`setup` key, no `realtimeInputConfig`, no `systemInstruction`) | Rejected | code=1011 "Internal error encountered" — **minimal possible setup still fails** |

**Setup message format investigation (v7→v10):**
- v7: `setup` + `generationConfig` + `systemInstruction` + `realtimeInputConfig` → code 1011
- v8: changed top-level key to `config` → code 1007 "Unknown name config" — **proves `setup` is correct**
- v9: `setup` + `generationConfig` + `systemInstruction` (no `realtimeInputConfig`) → code 1011
- v10: `setup` + `generationConfig` only (no `systemInstruction`, no `realtimeInputConfig`) → code 1011

**v10 exact JSON sent** (confirmed from diagnostics):
```json
{"setup":{"model":"models/gemini-3.1-flash-live-preview","generationConfig":{"responseModalities":["TEXT"]}}}
```

**SDK source code verification** (from `@google/genai@0.14.0` unpkg):
- SDK uses `setup` as top-level key ✓
- SDK puts `responseModalities` inside `setup.generationConfig` ✓
- SDK puts `systemInstruction` at `setup.systemInstruction` ✓
- Our format matches the SDK exactly. The internal error is NOT a message format issue.

**Conclusion**: `gemini-3.1-flash-live-preview` returns `Internal error` regardless of setup message content. Possible causes:
- API key doesn't have Live API access enabled (billing/quota issue)
- Regional restriction (Supabase edge function runs in a region not supported by Live API)
- Model is in preview and has intermittent availability issues
- The v1beta endpoint requires a different API version for this model

**UX issue**: The fail-fast fallback (~1.5s) creates a "double start" — streaming fails, batch starts a new recording. User hears/sees recording start twice, first few words are lost.

**Gemini Live API valid models** (from ListModels API query via our own API key):
- `gemini-2.5-flash-native-audio-latest` — supports `bidiGenerateContent` but requires audio output (code 1007 with TEXT modality)
- `gemini-3.1-flash-live-preview` — supports `bidiGenerateContent` but returns Internal error on setup

| v11 | `gemini-3.1-flash-live-preview` (`setup`, `["AUDIO"]`, `systemInstruction`) | **Not tested** — no edge diagnostics in logs, app was not rebuilt | Fix from Gemini: Live API requires AUDIO modality |
| v12 | `gemini-3.1-flash-live-preview` (`setup`, `["AUDIO"]`, `speechConfig.voiceName: "Aoede"`) | **Not tested** — same issue, app not rebuilt with latest C# | Fix from Gemini: explicit voice config prevents backend hang |

**Root cause of 1011 Internal Error (from Gemini):**
The `BidiGenerateContent` WebSocket endpoint requires `responseModalities: ["AUDIO"]`. Setting `["TEXT"]` causes the audio-out pipeline to fail to initialize, resulting in 1011. Additionally, omitting `speechConfig` with `["AUDIO"]` can cause the backend to hang silently (no `setupComplete`, no error). Fix: include explicit `speechConfig.voiceConfig.prebuiltVoiceConfig.voiceName`.

### Three problems identified and fixed (code written, not all tested)

**Problem 1: Gemini Live API setup** — Root cause identified via Gemini consultation:
- `responseModalities: ["TEXT"]` → 1011 Internal Error (all deploys v1-v10)
- `responseModalities: ["AUDIO"]` without `speechConfig` → silent hang (v11)
- `responseModalities: ["AUDIO"]` with `speechConfig.voiceName: "Aoede"` → deployed in v12, NOT YET TESTED
- Fallback plan: try `v1alpha` endpoint, or try `gemini-2.5-flash-native-audio-preview-12-2025` model

**Problem 2: Start/stop UX (double-start)** — FIXED in C#. Moved `ConnectAsync` BEFORE recording start. Set `_isRecording = true` immediately on hotkey to prevent parallel attempts. Extracted `ConnectWalletStreamingAsync()` helper. Pipeline skips `ConnectAsync` if already connected. If connect fails, `_isRecording` reset before throwing fallback exception. Batch starts clean with single recording.

**Problem 3: Streaming attempt delay when killswitch active** — FIXED in C#. When `wallet_pipeline_mode != 'streaming'`, the edge function returns 400 before WS upgrade, but the WS handshake + pre-flight still takes ~0.7-1s. Final fix: C# routing in `RunDictationPipelineAsync` now skips streaming entirely for wallet users — goes straight to `RunBatchDictationAsync`. Streaming path is commented out with a note to re-enable when `wallet_pipeline_mode` is set back to `streaming`.

### Current state (end of session 2)

**Database:** `wallet_pipeline_mode = 'batch_cheap'` (killswitch active, streaming disabled)

**Edge function:** `wallet-stream` v12 deployed with `["AUDIO"]` + `speechConfig` + `gemini-3.1-flash-live-preview`. Never successfully tested — `gemini_setup_complete` has never been seen.

**C# (uncommitted):** Streaming path disabled in routing (straight to batch). All streaming infrastructure code remains in place. Connect-before-recording fix applied. `_isRecording` guard against parallel attempts. 1149 tests pass, 0 warnings.

**App behavior:** Wallet dictation goes straight to batch Gemini audio pipeline. ~3-3.5s latency (Gemini `generateContent` with audio). Same as Session 1 findings. No streaming overhead.

**Session 2 net result:** Zero successful streaming dictations. Infrastructure built, bugs found and fixed, root causes identified via Gemini consultation, but the Gemini Live API connection was never established. The `["AUDIO"]` + `speechConfig` fix is the most promising lead but untested.

### Next session

**Option A (recommended): Revert and start fresh**
- `git checkout .` — clean slate
- Rebuild streaming with `["AUDIO"]` + `speechConfig` knowledge from the start
- Fix connect-before-recording architecture FIRST, test streaming connection SECOND
- Don't touch the batch pipeline until streaming works in isolation

**Option B: Continue from current state**
- Re-enable streaming path in `RunDictationPipelineAsync`
- Rebuild app, test v12 edge function
- Check logs for `gemini_setup_complete`
- Risk: 12 iterations of accumulated changes, hard to reason about

**Questions for Gemini (if v12 still fails):**
- Try `v1alpha` endpoint instead of `v1beta`
- Try `gemini-2.5-flash-native-audio-preview-12-2025` to prove the pipeline works with a stable model
- Consider using `@google/genai` SDK in the edge function instead of raw WebSocket
### Troubleshooting Log (April 6, 2026 - Gemini)

**Attempt 1 & Failure:**
- Incorrectly assumed `gemini-3.1-flash-live-preview` was a hallucination due to bias in older AI training data. Downgraded to 2.0-flash-exp. 
- *Result:* Failed instantly. Google outright rejects the connection if the model is incorrect or formatted wrong.

**Attempt 2 & Discovery:**
- Reverted to `gemini-3.1-flash-live-preview`.
- Discovered that the previous developer hardcoded `responseModalities: ["AUDIO"]` and `speechConfig` into the initial WebSocket payload to fix a 1011 internal error. 
- Using a raw script, I verified that 3.1 Live API does connect with `["AUDIO"]`, but it natively streams back chunked PCM audio (synthetic voice) instead of transcription text. Since the C# dictation pipeline parses for `text`, it would receive null properties, silently dropping the output. 
- Tested `responseModalities: ["TEXT"]` natively on 3.1. It connects perfectly and explicitly returns a `setupComplete` signal followed by text transcripts. The Edge function has been updated to use `["TEXT"]` and the unneeded speechConfig was strictly removed.

**Diagnosis of the "Does Nothing" UI Hang:**
- **The UX Flaw:** The C# application architecture was incorrectly modified by the previous AI to block the entire UI (and the start sound) until the WebSocket connection fully negotiates with Google. Because the user clicked the hotkey and received absolutely zero feedback, the app appears fundamentally broken.
- **The Sub-Second Latency Blockers:** The Edge Function requires a cold start, and negotiating WebSockets/TLS natively takes time. Establishing a direct connection to Google *on-demand* every time the user presses a hotkey will **always** introduce latency that makes sub-second performance physically impossible. To achieve "Aqua Voice" style instant dictation, the connection architecture must be entirely revamped so that the WebSocket is opened ahead of time and kept alive in the background, rather than negotiated after the user clicks the button.
