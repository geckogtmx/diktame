# Acciones de Visión

> **Entrada:** [Pantalla](../inputs/screen.md) · **Atajo:** Ctrl+Alt+S

La función **Vision** te permite capturar cualquier cosa de tu pantalla — una captura completa, una región seleccionada o una ventana específica — y preguntarle a la IA sobre lo que ve. La respuesta puede inyectarse en la posición de tu cursor, copiarse al portapapeles o enviarse al Chat Rápido, todo sin interrumpir tu flujo de trabajo.

![Panel de Acciones de Visión — Save / OCR / Edit / Clip / Chat / Note](/images/docs/workflow-vision-panel.png)

> [!TIP]
> Vision funciona de maravilla junto a tu voz. Después de capturar, puedes grabar una pregunta hablada (por ejemplo, *"¿Qué significa este error?"* o *"Resume los datos de esta tabla"*) antes de que la IA procese la imagen.

## Iniciar una Captura de Vision

Presiona la tecla de acceso rápido de **Vision** desde cualquier aplicación:

`Ctrl + Alt + S` (Predeterminado)

La pantalla se oscurece y aparece una superposición de recorte transparente con una barra de indicaciones en la parte inferior.

## Modos de Captura

| Entrada | Qué ocurre |
|---|---|
| **Clic** sobre una ventana | Captura esa ventana específica |
| **Clic y arrastrar** | Captura una región rectangular personalizada |
| **Presionar `F`** | Captura el monitor activo completo |
| **Presionar `A`** | Captura todos los monitores como una sola imagen panorámica |
| **Presionar `Esc`** | Cancela y vuelve a tu trabajo |

Después de hacer la selección, la superposición se cierra y aparece el **panel de acciones de Vision**.

## El Panel de Acciones de Vision

Este paso te permite escribir (o grabar) una pregunta opcional y elegir qué debe hacer la IA con la captura de pantalla.

### Consulta Opcional

El campo de texto acepta tu pregunta. Puedes escribirla o hacer clic en el **botón de micrófono** para grabar una consulta de voz (hasta 30 segundos). dIKta.me transcribe tu pregunta y rellena el campo automáticamente.

Si dejas el campo vacío, se utiliza la consulta predeterminada: *"Describe lo que ves y extrae cualquier texto visible."*

### Alternancia Local / Nube

Cambia entre **Local** (tu modelo de visión de Ollama configurado, que se ejecuta en tu dispositivo) y **Nube** (Gemini, Claude u OpenAI) para cada captura individualmente.

### Botones de Acción

| Botón | Qué hace |
|---|---|
| **Save** | Guarda la captura en un archivo y la copia al portapapeles. Sin IA. |
| **Clipboard** | Envía la imagen con la consulta a la IA y copia la respuesta a tu portapapeles. |
| **Chat** | Adjunta la captura al Chat Rápido para mantener una conversación de varios turnos sobre ella. |
| **Note** | Ejecuta la canalización de visión y graba una nota de voz que se añade a tu archivo de notas. |
| **OCR** | Extrae todo el texto visible de la captura exactamente como aparece y lo copia a tu portapapeles. |
| **Color** | Abre el [Selector de Color](#selector-de-color) sobre la captura realizada. |
| **Record** | Inicia una [grabación de video](#grabación-de-video-captura-de-momentos) de la región seleccionada. |

> [!NOTE]
> **Table** siempre usa el proveedor de nube independientemente de tu alternancia Local/Nube, ya que los modelos locales producen resultados poco fiables para esta tarea.

## Selector de Color

`Ctrl + Alt + C` también abre el Selector de Color directamente, sin pasar por la superposición de Vision.

Una vez abierta la superposición:

*   **Mueve el ratón** para ver una lupa en vivo con el color exacto del píxel bajo el cursor, junto con sus valores hexadecimal y RGB.
*   **Clic** para seleccionar un color. Los colores elegidos se acumulan en una tira de paleta en la parte inferior.
*   **Retroceso** para deshacer la última selección.
*   **Enter** para terminar y copiar todos los colores seleccionados al portapapeles.
*   **Tab** para terminar y enviar la paleta a la IA para un análisis de colores.
*   **Esc** para cancelar (si aún no has seleccionado ningún color) o terminar con la paleta actual.

## Grabación de Video (Captura de Momentos)

Inicia una grabación desde el panel de acciones de Vision haciendo clic en **Record**, o usa tu tecla de acceso rápido de video configurada.

Aparece la superposición de recorte para que selecciones una región o la pantalla completa. Una vez confirmada, aparece una barra flotante compacta en la parte superior de la pantalla que muestra:

*   Un punto rojo parpadeante y un temporizador en curso
*   Un botón de **Pausar / Reanudar**
*   Un botón de **Detener**

La grabación captura simultáneamente la pantalla, el audio del micrófono y el audio del sistema. Se puede activar una burbuja opcional de cámara web (imagen en imagen, esquina inferior derecha) en Ajustes.

La duración máxima de grabación predeterminada es de **120 segundos**.

### Después de Grabar

Al hacer clic en **Stop**, aparece un panel posterior a la captura:

| Botón | Qué hace |
|---|---|
| **Save** | Guarda el archivo MP4 localmente. Sin procesamiento de IA. |
| **Describe** | Sube el clip a Gemini y devuelve una descripción de lo que ocurrió. |
| **Document** | Pide a Gemini que escriba instrucciones paso a paso para las acciones mostradas. |
| **Bug Report** | Pide a Gemini que genere un informe de error estructurado basado en lo que ve. |
| **Chat** | Adjunta el clip al Chat Rápido para una conversación de varios turnos. |

> [!NOTE]
> Las acciones de IA para video requieren conexión a la nube. La acción **Save** siempre funciona sin conexión.

## Salida

De forma predeterminada, las respuestas de Vision se **inyectan en la posición de tu cursor**, igual que el Dictado. Puedes cambiar el comportamiento predeterminado por acción en **Ajustes → Vision**:

| Modo | Comportamiento |
|---|---|
| **Inject** (predeterminado) | La respuesta se escribe en la ventana activa en la posición del cursor. |
| **Clipboard** | La respuesta se copia al portapapeles. Una notificación confirma la acción. |
| **Toast Only** | La respuesta se muestra en una notificación de Windows. No se escribe ni se copia nada. |

## Modelos de Visión Locales

Si tienes Ollama instalado, puedes procesar imágenes completamente en tu propio equipo. Modelos recomendados:

| Modelo | Comando de Ollama | VRAM | Ideal para |
|---|---|---|---|
| `minicpm-v` (predeterminado) | `ollama pull minicpm-v` | ~2 GB | Uso general, OCR, descripción |
| `moondream` | `ollama pull moondream` | ~1.2 GB | Descripciones rápidas en hardware con poca VRAM |
| `llava-phi3` | `ollama pull llava-phi3` | ~2.5 GB | Razonamiento más potente |

Configura tu modelo de visión local en **Ajustes → Vision → Local Vision Model**.
