# AI Engine Settings

The **AI Engine** tab is the brain of your dIKta.me experience. It controls which Speech-to-Text (STT) and Large Language Model (LLM) engines handle each pipeline. The tab has a master-detail layout — select a category on the left to configure it on the right.

> [!TIP]
> **Cloud vs. Local**: You can switch the active environment directly from the Control Panel overlay without opening settings.

## Environment Routing

*   **Cloud Route (Default)**: Uses third-party API providers over the internet.
    *   **STT Provider**: Deepgram (streaming) or OpenAI Whisper (batch), depending on which keys you have configured.
    *   **LLM Provider**: Choose from Gemini, Anthropic (Claude), OpenAI (GPT), OpenRouter, or Requesty — whichever keys you have saved.
*   **Local Route (On-Device)**: Uses offline AI modules. Your audio never leaves your machine.
    *   **STT Provider**: **Whisper.net** — downloads an ONNX model the first time it runs.
    *   **LLM Provider**: **Ollama** — communicates with local models running on your hardware.

## Model Selection

Once an environment is active, use the model dropdowns to pick the exact model for each pipeline. For example, with Gemini selected you can choose between `gemini-2.5-flash` (fast, default) or a more capable model variant.

Changing the model dropdown affects every pipeline execution: speed, accuracy, and cost all shift accordingly.

## Sub-sections

The AI Engine settings page contains the following sub-sections:

| Section | What it configures |
|---------|-------------------|
| **API Keys** | Save and manage API keys for all cloud providers |
| **Speech to Text** | Active STT provider and model for Cloud and Local routes |
| **Language Model** | Active LLM provider and model for Cloud and Local routes |
| **Text to Speech** | Active TTS provider and voice settings |
| **Chat** | Quick Chat system prompt and history settings |
| **System Monitor** | Ollama health, GPU status, and model cache |
