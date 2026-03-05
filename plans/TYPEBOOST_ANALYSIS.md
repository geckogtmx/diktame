# Typeboost vs. dIKta.me Refine Analysis

**Typeboost** (https://www.typeboost.ai) is an AI writing toolkit for MacOS that allows users to apply saved customized AI prompts directly to selected text via a shortcut.

## Key Typeboost Features

1. **Saved Prompt Actions (Hub/Menu)**: Users trigger a shortcut (`Ctrl+Space`) to open an interactive menu of pre-saved prompts (e.g., "Make it professional", "Write LinkedIn post", etc.) without having to type or re-explain the prompt every time.
2. **In-place / Zero Copy & Paste**: It applies AI to any text directly in the app the user is using, acting as a layer on top of the OS.
3. **Customizability**: High emphasis on user-created custom prompts to fit individual workflows, synced across devices.
4. **Voice Mode**: Similar to dIKta.me, Typeboost allows users to dictate instructions ("speech-to-text -> AI instruction").
5. **Multi-language Output**: Switching seamlessly between languages ("Speak in French, get perfect English").
6. **Community/Pre-built Templates**: Immediate value with pre-packaged prompt templates.

## Current dIKta.me "Refine" Capability

As documented in `docs/user/features/refine.md`:
1. **Shortcut Driven**: `Ctrl+Alt+R` on selected text.
2. **Autopilot**: Quick release applies a single default prompt (fix grammar/style).
3. **Instruct (Voice)**: Hold keys, dictate an instruction, release to execute.
4. **Dual Personality**: Support for routing Refine to a different/smarter LLM model than general dictation.

## Recommended Features for dIKta.me (Inspired by Typeboost)

To elevate dIKta.me's Refine feature from a single-purpose editor to a productivity powerhouse, we can consider the following:

> [!TIP]
> **1. Refine Command Menu (Quick Actions)**
> Instead of just *Autopilot* or *Voice Instruct*, pressing `Ctrl+Alt+R` (or a dedicated quick tap) could pop up a lightweight desktop floating menu/palette at the cursor. This menu would list the user's saved prompt presets. This aligns with Typeboost's "Keyboard-first design" where users can quickly select an action without speaking if they are in a quiet environment.

> [!TIP]
> **2. Custom Prompt Library & Manager**
> A UI section in dIKta.me's Control Panel where users can manage their "Refine Presets".
> *Example Presets:* `[Code: Add Docstrings]`, `[Tone: Aggressive]`, `[Format: Markdown List]`, `[Translate: German]`.

> [!TIP]
> **3. Preset Chaining or Context-Aware Presets**
> Automatically suggest specific presets based on the text selected (e.g., if code is selected, show coding templates).

### Implementation Complexity

- **Refine Prompt Manager**: Relatively low complexity to add to existing MVVM architecture.
- **Floating Command/Action Palette**: Medium complexity. Requires WinUI 3 window positioning logic to float near the text cursor or screen center, and input handling for fast keyboard navigation.

---

### Conclusion
Typeboost has a "Keyboard-First UI" approach to executing favorite prompts, whereas dIKta.me relies heavily on "Voice-First Instruct". Combining our powerful Voice Instruct with a **Swift Preset Menu** would be a massive feature upgrade for power users.
