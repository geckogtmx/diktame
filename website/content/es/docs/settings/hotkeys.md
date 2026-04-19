# Ajustes de Teclas de Acceso Rápido (Hotkeys)

![Configuración de atajos — Dictar, Refinar, Preguntar, Traducir, Oops, Nota](/images/docs/settings-general-hotkeys.png)

La pestaña **Hotkeys (Teclas de Acceso Rápido)** te permite reasignar cada canalización para que coincida con tu flujo de trabajo existente y evitar conflictos con otras potentes aplicaciones de Escritorio.

Por defecto, cada acción involucra una combinación de tres teclas para asegurar que no actives una inyección de forma inadvertida mientras presionas una tecla genérica.

## Creando atajos globales efectivos
Debido a que dIKta.me está diseñado para permanecer invisible en segundo plano, sus teclas de acceso rápido son **Ganchos Globales de Windows (Global Windows Hooks)**. Esto significa que funcionan sin importar qué aplicación tengas abierta actualmente.

**Es fundamental que selecciones teclas que normalmente no uses.**

Si reasignas tu atajo de Dictado a una sola letra como la `D`, tu computadora grabará tu voz en el momento en que presiones esa letra y se detendrá cuando la sueltes—haciéndote efectivamente incapaz de escribir la `D` correctamente en cualquier otro lugar de Windows.

*   **Modificadores Compatibles**: Recomendamos mapearlos directamente a combinaciones con `Ctrl`, `Alt`, `Shift`, o `Win` (ej., `Ctrl + Shift + D` o `Ctrl + Alt + W`).
*   **Grabando una Tecla de Acceso Rápido**: Haz clic directamente dentro del cuadro de texto del atajo que deseas cambiar, presiona tu combinación de teclado deseada en su totalidad, y haz clic en **Save (Guardar)**.

## Canalizaciones Principales

*   **Dictate (Dictar - `Ctrl + Alt + D`)**: Inicia la canalización principal de Dictado. Esto involucra la transcripción STT, el procesamiento LLM formateado por el Modo activo, y la inyección del formato pulido final en la posición de tu cursor.

*   **Refine (Refinar - `Ctrl + Alt + R`)**: Edita el texto seleccionado existente con Instrucciones de Voz STT sin escribir ni una sola letra. Alternativamente, puede ejecutar el modo Piloto Automático sobre el texto seleccionado si las Instrucciones de Voz están desactivadas dentro de la pestaña `General`.

*   **Ask (Preguntar - `Ctrl + Alt + A`)**: Procesa breves consultas STT (ej. "*¿Cuál es la capital de España?*") sin escribir en tu ventana activa. Los resultados se copian silenciosamente en tu portapapeles o se envían de forma nativa como una Notificación de Windows.

*   **Translate (Traducir - `Ctrl + Alt + T`)**: Detecta automáticamente el idioma de entrada hablado, lo transcribe, usa la traducción del LLM para convertirlo nativamente a tu idioma de destino, e inyecta de forma natural los resultados.

*   **Note (Nota - `Ctrl + Alt + N`)**: Procesa silenciosamente tu pensamiento grabado y lo añade de forma iterativa con un pie de página de Marca de Tiempo a un archivo personalizado de Bloc de Notas o Markdown en tu computadora sin tocar nunca tu espacio de trabajo principal en pantalla.

*   **Oops (Ups - `Ctrl + Alt + V`)**: Recuerda la última carga útil literal de texto generada por las canalizaciones STT/LLM. La vuelve a pegar exitosamente en una ubicación del cursor corregida usando el historial volátil del Portapapeles de Windows sin hacerte repetir todo el dictado de voz.

## Ventanas Superpuestas

*   **Quick Chat Window (Ventana de Chat Rápido - `Ctrl + Alt + C`)**: En lugar de procesar cosas directamente bajo tu cursor, esta tecla invoca manualmente la interfaz de chat de LLM flotante e independiente, lo que permite amplias conversaciones de múltiples turnos que presentan historial de texto, selector de modelos, editores de instrucciones del sistema, e interacciones personalizadas adjuntando el contenido del portapapeles.
