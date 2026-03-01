# SPEC_003: Text-to-Speech Module

> **Status:** DRAFT (Research sprint required before implementation)
> **Date:** 2026-03-01
> **Supersedes:** V1 `SPEC_019_QWEN3_TTS_RESEARCH.md` (deferred, never implemented)
> **Priority:** TBD (post-V2 launch, pending research GO/NO-GO)
> **Philosophy:** Local-first for privacy, cloud-first for instant value. Both paths available from day one.

---

## 1. Executive Summary

dIKta.me speaks **to** users (dictation, notes, meeting summaries) but never speaks **back**. The TTS module adds voice output — when the AI answers a question, translates text, or processes a meeting, it can read the response aloud.

**Strategic shift from V1:** The original spec treated cloud TTS as a quality ceiling to aspire to locally. The landscape has changed — cloud TTS (especially Inworld at $5-10/1M chars) is now cheap enough to offer as a first-class option alongside local models. The dual strategy is: **cloud-first for instant value** (zero GPU, zero setup), **local-first for privacy** (offline, no data leaves machine). Both paths available from day one.

**What changed since V1 spec (Jan 2026):**
- The TTS model ecosystem has matured significantly
- Multiple competitive options now exist beyond Qwen3-TTS
- Ollama has added TTS model support for some architectures
- V2 is C# + WinUI 3 (not Python + Electron) — the integration approach is fundamentally different

---

## 2. Model Landscape (March 2026)

The V1 spec was laser-focused on Qwen3-TTS. The landscape has exploded — there are now multiple strong open-source models under 4B parameters that support voice cloning and run on consumer GPUs.

### 2.0 Local Open-Source Models (<4B)

These are the models we should evaluate for local inference. All are small enough to run alongside Ollama on an 8GB GPU (with care) or comfortably on 12GB+.

