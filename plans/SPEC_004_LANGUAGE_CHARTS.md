# SPEC_004_LANGUAGE_CHARTS: STT & LLM Language Compatibility

> **Status:** REFERENCE
> **Date:** 2026-03-11
> **Companion to:** SPEC_004_INTERNATIONALIZATION.md (UI localization)
> **Purpose:** Document language capabilities of each STT and LLM engine, cross-reference for the fully-local pipeline, and recommend high-value features.

---

## 1. Whisper STT — 99 Languages

All model sizes (tiny, base, small, medium, large-v3, turbo) support the same 99 languages.
Language auto-detection is supported. Larger models give better accuracy for non-English.

| Code | Language | Code | Language | Code | Language |
|------|----------|------|----------|------|----------|
| af | Afrikaans | hu | Hungarian | pt | Portuguese |
| am | Amharic | hy | Armenian | ro | Romanian |
| ar | Arabic | id | Indonesian | ru | Russian |
| as | Assamese | is | Icelandic | sa | Sanskrit |
| az | Azerbaijani | it | Italian | sd | Sindhi |
| ba | Bashkir | ja | Japanese | si | Sinhalese |
| be | Belarusian | jw | Javanese | sk | Slovak |
| bg | Bulgarian | ka | Georgian | sl | Slovenian |
| bn | Bengali | kk | Kazakh | sn | Shona |
| bo | Tibetan | km | Khmer | so | Somali |
| br | Breton | kn | Kannada | sq | Albanian |
| bs | Bosnian | ko | Korean | sr | Serbian |
| ca | Catalan | la | Latin | su | Sundanese |
| cs | Czech | lb | Luxembourgish | sv | Swedish |
| cy | Welsh | ln | Lingala | sw | Swahili |
| da | Danish | lo | Lao | ta | Tamil |
| de | German | lt | Lithuanian | te | Telugu |
| el | Greek | lv | Latvian | tg | Tajik |
| en | English | mg | Malagasy | th | Thai |
| es | Spanish | mi | Maori | tk | Turkmen |
| et | Estonian | mk | Macedonian | tl | Tagalog |
| eu | Basque | ml | Malayalam | tr | Turkish |
| fa | Persian | mn | Mongolian | tt | Tatar |
| fi | Finnish | mr | Marathi | uk | Ukrainian |
| fo | Faroese | ms | Malay | ur | Urdu |
| fr | French | mt | Maltese | uz | Uzbek |
| gl | Galician | my | Burmese | vi | Vietnamese |
| gu | Gujarati | ne | Nepali | wa | Walloon |
| ha | Hausa | nl | Dutch | xh | Xhosa |
| haw | Hawaiian | nn | Norwegian Nynorsk | yi | Yiddish |
| he | Hebrew | no | Norwegian | yo | Yoruba |
| hi | Hindi | oc | Occitan | zh | Chinese |
| hr | Croatian | pa | Punjabi | zu | Zulu |
| ht | Haitian Creole | pl | Polish | | |
| | | ps | Pashto | | |

### Key Notes
- **English-only variants** (tiny.en, base.en, small.en, medium.en) exist for lower latency on English workloads. dIKta.me uses multilingual models.
- **Auto-detection**: Whisper analyzes the first ~30s of audio to identify language. Already implemented in V2 via `builder.WithLanguageDetection()`.
- **Quality by model size**: Tiny/base show more variance across languages. Medium/large/turbo perform consistently across all 99.
- **ISO 639-1 codes** used throughout.

---

## 2. Deepgram STT — 50+ Languages (Nova-3)

Nova-3 (current default in dIKta.me) supports 50+ languages. Nova-2 supports 36.

### Primary Languages (All Nova Models)
| Code | Language | Code | Language |
|------|----------|------|----------|
| ar | Arabic | ko | Korean |
| bn | Bengali | lv | Latvian |
| bg | Bulgarian | lt | Lithuanian |
| zh | Chinese (Simplified/Traditional) | no | Norwegian |
| hr | Croatian | pl | Polish |
| cs | Czech | pt | Portuguese |
| da | Danish | ro | Romanian |
| nl | Dutch | ru | Russian |
| en | English | sr | Serbian |
| et | Estonian | sk | Slovak |
| fi | Finnish | sl | Slovenian |
| fr | French | es | Spanish |
| de | German | sv | Swedish |
| el | Greek | sw | Swahili |
| he | Hebrew | th | Thai |
| hi | Hindi | tr | Turkish |
| hu | Hungarian | uk | Ukrainian |
| id | Indonesian | vi | Vietnamese |
| it | Italian | | |
| ja | Japanese | | |

