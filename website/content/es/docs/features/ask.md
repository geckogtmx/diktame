# Preguntar (Ask)

La canalización **Ask (Preguntar)** en dIKta.me está diseñada para consultas de voz unidireccionales en las que necesitas una respuesta de una IA, pero *no* quieres que escriba sobre lo que estás trabajando actualmente.

Es particularmente útil para preguntas rápidas, verificación de datos o para generar fragmentos de código en segundo local.

## Cómo usar Preguntar (Ask)

1. Mantén presionada la tecla de acceso rápido **Preguntar** (Predeterminado: `Ctrl + Alt + A`).
2. Haz tu pregunta (por ejemplo, *"¿Cuál es la capital de Australia?"*, o *"Dame una expresión regular que valide correos electrónicos."*).
3. Suelta la tecla.

dIKta.me transcribirá tu voz, enviará la pregunta al LLM (utilizando tu perfil de Preguntar configurado) y recuperará la respuesta.

## Modos de Salida

Debido a que Preguntar está diseñado para no ser intrusivo, la respuesta se maneja según tu **Modo de Salida de Preguntar**. 

Puedes configurar qué ocurre con la respuesta navegando a la pestaña **General** en la ventana de Ajustes. Las opciones son:

1. **Clipboard & Toast / Portapapeles y Notificación (Predeterminado)**: La respuesta de la IA se copia silenciosamente a tu portapapeles para que puedas pegarla (`Ctrl + V`) donde quieras. También aparecerá una pequeña notificación (Toast) de Windows mostrándote la respuesta.
2. **Clipboard Only (Solo Portapapeles)**: La respuesta se copia silenciosamente a tu portapapeles. No se muestra ninguna notificación.
3. **Toast Only (Solo Notificación)**: La respuesta se muestra en una notificación de resumen de Windows. Tu portapapeles permanece intacto.
4. **Inject Only (Solo Inyectar)**: La respuesta se comporta exactamente como el Dictado y se inyecta en la posición de tu cursor inmediatamente.

## Personalizando la instrucción de Preguntar

Por defecto, la canalización de Preguntar tiene una instrucción del sistema (system prompt) optimizada para responder preguntas de manera concisa. 

Si deseas que la canalización de Preguntar se comporte de manera diferente (por ejemplo, si quieres que *siempre* genere JSON, o que *siempre* responda en formato de poema haiku), puedes modificar su instrucción del sistema:

1. Abre el Panel de Control y haz clic en el engranaje de **Ajustes**.
2. Navega a la pestaña de **Modos (Modes)**.
3. Selecciona la canalización de **Preguntar (Ask)**.
4. Modifica el System Prompt para tu perfil Cloud (Nube) o Local.
