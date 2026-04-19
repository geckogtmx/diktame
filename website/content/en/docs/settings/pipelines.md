# Pipelines

![Pipelines — Ask (Cloud)](/images/docs/settings-pipelines-ask-cloud.png)

<div data-detail-section data-summary="All pipelines — Cloud + Local variants">

**Ask** (Cloud / Local):

![Ask — Cloud](/images/docs/settings-pipelines-ask-cloud.png)
![Ask — Local](/images/docs/settings-pipelines-ask-local.png)

**Refine (Auto)** (Cloud / Local):

![Refine Auto — Cloud](/images/docs/settings-pipelines-refine-auto-cloud.png)
![Refine Auto — Local](/images/docs/settings-pipelines-refine-auto-local.png)

**Refine (Verbal)** (Cloud / Local):

![Refine Verbal — Cloud](/images/docs/settings-pipelines-refine-verbal-cloud.png)
![Refine Verbal — Local](/images/docs/settings-pipelines-refine-verbal-local.png)

**Translate** (Cloud / Local):

![Translate — Cloud](/images/docs/settings-pipelines-translate-cloud.png)
![Translate — Local](/images/docs/settings-pipelines-translate-local.png)

**Notes**:

![Notes — Cloud](/images/docs/settings-pipelines-notes-cloud.png)
![Notes — detail (file path, LLM toggle, timestamp, live preview)](/images/docs/settings-pipelines-notes-detail.png)

**Vision**:

![Vision — pipeline toggles + token/size limits](/images/docs/settings-pipelines-vision.png)
![Vision — Default Query + System Prompt + Action Prompts](/images/docs/settings-pipelines-vision-prompts-detail.png)
![Vision — Screen Recording (quality, mic, webcam)](/images/docs/settings-pipelines-vision-recording-detail.png)

**Speak (TTS)**:

![Speak TTS](/images/docs/settings-pipelines-speak-tts.png)

</div>

The **Modes** tab configures the underlying artificial intelligence logic for dIKta.me's fixed utility pipelines. 

While the "Dictation Modes" tab lets you create infinite customizable profiles to switch between, the single **Modes** tab directly locks in the behavior for your core hotkeys: **Ask**, **Refine**, **Translate**, and **Note**.

## Pipeline Configuration

Every pipeline has a dedicated sub-section on this screen. Selecting a pipeline from the list will open its specific Cloud and Local system prompt editors (exactly like a standard Dictation preset).

1.  **Ask Pipeline**: The prompt sent to the LLM when you use `Ctrl+Alt+A`. 
    *   *Default*: "You are a helpful assistant. Provide concise, direct answers to the following question. Do not include conversational filler like 'Here is the answer'. Just answer the user."
2.  **Refine Pipeline**: The prompt sent to the LLM when you highlight text and press `Ctrl+Alt+R` (specifically in Autopilot mode).
    *   *Default*: "Fix any spelling or grammatical errors in the following text. Do not change the underlying tone or meaning. Maintain formatting."
3.  **Note Pipeline**: The prompt sent when you dictate a `Ctrl+Alt+N` diary entry.
    *   *Default*: "Format the following transcript into a coherent, properly punctuated note. Fix spelling mistakes but do not omit any information."
    *   *Note: If you have "LLM Processing" disabled in the General settings, these prompts are ignored entirely.*
4.  **Translate Pipeline**: The prompt sent when translating text natively via `Ctrl+Alt+T`.
    *   *Default*: "Translate the following text into English. Output only the translated text, nothing else."

## Context Injection

Some pipelines rely on injecting dynamic information into the model's instructions right before execution:

*   **Refine ({instruction})**: If you use Voice Instruction mode for Refine, dIKta.me will replace `{instruction}` in your written prompt with whatever you actually said out loud. Ensure your prompt includes `{instruction}` so the model knows what to do with the audio command!

## Cloud vs Local Tuning

As always, dIKta.me ensures your pipelines are robust whether you are online or entirely offline.

You must supply both a **Cloud System Prompt** (for models like `gpt-4o` or `gemini-1.5-pro`) and a **Local System Prompt** (for local Ollama models like `llama3` or `phi3`) for every utility mode to guarantee success when you flip the Control Panel Environment switch.
