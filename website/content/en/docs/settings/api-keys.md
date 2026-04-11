# API Keys Settings

The **API Keys** section (inside AI Engine settings) lets you save encrypted credentials for cloud providers — enabling Bring Your Own Key (BYOK) mode.

> [!NOTE]
> API Key mode requires a **Power License**. Keys are stored with Windows DPAPI and are never sent to dIKta.me servers — requests go directly from your machine to each provider over HTTPS.

## Speech-to-Text Providers

*   **Deepgram** — Cloud STT used for both batch and real-time streaming. Get a key at deepgram.com.
*   **OpenAI** — Also used for Whisper batch STT. Get a key at platform.openai.com.

## Language Model Providers

*   **Gemini** — Google's LLM family (`gemini-2.5-flash`, `gemini-2.0-flash`, etc.). Get a key at Google AI Studio.
*   **Anthropic** — Claude model family (`claude-sonnet-4-5`, `claude-3-5-haiku`, etc.). Get a key at console.anthropic.com.
*   **OpenAI** — GPT model family (`gpt-4o`, `gpt-4o-mini`, etc.).
*   **OpenRouter** — Single API key that routes to 200+ models across providers (OpenAI, Anthropic, Meta, Mistral, and more). Key prefix: `sk-or-...`. Get a key at openrouter.ai.
*   **Requesty** — Unified LLM gateway for 300+ models. Get a key at requesty.ai.

## Text-to-Speech Providers

*   **Deepgram** — Also used for cloud TTS (Aura voices).
*   **OpenAI** — TTS with voices like `alloy`, `nova`, `shimmer`. Uses the same OpenAI key.
*   **Inworld** — Conversational AI voices. Get a key at inworld.ai.

## Managing Keys

- Enter the full key string in the masked field and click the **save (✓)** button.
- Click the **delete (✗)** button to remove a key from the encrypted vault.
- A green indicator next to the provider name means a key is saved and ready.
