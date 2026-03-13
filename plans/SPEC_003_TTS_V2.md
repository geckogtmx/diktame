# SPEC_003_V2: Text-to-Speech — "dIKta.me Speaks Back"

> **Status:** APPROVED — Ready to implement
> **Created:** 2026-03-12
> **Supersedes:** `plans/SPEC_003_TTS.md` (V1 draft, retained as research reference)
> **Priority:** V2.1 (Post-ship enhancement)
> **V1 Reference:** `SPEC_019_QWEN3_TTS_RESEARCH.md` (deferred, never implemented)
> **Philosophy:** Local-first for privacy (Kokoro-ONNX), cloud-first for quality (Deepgram Aura-2 / Inworld). Both paths available from day one.

---

## 0. Session Handoff (Start Here)

> **For new Claude sessions:** Read this section first. It tells you exactly where we left off. Then read §9 (Implementation Plan) for task details and §15 (Task Log) for completion status. The rest of the spec is reference — read sections as needed during implementation.

### Quick Context
- **Project:** dIKta.me V2 — C# + WinUI 3 dictation app at `E:\git\diktame`
- **Solution:** `DiktaMe.sln` → `DiktaMe.App` (WinUI 3), `DiktaMe.Core` (class lib), `DiktaMe.Core.Tests` (xUnit)
- **This spec adds:** Voice output (TTS) — "Read This" hotkey, Ask/Chat voice responses, app notifications spoken aloud
- **Build:** `dotnet build DiktaMe.sln -c Release` | **Tests:** `dotnet test DiktaMe.sln`
- **Commit format:** `feat(tts): description [SPEC_003]`

### Current Phase & Status
<!-- UPDATE THIS SECTION when completing tasks or starting new phases -->

| Phase | Status | Notes |
|-------|--------|-------|
| **A: Core Infrastructure** | ✅ Done | ITTSProvider, TtsPlayerService, TextCleaner, settings |
| **B: KokoroSharp Local** | ✅ Done | Local TTS provider, factory, router |
| **C: Read Selection Hotkey** | ✅ Done | Ctrl+Alt+Q pipeline, hotkey wiring, toggle-stop |
| **D: Pipeline Integration** | ✅ Done | TtsSpeaker service, Ask/Chat/Translate hooks, NotificationService.SpeakAsync |
| **E: Cloud Providers** | ✅ Done | Deepgram, Inworld, OpenAI — 66 new tests, TtsFakeHandler |
| **F: UI** | ✅ Done | Settings page, Control Panel toggle, SanitizeNulls crash fix |
| **G: Polish** | ✅ Done | Interrupt, concurrency, edge cases, gap fixes, observability logging |

### Key Files to Read First (When Starting a Phase)
- **Phase A:** `Core/Pipeline/PipelineState.cs`, `Core/Config/AppSettings.cs`, `App/Services/NotificationService.cs`
- **Phase B:** `Core/LLM/LLMProviderFactory.cs` (caching pattern to follow), `Core/LLM/OllamaProvider.cs` (provider pattern)
- **Phase C:** `Core/Input/HotkeyManager.cs` (HotkeyId enum), `Core/Input/TextInjector.cs` (CaptureSelection), `App/ViewModels/LoadingViewModel.cs` (hotkey wiring)
- **Phase D:** `Core/Pipeline/AskPipeline.cs`, `Core/Pipeline/ChatPipeline.cs`, `Core/Pipeline/TranslatePipeline.cs`
- **Phase E:** `Core/LLM/OllamaProvider.cs` (HTTP provider pattern), `Core/Security/SecureStorage.cs`
- **Phase F:** `App/ViewModels/Settings/AudioSettingsViewModel.cs` (settings VM pattern), `App/Views/ControlPanelPage.xaml`
- **Phase G:** `Core/Audio/AudioDucker.cs`, `App/ViewModels/LoadingViewModel.cs` (interrupt wiring)

### Blocked / Open Questions
<!-- Add blockers here as they arise -->
- **B.8:** eSpeak-NG GPL compliance review — needed before shipping KokoroSharp. Not blocking development, only distribution.

---

## 1. Executive Summary

dIKta.me is a voice-in tool — it listens, transcribes, and injects text. This spec adds **voice-out**: the app speaks back. Not as a gimmick, but as an **ambient output channel** — a Jarvis-style feedback loop where the computer talks to you.

### Use Cases (Priority Order)

| # | Use Case | Trigger | What Speaks |
|---|----------|---------|-------------|
| 1 | **"Read This"** | Select text + hotkey (`Ctrl+Alt+Q`) | Selected text from any app — emails, recipes, code comments, articles |
| 2 | **Ask "Read Aloud"** | Toggle in Ask mode | LLM answer spoken instead of (or alongside) text injection |
| 3 | **App Notifications** | Automatic | "LLM not loaded", "Recording started", "Ollama offline" — voice status instead of easily-missed toasts |
| 4 | **Chat Voice Response** | Toggle in Quick Chat | Chat answers spoken aloud for hands-free conversation |
| 5 | **Translate Pronunciation** | Toggle in Translate mode | Hear the translation spoken in the target language |

### Design Principles

1. **Off by default** — TTS is opt-in. Zero impact on users who don't want it.
2. **Local-first** — Kokoro-ONNX runs in-process with zero external dependencies. No data leaves the machine.
3. **Cloud as premium** — Deepgram (already integrated for STT) or Inworld for users who want higher quality or zero GPU.
4. **Non-blocking** — TTS never delays text injection. Text arrives instantly; speech plays in parallel.
5. **Interruptible** — Any new dictation action, Escape key, or Stop button kills active playback immediately.

---

## 2. Current State (V2 — Verified from Codebase)

### What We Already Have

