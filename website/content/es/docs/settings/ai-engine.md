# Ajustes del Motor de IA (AI Engine)

La pestaña **Motor de IA (AI Engine)** es el cerebro de tu experiencia con dIKta.me. Te permite seleccionar qué motor de transcripción de Voz a Texto (STT) y qué motor de procesamiento de Modelo de Lenguaje (LLM) quieres activos en este momento.

> [!TIP]
> **Nube vs. Local**: Puedes cambiar rápidamente tu entorno activo en la superposición del Panel de Control sin tener que abrir el menú de ajustes. Elige entre un entorno puramente en la nube (Cloud) o una experiencia pura en tu máquina local.

## Enrutamiento del Entorno

*   **Ruta en la Nube (Cloud Route - Predeterminado)**: Usa proveedores de API de terceros con capacidad de internet que sobresalen tanto en velocidad como en precisión. 
    *   **Proveedor STT (Voz a Texto)**: Puede usar Deepgram o las APIs estándar de OpenAI Whisper dependiendo de qué claves tengas vinculadas.
    *   **Proveedor LLM (Modelo de Lenguaje)**: Usa modelos de chat de última generación de Google (Gemini) o Anthropic (Claude), procesando nativamente instrucciones de estilo complejas de forma rápida.
*   **Ruta Local (Local Route - En el dispositivo)**: Usa módulos de Inteligencia Artificial fuera de línea. Completamente libre de tarifas de suscripción y extremadamente seguro, ya que el audio de tu micrófono omite el internet por completo.
    *   **Proveedor STT**: Usa **Whisper.net**. Descargará un modelo de IA directamente a tu máquina local la primera vez que lo ejecutes.
    *   **Proveedor LLM**: Usa **Ollama**. Se comunica de forma transparente con aplicaciones de IA de Escritorio Locales, procesando grandes conjuntos de datos de parámetros de forma totalmente offline.

## Selección de Modelo

Una vez que un entorno está activo, puedes reducir exactamente qué modelos quieres que tomen el control de tus canalizaciones. Por ejemplo, si configuras el entorno de la Nube a `Gemini`, el menú desplegable del modelo LLM te permitirá elegir entre `gemini-1.5-pro` (más lento, mejor) o `gemini-1.5-flash` (increíblemente rápido, ligero).

Cambiar estos menús desplegables altera fundamentalmente la velocidad, la precisión y las capacidades de todas y cada una de las ejecuciones de las canalizaciones.
