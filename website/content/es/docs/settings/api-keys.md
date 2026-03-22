# Ajustes de Claves API

La pestaña de **Claves API (API Keys)** te permite integrar dIKta.me con servicios en la Nube premium y líderes en la industria, permitiéndote traer tu propia clave (BYOK).

En lugar de depender únicamente del marco de balance integrado de dIKta.me, conectar tus cuentas de desarrollador directamente te otorga control absoluto sobre tus costos y capacidades de API.

## Proveedores Compatibles

*   **Clave API de Deepgram**: Usada por nuestra canalización de STT en la Nube tanto para procesamiento por Lotes (Batch) como de Transmisión (Streaming) en tiempo real. Precisión extremadamente alta y tasas de reconocimiento asombrosamente rápidas. Consigue la tuya registrándote en deepgram.com.
*   **Clave API de Gemini**: Usada por nuestro motor de procesamiento LLM en la Nube. Proporciona transformaciones de texto robustas e increíblemente inteligentes. Consigue tu clave en Google AI Studio.
*   **Clave API de Anthropic**: Un procesador LLM en la Nube alternativo capaz de usar las familias de modelos Claude 3 y 3.5.
*   **Clave API de OpenAI**: Se conecta a Whisper STT para traducciones por Lotes (si no quieres usar Deepgram) o a los motores de procesamiento LLM GPT-4o.

## Seguridad

Tus claves nunca se transmiten de forma suelta o insegura. Ellas están:
1.  **Almacenadas de forma segura**: Se encriptan permanentemente a través de la integración estándar **DPAPI de Windows** (la misma forma en que Windows protege las contraseñas).
2.  **Procesadas localmente**: Nunca se envían a un servidor principal de dIKta.me. Solo se procesan las solicitudes de API enviadas directamente a través de HTTPS desde tu computadora explícitamente a Google, OpenAI, Deepgram, o Anthropic.

Para actualizar una clave API, ingresa toda la cadena en el cuadro de texto enmascarado y presiona **Save Keys (Guardar Claves)**. Siempre puedes hacer clic en `Clear (Limpiar)` para purgar las claves de la bóveda encriptada.