| Component | Status | Where in Code |
|-----------|--------|---------------|
| NAudio (audio framework) | ✅ Vendored | `DiktaMe.Core.csproj` — recording only, no playback |
| `AudioDucker` | ✅ Working | `Core/Audio/AudioDucker.cs` — ducks other apps' volume via WASAPI |
| `AudioRecorder` | ✅ Working | `Core/Audio/AudioRecorder.cs` — 16kHz WAV capture |
| Sound feedback | ✅ Working | `App/Services/NotificationService.cs` — `MediaPlayer` for WAV/system sounds |
| `TextInjector.CaptureSelection()` | ✅ Working | `Core/Input/TextInjector.cs` — clipboard-based selection capture via Ctrl+C |
| `HotkeyManager` | ✅ Working | `Core/Input/HotkeyManager.cs` — 7 hotkeys registered (IDs 1–7) |
| `PipelineFactory` | ✅ Working | `Core/Config/PipelineFactory.cs` — mode-aware provider creation |
| `ILLMProvider` pattern | ✅ Working | `Core/LLM/ILLMProvider.cs` — interface + factory + caching pattern |
| `AppSettings` hierarchy | ✅ Working | `Core/Config/AppSettings.cs` — nested records, source-gen JSON |
| Deepgram .NET SDK | ✅ Vendored | STT integration — reusable for Deepgram TTS (same API key) |

### What's Missing (Gap Analysis)

| Feature | Library/API | Status |
|---------|-------------|--------|
| **Audio playback (NAudio)** | `WasapiOut` / `WaveOutEvent` | ❌ No playback code exists |
| **ITTSProvider interface** | — | ❌ Not implemented |
| **KokoroSharp integration** | `KokoroSharp.CPU` / `.GPU` NuGet | ❌ Not implemented |
| **Cloud TTS providers** | Deepgram Aura-2, Inworld, OpenAI | ❌ Not implemented |
| **TTSRouter** | — | ❌ Not implemented |
| **TextCleaner** | — | ❌ Not implemented (Markdown→speech) |
| **"Read Selection" hotkey** | `HotkeyId.ReadSelection = 8` | ❌ Not registered |
| **TTS Settings** | `AppSettings.Tts` record | ❌ Not in AppSettings |
| **TTS Settings Page** | XAML + ViewModel | ❌ Not built |
| **Control Panel TTS toggle** | ViewModel + XAML | ❌ Not wired |
| **PipelineState.Speaking** | Enum extension | ❌ Not added |

---

## 3. Architecture

### 3.1 Provider Abstraction

```
DiktaMe.Core/
├── TTS/
│   ├── ITTSProvider.cs              // Interface: SynthesizeAsync(text, voice) → TtsResult
│   ├── TtsResult.cs                 // Result: audio bytes, duration, provider, latency
│   ├── KokoroTtsProvider.cs         // LOCAL: KokoroSharp in-process ONNX (default local)
│   ├── DeepgramTtsProvider.cs       // CLOUD: Deepgram Aura-2 (default cloud — same API key as STT)
│   ├── InworldTtsProvider.cs        // CLOUD: Inworld TTS-1.5 (#1 ranked quality, cheapest)
│   ├── OpenAITtsProvider.cs         // CLOUD: OpenAI TTS (BYOK — users with existing keys)
│   ├── TTSRouter.cs                 // Routes to local/cloud based on profile + settings
│   ├── TextCleaner.cs               // Markdown → speech-ready plain text
│   └── TtsPlayerService.cs          // NAudio playback: play, pause, resume, stop, volume
│
├── Config/
│   ├── ITTSProviderFactory.cs       // Factory interface
│   └── TTSProviderFactory.cs        // Creates + caches provider instances
```

### 3.2 ITTSProvider Interface

```csharp
public interface ITTSProvider : IDisposable
{
    string ProviderName { get; }
    bool SupportsStreaming { get; }

    /// <summary>Synthesize text to audio bytes (full generation).</summary>
    Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Check if the provider is ready (model loaded / API reachable).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public sealed record TtsResult(
    byte[] AudioData,          // PCM/WAV audio bytes
    TimeSpan Duration,         // Audio duration
    int SampleRate,            // 22050 / 24000 / 44100
    string Provider,           // "kokoro-onnx" / "deepgram-aura-2" / "inworld-1.5"
    long LatencyMs,            // Time-to-audio-complete
    string Format              // "wav" / "pcm"
);
```

### 3.3 Data Flow

```
USE CASE 1: "Read This" (Hotkey)
─────────────────────────────────
Ctrl+Alt+Q pressed
    → TextInjector.CaptureSelection()       // Grab selected text via clipboard
    → TextCleaner.CleanForSpeech(text)      // Strip markdown, expand symbols
    → TTSRouter.SynthesizeAsync(text)       // Route to local/cloud provider
    → TtsPlayerService.PlayAsync(audioData) // NAudio playback
    → AudioDucker.Duck() during playback    // Lower other apps' volume
    → AudioDucker.Restore() when done

USE CASE 2: Ask/Chat "Read Aloud" (Post-Pipeline)
──────────────────────────────────────────────────
AskPipeline.Completed → result.Text
    → if (TtsSettings.SpeakAskResponses)
        → TextCleaner.CleanForSpeech(result.Text)
        → TTSRouter.SynthesizeAsync(cleanedText)
        → TtsPlayerService.PlayAsync(audioData)    // Parallel with text display

USE CASE 3: App Notifications (Fire-and-Forget)
───────────────────────────────────────────────
NotificationService.SpeakNotification("LLM not loaded")
    → TTSRouter.SynthesizeAsync(short text)
    → TtsPlayerService.PlayAsync(audioData)  // Low volume, non-blocking
```

### 3.4 Provider Tiers

| Tier | Provider | Model | Cost | Latency | Quality | GPU Needed | When to Use |
|------|----------|-------|------|---------|---------|:----------:|-------------|
| **Local (Default)** | KokoroSharp | Kokoro v1.0 (82M) | Free | 300-500ms CPU / 25ms GPU | ★★★★★ | Optional | Privacy, offline, no cost |
| **Cloud Tier 1** | Deepgram | Aura-2 | $27/1M chars | 90-200ms | ★★★★ | No | Already have Deepgram key |
| **Cloud Tier 2** | Inworld | TTS-1.5 Mini | $5/1M chars | 130ms | ★★★★★ | No | Best quality/price ratio |
| **Cloud Tier 3** | OpenAI | TTS-1 | $15/1M chars | 200-500ms | ★★★★ | No | BYOK (existing OpenAI key) |

### 3.5 VRAM Budget (Local Mode)

