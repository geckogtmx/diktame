# Primeros Pasos

¡Bienvenido! Vamos a prepararte con **dIKta.me**. No requiere programación ni instalaciones complejas.

## 1. Instalación

1. **Descargar**: Descarga el último `DiktaMe_Setup.exe` desde la [página de Releases](https://github.com/geckogtmx/diktame/releases).
2. **Ejecutar**: Haz doble clic en el instalador.
   > *Advertencia de Windows SmartScreen la primera vez: haz clic en "Más información" → "Ejecutar de todas formas" para continuar.*
3. **Listo**: dIKta.me se iniciará y se ubicará en tu bandeja del sistema.

---

## 2. Asistente de Primera Ejecución

La primera vez que abras dIKta.me, un breve asistente de configuración te guiará.

### Paso 1 — Idioma

Elige inglés o español para la interfaz de la aplicación.

### Paso 2 — Elige tu modo

| Modo | Qué hace | Requiere |
|------|----------|----------|
| **Wallet (Billetera)** | Dictado en la nube con créditos de dIKta.me. Solo inicia sesión, sin claves de API. | Cuenta gratuita |
| **API Key (BYOK)** | Usa tus propias claves de Deepgram, Gemini, Anthropic y/u OpenAI. Las solicitudes van directamente a cada proveedor. | Power License |
| **Local** | Totalmente sin conexión con Whisper.net + Ollama en tu máquina. | Power License |

> **Wallet es la forma más rápida de empezar.** El asistente abrirá una pestaña del navegador para iniciar sesión y listo.
>
> **BYOK y Local** requieren una [Power License](https://www.dikta.me/pricing). Si seleccionas uno de estos sin una licencia activa, el asistente mostrará un aviso de activación antes de continuar.

### Pasos 3–5 — STT, LLM, TTS *(solo ruta BYOK)*

Si elegiste el modo API Key, el asistente te guía por:

- **Speech to Text (STT)**: Nube (Deepgram u OpenAI Whisper) o Local (Whisper.net, se descarga en el primer uso).
- **Language Model (LLM)**: Nube (Gemini, Claude, GPT, OpenRouter…) o Local (Ollama).
- **Text to Speech (TTS)**: Desactivado, Nube (Deepgram, OpenAI o Gemini TTS) o Local (Kokoro).

### Paso 6 — Claves de API *(si se seleccionaron proveedores en la nube)*

Introduce las claves para los proveedores en la nube que elegiste. Las claves se cifran inmediatamente con Windows DPAPI. Este paso se omite automáticamente si seleccionaste solo proveedores locales.

### Paso 7 — Prueba

Asegúrate de que tu micrófono sea detectado y de que la IA responda correctamente.

### Paso 8 — ¡Listo!

Todo está configurado. El Panel de Control aparece en pantalla y dIKta.me está listo para usar.

---

## 3. Cómo Usarlo

dIKta.me vive en silencio en tu bandeja del sistema.

- **Para Dictar**: Haz clic en cualquier lugar donde puedas escribir y presiona **`Ctrl+Alt+D`**. Mantenlo presionado mientras hablas, luego suéltalo — dIKta.me escribe lo que dijiste.
- **Para Configurar**: Haz clic derecho en el icono de la bandeja o haz clic en el icono de engranaje del Panel de Control para abrir la Configuración.
- **Para Cambiar de Modo**: Haz clic en el interruptor Nube/Local del HUD del Panel de Control.