### Nova-3 Extended Languages
be, bn, bs, hr, kn, mk, mr, ms, sr, sl, ta, tl, te, fa, ur, el, ro, sk, ca, lt, lv, et

### Feature Support by Language

| Feature | Scope | Notes |
|---------|-------|-------|
| **Punctuation** | All languages | Automatic capitalization and punctuation |
| **Smart Formatting** | Full: English only. Partial: other langs | EN gets dates, currency, phone numbers, URLs. Others get punctuation + paragraphs only |
| **Dictation** | English only | Converts spoken "comma", "period" → symbols. Requires `punctuate` or `smart_format` |
| **Language Detection** | Batch only | `detect_language=true`. **NOT supported in streaming** |
| **Code-Switching** | 10 languages (Nova-3) | en, es, fr, de, hi, ru, pt, ja, it, nl — mix languages in a single stream |

### Streaming vs Batch Differences

| Aspect | Batch (REST) | Streaming (WebSocket) |
|--------|-------------|----------------------|
| Language detection | ✅ `detect_language=true` | ❌ Not supported |
| Latency | Seconds to minutes | Sub-300ms chunks |
| Smart format buffer | None | ~3s entity completion buffer |
| Language support | Same as model | Same as model |

---

## 3. Gemma 3 LLM — Language Mapping

### The Problem
Google claims Gemma 3 supports "140+ languages" but has **never published an enumerated list**. Community requests on HuggingFace remain unanswered. However, Gemma 3 uses the **Gemini 2.0 tokenizer** (262K vocabulary SentencePiece), and the Gemini API documentation does list supported languages by tier.

**The Gemini language list is the best available proxy for Gemma 3 language coverage.**

### Tier 1: Primary — High Confidence for Local Pipeline (38 Languages)

These languages appear in Gemini's primary language tier. Quality should be good even on `gemma3:1b`.

| # | Language | Code | | # | Language | Code |
|---|----------|------|-|---|----------|------|
| 1 | Arabic | ar | | 20 | Japanese | ja |
| 2 | Bengali | bn | | 21 | Korean | ko |
| 3 | Bulgarian | bg | | 22 | Latvian | lv |
| 4 | Chinese | zh | | 23 | Lithuanian | lt |
| 5 | Croatian | hr | | 24 | Norwegian | no |
| 6 | Czech | cs | | 25 | Polish | pl |
| 7 | Danish | da | | 26 | Portuguese | pt |
| 8 | Dutch | nl | | 27 | Romanian | ro |
| 9 | English | en | | 28 | Russian | ru |
| 10 | Estonian | et | | 29 | Serbian | sr |
| 11 | Finnish | fi | | 30 | Slovak | sk |
| 12 | French | fr | | 31 | Slovenian | sl |
| 13 | German | de | | 32 | Spanish | es |
| 14 | Greek | el | | 33 | Swahili | sw |
| 15 | Hebrew | he | | 34 | Swedish | sv |
| 16 | Hindi | hi | | 35 | Thai | th |
| 17 | Hungarian | hu | | 36 | Turkish | tr |
| 18 | Indonesian | id | | 37 | Ukrainian | uk |
| 19 | Italian | it | | 38 | Vietnamese | vi |

### Tier 2: Extended — Likely Supported, Quality Varies (53 Languages)

These languages appear in Gemini's extended tier (Gemini 2.0 Flash+). Gemma 3 likely has training data for them, but quality degrades significantly on `gemma3:1b`. Recommend `gemma3:4b` minimum for these.

