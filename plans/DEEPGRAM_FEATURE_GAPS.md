# Deepgram STT — Feature Gap Analysis for dIKta.me V2

> **Date**: 2026-03-04
> **Source**: [Deepgram STT Docs](https://developers.deepgram.com/docs/stt/getting-started) + codebase audit
> **Supersedes**: `DEEPGRAM_TIPS.md` (which contained partial findings and is now folded into this document)

## Current Implementation Summary

| Aspect | Current Value |
|--------|---------------|
| **Provider** | `DeepgramProvider.cs` — 228 lines, batch REST API only |
| **Model** | `nova-2` (hardcoded constant) |
| **URL** | `https://api.deepgram.com/v1/listen?model=nova-2&smart_format=false` |
| **Parameters used** | `model`, `smart_format=false`, `language={lang}` or `detect_language=true` |
| **Audio format** | 16kHz, 16-bit mono WAV sent as binary body (`Content-Type: audio/wav`) |
| **Auth** | `Authorization: Token <API_KEY>` header |
| **Timeout** | 30 seconds |
| **Error handling** | 401 (invalid key), 429 (rate limit), generic non-2xx; JSON parse fallback |
| **Streaming** | None — record-then-transcribe only |
| **Tests** | 22 unit tests via `FakeHttpHandler` (no real API calls) |

**Key files**:
- `src/DiktaMe.Core/STT/DeepgramProvider.cs` — the provider
- `src/DiktaMe.Core/STT/ISTTProvider.cs` — interface + `TranscriptionResult` record
- `src/DiktaMe.Core/Config/STTProviderFactory.cs` — creates providers by name, pulls API key from `SecureStorage`
- `src/DiktaMe.Core/Config/AppSettings.cs` — root settings (no Deepgram-specific settings exist)
- `tests/DiktaMe.Core.Tests/STT/DeepgramProviderTests.cs` — 22 tests

---

## Tier 1 — Quick Wins (Batch API Parameter Changes)

These require only URL parameter changes in `DeepgramProvider.cs`. No new interfaces, no architectural changes.

### 1.1 Model Upgrade: Nova-2 → Nova-3

| | Details |
|---|---|
| **What** | Replace `model=nova-2` with `model=nova-3` |
| **Impact** | 54.2% reduction in word error rate (streaming), 47.4% (batch) vs competitors |
| **Languages** | 40+ languages (expanded from Nova-2's ~30) |
| **Variants** | `nova-3` (general), `nova-3-general`, `nova-3-medical` |
| **API** | Same `/v1/listen` endpoint, same response schema |
| **Risk** | Very low. Drop-in replacement. |
| **Effort** | Change one constant + update `ProviderName` string + update 2-3 test assertions |
| **Files** | `DeepgramProvider.cs` (line 17-18, 25), `DeepgramProviderTests.cs` (lines 65-68, 84) |

**Note**: Nova-2 had 8 specialized variants (meeting, phonecall, finance, conversationalai, voicemail, video, medical, drivethru). Nova-3 currently has only `general` and `medical`. If we ever used specialized Nova-2 variants, they'd need to stay on Nova-2. We don't — we use `nova-2` generic.

### 1.2 Dictation Mode (Critical for a dictation app)

| | Details |
|---|---|
| **What** | `dictation=true&punctuate=true` — converts spoken punctuation commands into actual typography |
| **Impact** | Saying "hello comma how are you question mark" → "Hello, how are you?" instead of "hello comma how are you question mark" |
| **Supported commands** | "period" (`.`), "comma" (`,`), "colon" (`:`), "question mark" (`?`), "exclamation mark/point" (`!`), "new line" (`\n`), "new paragraph" (`\n\n`) |
| **Prerequisites** | `punctuate=true` must also be set (dictation won't work without it) |
| **Languages** | English only (all regions) |
| **Availability** | Pre-recorded + Nova streaming. NOT available on Flux. |
| **Risk** | Low. Non-English users get no benefit but no harm either (param is ignored). |
| **Effort** | Append `&dictation=true&punctuate=true` to URL. Optionally make configurable via `DeepgramSettings`. |
| **dIKta.me mapping** | This is core functionality for a dictation app. Should be ON by default. Users dictating in non-English might want it off since it's English-only. |

**Decision point**: Should this be a global toggle, or per-DictationMode? For a first pass, global is simpler. Per-mode makes sense if someone dictates in English (wants dictation mode) and German (where it doesn't work) in the same session.

### 1.3 Punctuation

| | Details |
|---|---|
| **What** | `punctuate=true` — auto-adds punctuation marks and capitalization to transcripts |
| **Impact** | "hello and thank you for calling premier services" → "Hello, and thank you for calling Premier Services." |
| **Languages** | All available languages |
| **Availability** | Pre-recorded + Nova streaming. NOT Flux. |
| **Relationship** | Implicitly enabled by `smart_format=true`. Required prerequisite for `dictation=true`. |
| **Risk** | Very low. Pure improvement. |
| **Effort** | Append `&punctuate=true` to URL |
| **Note** | Currently we rely on the LLM cleanup pass to add punctuation. Enabling this at the STT level means: (a) raw STT output is already punctuated even without LLM, and (b) LLM cleanup can focus on semantic improvements rather than formatting. |

### 1.4 Smart Formatting

| | Details |
|---|---|
| **What** | `smart_format=true` — auto-formats dates, times, currency, phone numbers, emails, URLs |
| **Impact** | "I'll meet you on january fifteenth twenty twenty six at two thirty pm" → "I'll meet you on January 15th, 2026 at 2:30 PM" |
| **Includes** | Automatically enables `punctuate=true` and paragraph splitting |
| **Languages** | All languages get punctuation + paragraphs. English gets full formatting (dates, currency, phones, emails, URLs). Select other languages get numerals. |
| **Availability** | Pre-recorded + Nova streaming. **NOT Flux.** |
| **Streaming catch** | Buffers output up to 3 seconds to wait for complete entities (e.g., waiting to hear "dollars" after "fifty") |
| **Fix for streaming** | `no_delay=true` forces immediate return but sacrifices contextual formatting |
| **Currently** | **Explicitly disabled** (`smart_format=false` in our URL) |
| **Risk** | Medium. The 3s buffering on streaming is a real UX concern. For batch (our current mode), there's no buffering issue. For batch, this is a pure win. |
| **Effort** | Change `smart_format=false` to `smart_format=true` (or make configurable) |
| **dIKta.me mapping** | Excellent for professional/business dictation. May be unwanted for creative writing where "two hundred" should stay as words. Should be opt-in (off by default) for now, since dictation mode + punctuation cover the primary needs. |

**Decision point**: `smart_format=true` vs `dictation=true&punctuate=true` — these are complementary but `smart_format` is the heavier hammer. For batch mode, `smart_format` has no downside. For future streaming, it introduces latency.

### 1.5 Keywords / Keyword Boosting (Nova-2)

| | Details |
|---|---|
| **What** | `keywords=TERM:INTENSIFIER` — boosts recognition of uncommon words |
| **Format** | `&keywords=DiktaMe:2&keywords=NAudio:1.5` (URL-encoded) |
| **Intensifiers** | Exponential factor. Default 1. No upper limit but higher = more false positives. Decimals supported. |
| **Max** | 100 keywords per request |
| **Best for** | Proper nouns, product names, domain-specific terminology the model struggles with |
| **NOT for** | Common words already well-recognized; suppression (only works on Base models) |
| **Availability** | Nova-2, Nova-1, Enhanced, Base models. **NOT Nova-3** (use Keyterm Prompting instead). |
| **Risk** | Low. Over-boosted keywords may cause false positives. |
| **Effort** | Build keywords into URL from a settings list |
| **dIKta.me mapping** | Perfect for medical/legal dictation presets. Could be per-DictationMode. |

**Important**: If we upgrade to Nova-3 (which we should — 1.1), keywords param won't work. Nova-3 uses a different mechanism called "Keyterm Prompting" (different API, couldn't find full docs at time of research — may need follow-up).

### 1.6 Find and Replace (Server-Side)

| | Details |
|---|---|
| **What** | `replace=FIND:REPLACE` — server-side find-and-replace in transcription output |
| **Format** | `&replace=monika:Monica&replace=zen%20desk:Zendesk` |
| **Rules** | Find terms must be lowercase. Replacement can use any case. Omitting replacement = deletion. |
| **Max** | 200 terms per request |
| **Availability** | Pre-recorded + Nova streaming. All languages. |
| **Risk** | Low. Straightforward text substitution. |
| **Effort** | Build replacements into URL from a settings list |
| **dIKta.me mapping** | Overlaps with our `SnippetManager` (client-side post-processing). Server-side replacement happens before our LLM cleanup, so it's complementary. Good for consistent proper noun spelling without needing LLM. |

### 1.7 Diarization (Speaker Detection)

| | Details |
|---|---|
| **What** | `diarize=true` — assigns a speaker ID to each word in the transcript |
| **Response** | Each word gets `speaker` (int) + `speaker_confidence` (float) |
| **Max speakers** | Not documented — auto-detected |
| **Availability** | Pre-recorded + Nova streaming. NOT Flux. All languages. |
| **Risk** | Low. Additional response data; doesn't affect transcript text. |
| **Effort** | Add param to URL + extend `TranscriptionResult` to carry speaker info |
| **dIKta.me mapping** | Not relevant for single-user dictation. Relevant for a future "Meeting Notes" DictationMode preset where multiple speakers are present. |

### 1.8 Utterances (Semantic Segmentation)

| | Details |
|---|---|
| **What** | `utterances=true` — segments speech into meaningful semantic units |
| **Response** | Array of utterance objects with timing, confidence, transcript, and optional speaker ID |
| **Availability** | Pre-recorded only. NOT streaming (Nova or Flux). All languages. |
| **Risk** | Low. |
| **Effort** | Add param + parse utterances array from response |
| **dIKta.me mapping** | Useful for long-form dictation where we want paragraph breaks at natural speech boundaries. Currently we rely on LLM or `smart_format` for this. |

### Tier 1 Summary — Recommended Immediate Changes

| Priority | Feature | Parameter | Default |
|----------|---------|-----------|---------|
| **P0** | Nova-3 upgrade | `model=nova-3` | Always |
| **P0** | Dictation mode | `dictation=true&punctuate=true` | On (English) |
| **P1** | Punctuation | `punctuate=true` | On (all languages) |
| **P2** | Smart format | `smart_format=true` | Off (opt-in) |
| **P3** | Keywords | `keywords=TERM:INTENSIFIER` | Empty list |
| **P3** | Find & Replace | `replace=FIND:REPLACE` | Empty list |
| **P4** | Diarization | `diarize=true` | Off (future Meeting mode) |
| **P4** | Utterances | `utterances=true` | Off (future long-form mode) |

**Implementation pattern**: Replace the hardcoded `const string ListenUrl` with a `BuildListenUrl(language)` method that constructs the URL from a `DeepgramSettings` record added to `AppSettings`. Wire through `STTProviderFactory` (which needs `SettingsManager` injected — it's already in DI).

---

## Tier 2 — WebSocket Streaming (Real-Time Transcription)

### The Gap

Our current flow is: **Record full audio → Stop → Send WAV to REST API → Wait for response → Process → Inject**. This creates a perceptible delay between stopping recording and seeing text, because the entire audio must be uploaded and processed in one batch.

WebSocket streaming eliminates this: text appears as the user speaks.

### Architecture

#### Endpoint
`wss://api.deepgram.com/v1/listen?model=nova-3&encoding=linear16&sample_rate=16000&channels=1`

All the same query parameters from Tier 1 apply (dictation, punctuate, smart_format, keywords, replace, diarize).

#### Auth
Streaming uses query param auth (not header): append `&token=<API_KEY>` to the WebSocket URL.

#### Protocol
1. **Connect** — open WebSocket with parameters
2. **Send audio** — push raw PCM chunks (not WAV) as binary frames. Our `AudioRecorder` uses 16kHz/16-bit/mono which maps directly to `encoding=linear16&sample_rate=16000&channels=1`
3. **Receive transcripts** — JSON text frames with:
   - `is_final: false` — interim result (may change as more audio arrives)
   - `is_final: true` — finalized segment (won't change)
   - `speech_final: true` — natural speech endpoint detected (silence gap)
4. **KeepAlive** — send `{"type":"KeepAlive"}` every ~8 seconds to prevent timeout
5. **Close** — send `{"type":"CloseStream"}` to signal end of audio, wait for remaining finals

#### Streaming-Specific Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| `interim_results` | `true` | bool | Show partial results as user speaks |
| `endpointing` | `10` (ms) | int or `false` | VAD-based pause detection. Higher = more tolerance for natural pauses before finalizing. `false` = disable VAD, use Deepgram's chunking algorithm. |
| `vad_events` | `false` | bool | Emit explicit VAD start/stop events |
| `no_delay` | `false` | bool | Force immediate results (trade-off with smart_format buffering) |
| `encoding` | required | `linear16`, `mulaw`, `alaw`, `opus`, etc. | Audio encoding format |
| `sample_rate` | required | int | Sample rate in Hz |
| `channels` | `1` | int | Number of audio channels |

#### New Interface Needed

```
IStreamingSTTProvider (new)
├── ConnectAsync(language, ct)
├── SendAudioAsync(ReadOnlyMemory<byte>, ct)
├── CloseAsync(ct)
├── IsConnected { get; }
├── ProviderName { get; }
├── event PartialTranscriptReceived
├── event FinalTranscriptReceived
└── event ErrorOccurred
```

This is deliberately **separate from `ISTTProvider`** (batch). The streaming interface is event-driven, which is fundamentally different from the request/response model. The batch interface remains for fallback (offline Whisper, Gemini Audio) and when streaming fails.

#### AudioRecorder Changes

Currently `AudioRecorder.OnDataAvailable` writes directly to a `WaveFileWriter`. For streaming, it also needs to forward raw PCM buffers:

```
AudioRecorder
├── existing: writes to WaveFileWriter (for batch fallback / file retention)
└── new: raises AudioDataAvailable event with raw PCM bytes
```

The WAV file recording continues in parallel — needed for batch fallback and potential post-processing (Audio Intelligence, summarization).

#### SDK vs Raw WebSocket

**Recommendation: Use `System.Net.WebSockets.ClientWebSocket` directly** (not the Deepgram .NET SDK).

Rationale (from prior Deepgram C# demo analysis):
- Deepgram's own C# live transcription demo uses raw `ClientWebSocket`
- Gives granular control over NAudio buffer mapping and memory efficiency
- No SDK version dependency or abstraction overhead
- Reconnection logic is straightforward with raw WebSocket
- Our desktop app doesn't need the SDK's server-side abstractions

#### Files That Would Change

| File | Change |
|------|--------|
| `src/DiktaMe.Core/STT/IStreamingSTTProvider.cs` (new) | Interface + event args records |
| `src/DiktaMe.Core/STT/DeepgramStreamingProvider.cs` (new) | WebSocket implementation |
| `src/DiktaMe.Core/Audio/AudioRecorder.cs` | Add `AudioDataAvailable` event |
| `src/DiktaMe.Core/Pipeline/StreamingDictationPipeline.cs` (new) | Orchestrate streaming flow |
| `src/DiktaMe.Core/Pipeline/DictationPipeline.cs` | No change — batch pipeline coexists |

---

## Tier 3 — Flux Conversational Model

### What It Is

Flux is Deepgram's first **conversational** STT model — built specifically for voice agents, not dictation. It understands conversational dynamics (turn-taking, interruptions, pauses-that-aren't-endings) at the model level.

### Key Differences from Nova-3

| Aspect | Nova-3 | Flux |
|--------|--------|------|
| **Endpoint** | `/v1/listen` | `/v2/listen` (different API version!) |
| **Model ID** | `nova-3` | `flux-general-en` |
| **Languages** | 40+ | English only |
| **Smart format** | Yes | **No** |
| **Dictation mode** | Yes | **No** |
| **Diarization** | Yes | **No** |
| **Turn detection** | No (manual via endpointing) | Yes (AI-driven, 260ms latency) |
| **Best for** | Dictation, transcription, meetings | Voice agents, chat, conversations |

### Turn Detection Events

Flux emits three specialized events that Nova-3 does not:

#### `EndOfTurn`
- AI determines the user has finished their turn (not just paused)
- More sophisticated than VAD endpointing — understands sentence completeness
- Latency: ~260ms from actual end of speech

#### `EagerEndOfTurn`
- Emitted **while** the user is still speaking their last words
- Allows the LLM to start generating a response speculatively
- If the user continues speaking, `TurnResumed` cancels the speculative response
- **Cost implication**: Increases LLM API calls by 50-70% due to speculative generation

#### `TurnResumed`
- Signals the user continued speaking after an `EagerEndOfTurn` or `EndOfTurn` was fired
- The application should cancel any in-progress LLM response
- Handles "barge-in" and natural mid-thought pauses

### Configuration Parameters

| Parameter | Range | Default | Description |
|-----------|-------|---------|-------------|
| `eot_threshold` | 0.5–0.9 | 0.7 | Confidence level to trigger `EndOfTurn`. Higher = waits longer to be sure user is done. |
| `eager_eot_threshold` | 0.3–0.9 | None (disabled) | Confidence to trigger `EagerEndOfTurn`. Must be set to enable speculative responses. |
| `eot_timeout_ms` | 500–10000 | 5000 | Maximum silence before forcing `EndOfTurn` regardless of confidence. |

### Audio Specs

- Recommended: 80ms audio chunks
- Encodings: `linear16`, `linear32`, `mulaw`, `alaw`, `opus`, `ogg-opus`
- Sample rates: 8000, 16000, 24000, 44100, 48000 Hz
- Our 16kHz/16-bit/mono is fully compatible

### dIKta.me Mapping

| Flux Feature | dIKta.me Concept |
|---|---|
| `EndOfTurn` | Replace manual "stop recording" in Chat/Ask pipelines — the model detects when you're done |
| `EagerEndOfTurn` | Start LLM processing before user finishes — ultra-low perceived latency in Chat mode |
| `TurnResumed` | Cancel speculative LLM response if user continues speaking |
| Turn-based transcript | Maps to Chat pipeline's message-by-message model |

**Architectural note**: Flux maps specifically to the **Chat** and **Ask** pipelines (conversational). It does **not** map well to the **Dictate** pipeline (continuous text, no turns). The existing `DictationMode` vs `PipelineConfig` distinction already separates these concerns.

### Implementation Architecture

```
DeepgramFluxProvider : IStreamingSTTProvider
├── Connects to wss://api.deepgram.com/v2/listen  (v2!)
├── model=flux-general-en
├── Exposes: OnEndOfTurn, OnEagerEndOfTurn, OnTurnResumed
├── ChatPipeline subscribes to turn events
└── DictationPipeline does NOT use Flux
```

The `IStreamingSTTProvider` interface (from Tier 2) would need turn-taking events:
```
event EventHandler<TurnEventArgs>? EndOfTurnDetected;
event EventHandler<TurnEventArgs>? EagerEndOfTurnDetected;
event EventHandler? TurnResumed;
```

Nova-3 streaming provider simply never fires these events. Flux fires them. The pipeline decides how to react based on which events it receives.

---

## Tier 4 — Future / Complementary Features

These are Deepgram capabilities outside the core STT scope but relevant to the dIKta.me architecture.

### 4.1 Aura TTS (Text-to-Speech)

| | Details |
|---|---|
| **Endpoint** | `POST https://api.deepgram.com/v1/speak` |
| **Models** | `aura-helios-en` and others |
| **What** | Low-latency, high-quality text-to-speech |
| **dIKta.me mapping** | The "Mouth" component of the voice agent triad (Ears → Brain → Mouth). If we add voice responses to Chat mode, Aura could read responses aloud. |
| **Interface** | Would need `ITextToSpeechProvider` + `DeepgramTtsProvider` |

### 4.2 Voice Agent API (Managed Pipeline)

| | Details |
|---|---|
| **Endpoint** | `wss://agent.deepgram.com/agent` |
| **What** | Single WebSocket that manages the full STT → LLM → TTS loop. Deepgram handles calling the LLM (supports GPT-4o, Claude, etc.) and generating TTS audio. |
| **dIKta.me mapping** | Could replace our entire `DictationPipeline` + `ChatPipeline` + `LLMRouter` for a "zero-config cloud" mode. One WebSocket, microphone in, speaker out. |
| **Trade-off** | Loses local control — no SnippetManager, no custom prompts, no privacy controls. But minimal latency and zero local processing. |
| **Assessment** | Interesting as an "instant demo" mode, but contradicts dIKta.me's core value proposition of local control + customization. Low priority. |

### 4.3 Audio Intelligence (Post-Processing)

| | Details |
|---|---|
| **Endpoint** | Same `/v1/listen` REST API, additional params |
| **Features** | `summarize=v2` (TL;DR), `topics=v2` (topic detection), `intents=v2` (intent recognition), `sentiment=v2` (sentiment analysis) |
| **dIKta.me mapping** | After a long dictation session, auto-generate a summary. "You dictated 2,500 words about project planning. Key topics: budget, timeline, team allocation." |
| **Assessment** | Nice-to-have for the Note pipeline. Could auto-summarize notes at end of session. |

### 4.4 Multichannel

| | Details |
|---|---|
| **What** | `multichannel=true` — transcribes each audio channel independently (up to 20) |
| **dIKta.me mapping** | Not relevant for single-mic desktop dictation. Could be useful for future "transcribe a meeting recording" feature where left/right channels have different speakers. |

---

## Cross-Feature Compatibility Matrix

Not all features work together. This matrix captures key constraints discovered in the docs:

| Feature | Pre-recorded | Streaming (Nova) | Streaming (Flux) |
|---------|:---:|:---:|:---:|
| `punctuate` | Yes | Yes | **No** |
| `smart_format` | Yes | Yes (3s buffer) | **No** |
| `dictation` | Yes | Yes | **No** |
| `keywords` | Yes (Nova-2 only) | Yes (Nova-2 only) | **No** |
| `replace` | Yes | Yes | Unknown |
| `diarize` | Yes | Yes | **No** |
| `utterances` | Yes | **No** | **No** |
| `multichannel` | Yes | Yes | **No** |
| `interim_results` | N/A | Yes | Yes |
| `endpointing` | N/A | Yes | Via `eot_*` params |
| Turn events | N/A | **No** | Yes |

**Key takeaway**: Flux sacrifices nearly all formatting features for conversational intelligence. If we use Flux for Chat mode, the LLM must handle all formatting/punctuation.

---

## Cost & Pricing Considerations

| Model | Per-Minute Cost (pay-as-you-go) |
|-------|-------------------------------|
| Nova-2 | Documented but varies by plan |
| Nova-3 | Same tier as Nova-2 (no premium) |
| Flux | Same tier as Nova-3 |

**EagerEndOfTurn cost**: Using Flux's `EagerEndOfTurn` increases LLM API calls by 50-70% due to speculative response generation. This is a downstream cost on the LLM provider (OpenAI, Gemini, Ollama), not on Deepgram.

---

## Relationship to Existing `DEEPGRAM_TIPS.md`

All content from `DEEPGRAM_TIPS.md` has been incorporated and expanded in this document:
- Section 1 (Streaming Architecture) → Tier 2 (expanded with full protocol details)
- Section 2 (Abstraction/Response Mapping) → Tier 2 architecture section
- Section 3 (Flux vs Nova-3) → Tier 3 (expanded with exact parameters and compatibility matrix)
- Section 4 (Aura TTS, Voice Agent API) → Tier 4
- Section 5 (Dictation & Smart Format) → Tier 1 sections 1.2, 1.3, 1.4

`DEEPGRAM_TIPS.md` can be deleted once this document is reviewed.
