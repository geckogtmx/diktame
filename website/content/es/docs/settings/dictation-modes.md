# Modos de Dictado (Dictation Modes)

![Presets de Dictado — Standard con perfil Cloud](/images/docs/settings-dictation-presets.png)

La pestaña **Modos de Dictado (Dictation Modes)** es donde configuras exactamente *cómo* la IA formatea tus palabras habladas antes de inyectarlas en tus aplicaciones. 

Puedes crear ilimitados Ajustes Preestablecidos (Presets) de Dictado personalizados (como "Tono Casual", "Comentarios de Código", o "Transcriptor Médico") y cambiar rápidamente entre ellos usando el menú desplegable en el Panel de Control flotante principal.

## Gestionando Ajustes Preestablecidos (Presets)

La interfaz te permite administrar una lista completa de diferentes perfiles:
*   **Add New (Añadir Nuevo)**: Crea un ajuste preestablecido en blanco.
*   **Duplicate (Duplicar)**: Clona tu ajuste preestablecido actualmente seleccionado para realizar pruebas A/B sencillas o realizar ajustes menores cómodamente.
*   **Delete (Eliminar)**: Elimina el ajuste preestablecido de tu configuración.

Cada ajuste preestablecido que crees requiere un **Nombre** altamente descriptivo (ej., "Escritor de Correos en Español") para que entiendas exactamente qué hace cuando lo elijas en el Panel de Control.

## Configurando Instrucciones (Prompts)

Para cada ajuste preestablecido, debes definir **dos** Instrucciones del Sistema separadas:

### 1. Instrucción del Sistema en la Nube (Cloud System Prompt)
Esta instrucción se envía a modelos de API premium y altamente inteligentes como Gemini de Google, Claude de Anthropic o GPT-4o de OpenAI. 
Debido a que estos modelos son masivos, puedes escribir instrucciones complejas e intrincadas aquí.

*Ejemplo de Instrucción en la Nube:*
> "Eres un Desarrollador C# experto. El usuario va a dictar comentarios de código. Formatea todo lo que diga correctamente según los estándares XML. Infiere los nombres de las variables por el contexto y capitalízalos adecuadamente. Genera solo el comentario terminado, nada más."

### 2. Instrucción del Sistema Local (Local System Prompt)
Esta instrucción se envía a modelos locales que se ejecutan en tu máquina a través de Ollama (como `llama3:8b` o `mistral`).
Debido a que estos son modelos livianos diseñados para ejecutarse rápidamente en una GPU de consumo, funcionan mejor con comandos más cortos, altamente repetitivos y extremadamente directos. 

*Ejemplo de Instrucción Local:*
> "Formatea estrictamente como un comentario técnico de C#. No generes relleno conversacional. Asegura la precisión ortográfica."

## Opciones de Ajuste Preestablecido Avanzadas
*   **Per-Preset Trailing Space (Espacio Final por Ajuste Preestablecido)**: Puedes configurar si se añade automáticamente un espacio en blanco después de la inyección en base a *cada ajuste preestablecido*. Este control granular permite que tu ajuste preestablecido de "Dictado Estándar" añada espacios para una escritura fluida, mientras que tus ajustes de "Comandos de Terminal" o "Fragmentos de Código" pueden omitir el espacio para evitar errores de formato.

## ¿Por qué separarlos?
Al tener dos instrucciones distintas para cada ajuste preestablecido, dIKta.me asegura que sin importar si estás ejecutando en Modo Nube (para formatos complejos) o Modo Local (para privacidad absoluta sin internet), tu ajuste preestablecido se ejecutará sin problemas y sin perder el ritmo. Simplemente cambias el interruptor de "Nube/Local" (Cloud/Local) en el Panel de Control y la IA sabe exactamente cuál de las dos instrucciones usar silenciosamente en segundo plano.
