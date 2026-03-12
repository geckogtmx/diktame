# Migrating from V1 to V2

Welcome to dIKta.me V2! If you are a developer, power user, or contributor who spent time in the `diktate` V1 repository, this guide will help you understand the architectural shifts and what happened to your data.

## 1. Architectural Paradigm Shift

The biggest change is the underlying technology stack. 

*   **V1 Structure**: The application was inherently split. It used an **Electron (Node.js/React)** frontend for the UI and a background **Python FastAPI** server to handle hardware audio capture and AI model execution.
*   **V2 Structure**: The application is now a single, native, compiled **C# (.NET 8)** process utilizing **WinUI 3** for presentation.

This means you no longer have to manage mismatched Python dependencies, pip environments, or debug WebSocket IPC failures between the frontend and the backend. The core business logic (`DiktaMe.Core`) and UI (`DiktaMe.App`) run in the exact same memory space.

## 2. Terminology Changes

Many internal concepts have been rebranded for clarity in V2:

| V1 (Legacy) Concept | V2 Equivalent | Notes |
| :--- | :--- | :--- |
| `dIKtate` | **dIKta.me** | The branding has officially dropped the "ate" for ".me". |
| "Continuous Dictation" | **Batch Processing** | Audio is now captured into memory and processed in chunks, regardless of length. |
| "Live Dictation" | **Streaming Pipeline** | WebSockets are used natively via `IStreamingSTTProvider` (e.g. Deepgram) to return text while you speak. |
| `config.json` | **`settings.json`** | Application state and configurations. |
| `prompts.json` | **Dictation Modes** (stored in `settings.json`) | V1 system prompts and trigger modes have been unified into customizable CRU objects containing their own LLM/STT selections. |

## 3. Where is My Data?

In V1, configurations were heavily scattered in the installation folder or local AppData depending on the build. 

In V2, **absolutely everything** related to your user state is strictly isolated in a single directory:
`%APPDATA%\DiktaMe\`

This folder contains:
1.  **`settings.json`**: This replaces both the old `config.json` and `prompts.json`. It stores your hotkeys, API selections, and all 16 custom Dictation Modes.
2.  **`history.db`**: A robust SQLite database replacing messy filesystem logs. It tracks your transcription history, metrics, and wallet balances securely.
3.  **`secrets.dat`**: Your API keys are no longer stored in plaintext. They are encrypted at rest using Windows DPAPI algorithms.

## 4. Local Execution (Whisper & Ollama)

If you relied on `whisper.cpp` or local HuggingFace transformers in V1, the pipeline is significantly more stable now.

*   **Transcription**: We utilize `Whisper.net` configured to automatically use the Vulkan runtime. It achieves near 7x parity with pure CUDA deployment without requiring you to install gigabytes of NVIDIA C++ runtime dependencies manually.
*   **Formatting**: We now officially support `Ollama` natively. The V2 app handles keep-alives, model pulls, and version parsing directly via their local HTTP API. You no longer need to manage a separate Python environment just to run local language models.

*For detailed API documentation on adding new providers to V2, see the `api/stt-providers.md` and `api/llm-providers.md` developer guides.*