Kokoro-ONNX is lightweight enough to coexist with the existing Ollama LLM stack:

| Workload | VRAM | Notes |
|----------|------|-------|
| Ollama gemma3:1b (LLM) | ~1.5 GB | Current default local LLM |
| Kokoro v1.0 int8 (TTS) | ~1.5-2 GB | 88MB model, inference working memory |
| **Combined** | **~3-3.5 GB** | Fits comfortably on 8GB GPU |
| Remaining headroom (8GB) | ~4.5 GB | Room for browser, games, etc. |

CPU-only users: Kokoro int8 runs at 300-500ms on modern CPUs (no GPU needed). Acceptable for read-back and notifications.

---

## 4. Provider Implementation Details

### 4.1 KokoroTtsProvider (Local Default)

**NuGet:** `KokoroSharp.CPU` (v0.6.4) or `KokoroSharp.GPU` (CUDA 12.x)

**Key characteristics:**
- 82M parameters, StyleTTS 2 architecture
- 48 pre-bundled voices across 8 languages (EN, ZH, JA, HI, ES, FR, IT, PT)
- Streaming segment-based generation with background threading
- Voice mixing capabilities
- #1 on HuggingFace TTS Spaces Arena

**Model variants:**

| File | Size | Use Case |
|------|------|----------|
| `kokoro-v1.0.int8.onnx` | 88 MB | Default — best size/quality trade-off |
| `kokoro-v1.0.fp16.onnx` | 169 MB | Higher quality if GPU available |
| `kokoro-v1.0.onnx` | 310 MB | Maximum quality (FP32) |