| Model | Params | VRAM | Voice Cloning | Latency | Quality | Languages | License | Notes |
|-------|--------|------|:------------:|---------|---------|-----------|---------|-------|
| **Orpheus 3B** | 3B | ~6-8GB (FP16), <4GB (GGUF q4) | Yes (5s, zero-shot) | ~100-200ms streaming | Excellent — rivals ElevenLabs | EN (expanding) | Apache 2.0 | **Top pick.** Llama-3B backbone, 100k+ hrs training, guided emotion tags (`<laugh>`, `<sigh>`), 4 size variants. [FastAPI server available](https://github.com/Lex-au/Orpheus-FastAPI). |
| **Orpheus 1B** | 1B | ~2-3GB | Yes (5s) | Fast | Very Good | EN | Apache 2.0 | Sweet spot for 8GB GPUs. Same architecture, slightly less expressive. |
| **Orpheus 400M** | 400M | ~1.5GB | Yes (5s) | Very Fast | Good | EN | Apache 2.0 | Ultra-light. Could run concurrently with Whisper + LLM on 8GB. |
| **Orpheus 150M** | 150M | <1GB | Yes (5s) | Fastest | Decent | EN | Apache 2.0 | Smallest variant. CPU-viable for basic TTS. |
| **Kani-TTS-2** | 400M | **3GB** | Yes (speaker embeddings) | RTF 0.2 (10s speech in 2s) | Good | EN, AR, ZH, FR, DE, JA, KO, ES | Apache 2.0 | New contender (Feb 2026). Best multilingual option at this size. No fine-tuning needed for cloning. |
| **Qwen3-TTS** | 0.6B / 1.7B | ~2-4GB | Yes (3s sample) | TBD | High | 10 | Apache 2.0 | V1 spec target. Emotion/prosody control, streaming. Alibaba. |
| **Fish Speech V1.5** | ~500M | ~3-4GB | Yes (10-30s) | TBD | Very High (ELO 1339) | EN, ZH, JA | Apache 2.0 | DualAR architecture, 300k+ hrs training. WER 3.5%. |
| **OpenAudio S1-mini** | 0.5B | TBD | Yes (10-30s) | TBD | Very High | Multi | Open | Distilled from 4B S1. RLHF-trained. CER 0.4%, WER 0.8%. |
| **CosyVoice2** | 0.5B | ~2-3GB | Yes | 150ms streaming | High (MOS 5.53) | ZH, EN, JA, KO | Apache 2.0 | Alibaba. Best for CJK languages. 30-50% fewer pronunciation errors vs v1. |
| **Dia2** | 1B / 2B | ~4GB (1B), ~12GB (2B) | Yes (5-15s conditioning) | Real-time streaming | High | EN | Apache 2.0 | Nari Labs. Streaming architecture — starts generating before full text input. Dialogue specialist. |
| **Kokoro** | 82M | <1GB, CPU-viable | No | <100ms | Good | EN/multi | Apache 2.0 | Tiny and fast, but no voice cloning — use as CPU fallback only. |
| **Piper** | ~30MB | CPU only | No | <50ms | Decent | 40+ | MIT | Baseline comparison. Notification-quality, not conversational. |

#### Recommended Evaluation Order (Research Sprint)

1. **Orpheus 1B/3B** — Best overall: voice cloning, emotion control, multiple sizes, GGUF quantization for low VRAM, OpenAI-compatible FastAPI server exists. Start here.
2. **Kani-TTS-2 (400M)** — Best bang-for-buck: 3GB VRAM, 8 languages, Apache 2.0, voice cloning via speaker embeddings. Strong for multilingual users on modest hardware.
3. **Qwen3-TTS (0.6B)** — Original V1 target: streaming, emotion control. Evaluate against Orpheus to see if it justifies the switch.
4. **Fish Speech V1.5 / OpenAudio S1-mini** — Highest benchmark scores but less documented for local self-hosting. Evaluate if top 3 disappoint.

#### VRAM Budget Scenarios

| GPU | Available VRAM | Best TTS fit | Can run alongside Ollama LLM? |
|-----|---------------|-------------|:-----------------------------:|
| RTX 4060 (8GB) | ~6GB free | Orpheus 400M (1.5GB) or Kani-TTS-2 (3GB) | Yes, with small LLM (1-4B) |
| RTX 4060 Ti (16GB) | ~14GB free | Orpheus 3B GGUF (4GB) | Yes, comfortably |
| RTX 4070+ (12GB+) | ~10GB+ free | Orpheus 3B FP16 (6-8GB) | Yes |
| CPU-only (no GPU) | N/A | Kokoro 82M or Piper | N/A (no local LLM) — use cloud TTS |

*Sources: [Orpheus TTS GitHub](https://github.com/canopyai/Orpheus-TTS), [Orpheus FastAPI](https://github.com/Lex-au/Orpheus-FastAPI), [Kani-TTS-2 (MarkTechPost)](https://www.marktechpost.com/2026/02/15/meet-kani-tts-2-a-400m-param-open-source-text-to-speech-model-that-runs-in-3gb-vram-with-voice-cloning-support/), [LocalClaw TTS Guide 2026](https://localclaw.io/blog/local-tts-guide-2026), [SiliconFlow Small TTS Guide](https://www.siliconflow.com/articles/en/best-small-text-to-speech-models-2025), [Dia2 GitHub](https://github.com/nari-labs/dia2)*

### 2.1 Cloud TTS Landscape (March 2026)

Cloud TTS is no longer just "OpenAI or ElevenLabs." The market has diversified dramatically. Inworld AI has emerged as a serious contender with top-ranked quality at a fraction of incumbent pricing.

| Provider | Model | Cost / 1M chars | Latency (P90) | Quality (ELO) | Voice Cloning | Languages | Streaming |
|----------|-------|-----------------|---------------|---------------|:-------------:|-----------|:---------:|
| **Inworld** | TTS-1.5 Mini | **$5** | <130ms | #1 (1,160) | Free instant (5-15s) | 15 | WebSocket + HTTP |
| **Inworld** | TTS-1.5 Max | **$10** | <250ms | #1 (1,160) | Free instant (5-15s) | 15 | WebSocket + HTTP |
| **OpenAI** | TTS-1 | $15 | ~200ms | #4 (1,106) | No | 57+ | HTTP |
| **OpenAI** | TTS-1-HD | $30 | ~500ms | ~#4 | No | 57+ | HTTP |
| **ElevenLabs** | Multilingual v2 | **$206** | ~75ms | #3 (1,108) | Professional | 29+ | WebSocket |
| **Hume AI** | Octave 2 | $7.60 | ~100ms | #14 (1,046) | Yes (15s) | Multi | HTTP |
| **Amazon** | Polly (Generative) | $30 | 100ms-1s | #8 (1,060) | No | 30+ | HTTP |
| **Google** | Cloud Studio | $160 | 200-250ms | #13 (1,048) | No | 30+ | HTTP |

**Key insight:** Inworld delivers **#1 quality at 1/20th the cost of ElevenLabs** and **1/3 the cost of OpenAI**. Voice cloning is free and instant (5-15s sample). This makes it the default cloud recommendation for dIKta.me.

**Why not just Inworld?** We support multiple cloud providers because:
1. Users may already have OpenAI API keys (BYOK model)
2. ElevenLabs has the widest language coverage (57+)
3. Provider diversity prevents vendor lock-in
4. Some users prefer providers they already trust

*Sources: [Artificial Analysis TTS Rankings](https://artificialanalysis.ai/text-to-speech), [Inworld 2026 Benchmarks](https://inworld.ai/resources/best-voice-ai-tts-apis-for-real-time-voice-agents-2026-benchmarks)*

---

## 3. V2 Architecture: How TTS Fits

### 3.1 The Key Difference from V1

V1 was Python — TTS models run natively via `transformers` + PyTorch. V2 is C# — we need a bridge:

| Approach | Pros | Cons | Recommendation |
|----------|------|------|----------------|
| **Ollama TTS** | Already integrated, model management built-in | Limited TTS model support, API may not cover all features | Monitor — ideal if Ollama adds first-class TTS |
| **Python sidecar** | Full PyTorch ecosystem, direct model access | Extra process, 200MB+ dependency, complex lifecycle | Fallback if Ollama insufficient |
| **ONNX Runtime** | Native C#, no Python dependency, fast inference | Model conversion required, may lose features | Best long-term option if models export cleanly |
| **HTTP API to local server** | Model-agnostic, clean separation | Extra process, port management | Good middle ground |

**Recommended approach:** Start with **HTTP API to a local TTS server** (e.g., the model's built-in server or a thin FastAPI wrapper). This is model-agnostic and cleanly separates concerns. If Ollama adds robust TTS support, migrate to that. ONNX Runtime is the long-term goal for zero-dependency native C#.

### 3.2 Component Architecture

```
DiktaMe.Core/
├── TTS/
│   ├── ITTSProvider.cs             // Interface: GenerateSpeechAsync(text, voice?) → AudioData
│   ├── TTSResult.cs                // Result model (audio bytes, duration, provider info)
│   ├── LocalTTSProvider.cs         // HTTP client to local TTS server (Qwen3/Orpheus/Fish)
│   ├── InworldTTSProvider.cs       // Inworld TTS API (recommended cloud — best price/quality)
│   ├── OpenAITTSProvider.cs        // OpenAI TTS API (cloud BYOK option)
│   ├── ElevenLabsTTSProvider.cs    // ElevenLabs TTS API (cloud — widest language support)
│   ├── TTSRouter.cs                // Routes to local/cloud based on profile + provider selection
│   ├── VoiceCloneManager.cs        // Voice sample recording, storage, management
│   ├── TTSSettings.cs              // Settings record
│   └── AudioPlayer.cs              // NAudio playback with volume control, pause/resume/stop

DiktaMe.App/
├── Views/
│   ├── VoiceRecordingWizard.xaml   // 3-step voice cloning wizard
│   └── Settings/
│       └── TTSSettingsPage.xaml    // TTS configuration tab
```

### 3.3 ITTSProvider Interface

```csharp
public interface ITTSProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    bool SupportsVoiceCloning { get; }

    Task<TTSResult> GenerateSpeechAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default);

    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record TTSResult(
    byte[] AudioData,        // WAV/PCM audio bytes
    TimeSpan Duration,       // Audio duration
    string Provider,         // "qwen3-tts" / "orpheus" / "openai"
    int SampleRate,          // 22050 / 44100 / etc.
    string Format            // "wav" / "pcm"
);
```

### 3.4 AudioPlayer (NAudio)

Playback via NAudio — already a project dependency:

```csharp
public sealed class AudioPlayer : IDisposable
{
    // Core playback
    Task PlayAsync(byte[] audioData, int sampleRate, CancellationToken ct);
    void Pause();
    void Resume();
    void Stop();

    // Volume (0.0 – 1.0)
    float Volume { get; set; }

    // State
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    // Events
    event EventHandler PlaybackFinished;
}
```

Uses `WaveOutEvent` or `WasapiOut` for low-latency output. Audio ducking integration: when TTS speaks, optionally duck other apps (reuse `AudioDucker`).

---

## 4. Feature Specification

### 4.1 Where TTS Speaks

TTS is not a standalone mode — it's a **cross-cutting output layer** that enhances existing modes:

| Mode | TTS Behavior | Priority |
|------|-------------|----------|
| **Ask** (`Ctrl+Alt+A`) | Speak the AI's answer aloud | HIGH — primary use case. Hands-free Q&A. |
| **Quick Chat** | Speak chat responses | HIGH — conversational flow |
| **Translate** (`Ctrl+Alt+T`) | Speak the translated text (in target language) | MEDIUM — pronunciation aid |
| **Meeting synthesis** (SPEC_001) | Speak the meeting summary | LOW — usually long, better as text |
| **Dictate / Refine / Note** | No TTS — these inject into other apps | N/A |

### 4.2 TTS Flow

```
[1] Pipeline completes (Ask/Chat/Translate) → has response text

[2] TTS enabled? Check TTSSettings.Enabled + mode-specific toggle
    → No: inject text only (current behavior)
    → Yes: continue

[3] Clean text for speech:
    → Strip Markdown formatting (headers, bullets, code blocks)
    → Expand abbreviations and symbols (& → "and", % → "percent")
    → Truncate if > MaxSpeechWords (configurable, default: 500)

[4] Generate speech:
    → TTSRouter selects provider (local/cloud based on active profile)
    → Provider.GenerateSpeechAsync(cleanedText, selectedVoice)

[5] Parallel output:
    → AudioPlayer.PlayAsync(audioData) — speak response
    → TextInjector.InjectText(originalText) — inject full text simultaneously
    → Both happen concurrently — user reads AND hears

[6] Controls available during playback:
    → Pause/Resume (via control panel button or hotkey)
    → Stop (new dictation action interrupts TTS)
    → Volume adjustment
```

### 4.3 Voice Cloning

**Why it matters:** The V1 spec's strongest argument — accessibility (voice banking for ALS/cancer patients) and personalization ("hear answers in my own voice"). This remains the key differentiator vs. generic TTS.

**Workflow:**
1. User goes to Settings > TTS > Voice Library
2. Clicks "Record New Voice"
3. Voice Recording Wizard opens (3 steps):
   - **Step 1:** Record 5-10 seconds of speech (guided sentence displayed)
   - **Step 2:** Preview — hear cloned voice speak a test sentence
   - **Step 3:** Save with name (e.g., "My Voice")
4. Voice sample stored at `%APPDATA%/DiktaMe/voices/{voice_id}.wav`
5. Voice ID sent to TTS provider with each request

**Voice Library Management:**
- Multiple voices supported (e.g., "My Voice", "Professional", "Casual")
- Default voice selection
- Test / Re-record / Delete / Rename
- Import/Export voice samples (`.wav` files)

### 4.4 Text Cleaning for Speech

LLM responses often contain formatting that sounds terrible when read aloud. The `TextCleaner` preprocesses text:

```
Input:  "## Summary\n\nThe **budget** is $50k.\n- Item 1\n- Item 2\n```code block```"
Output: "Summary. The budget is 50 thousand dollars. Item 1. Item 2."
```

Rules:
- Strip Markdown: headers → sentence, bold/italic → plain, code blocks → skip or "code block omitted"
- Numbers: `$50k` → "50 thousand dollars", `3.14` → "three point one four"
- Symbols: `→` → "arrow", `•` → (pause), `&` → "and"
- Lists: bullets → sentences with pauses
- URLs: skip or "link omitted"
- Truncation: if > MaxSpeechWords, speak first N words + "... and more. Full text has been injected."

### 4.5 Settings

```csharp
public sealed record TTSSettings
{
    public bool Enabled { get; init; } = false;              // Off by default — opt-in feature
    public string DefaultVoiceId { get; init; } = "default"; // "default" = model's built-in voice
    public float Volume { get; init; } = 0.8f;               // 0.0–1.0
    public int MaxSpeechWords { get; init; } = 500;           // Truncate long responses
    public TTSMode Mode { get; init; } = TTSMode.Auto;       // Always / Auto / AskOnly / Never

    // Per-mode toggles
    public bool SpeakAskResponses { get; init; } = true;
    public bool SpeakChatResponses { get; init; } = true;
    public bool SpeakTranslations { get; init; } = false;    // Off by default (usually visual)

    // Local TTS server
    public string LocalServerUrl { get; init; } = "http://localhost:8880";
    public string LocalModelName { get; init; } = "";         // e.g., "qwen3-tts-0.6b"

    // Cloud TTS (optional)
    public bool AllowCloudTTS { get; init; } = false;
    public string CloudProvider { get; init; } = "inworld";   // "inworld" | "openai" | "elevenlabs"
    public string InworldModel { get; init; } = "tts-1.5-max"; // "tts-1.5-max" | "tts-1.5-mini"

    // Audio ducking during TTS playback
    public bool DuckOtherApps { get; init; } = true;
}

public enum TTSMode { Always, Auto, AskOnly, Never }
```

---

## 5. Cloud TTS Providers

While the strategic focus is local-first, cloud TTS is a valid — and often superior — option for users who:
- Don't have GPU hardware for local models
- Want highest possible quality with zero setup
- Already use cloud providers for STT/LLM (privacy trade-off already accepted)
- Need language coverage beyond what local models offer

All cloud providers implement `ITTSProvider` and reuse existing `SecureStorage` for API keys.

### 5.1 Inworld (Recommended Default)

**Why recommended:** #1 ranked quality, 1/20th the cost of ElevenLabs, free instant voice cloning, sub-250ms latency.

```
POST https://api.inworld.ai/tts/v1/synthesize
Headers: Authorization: Bearer {api_key}
{
  "text": "The quick brown fox...",
  "voice_id": "sarah",           // Built-in or cloned voice ID
  "model": "tts-1.5-max",        // "tts-1.5-mini" for lower latency
  "output_format": "wav"         // "mp3" | "wav" | "opus"
}
→ Returns audio data (base64 or streaming)
```

**Voice cloning:** Upload 5-15 seconds of audio → get a `voice_id` back instantly. Free for all users.

**Streaming:** WebSocket support for real-time synthesis — audio streams as it's generated.

**Languages:** 15 (EN, ES, FR, DE, IT, PT, ZH, JA, KO, NL, PL, RU, HI, AR, HE)

### 5.2 OpenAI

**Why include:** Many dIKta.me users already have OpenAI API keys. Simple REST API, good quality, 57+ languages.

```
POST https://api.openai.com/v1/audio/speech
{
  "model": "tts-1",
  "input": "The quick brown fox...",
  "voice": "alloy"
}
→ Returns audio/mpeg stream
```

**Limitations:** No voice cloning. Higher cost than Inworld ($15/1M chars). 6 built-in voices only.

### 5.3 ElevenLabs

**Why include:** Widest language coverage (57+), highest-quality voice cloning (Professional tier), established brand.

```
POST https://api.elevenlabs.io/v1/text-to-speech/{voice_id}
Headers: xi-api-key: {api_key}
{
  "text": "The quick brown fox...",
  "model_id": "eleven_multilingual_v2"
}
→ Returns audio/mpeg stream
```

**Limitations:** Expensive ($206/1M chars at overage rates). Best for users who already have an ElevenLabs subscription.

### 5.4 Cloud Provider Comparison Summary

| Feature | Inworld | OpenAI | ElevenLabs |
|---------|---------|--------|------------|
| **Cost / 1M chars** | $5-10 | $15-30 | $120-206 |
| **Quality ranking** | #1 | #4 | #3 |
| **Voice cloning** | Free instant | No | Paid |
| **Languages** | 15 | 57+ | 29+ |
| **Streaming** | WebSocket + HTTP | HTTP | WebSocket |
| **Latency** | <130-250ms | ~200ms | ~75ms |
| **Best for** | Default cloud, budget-conscious | BYOK users, max languages | Premium cloning, existing subscribers |

---

## 6. Research Sprint (Pre-Implementation)

The V1 spec's 5-day research sprint remains relevant but needs adaptation for C#:

### Day 1-2: Model Evaluation

Test the **top 3 candidates** in order (see §2.0 Recommended Evaluation Order): Orpheus 1B/3B, Kani-TTS-2 (400M), Qwen3-TTS (0.6B). Fish Speech V1.5 as backup:

| Metric | Target | Method |
|--------|--------|--------|
| Cold start latency | <3s | Time from server start to first audio |
| Warm start (TTFAC) | <500ms | Time to first audio chunk on subsequent requests |
| Voice cloning quality | 7+/10 (subjective) | Compare 5s sample clone to original |
| VRAM usage | <3GB (0.6B model) | `nvidia-smi` during inference |
| Concurrent with Ollama | No crashes | Run TTS while Ollama serves LLM |
| Audio quality | 8+/10 | Compare to OpenAI TTS baseline |
| Streaming support | Yes | Can audio start before full text is processed? |

### Day 3: Integration Testing

- Stand up each model's HTTP server
- Test C# `HttpClient` calls from a minimal console app
- Verify audio format (WAV PCM, sample rate) compatibility with NAudio
- Test voice cloning API (upload sample, generate with clone)
- Measure end-to-end latency: text in → audio playing

### Day 4: C# Integration POC

- Implement `LocalTTSProvider` against winning model's API
- Implement `AudioPlayer` with NAudio `WasapiOut`
- Test full flow: text → HTTP → audio bytes → NAudio playback
- Verify concurrent playback doesn't conflict with `AudioRecorder`

### Day 5: GO/NO-GO Decision

**Must-have criteria (ALL must pass):**
- [ ] Warm start TTFAC <500ms
- [ ] Voice cloning works with 5-10s sample (similarity 7+/10)
- [ ] Quality 8+/10 (better than Piper, competitive with cloud)
- [ ] VRAM <3GB for smallest model
- [ ] No crashes when Ollama is also running
- [ ] Clean HTTP API for C# integration

**If ANY must-have fails → NO-GO.** TTS deferred indefinitely.

---

## 7. Implementation Phases (If Research Succeeds)

### Phase 1: Core TTS Engine (3-4 days)
1. `ITTSProvider` interface + `TTSResult` model
2. `InworldTTSProvider` — REST + WebSocket client (recommended cloud default)
3. `OpenAITTSProvider` — REST client (BYOK users)
4. `ElevenLabsTTSProvider` — REST client (existing subscribers)
5. `LocalTTSProvider` — HTTP client to local TTS server
6. `TTSRouter` — local/cloud selection + provider routing based on profile
7. `AudioPlayer` — NAudio playback with volume, pause/resume/stop
8. `TextCleaner` — Markdown stripping, symbol expansion, truncation
9. Wire into Ask pipeline: `AskPipeline.RunAsync()` → TTS on completion
10. Basic `TTSSettings` in AppSettings (cloud provider selector, API key storage)

### Phase 2: Voice Cloning + UI (2-3 days)
1. `VoiceCloneManager` — record, store, manage voice samples
2. Voice Recording Wizard (3-step WinUI 3 dialog)
3. TTS Settings page (enable/disable, voice selector, volume, per-mode toggles)
4. Voice Library UI (list voices, test, delete, rename)
5. Wire into Quick Chat: speak chat responses

### Phase 3: Polish + Integration (2-3 days)
1. Audio ducking during TTS playback (reuse `AudioDucker`)
2. Control Panel TTS controls (play/pause/stop, volume, voice selector)
3. Translate mode TTS (speak in target language)
4. Interrupt: new dictation action stops TTS playback
5. Streaming playback (start playing before full audio generated — if model supports)
6. Keyboard shortcut: toggle TTS on/off (quick mute)

### Phase 4: Advanced (Future)
1. ONNX Runtime provider (native C#, no external server)
2. Ollama TTS provider (when Ollama adds TTS support)
3. Voice import/export
4. Per-mode voice selection (different voice for translations vs. answers)
5. Scribe session integration (SPEC_001): speak meeting summary
6. Accessibility: screen reader integration, voice banking documentation

---

## 8. Existing Code to Reuse

| Component | File | Reuse |
|-----------|------|-------|
| NAudio | Already in project | `WasapiOut` / `WaveOutEvent` for playback |
| `AudioDucker` | `Core/Audio/AudioDucker.cs` | Duck other apps during TTS playback |
| `AudioRecorder` | `Core/Audio/AudioRecorder.cs` | Record voice samples for cloning |
| `SecureStorage` | `Core/Security/SecureStorage.cs` | Store cloud TTS API key |
| `NotificationService` | `App/Services/NotificationService.cs` | Playback state toasts |
| `LLMRouter` pattern | `Core/LLM/LLMRouter.cs` | Inspiration for `TTSRouter` (local/cloud/multi-provider routing) |
| `ApiKeyValidator` | `Core/Security/ApiKeyValidator.cs` | Validate Inworld/OpenAI/ElevenLabs API keys |
| `PipelineResult` | `Core/Pipeline/PipelineResult.cs` | Add `TTSPlayed: bool` flag |
| `AskPipeline` | `Core/Pipeline/AskPipeline.cs` | Wire TTS as post-processing step |
| `ChatPipeline` | `Core/Pipeline/ChatPipeline.cs` | Wire TTS for chat responses |

---

## 9. Accessibility: Voice Banking

The V1 spec's strongest argument carries forward. Voice banking lets users with progressive speech conditions (ALS, throat cancer) preserve their voice:

1. Record extensive voice samples while speech is still clear
2. Store as high-quality WAV files
3. Use cloned voice for TTS output — continue "speaking" with their own voice

**Implementation:**
- Same voice cloning workflow, but with extended recording option (30-60s samples)
- Export voice data for backup/portability
- Documentation: partnering with accessibility organizations for beta testing

**This is a compelling story for press, enterprise sales, and differentiation.** No competitor in the dictation space offers local voice banking.

---

## 10. Error Handling

| Scenario | Response |
|----------|----------|
| Local TTS server not running | Toast: "TTS server not available. Start [model name] or disable TTS in Settings." |
| VRAM exhaustion | Log warning, skip TTS for this response, continue with text-only |
| Voice sample too short (<3s) | Wizard: "Recording too short. Please record at least 5 seconds." |
| Voice sample too noisy | Wizard: "Background noise detected. Try recording in a quieter environment." |
| Audio playback device unavailable | Toast: "No audio output device found." |
| Cloud TTS API error (any provider) | Fall back to next provider (Inworld → OpenAI → local), else skip TTS |
| Cloud TTS rate limit | Log warning, skip TTS for this response. Toast: "TTS rate limit reached." |
| Text too long for TTS | Truncate at MaxSpeechWords, append "Full text injected at cursor." |
| TTS fails mid-playback | Stop gracefully, log error. Text injection already completed (parallel). |

---

## 11. Success Criteria

- [ ] Ask mode speaks AI responses aloud with natural-sounding voice
- [ ] Quick Chat speaks responses with <1s delay after LLM completion
- [ ] Voice cloning produces recognizable voice from 5-10s sample
- [ ] Local TTS works fully offline (no internet required)
- [ ] Cloud TTS works with Inworld (recommended), OpenAI, or ElevenLabs API keys
- [ ] Cloud voice cloning works via Inworld (5-15s sample → instant voice ID)
- [ ] Volume control, pause/resume/stop all function correctly
- [ ] Audio ducking reduces other app volume during TTS playback
- [ ] Text injection happens simultaneously with speech (parallel, not sequential)
- [ ] TTS failure never blocks or breaks the underlying pipeline
- [ ] Voice Library supports multiple stored voices with easy management
- [ ] TTS can be toggled on/off per-mode (Ask, Chat, Translate independently)

---

## 12. Open Questions

1. **Which local TTS model wins?** — Research sprint (Day 1-2) will determine. Orpheus (1B or 3B) is the front-runner due to multiple size variants, GGUF quantization, emotion tags, and existing FastAPI server. Kani-TTS-2 (400M) is the dark horse for multilingual on modest hardware.
2. **Inworld vs. OpenAI as cloud default?** — Inworld wins on price/quality (#1 ranked, 1/3 OpenAI's cost), but OpenAI has broader language coverage and users may already have keys. Current recommendation: Inworld as default, OpenAI as BYOK alternative.
3. **Ollama TTS support?** — Monitor Ollama releases. If they add first-class TTS, it simplifies local integration (no separate server needed).
4. **ONNX export feasibility?** — If the winning local model can export to ONNX, we can run inference natively in C# via `Microsoft.ML.OnnxRuntime` with zero Python dependency. Research Day 4 should evaluate this.
5. **Streaming playback?** — Starting audio before full generation completes is ideal for long responses. Inworld supports WebSocket streaming; local models may vary.
6. **Sidecar lifecycle management?** — If we need a Python TTS server for local models, how does it start/stop with the app? Options: launch as child process, Windows service, or bundled executable. (Not relevant for cloud providers.)
7. **Voice cloning portability?** — Inworld voice clones are tied to their platform. Should we also store raw audio samples locally so users can re-clone on a different provider?

---

## 13. References

### Local Models
- V1 Spec: `E:\git\diktate\docs\internal\specs\deferred\SPEC_019_QWEN3_TTS_RESEARCH.md`
- [Qwen3-TTS (GitHub)](https://github.com/QwenLM/Qwen3-TTS) — 0.6B/1.7B, Apache 2.0, voice cloning, streaming
- [Orpheus TTS](https://www.bentoml.com/blog/exploring-the-world-of-open-source-text-to-speech-models) — Llama-based, 150M-3B params, zero-shot cloning
- [Fish Speech V1.5](https://www.siliconflow.com/articles/en/best-open-source-models-for-voice-cloning) — DualAR, ELO 1339, 300k+ hours training
- [Open-source TTS comparison (Modal)](https://modal.com/blog/open-source-tts)
- [Top open-source TTS models (BentoML)](https://www.bentoml.com/blog/exploring-the-world-of-open-source-text-to-speech-models)
- [ElevenLabs alternatives comparison](https://ocdevel.com/blog/20250720-tts)

### Cloud Providers
- [Inworld TTS API](https://inworld.ai/tts-api) — #1 ranked, $5-10/1M chars, free voice cloning
- [Inworld TTS Documentation](https://docs.inworld.ai/docs/tts/tts) — REST, HTTP streaming, WebSocket APIs
- [Inworld 2026 Benchmarks](https://inworld.ai/resources/best-voice-ai-tts-apis-for-real-time-voice-agents-2026-benchmarks) — Full provider comparison
- [Artificial Analysis TTS Rankings](https://artificialanalysis.ai/text-to-speech) — Independent quality/price rankings
- [OpenAI TTS API](https://platform.openai.com/docs/guides/text-to-speech) — $15/1M chars, 57+ languages
- [ElevenLabs API](https://elevenlabs.io/docs/api-reference/text-to-speech) — $206/1M chars, professional voice cloning
- [ElevenLabs Pricing Breakdown](https://flexprice.io/blog/elevenlabs-pricing-breakdown) — Tier-based pricing analysis

### Infrastructure
- NAudio: Already in project for audio recording — reuse for playback
