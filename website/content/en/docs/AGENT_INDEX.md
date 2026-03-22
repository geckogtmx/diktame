# 🤖 Agent Index (Documentation Spine)

> **Context for AI Agents & LLMs:** This document serves as the master map of the dIKta.me documentation. Use this spine to quickly locate relevant information without having to traverse the entire directory tree sequentially. This documentation acts as the source of truth for the product.

## 📍 Quick Navigation

### 1. 📖 User Guides & Core Concepts
Located at `website/content/{locale}/docs/`
- `index.md` - Homepage documentation and high-level introduction.
- `getting-started.md` - Installation, setup, and first-run experience.
- `troubleshooting.md` - Common issues, error codes, and fixes.
- `settings.md` - Root page for all application settings.

### 2. ✨ Feature Deep-Dives
Located at `website/content/{locale}/docs/`
- `ask.md` - Voice-activated LLM queries.
- `translate.md` - Text translation modes.
- `refine.md` - Text polishing and formatting.
- `note.md` - Background note-taking mode.
- `quick-chat.md` - Overlay chat window for back-and-forth LLM interactions.
- `snippets.md` - Custom text expansion and templating.
- `tts.md` - Text-to-Speech playback of responses.
- `oops.md` - Undo/Redo formatting stack.

### 3. ⚙️ Settings Reference
Located at `website/content/{locale}/docs/settings/`
- Every file maps directly to a tab in the dIKta.me Settings UI.
- Critical files include `api-keys.md` (BYOK management), `audio.md` (devices and ducking), `general.md`, `hotkeys.md`, `modes.md`, `ollama.md`, and `privacy.md`.

### 4. 🛠️ Developer & Architecture Hub
Located at `website/content/{locale}/docs/dev/`
- `index.md` - Developer onboarding landing page.
- `setup.md` - Environment setup (.NET 8, WinUI 3, etc.).
- **Architecture (`dev/architecture/`):**
  - `ui-mvvm.md` - Strict MVVM bindings and `CommunityToolkit.Mvvm` usage.
  - `audio-pipeline.md` - NAudio WASAPI capture and latency optimization.
  - `threat-model.md` - STRIDE analysis and security boundaries.
- **API Providers (`dev/api/`):**
  - `stt-providers.md` - Speech-to-Text bridging (Whisper, Deepgram, Gemini).
  - `llm-providers.md` - Large Language Model bridging (Ollama, Anthropic, Gemini).
  - `tts-providers.md` - Text-to-Speech bridging (Kokoro, Deepgram, Inworld).

## 🧠 Agent Heuristics
1. **Rule of Thumb:** Always refer to `dev/architecture/ui-mvvm.md` before attempting to modify WinUI 3 code.
2. **Providers:** All AI models are implemented as hot-swappable plugins. Check `dev/api/` before touching core engine code.
3. **Localization:** This spine applies exactly the same to the `en` and `es` language trees under `website/content/`. Parity must be strictly maintained. Check both trees when generating new documentation.

*End of Spine.*
