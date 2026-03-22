# Text-to-Speech (TTS)

dIKta.me can speak its responses and read selected text aloud. TTS is designed to be an ambient output channel that operates without blocking your workflow.

> **Privacy Note:** TTS is entirely opt-in. By default, it is **turned off** and requires no GPU processing. If you enable the **Kokoro (Local)** provider, speech is generated entirely on your device with no internet connection required.

## Enabling Text-to-Speech

1. Open **Settings** by clicking the gear icon or pressing `Ctrl+Alt+,`.
2. Navigate to the **Text-to-Speech** section.
3. Toggle the **Master Switch** to On.
4. Select a **Provider** (see below).

## Providers

dIKta.me offers multiple TTS providers ranging from local offline inference to ultra-high-quality cloud generation.

### Kokoro (Local)
* **Status:** Default (Local)
* **Cost:** Free
* **Privacy:** 100% Offline
* The Kokoro provider uses a highly optimized ONNX model downloaded directly to your machine. It requires an initial download (88 MB for the standard variant) and then runs either on your CPU or GPU without ever sending data to the cloud.

### Gemini TTS (Cloud)
* **Status:** Cloud Alternative
* **Cost:** Requires Gemini API Key
* Leverage Google's ultra-realistic voices for your Text-to-Speech responses. If you use Gemini for dictation, you can recycle your key to power highly natural spoken feedback.

### Deepgram Aura-2 (Cloud)
* **Status:** Cloud Alternative
* **Cost:** Requires Deepgram API Key (billed by usage)
* If you are already using Deepgram for speech-to-text, you can recycle your API key to generate high-quality voice responses.

### Inworld TTS-1.5 (Cloud)
* **Status:** Premium Cloud
* **Cost:** Requires Inworld API Key
* Delivers some of the highest quality and most natural-sounding voices available, operating at extremely low latency.

### OpenAI TTS (Cloud)
* **Status:** Cloud Alternative
* **Cost:** Requires OpenAI API Key
* Utilize your existing BYOK OpenAI credentials to leverage their library of high-quality conversational voices.

## When to Speak

You can fine-tune exactly when dIKta.me should speak aloud:

* **"Read Selection" Hotkey (`Ctrl+Alt+Q`):** Highlight text in any application and press this hotkey. dIKta.me will capture the selection, clean it for speech, and read it aloud.
* **Ask Mode Responses:** Hear the AI's answer read aloud simultaneously as it is typed into your active window.
* **Quick Chat Responses:** Maintain a hands-free conversation with the Quick Chat window by having the AI's replies spoken.
* **Translation Results:** Hear your spoken dictation translated and pronounced in the target language.
* **App Notifications:** Hear system status events ("LLM not loaded", "Recording started") spoken instead of relying solely on visual toasts.

## Audio Controls

TTS generation never delays text injection. The text arrives instantly, and the speech plays seamlessly in parallel.

* **Ducking:** By default, dIKta.me will temporarily lower the volume of other applications (like music or videos) while speaking.
* **Interrupting:** If you need to stop playback immediately, press the **Escape (`Esc`)** key or trigger any new dictation hotkey (e.g., `Ctrl+Alt+S`).
