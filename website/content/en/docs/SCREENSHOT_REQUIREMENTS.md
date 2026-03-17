# User Documentation: Screenshot Production Requirements

As requested, here is the master list of production notes detailing exactly which UI screenshots we need for the user documentation, what they should capture, and where the final `.png` files should be placed in the repository.

Please try to capture these at a standard resolution (e.g., 100% display scaling on 1080p) in **Dark Mode** to align with the website's aesthetic. Please crop them cleanly to the window boundaries.

## Target Directory
Place all finalized image files strictly in the following directory:
`E:\git\diktame\docs\assets\images\`

---

### 1. The Welcome Wizard
**Target File:** `docs/user/getting-started.md`
*   **`wizard_step_1_language.png`**: The very first screen asking the user for their primary language (English / Spanish).
*   **`wizard_step_2_stt.png`**: The Speech-to-Text configuration step, showing the Cloud vs Local toggle and the Whisper download progress bar.
*   **`wizard_step_3_llm.png`**: The LLM configuration step showing the Ollama model dropdown (`gemma3:4b` default).
*   **`wizard_step_4_api.png`**: The API Keys entry screen showing the DPAPI secure fields.

### 2. The Control Panel (HUD)
**Target File:** `docs/user/features/dictation.md`
*   **`control_panel_idle.png`**: The main floating acrylic HUD in its default resting state, showing the Cloud/Local toggle and selected Dictation Mode.
*   **`control_panel_recording.png`**: The HUD actively capturing audio (try to catch the UI state when `Ctrl+Alt+D` is held).
*   **`control_panel_transcribing.png`**: The HUD in the processing state with the spinner/loading indicator.

### 3. The Settings Window
**Target File:** Various files in `docs/user/settings/`
*   **`settings_general.png`**: The General tab showing Start with Windows, Language overriding, and Theme.
*   **`settings_dictation_modes.png`**: The CRUD interface for Dictation Modes showing the list of presets and the prompt editing area.
*   **`settings_api_keys.png`**: The API Keys tab. (Please enter dummy keys for the screenshot or blur your real ones!)
*   **`settings_ollama.png`**: The Ollama Management Hub showing the model selection dropdown and Local VRAM statistics.
*   **`settings_audio.png`**: The Audio tab showing the Microphone dropdown and the Ducking intensity slider.

### 4. Utility Workflows
**Target File:** Various files in `docs/user/features/`
*   **`quick_chat_window.png`**: The floating Quick Chat conversational window with an active back-and-forth dialogue history.
*   **`refine_mode_selection.png`**: A screenshot showing the user selecting text in an external app (like Notepad) before pressing `Ctrl+Alt+R`, or simply the Refine settings tab.

---

## Integration

Once you have captured these screenshots and placed them in the `docs/assets/images/` folder, copy that folder into `website/public/images/` as well.

The generated Markdown files already use standard markdown image syntax: `![Alt Text](assets/images/filename.png)`. 

Let me know when you've grabbed them! If there's anything else you'd like to tweak about the documentation copy or the Next.js UI, we can do that now.
