# Ajustes del Motor de IA (AI Engine)

La pestaña **Motor de IA (AI Engine)** es el cerebro de tu experiencia con dIKta.me. Controla qué motores de Voz a Texto (STT) y de Modelo de Lenguaje (LLM) manejan cada canalización. La pestaña tiene un diseño maestro-detalle: selecciona una categoría a la izquierda para configurarla a la derecha.

> [!TIP]
> **Nube vs. Local**: Puedes cambiar el entorno activo directamente desde el Panel de Control sin abrir la configuración.

## Enrutamiento del Entorno

*   **Ruta en la Nube (Cloud Route - Predeterminado)**: Usa proveedores de API de terceros a través de internet.
    *   **Proveedor STT**: Deepgram (streaming) u OpenAI Whisper (por lotes), según las claves configuradas.
    *   **Proveedor LLM**: Elige entre Gemini, Anthropic (Claude), OpenAI (GPT), OpenRouter o Requesty, según las claves guardadas.
*   **Ruta Local (Local Route - En el dispositivo)**: Usa módulos de IA sin conexión. Tu audio nunca sale de tu máquina.
    *   **Proveedor STT**: **Whisper.net** — descarga un modelo ONNX la primera vez que se ejecuta.
    *   **Proveedor LLM**: **Ollama** — se comunica con modelos locales que se ejecutan en tu hardware.

## Selección de Modelo

Una vez activo un entorno, usa los menús desplegables de modelo para elegir el modelo exacto para cada canalización. Por ejemplo, con Gemini seleccionado puedes elegir entre `gemini-2.5-flash` (rápido, predeterminado) u otra variante más potente.

Cambiar el modelo afecta cada ejecución de canalización: velocidad, precisión y costo cambian en consecuencia.

## Subsecciones

La página de ajustes del Motor de IA contiene las siguientes subsecciones:

| Sección | Qué configura |
|---------|--------------|
| **API Keys** | Guardar y gestionar claves de API para todos los proveedores en la nube |
| **Speech to Text** | Proveedor y modelo STT activo para las rutas Nube y Local |
| **Language Model** | Proveedor y modelo LLM activo para las rutas Nube y Local |
| **Text to Speech** | Proveedor TTS activo y ajustes de voz |
| **Chat** | Prompt del sistema del Quick Chat e historial |
| **System Monitor** | Estado de Ollama, GPU y caché de modelos |
