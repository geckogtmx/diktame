# Traducir (Translate)

La canalización **Translate (Traducir)** te permite hablar en un idioma y escribir instántaneamente en otro. 

Ya sea que te estés comunicando en salas de chat internacionales, escribiendo correos electrónicos a clientes extranjeros, o aprendiendo un nuevo idioma, Traducir cierra la brecha automáticamente sin necesidad de abrir una pestaña separada en el navegador.

## Cómo usar Traducir

1. Coloca tu cursor donde quieres que aparezca el texto traducido.
2. Presiona la tecla de **Traducir** (Predeterminado: `Ctrl + Alt + T`).
3. Habla tu oración en tu idioma de origen.
4. Suelta la tecla.

dIKta.me detectará automáticamente el idioma en el que estás hablando, lo transcribirá, lo traducirá a tu idioma de destino, e inyectará el resultado final directamente en tu ventana activa.

> [!TIP]
> **Respaldo:** Si el motor de traducción falla por cualquier motivo, dIKta.me inyectará perfectamente la transcripción cruda en tu idioma original para que no pierdas lo que dijiste.

## Configurando tu Idioma de Destino

Para cambiar el idioma *al que* traduce dIKta.me, simplemente actualizas la instrucción del sistema de tu perfil de Traducir:

1. Abre el Panel de Control y haz clic en el engranaje de **Ajustes**.
2. Navega a la pestaña de **Modos (Modes)**.
3. Selecciona la canalización **Traducir (Translate)**.
4. Modifica el System Prompt (Instrucción del Sistema). 

Por ejemplo, puedes ajustar la instrucción a:
*   `Translate this text to Spanish.` (Traduce este texto al español).
*   `Translate this to formal Japanese.` (Traduce esto al japonés formal).
*   `Translate the following to English, but keep any programming terms or variable names in English.` (Traduce lo siguiente al inglés, pero mantén cualquier término de programación o nombres de variables en inglés).

La IA seguirá estas instrucciones a la perfección cada vez que uses el atajo `Ctrl + Alt + T`.

## Una Nota sobre la Transmisión (Streaming)

Como todas las canalizaciones que dependen de un LLM para procesar y dar formato al texto, **Traducir opera exclusivamente en modo por Lotes (Batch)**. 

Incluso si tienes activado el Dictado por Transmisión (Streaming) en tus ajustes Generales, la canalización de Traducir esperará hasta que termines de hablar para asegurar que el LLM tiene el contexto completo de tu oración antes de intentar traducirla.
