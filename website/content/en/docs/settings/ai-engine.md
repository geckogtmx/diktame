# AI Engine Settings

The **AI Engine** tab is the brain of your dIKta.me experience. It allows you to select which specific Speech-to-Text (STT) transcribe engine and Large Language Model (LLM) processing engine you want active right now.

> [!TIP]
> **Cloud vs. Local**: You can rapidly switch your active environment in the Control Panel overlay without having to open the settings menu. Choose between a pure Cloud environment or a pure Local machine experience.

## Environment Routing

*   **Cloud Route (Default)**: Uses internet-capable third-party API providers that excel in both speed and accuracy. 
    *   **STT Provider**: Can use Deepgram or standard OpenAI Whisper APIs depending on what keys you have linked.
    *   **LLM Provider**: Uses state-of-the-art chat models from Google (Gemini) or Anthropic (Claude), natively processing complex stylistic prompt instructions quickly.
*   **Local Route (On-Device)**: Uses offline Artificial Intelligence modules. Completely free of subscription fees and extremely secure, as your microphone audio completely bypasses the internet.
    *   **STT Provider**: Uses **Whisper.net**. It will download an AI model directly onto your local machine the first time you run it.
    *   **LLM Provider**: Uses **Ollama**. It communicates seamlessly with Local Desktop AI applications processing large parameter datasets completely offline.

## Model Selection

Once an environment is active, you can narrow down exactly what models you want taking control of your pipelines. For example, if you set the Cloud environment to `Gemini`, the LLM model dropdown will let you choose between `gemini-1.5-pro` (slower, better) or `gemini-1.5-flash` (blazing fast, lightweight).

Changing these dropdowns fundamentally alters the speed, accuracy, and capabilities of every single pipeline execution.
