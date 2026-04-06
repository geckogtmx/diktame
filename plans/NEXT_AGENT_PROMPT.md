# Start Prompt for Next AI Session

*Copy and paste the text below into your first message to the new AI:*

***

**Role & Objective:**
You are picking up a critical, high-priority optimization task for dIKta.me. We are replacing a two-stage Dictation pipeline (Deepgram STT -> Gemini LLM) with a single, ultra-low-latency WebSocket connection direct to Google's live API. 

The main goal is to achieve true "sub-second" transcription latency. 

**CRITICAL PREREQUISITE (Do Not Skip):**
Before you write a single line of code, you MUST use your tools to read exactly two files:
1. `plans/SINGLE_PROVIDER_WALLET.md` — The core specification for the unified pipeline.
2. `plans/SINGLE_PROVIDER_WALLET_G.md` — The post-mortem document logging the failed architectural attempts of the two previous AIs.

**Strict Mandates to Avoid Past Failures:**
The previous two AI agents failed this task because they made assumptions and architected the latency backward. You are forbidden from repeating these mistakes:
1. **Never doubt the model name:** The Live pipeline MUST use `gemini-3.1-flash-live-preview`. Do not let your internal training bias convince you this model does not exist. It is the correct, mandated model. Do not downgrade to 2.0.
2. **Never request 'AUDIO':** The previous AI hardcoded `responseModalities: ["AUDIO"]` in the payload which triggered synthetic voice generation. You MUST negotiate `responseModalities: ["TEXT"]` over the Bidi WebSocket so it streams transcribed text.
3. **Never block the UI:** The previous AI put the WebSocket connection loop *before* the Start Sound and recording activation (`_isRecording = true`), causing the app to freeze in silence for 6 seconds during cold starts. For sub-second latency, you must either keep a background socket persistently "warm," OR decouple the UI so the microphone and "beep" trigger instantly while the network connects asynchronously.

**Execution:**
You are starting with a 100% clean slate. The bad code from previous sessions has been wiped via `git restore`. 
Read the two documents linked above, formulate your implementation plan based strictly on the rules above, and propose the architectural change required to achieve real-time, sub-second dictation streaming.
