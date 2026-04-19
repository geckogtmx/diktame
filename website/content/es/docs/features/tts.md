# Canalización de Texto a Voz (TTS)

dIKta.me no solo escucha—también puede hablar. La canalización de Texto a Voz (TTS) es un canal de salida ambiental que lee en voz alta el texto seleccionado o las respuestas de la IA. 

A diferencia de los lectores de pantalla tradicionales, el TTS de dIKta.me está diseñado para un flujo de trabajo fluido y sin interrupciones: el texto se inyecta al instante y la voz se reproduce sin problemas en segundo plano.

## Capacidades Clave

*   **Tecla "Leer Selección" (`Ctrl+Alt+Q`):** Resalta texto en cualquier aplicación—navegadores, editores de código o PDFs—y presiona la tecla de acceso rápido. La aplicación capturará la selección, la limpiará para formato de palabra hablada, y la leerá en voz alta.
*   **Modo Preguntar y Respuestas de Chat:** Haz que las respuestas de la IA a tus preguntas se lean dinámicamente mientras mantienes tu flujo de trabajo manos libres.
*   **Lectura de Traducciones:** Escucha las traducciones habladas de forma precisa en el idioma de destino.
*   **Notificaciones de la Aplicación:** En lugar de notificaciones visuales, escucha eventos críticos del sistema presentados de forma discreta (ej., "Grabación iniciada", "LLM fuera de línea").

## Cómo Funciona

La canalización TTS opera de manera silenciosa e inteligente:

1.  **Atenuación (Ducking):** Cuando comienza la voz, dIKta.me baja brevemente el volumen de otras aplicaciones (como Spotify o YouTube) para que puedas escuchar la voz con claridad. Una vez que la locución termina, el volumen de tu música se restaura automáticamente.
2.  **Limpieza de Texto:** La canalización limpia automáticamente el texto de formatos complejos antes de sintetizar la voz. Elimina los elementos Markdown, expande símbolos (como `$` a "dólares"), y suaviza las listas en oraciones conversacionales para asegurar que el audio suene natural.
3.  **Interrupción Instantánea:** Si inicias una nueva tecla de dictado o presionas la tecla **Escape (`Esc`)**, la voz se detiene instantáneamente para dejarte trabajar.

## Proveedores (Providers)

dIKta.me ofrece múltiples motores para generar voz, los cuales puedes configurar en la sección [Motor de IA → Texto a Voz](../settings/ai-engine.md):

*   **Kokoro (Local):** Un modelo local ONNX completamente fuera de línea y extremadamente rápido que se ejecuta en tu CPU.
*   **Gemini TTS (Nube):** Alternativa en la nube que aprovecha las voces ultra-realistas de Google y reutiliza tu clave de Gemini.
*   **Deepgram Aura-2:** Una opción en la nube de alta calidad que utiliza tu clave API existente de Deepgram.
*   **Inworld & OpenAI:** Voces premium en la nube para diálogos de la más alta calidad.

> **Nota:** TTS está **desactivado** por defecto. Para habilitarlo, navega a **Ajustes > Text-to-Speech (Texto a Voz)** y configura tu proveedor preferido y preferencias de reproducción.
