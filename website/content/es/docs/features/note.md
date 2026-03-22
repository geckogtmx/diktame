# Nota (Note)

La canalización **Note (Nota)** actúa como tu bloc de notas personal y manos libres. 

En lugar de inyectar texto en tu ventana actual o colocarlo en tu portapapeles, la canalización de Nota toma lo que dices y lo añade a un archivo de texto o Markdown en tu disco duro, completo con una marca de tiempo.

Esto es increíblemente útil durante reuniones, mientras investigas, o cuando tienes un pensamiento aleatorio mientras trabajas concentrado—puedes capturar la idea al instante sin cambiar de pestañas ni perder el contexto.

## Cómo usar Nota (Note)

1. Presiona la tecla de acceso rápido de **Nota** (Predeterminado: `Ctrl + Alt + N`).
2. Dicta tu nota (por ejemplo, *"Nota para mí: recordarle a Juan que actualice los certificados del servidor el viernes."*).
3. Suelta la tecla.

dIKta.me transcribirá la nota en segundo plano y la añadirá silenciosamente a tu archivo de Notas configurado. Por defecto, este archivo se encuentra en tu carpeta de Documentos como `diktame-notes.md`.

## Ajustes de Nota

Puedes personalizar completamente cómo se almacenan y formatean las notas. Abre la ventana de **Ajustes** y navega a la pestaña **General** para encontrar las configuraciones de Nota:

### File Path (Ruta del Archivo)
Puedes cambiar dónde se guardan tus notas. Puedes apuntar a un archivo genérico `.txt` en tu Escritorio, un archivo `.md` en una bóveda de Obsidian, o una carpeta sincronizada de Dropbox. dIKta.me creará el archivo si este no existe.

### Timestamp Format (Formato de Marca de Tiempo)
Cada vez que dictas una nota, dIKta.me antepone un encabezado con la hora. Por defecto usa el formato `yyyy-MM-dd HH:mm:ss` (por ejemplo, `## 2026-10-14 09:30:15`), pero puedes personalizar esto para que coincida con los estándares de formato de fecha y hora de C#.

### LLM Processing (Procesamiento LLM)
Por defecto, las Notas pasan por tu LLM antes de guardarse para que tengan el formato y la puntuación adecuados. Sin embargo, puedes desactivar completamente el **Procesamiento LLM** para las Notas. Cuando está desactivado, dIKta.me tomará la transcripción cruda directamente del proveedor de Voz-a-Texto (STT) y omitirá el modelo de lenguaje por completo, haciendo que el proceso de tomar la nota sea significativamente más rápido.
