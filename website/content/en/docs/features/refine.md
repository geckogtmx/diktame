# Refine

While Dictation is used to generate *new* text, the **Refine** pipeline is used to edit and manipulate *existing* text in-place.

When you trigger Refine, dIKta.me copies the text you have highlighted, sends it to an AI along with your instructions, and instantly replaces your original text with the rewritten version.

## How to use Refine

There are two primary ways to use the Refine pipeline, determined by the "Voice Instruction" toggle in `Settings -> General`.

### 1. Autopilot Mode (Default)
In Autopilot mode, Refine acts as a 1-click text transformer using a predefined system prompt.

1. Highlight a block of text in any application.
2. Press the **Refine** hotkey (Default: `Ctrl + Alt + R`).
3. The selected text is instantly captured, processed by the LLM using the active Refine profile, and replaced.

*Example Use Case*: You set your Refine system prompt to `"Fix all spelling and grammar errors, but keep the original tone."` Now, whenever you highlight a rough email draft and press `Ctrl+Alt+R`, it is instantly proofread and corrected.

### 2. Voice Instruction Mode
In Voice Instruction mode, you dictate *how* you want the text changed dynamically.

1. Highlight a block of text.
2. Press and hold the **Refine** hotkey (`Ctrl + Alt + R`).
3. Speak an instruction (e.g., *"Make this sound more professional"*, *"Translate this to Spanish"*, *"Summarize this into 3 bullet points"*).
4. Release the hotkey.

dIKta.me will transcribe your instruction, combine it with the text you highlighted, and ask the LLM to apply your instruction to the text before replacing it.

> [!TIP]
> **Fallback to Ask**: If you are in Voice Instruction mode, but you forget to highlight any text before speaking, dIKta.me will automatically fall back to the **Ask** pipeline and try to answer your instruction directly.

## How dIKta.me captures selected text
dIKta.me does not employ intrusive screen-reading APIs to see what you have highlighted. Instead, it temporarily saves your clipboard, rapidly simulates pressing `Ctrl + C` (Copy) to grab the selection, and then restores your original clipboard contents.

Because it uses `Ctrl + C` and `Ctrl + V`, Refine works universally across almost every Windows application.

## Configuring the Refine Prompt

Just like Dictation, Refine operates using Profiles configured in the **Modes** tab of the settings window.

To change how Autopilot mode behaves, or to give the AI background context for your Voice Instructions:
1. Open the Control Panel and click the **Settings** gear.
2. Navigate to the **Modes** tab.
3. Select the **Refine** pipeline.
4. Modify the System Prompt for either your Cloud or Local profile.
