# 🌊 Synergy Deep Dive: dIKta.me x Liquid AI LFM2.5

> **Status:** IDEATION / PLANNING
> **Topic:** Integration of Liquid Foundation Models (LFM2.5) into dIKta.me architecture.
> **Date:** March 2026

## 1. The Technology Baseline
Liquid AI's **LFM2.5** family represents a radical leap in "Edge AI" — models specifically designed to run locally, on constrained hardware, with extremely low latency. 
At a scale of `1.2B` to `1.6B` parameters, these models fit entirely within system RAM/VRAM, execute flawlessly on CPUs or NPUs via `llama.cpp` (GGUF) and `ONNX`, and do not require heavy CUDA GPU environments.

Because dIKta.me is built natively in .NET 8 / C# with ONNX Runtime already established for TTS, LFM2.5 is a **native structural fit** for the `DiktaMe.Core` BYOK/Local offline provider architecture.

---

## 2. Core Architectural Integrations

### A. The "Unified Voice" Engine (`LFM2.5-Audio-1.5B`)
*   **Current State:** dIKta.me uses a chained pipeline: Mic -> Whisper (STT) -> Ollama (LLM) -> Kokoro (TTS) -> Speaker. This causes cumulative latency.
*   **The LFM Synergy:** `LFM2.5-Audio` processes **audio natively in and out**. 
*   **Implementation:** We create a new monolithic `IUnifiedAudioProvider` interface. dIKta.me streams the NAudio WASAPI byte buffer directly into the model. The model bypasses intermediate text generation completely and outputs high-fidelity audio waveforms directly (via its custom INT4 QAT detokenizer, which is 8x faster than previous generations).
*   **Result:** True real-time, zero-latency conversational voice AI, bringing "Her"-like capabilities to standard Windows laptops.

### B. The "Lightning Formatter" (`LFM2.5-1.2B-Instruct`)
*   **Current State:** Local text formatting relies on Ollama running 8B parameter models, taking several seconds to spin up and process simple commands like "fix grammar".
*   **The LFM Synergy:** The 1.2B instruct model can execute a prompt in milliseconds on a standard CPU. 
*   **Implementation:** Implement a `LiquidLocalProvider : ILLMProvider` leveraging `LLamaSharp` (GGUF) or strictly `ONNX Runtime`, completely bypassing external binaries like Ollama so the user doesn't need to install background services. 

### C. The "Eye of the Desktop" (`LFM2.5-VL-1.6B`)
*   **Current State:** dIKta.me is entirely blind. It operates solely on what you say or what's in your clipboard.
*   **The LFM Synergy:** A 1.6B parameter Vision-Language Model that runs entirely offline and handles multi-image comprehension.
*   **Implementation:** Create an `IVisionProvider`. Bind a new Global Hotkey (e.g., `Ctrl+Shift+L`) that captures an active window screenshot via Windows Graphics APIs. The image byte array + user's voice prompt is sent to the VLM securely offline.

---

## 3. Brainstorming: The "Crazy / Weird" Use Cases 🚀
*Pushing the boundaries of what an offline Windows Assistant can do.*

### Use Case 1: "The Over-the-Shoulder Pair Programmer"
*   **The Concept:** dIKta.me takes a passive 1 FPS screenshot buffer of the active IDE (Visual Studio/VS Code) holding only the last 5 frames in memory.
*   **The Flow:** You hit a hotkey and just vocalize: *"Ugh, why is this breaking?"*
*   **The Magic:** You didn't select any code or paste an error. The VLM looks at the most recent screenshot buffer, spots the Red Squiggly line under your C# code, understands the visual context natively, and uses native TTS to say: *"You forgot to await the async call on Line 42."*

### Use Case 2: Visual Vocal Macros (Desktop Automation)
*   **The Concept:** Controlling Windows purely via "Screen-Reading" AI.
*   **The Flow:** You say: *"Click the green 'Submit' button, then close this window."*
*   **The Magic:** dIKta.me screenshots the UI. `LFM2.5-VL` receives a hidden prompt: `List the bounding box coordinates [x,y] for the green 'Submit' button.` Once it returns the coordinates, `DiktaMe.Core` uses `InputSimulatorStandard` to physically move the mouse to those pixels and simulate a left-click. We effectively replace strict UI Automation trees with robust visual spatial reasoning.

### Use Case 3: "Babel Fish" AR Translation Overlay
*   **The Concept:** Native translation of visual media without text-boxes.
*   **The Flow:** You are watching a Japanese YouTube video without subtitles, or reading a French comic book scan.
*   **The Magic:** You hit the hotkey. dIKta.me screenshots the foreign text/video frame, passes it to the `LFM2.5-VL` or `LFM2.5-JP` model, and uses `LFM-Audio` to instantly whisper the English translation into your headphones dynamically.

### Use Case 4: Context-Aware Passive Note-Taking
*   **The Concept:** The ultimate "Read the Room" assistant.
*   **The Flow:** You leave the dictation mode on "Continuous Listen" during a Zoom meeting.
*   **The Magic:** LFM automatically captures speaker audio. But more intensely, if a co-worker says *"As you can see down here on this slide..."*, dIKta.me triggers a screenshot, feeds the slide image + the speaker's audio into the model, and generates a markdown note that explicitly describes the visual graph the speaker was talking about, saving both the text and a cropped thumbnail of the visual into your `dIKta.me Notes` folder.

### Use Case 5: The "Proofreader's Veto"
*   **The Concept:** Formatting text visually before it's sent.
*   **The Flow:** You are writing an email. You finish, but you want it to sound more corporate. You hit the dictate key: *"Make this sound professional."*
*   **The Magic:** Instead of selecting the text manually, dIKta.me reads the Outlook window visually, rewrites the email, and uses keyboard simulation (`Ctrl+A` -> `Backspace` -> `Type`) to overwrite the informal email with a professional one entirely based on visual context, without needing an Office Add-In.

---

## 4. Path to Integration
1.  **Phase 1 (Easy):** Integrate `LFM2.5-1.2B-Instruct` GGUF as a localized text-engine replacement to reduce reliance on Ollama for minimal hardware users.
2.  **Phase 2 (Medium):** Build the Windows screen-capture hook (e.g., using `Graphics.CopyFromScreen` or `Windows.Graphics.Capture` API) and implement `IVisionProvider` with the `1.6B-VL` model.
3.  **Phase 3 (Hard/R&D):** Restructure the entire `AudioCaptureService` PIPELINE (NAudio) to support Bi-Directional Audio-Language streaming for `LFM2.5-Audio-1.5B`.
