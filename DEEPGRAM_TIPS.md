# Deepgram Integration Tips & Architecture Recommendations

This document summarizes findings from analyzing Deepgram's C# starter repositories and the new Flux model quickstart. It provides architectural recommendations for the `dIKta.me` V2 STT (Ears) implementation for the lead development agent.

## 1. Streaming Architecture (WebSockets)

*   **SDK vs. Raw WebSockets**: The official Deepgram .NET SDK provides an abstraction over streaming (`CreateListenWebSocketClient`), but Deepgram's own C# live transcription demo relies entirely on native `System.Net.WebSockets.ClientWebSocket` communicating directly with `wss://api.deepgram.com/v1/listen`.
*   **Recommendation**: For `DiktaMe.Core`, implementing the streaming logic using `ClientWebSocket` gives us granular control over NAudio buffer mapping, memory efficiency, and disconnection/reconnection logic natively within the WinUI 3 desktop client. There is no need for a proxy server.

## 2. Abstraction and Response Mapping

*   **Provider-Agnostic Model**: Deepgram returns a complex `SyncResponse`. 
*   **Recommendation**: `DiktaMe.Core` must define a canonical `ITranscriptionResult` interface. The `DeepgramStreamingProvider` should flatten the Deepgram-specific JSON into our simplified internal structure containing `{word, start, end, confidence, punctuated_word}`. This ensures the Engine (UI) remains completely agnostic to whether we are using Deepgram, Whisper.net, or another provider.

## 3. The Flux vs. Nova-3 Dilemma (Dictation vs. Assistant)

Deepgram has two primary models relevant to our use cases:

*   **Nova-3**: Best-in-class for straight transcription and pure voice dictation.
*   **Flux**: First conversational STT model built specifically for Voice Agents. It includes native AI-driven end-of-turn detection and handles conversational dynamics natively.

### The "Flux" Opportunity

Flux emits specialized events that negate the need for local Voice Activity Detection (VAD) heuristics:
*   `EndOfTurn`: AI determines when the user is actually finished, not just pausing.
*   `EagerEndOfTurn`: Emitted *while* the user is speaking their last words, allowing the "Brain" (LLM) to start generating a response pre-emptively, enabling ultra-low latency conversational flows.
*   `TurnResumed`: Handles "barge-in". If the user keeps talking or interrupts, we can cancel the draft LLM response immediately.

### Architecture Recommendation: The UX Switch

Since `dIKta.me` may serve dual purposes (pure dictation vs. AI assistant interactions), we should architect the UI to allow switching between these interaction paradigms.

**Recommendation**: Expose a switch/setting for the user to select their desired mode:
*   **Dictation Mode (Nova-3)**: Uses `nova-3`. Continuous, streaming text generation for long-form dictation without aggressive turn segmentation.
*   **Assistant Mode (Flux)**: Uses `flux-general`. Enables the `EagerEndOfTurn` and `TurnResumed` events. Plugs directly into the `DiktaMe.Core/Brain` LLM orchestration pipeline for seamless, real-time conversational UX.

**Core Interfaces**: Ensure the base `IStreamingTranscriptionService` (or similar interface) accounts for turn-taking events (e.g., `OnEndOfTurnDetected`, `OnEagerEndOfTurnDetected`, `OnTurnResumed`) so the pipeline orchestration can react appropriately when Flux (or a local equivalent) is active.

## 4. Expanding the Pipeline Sandbox (Additional Deepgram Features)

While `dIKta.me`'s core focus is STT dictation, Deepgram provides other APIs that perfectly complement the STT > LLM > TTS triad architecture. We should keep these in mind when designing the interfaces:

### The "Aura" TTS Model
Deepgram offers a highly optimized, low-latency Text-to-Speech (TTS) model called **Aura** (`aura-helios-en` etc.). 
*   **Recommendation**: If we build the "Mouth" (Speaker) component of the Voice Agent Triad, we should design an `ITextToSpeechService` interface. We can implement a `DeepgramTtsProvider` that hits `https://api.deepgram.com/v1/speak` to stream high-quality audio back to the user's speakers, replacing standard Windows local TTS.

### The All-In-One Voice Agent API
Deepgram recently released a consolidated **Voice Agent API** (`wss://agent.deepgram.com/agent`).
*   **What it is**: Instead of us orchestrating the (Deepgram Ears) -> (OpenAI/Gemini Brain) -> (Deepgram Mouth) pipeline locally inside the WinUI 3 app, we can just open *one* WebSocket connection. Deepgram handles STT, pings the LLM directly, generates the TTS audio, and streams the finished audio bytes back to us.
*   **Recommendation**: If the user just wants a standard voice assistant (e.g., using GPT-4o) without local processing, we could implement a `DeepgramManagedAgentProvider`. This would completely bypass the local `DiktaMe.Core/Brain` orchestration and just map microphone UI events to this single WebSocket. It is the ultimate low-latency cloud fallback.

## 5. Advanced Dictation & Formatting Gems

From a deeper sweep of the Deepgram API Reference and Guides, here are several crucial query parameters and features that are extremely relevant for a robust dictation app like dIKta.me V2:

### Dictation Formatting (Crucial for V2)
By default, saying the word "comma" will just transcribe the word "comma". Deepgram has a dedicated dictation mode that converts spoken punctuation into actual typography.
*   **Parameters to send**: `dictation=true&punctuate=true`
*   **Supported Spoken Commands**: "period" (`.`), "comma" (`,`), "colon" (`:`), "question mark" (`?`), "exclamation mark / point" (`!`), "new line", "new paragraph".
*   *Note: This must be explicitly enabled, otherwise the transcription will be literal.*

### Smart Formatting & `no_delay`
Deepgram can automatically format dates, times, numbers, and currencies.
*   **Parameters to send**: `smart_format=true` (this also implicitly enables `punctuate=true`).
*   **The Streaming Catch**: When using `smart_format` over a live WebSocket stream, Deepgram will sometimes buffer the output for up to 3 seconds to wait for the complete entity (e.g. waiting to hear if you say "dollars" after saying "fifty"). 
*   **The Fix**: You can pass `no_delay=true` to force it to return the transcript immediately, though this means you might sacrifice some contextual formatting. We should allow configuring this tradeoff in the app settings.

### Audio Intelligence (Post-Processing)
While Flux handles real-time conversational intelligence, Deepgram also offers **Audio Intelligence** features designed for pre-recorded or finalized audio segments.
*   **Features available**: Summarization, Topic Detection, Intent Recognition, and Sentiment Analysis.
*   **Recommendation**: We could implement a feature where after a user finishes a long dictation session, we send the finalized audio back to Deepgram's REST API with `summarize=v2` to automatically generate a TL;DR of their dictated notes.