**License considerations:**
- Model: Apache 2.0 (commercial OK)
- KokoroSharp NuGet: MIT License
- eSpeak-NG phonemizer (bundled): GPLv3 — **reviewed (B.8)**.
  - eSpeak-NG is invoked as a **separate process** by KokoroSharp's `Tokenizer.Phonemize()` (not statically linked).
  - The `.dll`/`.exe` binaries are bundled inside the KokoroSharp NuGet `content/espeak/` folder and copied to the output directory at build time.
  - **GPL compliance**: Since eSpeak-NG is distributed as a standalone binary (not linked into our code), this constitutes "mere aggregation" under GPLv3 §5. dIKta.me's own source need not be GPL-licensed. However, we **must**:
    1. Include the eSpeak-NG GPLv3 license text in distribution (a `THIRD_PARTY_NOTICES.md` or similar).
    2. Provide access to eSpeak-NG source code (link to https://github.com/espeak-ng/espeak-ng is sufficient).
    3. Not modify the eSpeak-NG binaries without releasing those modifications under GPL.
  - **Action**: Add `THIRD_PARTY_NOTICES.md` before first public release (Phase G polishing).

**Integration pattern:**
```csharp
// Singleton — load once, reuse across dictations
private readonly KokoroModel _model;
private readonly KokoroTokenizer _tokenizer;

public async Task<TtsResult> SynthesizeAsync(string text, string? voiceId, CancellationToken ct)
{
    var voice = voiceId ?? "af_bella";  // Default voice
    var sw = Stopwatch.StartNew();
    byte[] audio = await _model.SynthesizeAsync(text, voice, ct);
    return new TtsResult(audio, ..., sw.ElapsedMilliseconds, ...);
}
```

### 4.2 DeepgramTtsProvider (Cloud Default)

**Why Deepgram:** Already integrated for STT — same API key, same .NET SDK, zero new credentials.

**SDK:** `Deepgram` NuGet (already vendored)

| Metric | Value |
|--------|-------|
| API | `POST /v1/speak` + WebSocket streaming |
| Latency (TTFB) | 90-200ms |
| Voices | 40+ English, 10+ Spanish, 7 languages total |
| Cost | $27/1M characters |
| Streaming | ✅ WebSocket + REST |

### 4.3 InworldTtsProvider (Cloud Premium)

> **Note:** $10 USD Inworld credit available — enough for ~1-2M characters of testing.

| Metric | Value |
|--------|-------|
| API | `POST /tts/v1/synthesize` |
| Latency | 130ms (Mini), 250ms (Max) |
| Quality | #1 on Artificial Analysis leaderboard |
| Cost | $5-10/1M characters |
| Languages | 15 |
| Voice cloning | Free instant (5-15s sample) |

### 4.4 OpenAITtsProvider (Cloud BYOK)

| Metric | Value |
|--------|-------|
| API | `POST /v1/audio/speech` |
| Latency | 200-500ms |
| Voices | 13 (alloy, ash, ballad, coral, echo, etc.) |
| Cost | $15/1M characters |
| Streaming | ✅ Multiple output formats |

---

## 5. TtsPlayerService (Audio Playback)

NAudio-based playback with full transport controls. Uses `WasapiOut` for low-latency output.

```csharp
public sealed class TtsPlayerService : IDisposable
{
    // ── Playback Control ──
    Task PlayAsync(byte[] audioData, int sampleRate, CancellationToken ct);
    void Pause();
    void Resume();
    void Stop();

    // ── State ──
    bool IsPlaying { get; }
    bool IsPaused { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    // ── Volume (0.0–1.0) ──
    float Volume { get; set; }

    // ── Events ──
    event EventHandler? PlaybackStarted;
    event EventHandler? PlaybackFinished;
    event EventHandler? PlaybackStopped;  // User-initiated stop (vs natural finish)
}
```

**Integration with AudioDucker:**
- Before `PlayAsync()`: call `AudioDucker.Duck()` to lower other apps
- On `PlaybackFinished` / `PlaybackStopped`: call `AudioDucker.Restore()`
- Reuses existing `AudioDuckingSettings.DuckLevelPercent` from `AppSettings`

**Interrupt behavior:**
- New dictation hotkey press → `Stop()` immediately
- Escape key → `Stop()`
- New TTS request → `Stop()` current, start new

---

## 6. TextCleaner — Markdown to Speech

LLM responses contain formatting that sounds terrible when read aloud. `TextCleaner` preprocesses text before synthesis.

```
Input:  "## Summary\n\nThe **budget** is $50k.\n- Item 1\n- Item 2\n```code block```"
Output: "Summary. The budget is 50 thousand dollars. Item 1. Item 2."
```

**Rules:**

| Pattern | Transformation |
|---------|---------------|
| `## Header` | `"Header."` (add period, strip `#`) |
| `**bold**` / `*italic*` | Strip markers, keep text |
| `` `inline code` `` | Keep text, strip backticks |
| ```` ```code block``` ```` | `"Code block omitted."` |
| `- Item` / `* Item` | `"Item."` (add period for pause) |
| `$50k` | `"50 thousand dollars"` |
| `3.14` | `"3 point 1 4"` |
| `→` / `&` / `%` | `"arrow"` / `"and"` / `"percent"` |
| URLs | `"Link omitted."` |
| `> blockquote` | Strip `>`, keep text |
| Excess whitespace | Collapse to single space |

**Truncation:** If text > `MaxSpeechWords` (default 500), speak first N words + "Full text has been injected."

---

## 7. Settings

### 7.1 TtsSettings Record

```csharp
public sealed record TtsSettings
{
    /// <summary>Master TTS toggle (off by default — opt-in feature).</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>TTS provider: "kokoro", "deepgram", "inworld", "openai", "none".</summary>
    public string Provider { get; init; } = "kokoro";

    /// <summary>Voice ID (provider-specific). Empty = provider default.</summary>
    public string VoiceId { get; init; } = string.Empty;

    /// <summary>Speaking rate multiplier (0.5–2.0). 1.0 = normal speed.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Playback volume (0–100). Independent of system volume.</summary>
    public int VolumePercent { get; init; } = 80;

    /// <summary>Max words to speak before truncating. 0 = unlimited.</summary>
    public int MaxSpeechWords { get; init; } = 500;

    /// <summary>Duck other apps during TTS playback.</summary>
    public bool DuckDuringPlayback { get; init; } = true;

    // ── Per-mode toggles ──

    /// <summary>Speak Ask mode responses aloud.</summary>
    public bool SpeakAskResponses { get; init; } = false;

    /// <summary>Speak Quick Chat responses aloud.</summary>
    public bool SpeakChatResponses { get; init; } = false;

    /// <summary>Speak translated text aloud.</summary>
    public bool SpeakTranslations { get; init; } = false;

    /// <summary>Speak app notifications aloud (errors, warnings, status).</summary>
    public bool SpeakNotifications { get; init; } = false;

    // ── Kokoro-specific ──

    /// <summary>Kokoro model variant: "int8", "fp16", "fp32".</summary>
    public string KokoroModelVariant { get; init; } = "int8";
}
```

**Placement in AppSettings:**
```csharp
public sealed record AppSettings
{
    // ... existing settings ...
    public TtsSettings Tts { get; init; } = new();
}
```

### 7.2 Hotkey Addition

```csharp
public sealed record HotkeySettings
{
    // ... existing hotkeys ...
    public string ReadSelection { get; init; } = "Ctrl+Alt+Q";
}
```

```csharp
public enum HotkeyId
{
    // ... existing 1-7 ...
    ReadSelection = 8,
}
```

### 7.3 PipelineState Extension

```csharp
public enum PipelineState
{
    // ... existing states ...
    Speaking,   // TTS playback in progress
}
```

---

## 8. UI Design

### 8.1 Control Panel — TTS Toggle

Add to quick toggles row (existing 6 columns → 7 columns):

```
┌─ Quick Toggles ──────────────────────────────────────────────────────┐
│  SOUND   STT    LLM    +KEY    RAW    REFINE   TTS                  │
│  [ON]   [Local] [Local] [OFF]  [OFF]  [OFF]    [OFF]               │
│   On     Ollama  Ollama  Off    Off    Off      Off                 │
└─────────────────────────────────────────────────────────────────────┘
```

Toggle saves `TtsSettings.Enabled` immediately via `_settings.UpdateAsync()`.

### 8.2 TTS Settings Page

```
┌─ Settings > Text-to-Speech ─────────────────────────────────────────┐
│                                                                      │
│  ┌─ Provider ──────────────────────────────────────────────────┐    │
│  │  ○ Kokoro (Local — offline, private, no cost)               │    │
│  │  ○ Deepgram Aura-2 (Cloud — uses your Deepgram key)        │    │
│  │  ○ Inworld TTS-1.5 (Cloud — best quality, lowest cost)     │    │
│  │  ○ OpenAI TTS (Cloud — uses your OpenAI key)               │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  Voice:    [ af_bella ▾ ]     Speed:  [────●──── 1.0x]             │
│  Volume:   [────────●── 80%]  Max words: [ 500 ]                    │
│                                                                      │
│  [ ▶ Test Voice ]  "The quick brown fox jumps over the lazy dog."   │
│                                                                      │
│  ┌─ When to Speak ─────────────────────────────────────────────┐    │
│  │  □ Ask mode responses          □ App notifications           │    │
│  │  □ Quick Chat responses        □ Duck other apps             │    │
│  │  □ Translation results                                       │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  "Read Selection" Hotkey:  [ Ctrl+Alt+Q ]                           │
│                                                                      │
│  ┌─ Kokoro Model (Local Only) ─────────────────────────────────┐   │
│  │  Model: kokoro-v1.0.int8.onnx (88 MB)    Status: ● Loaded   │   │
│  │  [ Download fp16 (169 MB) ] [ Download fp32 (310 MB) ]      │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 8.3 Ask Mode — Read Aloud Toggle

In Ask output notification (toast or window), add a speaker icon button:

```
┌─ Ask Result ──────────────────────────────────┐
│  The capital of France is Paris. It's known   │
│  for the Eiffel Tower and...                  │
│                                               │
│  [ 🔊 Read Aloud ]  [ Copy ]  [ Dismiss ]    │
└───────────────────────────────────────────────┘
```

If `TtsSettings.SpeakAskResponses = true`, auto-plays without button press.

---

## 9. Implementation Plan

### Phase A: Core TTS Infrastructure (Foundation)

| Task | Description | Files |
|------|-------------|-------|
| A.1 | `ITTSProvider` interface + `TtsResult` record | `Core/TTS/ITTSProvider.cs`, `Core/TTS/TtsResult.cs` |
| A.2 | `TtsPlayerService` — NAudio `WasapiOut` playback with play/pause/stop/volume | `Core/TTS/TtsPlayerService.cs` |
| A.3 | `TextCleaner` — Markdown stripping, symbol expansion, truncation | `Core/TTS/TextCleaner.cs` |
| A.4 | `TtsSettings` record + add to `AppSettings` + JSON source-gen update | `Core/Config/AppSettings.cs` |
| A.5 | `PipelineState.Speaking` enum value | `Core/Pipeline/PipelineState.cs` |
| A.6 | Unit tests for `TextCleaner` (20+ cases: markdown, symbols, truncation, edge cases) | `Tests/TTS/TextCleanerTests.cs` |
| A.7 | Unit tests for `TtsPlayerService` (mock audio, state transitions) | `Tests/TTS/TtsPlayerServiceTests.cs` |

### Phase B: KokoroSharp Local Provider

| Task | Description | Files |
|------|-------------|-------|
| B.1 | Add `KokoroSharp.CPU` NuGet to `DiktaMe.Core.csproj` | `DiktaMe.Core.csproj` |
| B.2 | `KokoroTtsProvider` — load model, synthesize, voice selection | `Core/TTS/KokoroTtsProvider.cs` |
| B.3 | Model download manager — download Kokoro ONNX models to `%APPDATA%/DiktaMe/models/tts/` | `Core/TTS/KokoroModelManager.cs` |
| B.4 | `ITTSProviderFactory` + `TTSProviderFactory` with caching (follow `LLMProviderFactory` pattern) | `Core/Config/ITTSProviderFactory.cs`, `Core/Config/TTSProviderFactory.cs` |
| B.5 | `TTSRouter` — provider routing based on `TtsSettings.Provider` | `Core/TTS/TTSRouter.cs` |
| B.6 | Unit tests for `KokoroTtsProvider` (mock model, voice selection, error paths) | `Tests/TTS/KokoroTtsProviderTests.cs` |
| B.7 | Unit tests for `TTSRouter` (routing logic, fallback, availability check) | `Tests/TTS/TTSRouterTests.cs` |
| B.8 | Verify eSpeak-NG GPL compliance for distribution model | Legal review (document in this spec) |

### Phase C: "Read Selection" Hotkey Pipeline

| Task | Description | Files |
|------|-------------|-------|
| C.1 | Add `HotkeyId.ReadSelection = 8` to enum | `Core/Input/HotkeyManager.cs` |
| C.2 | Add `ReadSelection` to `HotkeySettings` (default: `Ctrl+Alt+Q`) | `Core/Config/AppSettings.cs` |
| C.3 | Register hotkey in `LoadingViewModel.InitializeHotkeys()` | `App/ViewModels/LoadingViewModel.cs` |
| C.4 | `ReadSelectionPipeline` — capture selection → clean → synthesize → play | `Core/Pipeline/ReadSelectionPipeline.cs` |
| C.5 | Wire hotkey handler: `OnHotkeyPressed` → `ReadSelectionPipeline.RunAsync()` | `App/ViewModels/LoadingViewModel.cs` |
| C.6 | Audio ducking integration: `Duck()` on playback start, `Restore()` on finish | `Core/Pipeline/ReadSelectionPipeline.cs` |
| C.7 | Interrupt: Escape key or new hotkey press → `TtsPlayerService.Stop()` | `App/ViewModels/LoadingViewModel.cs` |
| C.8 | Unit tests for `ReadSelectionPipeline` (mock injector, mock TTS, state transitions) | `Tests/Pipeline/ReadSelectionPipelineTests.cs` |

### Phase D: Pipeline TTS Integration (Ask, Chat, Translate)

| Task | Description | Files |
|------|-------------|-------|
| D.1 | Post-pipeline TTS hook: `AskPipeline.Completed` → optional TTS | `App/ViewModels/LoadingViewModel.cs` |
| D.2 | Post-pipeline TTS hook: `ChatPipeline.Completed` → optional TTS | `App/ViewModels/ChatViewModel.cs` (or equivalent) |
| D.3 | Post-pipeline TTS hook: `TranslatePipeline.Completed` → optional TTS | `App/ViewModels/LoadingViewModel.cs` |
| D.4 | `PipelineResult` — add `TtsPlayedMs` field for telemetry | `Core/Pipeline/PipelineResult.cs` |
| D.5 | Notification voice: `NotificationService.SpeakAsync(text)` for app status | `App/Services/NotificationService.cs` |
| D.6 | Unit tests for pipeline TTS hooks (verify TTS triggers when enabled, skips when disabled) | `Tests/Pipeline/PipelineTtsIntegrationTests.cs` |

### Phase E: Cloud Providers

| Task | Description | Files |
|------|-------------|-------|
| E.1 | `DeepgramTtsProvider` — REST API with Deepgram .NET SDK | `Core/TTS/DeepgramTtsProvider.cs` |
| E.2 | `InworldTtsProvider` — REST API client | `Core/TTS/InworldTtsProvider.cs` |
| E.3 | `OpenAITtsProvider` — REST API client | `Core/TTS/OpenAITtsProvider.cs` |
| E.4 | API key storage: reuse `SecureStorage` for Inworld key (Deepgram/OpenAI keys already stored) | `Core/Config/TTSProviderFactory.cs` |
| E.5 | Unit tests for each cloud provider (fake HTTP handler pattern) | `Tests/TTS/DeepgramTtsProviderTests.cs`, etc. |

### Phase F: UI — Settings & Control Panel

| Task | Description | Files |
|------|-------------|-------|
| F.1 | `TtsSettingsViewModel` — provider selection, voice, speed, volume, per-mode toggles | `App/ViewModels/Settings/TtsSettingsViewModel.cs` |
| F.2 | `TtsSettingsPage.xaml` — full settings UI per §8.2 design | `App/Views/Settings/TtsSettingsPage.xaml` |
| F.3 | Register settings page in navigation + DI container | `App/Views/SettingsWindow.xaml`, `App/App.xaml.cs` |
| F.4 | Control Panel TTS toggle (7th column) | `App/ViewModels/ControlPanelViewModel.cs`, `App/Views/ControlPanelPage.xaml` |
| F.5 | "Test Voice" button: synthesize + play sample text | `App/ViewModels/Settings/TtsSettingsViewModel.cs` |
| F.6 | Kokoro model download progress UI (if model not present on first enable) | `App/Views/Settings/TtsSettingsPage.xaml` |
| F.7 | Localization strings (en Resources.resw) | `App/Strings/en/Resources.resw` |

### Phase G: Polish & Edge Cases

| Task | Description | Files |
|------|-------------|-------|
| G.1 | Playback interrupt: new dictation cancels active TTS | `App/ViewModels/LoadingViewModel.cs` |
| G.2 | Concurrent safety: prevent multiple TTS playbacks overlapping | `Core/TTS/TtsPlayerService.cs` |
| G.3 | Graceful degradation: TTS failure → log warning, continue without speech | `Core/TTS/TTSRouter.cs` |
| G.4 | Model not downloaded: toast prompt to download on first use | `App/ViewModels/Settings/TtsSettingsViewModel.cs` |
| G.5 | No audio output device: log warning, skip TTS silently | `Core/TTS/TtsPlayerService.cs` |
| G.6 | Long text handling: verify truncation + "Full text injected" suffix works correctly | Manual test |
| G.7 | Privacy: TTS text NOT logged when `PrivacyLevel = Ghost` | All TTS code |

---

## 10. Verification Plan

### 10.1 Unit Tests

**Expected test count:** ~60-80 new tests across 8 test files.

| Test File | Coverage | Est. Tests |
|-----------|----------|:----------:|
| `TextCleanerTests.cs` | Markdown stripping, symbols, numbers, truncation, edge cases | 20+ |
| `TtsPlayerServiceTests.cs` | Play/pause/stop state machine, volume, events | 10+ |
| `KokoroTtsProviderTests.cs` | Synthesis, voice selection, model loading, errors | 8+ |
| `TTSRouterTests.cs` | Routing logic, fallback, availability, settings respect | 8+ |
| `ReadSelectionPipelineTests.cs` | Full pipeline: capture → clean → synth → play | 6+ |
| `PipelineTtsIntegrationTests.cs` | Post-pipeline hooks, per-mode toggles | 6+ |
| `DeepgramTtsProviderTests.cs` | HTTP mocking, response parsing, auth | 5+ |
| `InworldTtsProviderTests.cs` | HTTP mocking, response parsing | 5+ |

**Run:** `dotnet test DiktaMe.sln`

### 10.2 Manual Testing Checklist

- [ ] Enable TTS in Settings → Kokoro provider selected → "Test Voice" plays audio
- [ ] Select text in Notepad → `Ctrl+Alt+Q` → text read aloud with ducking
- [ ] Ask mode → speak question → AI answer read aloud (when toggle on)
- [ ] Quick Chat → send message → response spoken (when toggle on)
- [ ] Translate → speak English → Spanish translation read aloud
- [ ] Press `Ctrl+Alt+Q` during playback → playback stops
- [ ] Start new dictation during TTS → TTS stops, recording starts
- [ ] Disable TTS toggle → no speech on any pipeline
- [ ] Switch from Kokoro to Deepgram → test voice uses cloud
- [ ] Volume slider → changes audible volume
- [ ] Speed slider → changes speaking rate
- [ ] No GPU → Kokoro CPU mode works (slower but functional)
- [ ] No internet → cloud providers fail gracefully, local works
- [ ] Very long text (1000+ words) → truncated, "Full text injected" spoken
- [ ] App notification "LLM not loaded" → spoken aloud (when notifications toggle on)
- [ ] Settings persist across restart

---

## 11. Success Criteria

- [ ] "Read Selection" hotkey works: select text anywhere → hear it read aloud
- [ ] Ask mode can speak LLM responses with <1s delay after generation
- [ ] Kokoro-ONNX works fully offline with zero external dependencies
- [ ] At least one cloud provider (Deepgram) works with existing API key
- [ ] Audio ducking lowers other apps during TTS playback
- [ ] TTS never blocks or delays text injection (parallel execution)
- [ ] Any new dictation action immediately stops TTS playback
- [ ] TTS failure never crashes the app or blocks pipelines
- [ ] TTS is off by default — zero impact on users who don't enable it
- [ ] 60+ unit tests passing
- [ ] Build: 0 warnings, 0 errors (`dotnet build DiktaMe.sln -c Release`)
- [ ] Settings page: provider selection, voice, speed, volume, per-mode toggles all functional

---

## 12. Error Handling

| Scenario | Response |
|----------|----------|
| Kokoro model not downloaded | Toast: "TTS model not found. Download in Settings > Text-to-Speech." |
| Kokoro ONNX runtime failure | Log error, disable TTS for session. Toast: "TTS engine failed to initialize." |
| Cloud API key missing | Toast: "No API key for {provider}. Add key in Settings > API Keys." |
| Cloud API error (any provider) | Log warning, skip TTS. Toast: "{Provider} TTS unavailable." |
| Cloud rate limit | Log warning, skip TTS. No retry. |
| No audio output device | Log warning, skip TTS silently. |
| Audio playback device lost mid-play | Stop gracefully, log error. |
| VRAM exhaustion (Kokoro GPU) | Fall back to CPU inference, log warning. |
| Text empty or whitespace-only | Skip TTS silently (no error). |
| CancellationToken cancelled | Stop synthesis/playback immediately, clean up. |

---

## 13. Dependencies

### New NuGet Packages

| Package | Version | Purpose | Size Impact |
|---------|---------|---------|-------------|
| `KokoroSharp.CPU` | 0.6.4+ | Local TTS provider | ~10 MB (NuGet) + 88-310 MB (model download) |
| `KokoroSharp.GPU` | 0.6.4+ | GPU-accelerated variant (optional) | ~15 MB + CUDA runtime (user's responsibility) |

### Existing Dependencies (Reused)

| Package | Purpose |
|---------|---------|
| `NAudio` / `NAudio.Wasapi` | Audio playback (`WasapiOut`) |
| `Deepgram` .NET SDK | Deepgram TTS API client |
| `Serilog` | Logging |

### Model Files (Downloaded on First Use)

| File | Size | Location |
|------|------|----------|
| `kokoro-v1.0.int8.onnx` | 88 MB | `%APPDATA%/DiktaMe/models/tts/kokoro-v1.0.int8.onnx` |
| `kokoro-v1.0.fp16.onnx` | 169 MB | `%APPDATA%/DiktaMe/models/tts/kokoro-v1.0.fp16.onnx` (optional) |

**Publish size impact:** NuGet package only (~10 MB). Model files are downloaded separately on first use — not bundled in the installer. This keeps the installer at current ~70 MB compressed.

---

## 14. Future Considerations (Out of Scope for This Spec)

| Feature | Why Deferred | When |
|---------|-------------|------|
| **Voice cloning** (record your voice, TTS speaks as you) | Requires Orpheus/OuteTTS sidecar (Python) or Inworld cloud clone | V2.2+ |
| **Streaming TTS** (play audio as it generates, token by token) | Kokoro supports segments; cloud providers support WebSocket. Add after core works. | V2.2 |
| **Per-mode voice selection** (different voice for Ask vs Translate) | Nice-to-have, not essential for launch | V2.2 |
| **Emotion/prosody tags** (Orpheus `<laugh>`, `<sigh>`) | Requires Orpheus model integration | V2.2+ |
| **Voice banking** (accessibility — preserve voice for ALS/cancer patients) | Compelling story but complex; needs extended recording + dedicated UI | V2.2+ |
| **ONNX Runtime GenAI for TTS** | Monitor `Microsoft.ML.OnnxRuntimeGenAI` maturity (currently DirectML lags at v0.9.0) | When stable |
| **Ollama TTS models** | Monitor Ollama TTS model support; would simplify if they add first-class TTS | When available |
| **Read Aloud button in Ask/Chat UI** | Manual trigger alongside auto-play. Low effort, nice UX. | Phase G polish or V2.2 |

---

## 15. Task Log (Implementation Status)

> **Status**: All phases complete (A–G)

### Phase A: Core TTS Infrastructure
| Task | Status | Notes |
|------|--------|-------|
| A.1 | ✅ Done | ITTSProvider + TtsResult |
| A.2 | ✅ Done | TtsPlayerService (NAudio WasapiOut) |
| A.3 | ✅ Done | TextCleaner (18 GeneratedRegex rules) |
| A.4 | ✅ Done | TtsSettings + AppSettings + ReadSelection hotkey |
| A.5 | ✅ Done | PipelineState.Speaking |
| A.6 | ✅ Done | 36 TextCleaner tests |
| A.7 | ✅ Done | 16 TtsPlayerService tests |

### Phase B: KokoroSharp Local Provider
| Task | Status | Notes |
|------|--------|-------|
| B.1 | ✅ Done | KokoroSharp.CPU 0.6.5 + TrimmerRootAssembly |
| B.2 | ✅ Done | KokoroTtsProvider — KokoroModel.Infer + Tokenizer + voice |
| B.3 | ✅ Done | KokoroModelManager — download + progress events |
| B.4 | ✅ Done | ITTSProviderFactory + TTSProviderFactory with ConcurrentDictionary cache |
| B.5 | ✅ Done | TTSRouter — primary + fallback pattern |
| B.6 | ✅ Done | 20 tests (KokoroTtsProvider + KokoroModelManager) |
| B.7 | ✅ Done | 22 tests (TTSRouter) + 11 tests (TTSProviderFactory) |
| B.8 | ✅ Done | GPL review — eSpeak-NG is process-invoked, "mere aggregation" OK |

### Phase C: Read Selection Hotkey
| Task | Status | Notes |
|------|--------|-------|
| C.1 | ✅ Done | HotkeyId.ReadSelection = 8 |
| C.2 | ✅ Done | HotkeySettings.ReadSelection (Phase A) |
| C.3 | ✅ Done | Hotkey registration in LoadingViewModel |
| C.4 | ✅ Done | ReadSelectionPipeline — clean → synth → play |
| C.5 | ✅ Done | OnHotkeyPressed → RunReadSelectionPipelineAsync |
| C.6 | ✅ Done | Audio ducking in handler |
| C.7 | ✅ Done | Toggle-stop: any hotkey stops TTS, ReadSelection toggles |
| C.8 | ✅ Done | 20 ReadSelectionPipelineTests |

### Phase D: Pipeline TTS Integration
| Task | Status | Notes |
|------|--------|-------|
| D.1 | ✅ Done | Ask → TtsSpeaker.SpeakIfEnabledAsync after output routing |
| D.2 | ✅ Done | Chat → TtsSpeaker.SpeakIfEnabledAsync after UI update |
| D.3 | ✅ Done | Translate → TtsSpeaker.SpeakIfEnabledAsync after injection |
| D.4 | ✅ Done | PipelineResult.TtsPlayedMs field added |
| D.5 | ✅ Done | NotificationService.SpeakAsync delegates to TtsSpeaker |
| D.6 | ✅ Done | 16 TtsSpeakerTests (toggle, mode, synth, play, duck, cancel) |

### Phase E: Cloud Providers
| Task | Status | Notes |
|------|--------|-------|
| E.1 | ✅ Done | DeepgramTtsProvider — POST /v1/speak, Token auth, linear16 PCM 24kHz |
| E.2 | ✅ Done | InworldTtsProvider — POST /tts/v1/voice, Basic auth, base64 JSON → PCM |
| E.3 | ✅ Done | OpenAITtsProvider — POST /v1/audio/speech, Bearer auth, PCM 24kHz |
| E.4 | ✅ Done | SecureStorage + "inworld", TTSProviderFactory wired with SecureStorage |
| E.5 | ✅ Done | 66 new tests (TtsFakeHandler, 3 provider test files, factory tests) |

### Phase F: UI
| Task | Status | Notes |
|------|--------|-------|
| F.1 | ✅ Done | TtsSettingsViewModel — provider, voice, speed, volume, toggles |
| F.2 | ✅ Done | TtsSettingsPage.xaml — full settings UI |
| F.3 | ✅ Done | SettingsWindow nav + DI registration |
| F.4 | ✅ Done | Control Panel TTS toggle (7th column) |
| F.5 | ✅ Done | "Test Voice" button |
| F.6 | ✅ Done | Kokoro model download progress UI |
| F.7 | ✅ Done | Localization (en + es-MX) |

### Phase G: Polish
| Task | Status | Notes |
|------|--------|-------|
| G.1 | ✅ Done | Any hotkey stops active TTS (LoadingViewModel:344) |
| G.2 | ✅ Done | Lock-based state machine in TtsPlayerService |
| G.3 | ✅ Done | TTSRouter try/catch + fallback, returns empty result |
| G.4 | ✅ Done | Download UI + progress for Kokoro model |
| G.5 | ✅ Done | WasapiOut exception caught, logged, returns gracefully |
| G.6 | ✅ Done | TextCleaner truncation (500 words default) |
| G.7 | ✅ Done | Ghost mode skips all text logging |

**Total tasks:** 40 across 7 phases (A–G)

### Post-Phase G: E2E Bug Fixes (2026-03-12)
| Bug | Fix | Commit |
|-----|-----|--------|
| Kokoro download file-lock (stale `.tmp`) | GUID-based temp files + `CleanupStaleTempFiles()` | `d548e66` |
| Misleading developer-facing error messages | User-friendly text + pre-flight model check in TestVoice | `d548e66` |
| Inworld API `encoding` field name wrong | Changed to `audioEncoding` per Inworld API docs | pending |
| Cloud providers receiving `"int8"` as model | Conditional variant routing: only Kokoro gets `KokoroModelVariant`, cloud gets `null` → `ResolveVariant()` | pending |
| KokoroTtsProvider tests fail after model download | Changed tests from `int8` to `fp32` variant (310MB, not downloaded) | pending |

---

## 16. References

### Local TTS
- [KokoroSharp GitHub](https://github.com/Lyrcaxis/KokoroSharp) — C# NuGet, MIT License
- [Kokoro-ONNX GitHub](https://github.com/thewh1teagle/kokoro-onnx) — ONNX wrapper
- [Kokoro-82M HuggingFace](https://huggingface.co/hexgrad/Kokoro-82M) — Apache 2.0 model
- V2 research: `plans/SPEC_003_TTS.md` (V1 draft with full model landscape)

### Cloud TTS
- [Deepgram Aura-2 TTS](https://deepgram.com/learn/introducing-aura-2-enterprise-text-to-speech) — $27/1M chars, .NET SDK
- [Deepgram .NET SDK Streaming TTS](https://developers.deepgram.com/docs/dotnet-sdk-streaming-text-to-speech)
- [Inworld TTS-1.5](https://inworld.ai/tts-api) — #1 ranked, $5-10/1M chars
- [Inworld TTS Docs](https://docs.inworld.ai/docs/tts/tts)
- [OpenAI TTS API](https://platform.openai.com/docs/guides/text-to-speech) — $15/1M chars
- [Artificial Analysis TTS Rankings](https://artificialanalysis.ai/text-to-speech)

### Existing Code
- `Core/Audio/AudioDucker.cs` — Volume ducking via WASAPI
- `Core/Input/TextInjector.cs` — `CaptureSelection()` for "Read This"
- `Core/Input/HotkeyManager.cs` — Global hotkey registration (IDs 1-7)
- `Core/Config/PipelineFactory.cs` — Factory pattern for pipeline creation
- `Core/LLM/LLMProviderFactory.cs` — Caching pattern to follow for TTS
- `App/Services/NotificationService.cs` — `MediaPlayer` playback pattern
- `Core/Pipeline/PipelineResult.cs` — Result model to extend with TTS telemetry

---

## 17. Debug & Fine-Tuning Logging

TTS spans multiple subsystems (synthesis, playback, ducking, settings toggles). For debugging and performance tuning, the following structured logging is in place via Serilog:

### Current Log Points

| Component | Log Level | What's Logged |
|-----------|-----------|---------------|
| `TtsSpeaker` | Information | `"TtsSpeaker: played {Chars} chars in {Ms}ms via {Provider}"` — synthesis + playback timing |
| `TtsSpeaker` | Warning | `"TtsSpeaker: synthesis returned empty audio"` — provider returned no data |
| `TtsSpeaker` | Warning | `"TtsSpeaker: synthesis/playback failed"` — exception details with stack trace |
| `ReadSelectionPipeline` | Information | `"synthesized {Chars} chars in {Ms}ms via {Provider}"` — per-stage timing |
| `ReadSelectionPipeline` | Warning | `"synthesis returned empty audio"` |
| `LoadingViewModel (Ask)` | Information | `"Ask: TTS played in {TtsMs}ms"` — end-to-end TTS for Ask responses |
| `LoadingViewModel (Translate)` | Information | `"Translate: TTS played in {TtsMs}ms"` — end-to-end TTS for translations |
| `QuickChatViewModel` | Information | `"Chat: TTS played in {TtsMs}ms"` — end-to-end TTS for chat responses |
| `TtsPlayerService` | Warning | `"no audio output device available"` — WasapiOut init failure |
| `TtsPlayerService` | Warning | `"error during Stop"` — cleanup errors |

### Future Logging Needs (Phase G Polish)

- **Settings state dump on first TTS call** — log `TtsSettings` snapshot (Enabled, Provider, VoiceId, mode toggles) at startup or first invocation. Helps diagnose "TTS not working" reports without reproducing.
- **Provider factory cache hit/miss** — log when `TTSProviderFactory` creates a new provider vs returns cached instance. Key for diagnosing latency spikes on first invocation.
- **Ducking state transitions** — log when `AudioDucker.Duck()`/`Restore()` is called by TtsSpeaker, to debug volume glitches.
- **Synthesis latency vs playback latency split** — currently `TtsSpeaker` logs combined time. Split into `SynthesisMs` and `PlaybackMs` for latency profiling. `PipelineResult.TtsPlayedMs` is available for this.
- **Audio buffer stats** — log sample rate, byte count, and estimated duration before playback. Helps catch format mismatches.
- **TextCleaner before/after diff** — at Debug level, log original text length vs cleaned length to verify truncation and stripping rules.
