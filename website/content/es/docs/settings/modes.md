# Modos de Utilidad (Utility Modes)

La pestaña de **Modos (Modes)** configura la lógica de inteligencia artificial subyacente para las canalizaciones de utilidad fijas de dIKta.me. 

Mientras que la pestaña "Modos de Dictado" te permite crear infinitos perfiles personalizables entre los cuales puedes cambiar, la pestaña única de **Modos (Modes)** bloquea directamente el comportamiento de tus teclas de acceso rápido principales: **Preguntar (Ask)**, **Refinar (Refine)**, **Traducir (Translate)** y **Nota (Note)**.

## Configuración de Canalización

Cada canalización tiene una subsección dedicada en esta pantalla. Seleccionar una canalización de la lista abrirá sus editores específicos de instrucciones del sistema en la Nube y Locales (exactamente como un ajuste preestablecido de Dictado estándar).

1.  **Canalización Preguntar (Ask Pipeline)**: La instrucción enviada al LLM cuando usas `Ctrl+Alt+A`. 
    *   *Predeterminado*: "Eres un asistente útil. Proporciona respuestas concisas y directas a la siguiente pregunta. No incluyas relleno conversacional como 'Aquí está la respuesta'. Solo responde al usuario."
2.  **Canalización Refinar (Refine Pipeline)**: La instrucción enviada al LLM cuando resaltas texto y presionas `Ctrl+Alt+R` (específicamente en modo Piloto Automático).
    *   *Predeterminado*: "Corrige cualquier error ortográfico o gramatical en el siguiente texto. No cambies el tono o significado subyacente. Mantén el formato."
3.  **Canalización Nota (Note Pipeline)**: La instrucción enviada cuando dictas una entrada de diario con `Ctrl+Alt+N`.
    *   *Predeterminado*: "Formatea la siguiente transcripción en una nota coherente y debidamente puntuada. Corrige los errores de ortografía pero no omitas ninguna información."
    *   *Nota: Si tienes el "Procesamiento LLM" desactivado en los ajustes Generales, estas instrucciones se ignoran por completo.*
4.  **Canalización Traducir (Translate Pipeline)**: La instrucción enviada al traducir texto nativamente a través de `Ctrl+Alt+T`.
    *   *Predeterminado*: "Traducir el siguiente texto al español. Muestra únicamente el texto traducido, no incluyas ningún otro comentario."

## Inyección de Contexto

Algunas canalizaciones dependen de inyectar información dinámica en las instrucciones del modelo justo antes de la ejecución:

*   **Refinar ({instruction})**: Si usas el modo de Instrucción de Voz para Refinar, dIKta.me reemplazará `{instruction}` en tu instrucción escrita con lo que realmente dijiste en voz alta. ¡Asegúrate de que tu instrucción incluya `{instruction}` para que el modelo sepa qué hacer con el comando de audio!

## Ajuste Fino en Nube vs Local

Como siempre, dIKta.me asegura que tus canalizaciones sean robustas ya sea que estés en línea o completamente fuera de línea.

Debes proporcionar tanto una **Instrucción del Sistema en la Nube (Cloud System Prompt)** (para modelos como `gpt-4o` o `gemini-1.5-pro`) como una **Instrucción del Sistema Local (Local System Prompt)** (para modelos locales de Ollama como `llama3` o `phi3`) para cada modo de utilidad para garantizar el éxito cuando cambies el interruptor de Entorno del Panel de Control.
