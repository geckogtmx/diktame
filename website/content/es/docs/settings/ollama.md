# Ajustes de Ollama

La pestaña **Ollama** administra tus modelos de IA Locales, fuera de línea y en el dispositivo, que potencian la ruta de entorno "Local" de dIKta.me. Asegura una privacidad absoluta porque no se envían datos a través de internet para su procesamiento.

Ollama es un marco de trabajo (framework) potente y liviano que se ejecuta en segundo plano en tu PC. dIKta.me se comunica intensamente con él para dar formato y analizar tus transcripciones de forma completamente privada.

## Configuración del Servidor

Por defecto, dIKta.me asume que Ollama se está ejecutando en el entorno estándar Localhost en tu propia computadora.

*   **URL del Host (`http://localhost:11434`)**: Si tienes un servidor dedicado ejecutando Ollama en otro lugar de tu red, cambia esta URL para apuntar el procesador LLM a la dirección IP correcta. De lo contrario, déjalo como predeterminado.
*   **Test Connection (Probar Conexión)**: Verifica instantáneamente que tu PC puede hablar activamente con el servidor de Ollama y enumera la versión del marco instalada en segundo plano.

## Centro de Gestión de Modelos

En lugar de forzarte a usar comandos de terminal, la pestaña de Ollama de dIKta.me actúa como un administrador completo que se integra directamente en la canalización:

*   **Installed Models (Modelos Instalados)**: Una tabla en vivo de todos los modelos actualmente descargados en tu biblioteca de Ollama, mostrando tamaños de archivo y estructuras de parámetros (ej. `llama3:8b`, `mistral`).
*   **Delete (Eliminar)**: Libera instantáneamente espacio en el disco duro eliminando un modelo instalado nativamente dentro de la GUI.
*   **Download New Model (Descargar Nuevo Modelo)**: ¿Necesitas `phi3` o `gemma`? Escribe la etiqueta del modelo en la barra y dIKta.me te mostrará una superposición de progreso mientras el modelo se descarga e instala directamente en tu entorno.

Una vez que los modelos están instalados aquí, puedes navegar sin problemas de regreso a la pestaña `AI Engine (Motor de IA)` y configurar cualquiera de estos modelos fuera de línea recién descargados como tu formateador primario de Dictado.
