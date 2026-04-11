# Ajustes de Claves API

La sección **Claves API (API Keys)** (dentro de los ajustes del Motor de IA) te permite guardar credenciales cifradas para los proveedores en la nube, habilitando el modo Trae Tu Propia Clave (BYOK).

> [!NOTE]
> El modo API Key requiere una **Power License**. Las claves se almacenan con Windows DPAPI y nunca se envían a los servidores de dIKta.me — las solicitudes van directamente desde tu máquina a cada proveedor a través de HTTPS.

## Proveedores de Voz a Texto (STT)

*   **Deepgram** — STT en la nube para procesamiento por lotes y en tiempo real (streaming). Consigue una clave en deepgram.com.
*   **OpenAI** — También usado para STT de Whisper por lotes. Consigue una clave en platform.openai.com.

## Proveedores de Modelo de Lenguaje (LLM)

*   **Gemini** — Familia de LLM de Google (`gemini-2.5-flash`, `gemini-2.0-flash`, etc.). Consigue una clave en Google AI Studio.
*   **Anthropic** — Familia de modelos Claude (`claude-sonnet-4-5`, `claude-3-5-haiku`, etc.). Consigue una clave en console.anthropic.com.
*   **OpenAI** — Familia de modelos GPT (`gpt-4o`, `gpt-4o-mini`, etc.).
*   **OpenRouter** — Una sola clave de API que enruta a 200+ modelos de distintos proveedores (OpenAI, Anthropic, Meta, Mistral y más). Prefijo de clave: `sk-or-...`. Consigue una clave en openrouter.ai.
*   **Requesty** — Pasarela unificada de LLM para 300+ modelos. Consigue una clave en requesty.ai.

## Proveedores de Texto a Voz (TTS)

*   **Deepgram** — También usado para TTS en la nube (voces Aura).
*   **OpenAI** — TTS con voces como `alloy`, `nova`, `shimmer`. Usa la misma clave de OpenAI.
*   **Inworld** — Voces de IA conversacional. Consigue una clave en inworld.ai.

## Gestión de Claves

- Ingresa la cadena completa de la clave en el campo enmascarado y haz clic en el botón **guardar (✓)**.
- Haz clic en el botón **eliminar (✗)** para eliminar una clave de la bóveda cifrada.
- Un indicador verde junto al nombre del proveedor significa que hay una clave guardada y lista.