| # | Language | Code | | # | Language | Code |
|---|----------|------|-|---|----------|------|
| 39 | Afrikaans | af | | 66 | Maltese | mt |
| 40 | Amharic | am | | 67 | Maori | mi |
| 41 | Assamese | as | | 68 | Marathi | mr |
| 42 | Azerbaijani | az | | 69 | Mongolian | mn |
| 43 | Basque | eu | | 70 | Burmese | my |
| 44 | Belarusian | be | | 71 | Nepali | ne |
| 45 | Bosnian | bs | | 72 | Pashto | ps |
| 46 | Catalan | ca | | 73 | Persian | fa |
| 47 | Galician | gl | | 74 | Punjabi | pa |
| 48 | Georgian | ka | | 75 | Shona | sn |
| 49 | Gujarati | gu | | 76 | Sindhi | sd |
| 50 | Haitian Creole | ht | | 77 | Sinhalese | si |
| 51 | Hausa | ha | | 78 | Somali | so |
| 52 | Hawaiian | haw | | 79 | Albanian | sq |
| 53 | Armenian | hy | | 80 | Sundanese | su |
| 54 | Icelandic | is | | 81 | Tamil | ta |
| 55 | Javanese | jw | | 82 | Telugu | te |
| 56 | Kannada | kn | | 83 | Tajik | tg |
| 57 | Kazakh | kk | | 84 | Tagalog | tl |
| 58 | Khmer | km | | 85 | Urdu | ur |
| 59 | Latin | la | | 86 | Uzbek | uz |
| 60 | Lao | lo | | 87 | Welsh | cy |
| 61 | Luxembourgish | lb | | 88 | Xhosa | xh |
| 62 | Macedonian | mk | | 89 | Yiddish | yi |
| 63 | Malagasy | mg | | 90 | Yoruba | yo |
| 64 | Malay | ms | | 91 | Zulu | zu |
| 65 | Malayalam | ml | | | | |

### Tier 3: Whisper-Only — No Gemini/Gemma Coverage (11 Languages)

These 11 languages can be transcribed by Whisper but are **not listed in any Gemini language tier**. The LLM post-processing step may produce garbled output.

| # | Language | Code | Family |
|---|----------|------|--------|
| 92 | Bashkir | ba | Turkic minority (Russia) |
| 93 | Breton | br | Celtic minority (France) |
| 94 | Tibetan | bo | Sino-Tibetan |
| 95 | Faroese | fo | Nordic minority |
| 96 | Lingala | ln | Bantu (Central Africa) |
| 97 | Norwegian Nynorsk | nn | Norwegian variant |
| 98 | Occitan | oc | Romance minority (France) |
| 99 | Sanskrit | sa | Ancient/liturgical |
| 100 | Tatar | tt | Turkic (Russia) |
| 101 | Turkmen | tk | Turkic (Central Asia) |
| 102 | Walloon | wa | Romance minority (Belgium) |

## 4. Kokoro TTS — 8 Languages (Local)

Kokoro-82M is the primary local TTS engine for dIKta.me, providing high-quality synthetic speech without cloud dependencies. It is optimized for speed and runs on CPU or GPU via ONNX Runtime.

| Code | Language | Voice Prefix | espeak-ng Fallback |
|------|----------|--------------|--------------------|
| a | English (American) | `af_`, `am_` | en-us |
| b | English (British) | `bf_`, `bm_` | en-gb |
| e | Spanish | `ef_`, `em_` | es |
| f | French | `ff_`, `fm_` | fr-fr |
| h | Hindi | `hf_`, `hm_` | hi |
| i | Italian | `if_`, `im_` | it |
| p | Portuguese (Brazilian) | `pf_`, `pm_` | pt-br |
| j | Japanese | `jf_`, `jm_` | N/A (misaki[ja]) |
| z | Chinese (Mandarin) | `zf_`, `zm_` | N/A (misaki[zh]) |

### Key Notes
- **Voice Blending**: Kokoro supports blending multiple voice vectors to create unique personas (e.g., `af_sky + af_sarah`).
- **Phonemization**: Uses `misaki` and `espeak-ng` for high-accuracy grapheme-to-phoneme conversion.
- **V2 Implementation**: Integrated via `KokoroTtsProvider` using KokoroSharp. Supports `gpu`, `int8`, `fp16`, and `fp32` model variants.

---

