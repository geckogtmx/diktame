# Text-to-Speech (TTS) Pipeline

dIKta.me doesn't just listen—it can speak back. The Text-to-Speech (TTS) pipeline is an ambient output channel that reads selected text or AI responses aloud. 

Unlike traditional screen readers, dIKta.me's TTS is designed for a seamless, non-blocking workflow: text is injected instantly, and the speech plays seamlessly in the background.

## Key Capabilities

*   **"Read Selection" Hotkey (`Ctrl+Alt+Q`):** Highlight text in any application—browsers, code editors, or PDFs—and press the hotkey. The app will capture the selection, clean it up for spoken word format, and read it aloud.
*   **Ask Mode & Chat Responses:** Have the AI's answers to your questions dynamically read back to you while preserving your hands-free workflow.
*   **Translation Reading:** Listen to translations spoken accurately in the target language.
*   **App Notifications:** Instead of visual toasts, hear critical system events (e.g., "Recording started," "LLM offline") spoken quietly.

## How It Works

The TTS pipeline operates quietly and intelligently:

1.  **Ducking:** When speech starts, dIKta.me briefly lowers the volume of other applications (like Spotify or YouTube) so you can clearly hear the voice. Once the speech finishes, your music volume restores automatically.
2.  **Text Sanitization:** The pipeline automatically cleans up complex formatting from the text before synthesizing speech. It strips out Markdown elements, expands symbols (like `$` to "dollars"), and smooths out lists into conversational sentences to ensure the audio sounds natural.
3.  **Instant Interrupt:** If you start a new dictation hotkey or hit the **Escape (`Esc`)** key, the speech instantly stops to get out of your way.

## Providers

dIKta.me offers multiple engines for generating speech, which you can configure in the [AI Engine → Text to Speech](../settings/ai-engine.md) section:

*   **Kokoro (Local):** A completely offline, extremely fast local ONNX model that runs on your CPU.
*   **Deepgram Aura-2:** A high-quality cloud option utilizing your existing Deepgram API key.
*   **Inworld & OpenAI:** Premium cloud voices for the highest quality dialogue.

> **Note:** TTS is **turned off** by default. To enable it, navigate to **Settings > Text-to-Speech** and configure your preferred provider and playback preferences.
