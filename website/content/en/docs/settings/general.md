# General Settings

The **General** tab in the dIKta.me Control Panel settings houses the core application configurations, user interface language, and baseline integration behavior.

## Behavior

*   **Launch on Windows Startup**: When checked (Autostart), dIKta.me will automatically launch and minimize to the System Tray every time you turn on your computer.
*   **Play Feedback Sounds**: Enables subtle audio cues (like soft clicks or chimes) when you start or stop a recording, letting you know the microphone is active without having to look at the Control Panel HUD.

## Injection Settings

These options customize *how* the text lands in your target application.

*   **Add space after injection**: Automatically appends a single trailing whitespace after the dictated text is pasted. This allows for fluid, uninterrupted dictation across multiple sentences.
*   **Append key after injection**: Allows you to simulate a keyboard press immediately after the text lands.
    *   `None`: Dictation stops precisely at the end of the text.
    *   `Enter`: Automatically submits the text (perfect for chat applications like Slack, Teams, or Discord).
    *   `Tab`: Automatically tabs to the next field (perfect for filling out spreadsheets or web forms).

## Pipeline Overrides

*   **Refine: Use Voice Instruction Mode**: Changes how the `Ctrl+Alt+R` [Refine](../features/refine.md) hotkey operates. 
    *   *Checked*: You must press the hotkey, speak an instruction, and then release it (Voice Instruction mode).
    *   *Unchecked*: Pressing the hotkey instantly processes the highlighted text using the system prompt without recording audio (Autopilot mode).
*   **Ask Output Mode**: Determines how the [Ask](../features/ask.md) pipeline delivers 
answers.
    *   `Clipboard and Toast`: Answers are copied silently and shown in a Windows notification.
    *   `Clipboard Only`: Answers are only copied to the clipboard.
    *   `Toast Only`: Answers are only shown via notification.
    *   `Inject Only`: Answers are pasted directly over your cursor.
*   **Global Raw Mode**: Forces all dictation pipelines to skip Artificial Intelligence processing and simply output whatever the Speech-to-Text provider recognized. Useful if you want absolute verbatim transcription with zero formatting.
*   **Enable Streaming Dictation**: Activates real-time dictation using a WebSocket if your STT provider (Deepgram) supports it. Replaces "Batch Mode" recording. *Note: Streaming mode bypasses LLM system prompts and always outputs raw text.*

## Language Properties 

*   **UI Language**: Controls the display language for the dIKta.me interface components like the settings menus and Control Panel buttons (e.g., English, Spanish). *Changing this requires a restart.*
*   **Interaction Language**: Controls the default expected spoken language sent to the AI processing pipelines (e.g., `en`, `es`, `fr`). It helps the STT engine prioritize your mother tongue.