## 5. Cloud TTS Providers

dIKta.me supports three major cloud TTS engines, each with different language capabilities and latency profiles.

### Deepgram Aura
Aura is highly optimized for real-time conversational AI (ultra-low latency).
**Supported Languages (7):** English (US, UK, AU, IE), Spanish, French, German, Dutch, Italian, Japanese.

### OpenAI TTS (tts-1)
Provides highly natural, expressive voices at the cost of slightly higher latency.
**Supported Languages (50+):** Matches the Whisper STT language list. If Whisper can transcribe it, OpenAI TTS can synthesize it.

### Inworld TTS
Designed for gaming and NPC voice generation with emotional range.
**Supported Languages (16):** English, Spanish, French, German, Italian, Portuguese, Mandarin, Japanese, Korean, Dutch, Polish, Russian, Hindi, Arabic, Hebrew.

---

## 6. Compatibility Summary

| Tier | Languages | Local STT (Whisper) | Cloud STT (Deepgram) | Cloud LLM (Gemini/OpenAI) | Local TTS (Kokoro) | Cloud TTS (Aura/Inworld) | Cloud TTS (OpenAI) |
|------|-----------|--------------------|-----------------------|---------------------------|---------------------|---------------------------|--------------------|
| **Tier 1** | 38 | ✅ | ✅ (Nova-3) | ✅ High | ⚠️ (8/38) | ⚠️ Partial | ✅ Full |
| **Tier 2** | 53 | ✅ | ⚠️ Partial | ⚠️ Medium | ❌ No | ❌ No | ✅ Full |
| **Tier 3** | 11 | ✅ | ❌ No | ❌ No | ❌ No | ❌ No | ⚠️ Varies |


### Deepgram ↔ Whisper Overlap

Deepgram Nova-3 covers ~38 of the Tier 1 languages almost completely. For Tier 2, coverage is partial — roughly 25 of the 53 extended languages appear in Nova-3's expanded list. The remaining ~28 Tier 2 languages are Whisper-only on the STT side.

**Bottom line for cloud mode:** Deepgram handles the major languages well; for minority languages, users would need Whisper (or Gemini Audio) as their STT provider.

---

## 7. High-Value Feature Recommendations

### 7.1 Expand Language Dropdown (Low Effort, High Impact)

**Current state:** Only English and Spanish in the Settings dropdown.
**Proposal:** Expand to all 38 Tier 1 languages.

The infrastructure already supports arbitrary language codes — `LanguageCodes[]` and `Languages[]` arrays in `GeneralSettingsViewModel.cs` just need more entries. All STT providers accept ISO 639-1 codes. No pipeline changes needed.

**Implementation:**
- Add the 38 Tier 1 language codes to `LanguageCodes[]`
- Add display names to `Languages[]` (via localization keys)
- Group or alphabetize for usability
- Consider a searchable ComboBox (AutoSuggestBox) given 38+ options

**Files:** `GeneralSettingsViewModel.cs`, `GeneralSettingsPage.xaml`, `Resources.resw`

### 7.2 Auto-Detect Language Option (Low Effort, High Impact)

**Current state:** Language is always explicitly set. Auto-detection is implemented in both Whisper (`WithLanguageDetection()`) and Deepgram batch (`detect_language=true`) but not exposed in UI.

**Proposal:** Add `"Auto-detect"` as the first option in the language dropdown, mapped to code `"auto"`.

All three STT providers already handle `language="auto"`:
- **Whisper:** `builder.WithLanguageDetection()` — works well
- **Deepgram batch:** `detect_language=true` — works for 16+ languages
- **Deepgram streaming:** Not supported — would need to fall back to a default or show a warning
- **Gemini Audio:** Prompt-based ("Transcribe the following audio exactly") — works naturally

**Caveat for streaming:** Show a note in Settings: "Auto-detect is not available with Deepgram streaming. A language must be selected."

**Files:** `GeneralSettingsViewModel.cs`, `GeneralSettingsPage.xaml`

### 7.3 Skip LLM for Unsupported Languages (Medium Effort, Medium Impact)

**Problem:** For Tier 3 languages (and possibly some Tier 2), the Gemma 3 LLM step may garble the transcription rather than improve it.

