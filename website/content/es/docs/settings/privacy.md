# Ajustes de Privacidad (Privacy)

La pestaña **Privacidad (Privacy)** controla exactamente cuánta información dIKta.me registra y retiene sobre tus sesiones de dictado.

Debido a que dictas información sobre tu trabajo, contraseñas y otros detalles potencialmente delicados, necesitas opciones sólidas para controlar exactamente cómo, qué y dónde se registran las cosas localmente en tu computadora.

## Niveles de Registro (Logging Levels)

Puedes ajustar el nivel de registro para dictar exactamente cuánta información de diagnóstico se guarda en tu directorio local `logs` dentro de `%APPDATA%\DiktaMe`.

1.  **Full Logging (Registro Completo)**: Captura todo. El contenido completo de tus transcripciones de Voz a Texto y la salida completa y formateada del Modelo de Lenguaje. Es el mejor nivel para crear nuevos Modos de Instrucciones o depurar problemas de formato.
2.  **Balanced Logging (Registro Equilibrado)**: Elimina automáticamente la información confidencial antes de escribir el texto en el disco duro de tu computadora. Ejecuta un módulo de limpieza localizado construido específicamente para la preservación de tu privacidad.
3.  **Stats Logging (Registro de Estadísticas)**: Tu texto de salida nunca se guarda en los registros de errores. Solo registra las marcas de tiempo, los mensajes de error (como `Clave API rechazada`) y los conteos totales de tokens LLM generados durante la sesión.
4.  **Ghost Mode (Modo Fantasma)**: dIKta.me no registra absolutamente nada localmente. Ni siquiera los conteos de tokens o los errores de latencia del proveedor de STT se escriben en el disco. La aplicación funciona de forma completamente anónima.

> [!CAUTION]
> Si encuentras un error repetible mientras estás en **Ghost Mode (Modo Fantasma)**, no podremos ayudarte a depurarlo, ¡ya que no existirá el historial de errores!

## Limpieza de PII (PII Scrubbing)

Si estás utilizando el **Registro Equilibrado (Balanced Logging)**, dIKta.me ejecutará localmente una redacción de PII (Información de Identificación Personal) basada en expresiones regulares antes de que se escriba cualquier cosa en los archivos de registro de diagnóstico.

El limpiador detecta y sobrescribe automáticamente:
*   Direcciones de correo electrónico (ej. `usuario@ejemplo.com` se convierte en `[EMAIL REDACTED]`)
*   Números de Seguro Social (SSN)
*   Números de tarjetas de crédito o patrones bancarios estandarizados complejos
*   Números de teléfono

*Nota: Esta limpieza solo se aplica a tus **archivos de registro locales**. El texto completo real siempre se envía al proveedor de LLM activo (como Anthropic u Ollama) para que se formatee correctamente antes de realizar la inyección de texto.*
