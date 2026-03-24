# Documentation Strategy & Plan (dIKta.me V2)

**Goal**: Create comprehensive, dual-audience documentation for **dIKta.me V2** (C# / WinUI 3), leveraging the foundation of V1. The documentation must accurately reflect the existing application architecture, user interfaces, and development patterns.

---

## 1. Documentation Scope

### A. User Documentation (`docs/user/`)
Focuses on how to use the dIKta.me desktop application, configuring settings, and troubleshooting.

*   **`getting-started.md`**: The critical "first contact" guide for non-technical users.
    *   Installation and navigating the initial onboarding Wizard.
    *   Educational explanations of core concepts (e.g., "What is STT (Speech-to-Text)?" and "What is an LLM (Large Language Model)?").
    *   Setting up the first STT/LLM provider without overwhelming the user.
    *   Interacting with the System Tray icon and background operation.
*   **`features/dictation.md`**: Core dictation workflow, the main Control Panel interface, using global hotkeys, streaming vs. batch dictation.
*   **`features/refine.md`**: The post-processing pipeline (Refine), configuring rewriting styles, text expansion, and system prompts.
*   **`features/ask.md`**: Using the "Ask" pipeline for voice-driven prompt execution and in-place substitution.
*   **`features/translate.md`**: Using the "Translate" pipeline for real-time translation of dictated audio.
*   **`features/note.md`**: Using the "Note" pipeline for pure STT transcribing (bypassing LLM processing).
*   **`features/quick-chat.md`**: Using the Quick Chat overlay window ("Chat" pipeline) for ad-hoc assistant interactions and context injection.
*   **`features/oops.md`**: Using the "Oops" (`Ctrl+Alt+V`) hotkey to undo or fix mistakes on the fly.
*   **`settings/`**: Comprehensive guide to all 12 Control Panel settings tabs:
    *   `general.md`: Language, autostart, and basic app behavior.
    *   `account.md`: Wallet balances, trial status, and dIKta.me authentication.
    *   `api-keys.md`: Configuring BYOK (Bring Your Own Key) for Anthropic, Deepgram, Gemini, and OpenAI.
    *   `ai-engine.md`: Global model selections and prompt assignments.
    *   `audio.md`: Input devices, recording times, and audio ducking features.
    *   `dictation-modes.md`: Managing CRUD operations for custom dictation presets.
    *   `modes.md`: Configuring utility pipelines (Ask, Refine, Translate).
    *   `hotkeys.md`: Mapping global keybindings.
    *   `Macros.md`: Managing local text expansion and auto-corrections.
    *   `ollama.md`: Local inference settings (Models, Keep-alive, Context Windows).
    *   `privacy.md`: History retention, PII scrubbing (Levels 0-3), and logging.
    *   `control-panel.md`: HUD visibility toggles.
*   **`troubleshooting.md`**: Common Windows-specific issues (Scaling, Permissions, Audio Driver locking, DPAPI issues).

### B. Developer Documentation (`docs/dev/`)
Focuses on how to build, extend, and maintain the dIKta.me V2 codebase.

*   **`setup.md`**: Dev environment setup (VS 2022, `.editorconfig`, running the WinUI 3 project).
*   **`architecture/`**: Expansions of the high-level `ARCHITECTURE.md`.
    *   **`audio-pipeline.md`**: The NAudio implementation, input capture, Voice Activity Detection (VAD).
    *   **`ui-mvvm.md`**: WinUI 3 structure, CommunityToolkit.Mvvm usage, Dependency Injection.
*   **`api/`**: Core internal extension points.
    *   **`stt-providers.md`**: Implementing `ISTTProvider` and `IStreamingSTTProvider` (covering existing implementations like Deepgram, GeminiAudio, Whisper).
    *   **`llm-providers.md`**: Implementing `ILLMProvider` (Anthropic, Gemini, Ollama, OpenAICompat).
    *   **`pipeline.md`**: The text transformation pipeline orchestration.
*   **`testing.md`**: Running tests (xUnit), writing mocks, UI testing notes.



---

## 2. Task Logs & Phased Execution

| Phase | Task ID | Description | Status |
| :--- | :--- | :--- | :--- |
| **Phase 1: Foundation (Current)** | DOC-1.1 | Create `docs/` directory structure stubs. | ⏳ Pending |
| | DOC-1.2 | Update `DOCUMENTATION_PLAN.md` with scope and criteria. | ✅ Done |
| | DOC-1.3 | Create `docs/user/index.md` & `docs/dev/index.md`. | ⏳ Pending |
| **Phase 2: Core User Guides** | DOC-2.1 | Draft `getting-started.md` (Detailed beginner guide, Wizard, STT/LLM explanations). | ⏳ Pending |
| | DOC-2.2 | Draft `features/dictation.md` (Control Panel, Hotkeys). | ✅ Done |
| | DOC-2.3 | Draft `features/refine.md` (Pipeline, Styles). | ✅ Done |
| | DOC-2.4 | Draft `features/quick-chat.md` (QuickChatWindow, Chat pipeline). | ✅ Done |
| | DOC-2.5 | Draft `troubleshooting.md`. | ⏳ Pending |
| **Phase 3: Utility Pipelines**| DOC-3.1 | Draft `features/ask.md` (Voice-driven prompt execution). | ✅ Done |
| | DOC-3.2 | Draft `features/translate.md` (Real-time translation). | ✅ Done |
| | DOC-3.3 | Draft `features/note.md` (Pure STT transcription). | ✅ Done |
| | DOC-3.4 | Draft `features/oops.md` (Ctrl+Alt+V Undo/Fix). | ✅ Done |
| **Phase 4: Settings Exhaustive Guide** | DOC-4.1 | Draft settings fundamentals (`general.md`, `audio.md`, `control-panel.md`, `hotkeys.md`). | ✅ Done |
| | DOC-4.2 | Draft LLM/STT configurations (`ai-engine.md`, `api-keys.md`, `ollama.md`). | ✅ Done |
| | DOC-4.3 | Draft Modes config (`dictation-modes.md` presets, `modes.md` utility profiles). | ✅ Done |
| | DOC-4.4 | Draft Account & Privacy (`account.md`, `privacy.md`, `Macros.md`). | ✅ Done |
| **Phase 5: Developer Fundamentals**| DOC-5.1 | Draft `setup.md` (VS2022 setup, Windows App SDK). | ⏳ Pending |
| | DOC-5.2 | Draft `architecture/audio-pipeline.md` & `architecture/ui-mvvm.md`.| ⏳ Pending |
| **Phase 6: API & Extensibility** | DOC-6.1 | Draft `api/stt-providers.md` (`ISTTProvider`, `STTRouter`). | ⏳ Pending |
| | DOC-6.2 | Draft `api/llm-providers.md` (`ILLMProvider`, `LLMRouter`). | ⏳ Pending |
| **Phase 7: Finalization** | DOC-7.1 | Produce and embed Mermaid.js diagrams. | ⏳ Pending |
| | DOC-7.2 | Capture UI screenshots for User Docs. | ⏳ Pending |


---

## 3. Acceptance Criteria

Before any documentation phase is marked complete, the following criteria must be met:

1.  **Code Accuracy**: Developer documentation must perfectly align with the `main` branch. e.g., STT docs must list exactly `Deepgram`, `GeminiAudio`, and `Whisper`.
2.  **UI Reality**: User documentation screenshots and descriptions must match the WinUI 3 components (e.g., `WizardWindow`, `ControlPanelPage`, `QuickChatWindow`, `SettingsWindow`).
3.  **Audience Separation**: Developer concepts (DI, MVVM, interfaces) must not leak into the User Documentation. User documentation must remain accessible to non-technical users.
4.  **Formatting**: All files must be pure Markdown (`.md`), use active voice, and avoid platform-specific Markdown extensions unless natively supported by GitHub.
5.  **Navigation**: All pages must be accessible from their respective section indices (`user/index.md` or `dev/index.md`).

## 4. Tools & Standards
*   **Format**: Markdown (`.md`).
*   **Diagrams**: Mermaid.js for flows and architecture.
*   **Images**: Stored in `docs/assets/` (To be captured later).