**Proposal:** When the selected language is Tier 3 (one of the 11 Whisper-only languages), automatically skip the LLM step — equivalent to `RawMode = true`. Show an InfoBar explaining why.

**Implementation:**
- Define a `HashSet<string>` of Tier 3 codes: `ba, br, bo, fo, ln, nn, oc, sa, tt, tk, wa`
- In `PipelineFactory` or `LoadingViewModel`, check if language is in the set
- If yes, force `RawMode = true` and log the reason
- Show InfoBar: "LLM post-processing is not available for {language}. Raw transcription will be used."

**Files:** `PipelineFactory.cs` or `LoadingViewModel.cs`, constants file

### 7.4 Per-Mode Language Setting (Medium Effort, High Impact)

**Current state:** Language is global — all dictation modes use the same language from `GeneralSettings.Language`.

**Proposal:** Add an optional `Language` property to `DictationMode` / `DictationProfile`. When set, it overrides the global language for that mode. When null/empty, falls back to global.

**Use case:** A user who dictates medical notes in English but personal notes in Spanish. Currently requires manually switching the global setting each time.

**Implementation:**
- Add `Language` property to `DictationMode` record
- Add language dropdown to the mode editor in `DictationModesSettingsPage`
- In `LoadingViewModel`, prefer `profile.Language` over `settings.General.Language` when building `DictationOptions`
- Default: empty/null (use global)

**Files:** `DictationMode.cs`, `DictationModesSettingsPage.xaml/.cs`, `LoadingViewModel.cs`

### 7.5 Model Size Recommendation by Language (Low Effort, Medium Impact)

**Problem:** Users on `gemma3:1b` using Tier 2 languages may get poor LLM quality without understanding why.

**Proposal:** When a Tier 2 language is selected AND the LLM model is `gemma3:1b`, show an InfoBar in Settings:

> "For best results with {language}, we recommend using gemma3:4b or larger. The 1B model has limited support for this language."

This is informational only — no blocking, no forced changes.

**Files:** `GeneralSettingsViewModel.cs` or `AIEngineSettingsViewModel.cs`

---

## 8. Feature Priority Matrix

| # | Feature | Effort | Impact | Dependencies |
|---|---------|--------|--------|-------------|
| 7.1 | Expand language dropdown | Low (1-2h) | High | None |
| 7.2 | Auto-detect language | Low (1-2h) | High | None |
| 7.5 | Model size recommendation | Low (1h) | Medium | None |
| 7.3 | Skip LLM for unsupported langs | Medium (2-3h) | Medium | 7.1 |
| 7.4 | Per-mode language | Medium (3-4h) | High | 7.1 |

Recommended order: 7.1 → 7.2 → 7.5 → 7.3 → 7.4

---

## 9. Advanced Multilingual Workflows & Use Cases

With the complete linguistic pipeline mapped (STT → LLM → TTS), dIKta.me is uniquely positioned to handle complex multilingual scenarios. Beyond simple transcription, these interconnected workflows represent the highest-value opportunities for our users.

### 9.1 Multilingual Meeting Live Transcriptions (The "Babel" Overlay)
**The Need:** Users attending international Zoom/Teams meetings need real-time, translated context without relying on enterprise-tier software add-ons.
**The Solution:** 
- The user selects system audio (Stereo Mix or virtual cable) as the input device.
- We utilize Deepgram Nova-3's **Code-Switching** capability, which can automatically identify and transcribe up to 10 mixed languages in a single live audio stream (en, es, fr, de, hi, ru, pt, ja, it, nl).
- The raw, mixed-language transcript is streamed into a floating UI overlay (like the Chat window) where an LLM translates it into the user's native language on the fly. 

### 9.2 Cross-Lingual Professional Dictation (Summarize & Translate)
**The Need:** Medical, legal, or research professionals operating in non-English countries often need to produce English documentation (EHR systems, international papers) while thinking and speaking in their native tongue.
**The Solution:**
- Asynchronous batched dictation: The user dictates a full patient encounter in Spanish.
- Whisper STT (which has excellent non-English clinical vocabulary recognition) produces the base transcript.
- Gemma 3 (or Cloud LLM) intercepts the Spanish transcript, translates it to English, and structures it into a formal SOAP note or summary, outputting perfectly formatted English directly to the clipboard.

