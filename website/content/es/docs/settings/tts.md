# Texto a Voz (TTS) Settings

dIKta.me puede decir sus respuestas y leer el texto seleccionado en voz alta. El TTS está diseñado para ser un canal de salida ambiental que opera sin bloquear tu flujo de trabajo de dictado.

> **Nota de Privacidad:** El TTS es completamente opcional. Por defecto, está **desactivado** y no requiere procesamiento de GPU. Si habilitas el proveedor **Kokoro (Local)**, la locución se genera completamente en tu dispositivo sin requerir conexión a internet.

## Habilitando Texto a Voz

1. Abre los **Ajustes** haciendo clic en el icono del engranaje o presionando `Ctrl+Alt+,`.
2. Navega a la sección **Text-to-Speech (Texto a Voz)**.
3. Alterna el **Master Switch (Interruptor Principal)** a Encendido (On).
4. Selecciona un **Provider (Proveedor)** (ver más abajo).

## Proveedores

dIKta.me ofrece múltiples proveedores de TTS, que van desde la inferencia rápida local y fuera de línea hasta la generación en la nube de ultra alta calidad.

### Kokoro (Local)
* **Estado:** Predeterminado (Local)
* **Costo:** Gratis
* **Privacidad:** 100% Fuera de línea (Offline)
* El proveedor Kokoro usa un modelo ONNX altamente optimizado descargado directamente a tu máquina. Requiere una descarga inicial (88 MB para la variante estándar) y luego se ejecuta en tu CPU sin enviar nunca datos a la nube.

### Gemini TTS (Cloud / Nube)
* **Estado:** Alternativa en la Nube
* **Costo:** Requiere Clave API de Gemini
* Aprovecha las voces ultrarrealistas de Google para tus respuestas de Texto a Voz. Si usas Gemini para dictar, puedes reutilizar tu clave para potenciar una retroalimentación hablada altamente natural.

### Deepgram Aura-2 (Cloud / Nube)
* **Estado:** Alternativa en la Nube
* **Costo:** Requiere Clave API de Deepgram (facturado por uso)
* Si ya estás usando Deepgram para voz a texto, puedes reutilizar tu clave API para generar respuestas de voz de alta calidad.

### Inworld TTS-1.5 (Cloud / Nube)
* **Estado:** Nube Premium
* **Costo:** Requiere Clave API de Inworld
* Ofrece algunas de las voces de más alta calidad y más naturales disponibles, operando a una latencia extremadamente baja.

### OpenAI TTS (Cloud / Nube)
* **Estado:** Alternativa en la Nube
* **Costo:** Requiere Clave API de OpenAI
* Utiliza tus credenciales BYOK (Trae Tu Propia Clave) existentes de OpenAI para aprovechar su biblioteca de voces conversacionales de alta calidad.

## Cuándo Hablar

Puedes ajustar con precisión exactamente cuándo dIKta.me debería hablar en voz alta:

* **Tecla "Leer Selección" (`Ctrl+Alt+Q`):** Resalta texto en cualquier aplicación y presiona esta tecla. dIKta.me capturará la selección, la limpiará para hablarla, y la leerá en voz alta.
* **Respuestas del Modo Preguntar:** Permite reproducir la respuesta de la IA en voz alta simultáneamente mientras se escribe en tu ventana activa.
* **Respuestas de Chat Rápido:** Mantén una conversación manos libres con la ventana de Chat Rápido haciendo que se lean las respuestas de la IA de vuelta a ti.
* **Resultados de Traducción:** Escucha tu dictado hablado traducido y pronunciado en el idioma de destino.
* **Notificaciones de la Aplicación:** Escucha los eventos de estado del sistema ("LLM no cargado", "Grabación iniciada") de forma hablada en lugar de depender únicamente de notificaciones visuales (toasts).

## Controles de Audio

La generación de TTS nunca retrasa la inyección de texto. El texto llega al instante y la voz se reproduce sin problemas en paralelo.

* **Ducking (Atenuación):** Por defecto, dIKta.me bajará temporalmente el volumen de otras aplicaciones (como música o videos) mientras habla.
* **Interrupting (Interrupción):** Si necesitas detener la reproducción de inmediato, presiona la tecla **Escape (`Esc`)** o activa cualquier nueva tecla de acceso rápido de dictado (ej. `Ctrl+Alt+S`).
