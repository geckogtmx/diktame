# Ajustes de Vision

La página de ajustes de **Vision** controla cómo dIKta.me captura tu pantalla, qué modelos de IA procesan imágenes y video, y qué ocurre con los resultados.

---

## Modelos de IA

### Proveedor / Modelo de Visión en la Nube

El proveedor y modelo de nube que se utiliza cuando se selecciona la alternancia **Cloud** en el panel de acciones de Vision.

*   **Proveedor predeterminado**: Gemini
*   **Modelo predeterminado**: `gemini-2.5-flash`

Cualquier modelo con capacidad de visión de tu proveedor de nube configurado funciona aquí:

| Proveedor | Modelo recomendado |
|---|---|
| Gemini | `gemini-2.5-flash` (predeterminado), `gemini-2.0-flash` |
| Anthropic | `claude-opus-4-5`, `claude-sonnet-4-5` |
| OpenAI | `gpt-4o`, `gpt-4o-mini` |

### Modelo de Visión Local

El modelo de Ollama que se utiliza cuando se selecciona la alternancia **Local**.

*   **Predeterminado**: `minicpm-v`
*   Debe ser un modelo con capacidad de visión descargado en Ollama antes de usarlo.

| Modelo | Comando de descarga | VRAM |
|---|---|---|
| `minicpm-v` (predeterminado) | `ollama pull minicpm-v` | ~2 GB |
| `moondream` | `ollama pull moondream` | ~1.2 GB |
| `llava-phi3` | `ollama pull llava-phi3` | ~2.5 GB |

---

## Comportamiento de Captura

### Consulta Predeterminada

El texto que se envía a la IA cuando envías sin escribir ni grabar una pregunta.

**Predeterminado:** `Describe what you see and extract any visible text.`

### Grabación Automática de Consulta de Voz

Cuando está activado, el micrófono empieza a grabar automáticamente después de tomar una captura de pantalla para que puedas hablar tu pregunta directamente.

*   **Predeterminado**: Activado
*   Se detiene cuando transcurre el **Tiempo de espera de consulta** sin detectar voz.

### Tiempo de Espera de Consulta (segundos)

Cuánto tiempo espera dIKta.me la entrada de voz antes de continuar con la consulta predeterminada.

*   **Predeterminado**: 10 segundos

### Dimensión Máxima de Imagen (px)

El lado más largo que puede tener una imagen antes de que dIKta.me la redimensione antes de enviarla a la IA.

*   **Predeterminado**: 2 048 px
*   Si la imagen sigue superando 1 MB después de redimensionarla, se recodifica como JPEG con calidad del 85 %.

---

## Comportamiento de Salida

Cada acción de Vision tiene su propia alternancia **Inject at cursor**. Cuando está activada, la respuesta de la IA se escribe en la ventana activa en la posición del cursor. Cuando está desactivada, la respuesta va únicamente al portapapeles.

| Acción | Inject at cursor (predeterminado) |
|---|---|
| Acción Clipboard | Activado |
| Acción OCR | Activado |
| Color Picker | Activado |
| Video AI (Describe / Document / Bug Report) | Activado |

---

## Grabación de Video

### Calidad de Video

| Ajuste | Tasa de bits | Ideal para |
|---|---|---|
| Low | ~2 500 kbps | Grabaciones largas, espacio en disco limitado |
| **Medium** (predeterminado) | ~5 000 kbps | Uso general |
| High | ~10 000 kbps | Contenido de pantalla detallado, texto fino |

La frecuencia de fotogramas es fija a 30 fps.

### Audio del Micrófono

Captura tu micrófono durante la grabación. Utiliza el mismo dispositivo de entrada configurado en **Ajustes → Audio**.

*   **Predeterminado**: Activado

### Audio del Sistema

Captura el audio que se reproduce en tu computadora (aplicaciones, pestañas del navegador, etc.).

*   **Predeterminado**: Activado

### Burbuja de Cámara Web

Superpone una transmisión de cámara web en imagen en imagen en la esquina inferior derecha de la grabación.

*   **Predeterminado**: Activado
*   **Tamaño**: Ancho de la burbuja en píxeles (predeterminado: 200 px, siempre en proporción 16:9).
*   dIKta.me da preferencia automáticamente a una cámara USB sobre la cámara web integrada.

> [!NOTE]
> Si no hay ninguna cámara conectada, la burbuja de cámara web se omite silenciosamente.

### Duración Máxima de Grabación (segundos)

La grabación se detiene automáticamente después de este número de segundos aunque no hayas hecho clic en Stop.

*   **Predeterminado**: 120 segundos

---

## Avanzado

### Ollama Keep-Alive (segundos)

Cuánto tiempo mantiene Ollama el modelo de visión local cargado en VRAM después de la última llamada de inferencia.

*   **Predeterminado**: 300 segundos (5 minutos)
*   Aumenta este valor si tomas varias capturas de pantalla en rápida sucesión y quieres evitar recargar el modelo cada vez.

### Máximo de Tokens de Respuesta

Límite superior de los tokens que la IA puede devolver para una consulta de visión.

*   **Predeterminado**: 4 096

### Temperature

Controla qué tan literal o creativa es la respuesta de la IA.

*   **Predeterminado**: 0.3
*   Mantén el valor bajo para OCR y extracción de tablas. Auméntalo ligeramente para tareas descriptivas.