### 9.3 Live Voice-to-Voice Translation (Real-time Interpreter)
**The Need:** Direct, conversational communication with non-native speakers in person, serving as a pocket translator.
**The Solution:**
- **Input:** User speaks in Language A. Deepgram Streaming STT captures it with sub-300ms latency.
- **Processing:** Fast LLM (e.g., `gpt-4o-mini` or `gemma3:8b`) translates to Language B instantly.
- **Output:** Kokoro TTS (local) or Deepgram Aura (cloud) speaks the translation in Language B.
- *Constraint:* Latency is critical here. While local processing is private, the cloud pipeline currently provides the most natural sub-2-second conversational flow.

### 9.4 Language Learning & Pronunciation Trainer
**The Need:** Language learners need a safe, non-judgmental space to practice speaking and immediately hear the correct pronunciation.
**The Solution:**
- The user speaks in their target learning language (e.g., struggling through a French sentence).
- STT transcribes the attempt. The LLM acts as the "Tutor," identifying grammatical or structural errors, explaining the correction in English, and providing the perfect French sentence.
- Kokoro TTS or OpenAI TTS reads back the *corrected* French sentence using a native accent voice (e.g., Kokoro `ff_` prefix) for the user to mimic.

### 9.5 The "Read Selection" Accent Matcher (Accessibility)
**The Need:** Reading foreign language web pages or documents seamlessly without standard screen readers using jarring, robotic OS voices attempting foreign words.
**The Solution:** 
- A user highlights foreign text (e.g., an Italian news article) and hits the `Ctrl+Alt+Q` "Read Selection" hotkey.
- The pipeline detects the text language using the LLM (or simple heuristic) and automatically routes it to the corresponding Kokoro local voice (`if_` for Italian), bypassing English default voices to provide a native listening experience.

### 9.6 Multilingual Accessibility Board (AAC Communication)
**The Need:** Assisting users with speech impairments (e.g., ALS, aphasia) to communicate while traveling or in multi-cultural environments.
**The Solution:**
- The user types rapidly in their native language (or triggers predefined text snippets).
- The LLM translates the text to the local language of the environment and Kokoro/Inworld TTS synthesizes it aloud. This turns the laptop/tablet into a localized communication device.

---

## 10. Sources

- [OpenAI Whisper — GitHub](https://github.com/openai/whisper) — tokenizer.py LANGUAGES dict
- [Whisper Supported Languages](https://whisper-api.com/docs/languages/)
- [Deepgram Models & Languages](https://developers.deepgram.com/docs/models-languages-overview)
- [Deepgram Language Detection](https://developers.deepgram.com/docs/language-detection)
- [Deepgram Aura (TTS) — Languages](https://developers.deepgram.com/docs/tts-models)
- [Deepgram Nova-3 Language Expansion](https://deepgram.com/learn/deepgram-expands-nova-3-with-11-new-languages-across-europe-and-asia)
- [Deepgram Smart Formatting](https://developers.deepgram.com/docs/smart-format)
- [Deepgram Dictation](https://developers.deepgram.com/docs/dictation)
- [Gemma 3 Technical Report](https://arxiv.org/abs/2503.19786)
- [Gemma 3 Model Card](https://ai.google.dev/gemma/docs/core/model_card_3)
- [Gemma 3 Overview](https://ai.google.dev/gemma/docs/core)
- [Gemini Supported Languages (Firebase)](https://firebase.google.com/docs/ai-logic/models)
- [Gemma 3 HuggingFace Language Discussion](https://huggingface.co/google/gemma-3-27b-it/discussions/16)
- [Gemma 3 on Ollama](https://ollama.com/library/gemma3)
- [Kokoro-82M — Hugging Face](https://huggingface.co/hexgrad/Kokoro-82M)
- [Kokoro TTS CLI — GitHub](https://github.com/nazdridoy/kokoro-tts)
- [Kokoro VOICES — GitHub](https://github.com/hexgrad/kokoro/blob/main/VOICES.md)
