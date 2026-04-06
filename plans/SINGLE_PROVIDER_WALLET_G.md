# SINGLE_PROVIDER_WALLET_G (Gemini Live API Migration)

## 1. Research & Findings
The core objective is to replace a dual-call wallet pipeline (Deepgram STT + Gemini LLM) with a **single, unified WebSocket connection** to the **Gemini 3.1 Flash Live Preview** model, specifically achieving sub-second transcription latency.

**API & Modality Constraints (Why the previous attempt failed):**
- The prior AI hardcoded `responseModalities: ["AUDIO"]` to bypass a 1011 internal Edge Function error. 
- However, since Gemini 3.1 Live is an Audio-to-Audio (A2A) model, passing `["AUDIO"]` natively instructs Google to stream back synthetic speech bytes. Since the C# client explicitly listens for `part.text` to inject into the user's document, it receives nothing and drops the output.
- **Solution:** We must explicitly negotiate `responseModalities: ["TEXT"]` over the BidiGenerateContent WebSocket. This forces the model to respond with textual transcripts of the audio instead of voice.

## 2. The Architectural UX Hang
**The Problem:**
Currently, pressing `Ctrl+Alt+D` causes the C# application completely "freeze" without playing a Start Sound or recording audio. 
- The C# pipeline was fundamentally altered to **block** the UI until `ConnectWalletStreamingAsync()` fully establishes a TLS/WebSocket handshake with the Supabase Edge Function and Google.
- Because Edge Functions can take ~3 seconds to cold start and WebSocket handshakes take an additional ~1.5 seconds, the user is stranded in a 5-6 second period of silence, causing the app to appear dead or unresponsive.

**The Reality of Sub-Second Latency:**
It is physically impossible to achieve sub-second latency (like Aqua Voice or Whisper Flow) if the network WebSocket connection is initialized *on-demand* (reactively when the hotkey is pressed). 

## 3. The Plan
To salvage this pipeline and achieve the latency targets, the architecture must be modernized to "warm" the connection:
1. **Decouple the UI:** Revert `LoadingViewModel.cs` to trigger the "Start Sound" and physical microphone recording **instantly**, identical to the BYOK (Bring Your Own Key) Deepgram implementation. This fixes the perceived "hang."
2. **Warm the Socket:** Shift `ConnectWalletStreamingAsync()` to initialize in the background (either at app launch, component load, or via a background worker). Keep the socket alive with standard ping/pong `keepalive` frames.
3. **Insta-Stream:** When the user presses `Ctrl+Alt+D`, instantly stream the PCM audio bytes across the pre-warmed, authenticated WebSocket for sub-second, real-time transcription.

## 4. Task Log (Session April 6, 2026)
- [x] Initial review of `wallet-stream/index.ts`.
- [x] Reverted erroneous model downgrade from `gemini-2.0-flash-exp` back to `gemini-3.1-flash-live-preview`.
- [x] Researched Google BidiGenerateContent API formatting for the 3.1 preview.
- [x] Discovered the `responseModalities: ["AUDIO"]` vs `["TEXT"]` conflict.
- [x] Documented the UI latency flaw causing the 6+ second hang.
- [ ] Implement UI decoupling *(Deferred to next session due to context degradation)*.
- [ ] Implement socket warming *(Deferred to next session)*.

## 5. Success Criteria for Next Session
1. Setting `pipelineMode` to `"streaming"` securely connects to Google using `gemini-3.1-flash-live-preview`.
2. Hitting `Ctrl+Alt+D` plays the dictation start sound **immediately**, without network delay.
3. Spoken words are returned as text (not audio) over the WebSocket natively.
4. Dictated text appears on the user's screen in < 1 second.
