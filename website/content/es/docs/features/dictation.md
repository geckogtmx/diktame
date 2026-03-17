# Dictado

El dictado es la función principal de dIKta.me, diseñada para convertir tu voz en texto altamente preciso y perfectamente formateado, inyectándolo directamente en cualquier aplicación.

> [!TIP]
> Esta guía cubre los modos principales de dictado. Para editar texto existente con instrucciones, consulta la guía de [Refinar](refine.md).

## Cómo funciona

A grandes rasgos, el flujo estándar de dictado sigue estos pasos:
1. **Grabar**: Presionas la tecla de acceso rápido de dictado y dIKta.me captura tu voz.
2. **Transcribir (STT)**: El audio se envía a tu proveedor de IA de voz a texto (como Deepgram o Whisper) para convertirlo en texto sin formato.
3. **Procesar (LLM)**: El texto sin formato se pasa a un modelo de lenguaje de IA (como Gemini o OpenAI) usando tu **Prompt de sistema** personalizado para dar formato, puntuar o reescribir el texto.
4. **Inyectar**: El texto final pulido se inyecta en la ventana activa en la posición de tu cursor, al instante.

Como dIKta.me utiliza tu portapapeles para pegar el texto rápidamente, guarda temporalmente el contenido existente de tu portapapeles, pega el dictado y luego restaura tu portapapeles de forma transparente.

## Iniciar un dictado

Para empezar a dictar, coloca tu cursor donde quieras escribir (por ejemplo, MS Word, un navegador web, Slack) y presiona la tecla de acceso rápido de **Dictar**:

`Ctrl + Alt + D` (Por defecto)

El panel de control HUD cambiará para mostrar que está **Grabando**. Cuando termines de hablar, suelta la tecla de acceso rápido (si usas Mantener para hablar) o presiona la tecla de acceso rápido de nuevo para detener. Una vez que el procesamiento se complete, el texto aparecerá.

![Panel de control grabando](/docs/assets/control-panel-recording.png)

## Dictado en streaming vs. por lotes

dIKta.me ofrece dos formas distintas de transcribir tu voz, dependiendo de tu proveedor STT seleccionado y la configuración.

### 1. Dictado por lotes (Por defecto)
En el modo por lotes, dIKta.me espera hasta que hayas terminado de hablar y presionado "Detener" antes de enviar el audio al proveedor STT.
- **Ventajas**: Permite el procesamiento LLM. Puedes aplicar prompts muy específicos (como "Reescribe esto como si fueras un pirata" o "Formatea como una lista con viñetas") porque la IA tiene el contexto completo de la frase antes de reescribirla.
- **Desventajas**: Mayor tiempo hasta obtener el texto, ya que debes esperar a que la grabación termine antes de que comience el procesamiento.

### 2. Dictado en streaming
En el modo de streaming, tu audio se envía al proveedor STT (actualmente compatible con Deepgram) en tiempo real, fragmento por fragmento a través de un WebSocket. Mientras hablas, las palabras aparecen en tu pantalla casi al instante.
- **Ventajas**: Extremadamente rápido, retroalimentación en tiempo real.
- **Desventajas**: Como las palabras se inyectan a medida que las dices, **el dictado en streaming omite completamente el procesamiento LLM (Modo sin formato).** No puedes usar prompts de formato del sistema mientras estés en streaming.

Puedes activar el streaming explícitamente en `Configuración -> General` si tu proveedor activo lo admite.

## Configuración de inyección

dIKta.me te permite personalizar *cómo* el texto llega a tu aplicación. En la ventana de Configuración (pestaña General), puedes configurar:

* **Espacio final**: Cuando está activado (por defecto), dIKta.me añade automáticamente un espacio en blanco después de tu texto dictado. Esto te permite continuar dictando la siguiente frase inmediatamente sin tener que presionar manualmente la barra espaciadora.
* **Tecla adicional**: Puedes indicarle a dIKta.me que simule una pulsación de teclado inmediatamente después de inyectar el texto. Las opciones útiles incluyen:
  * `Enter` / `Return`: Perfecto para dictar mensajes de chat y enviarlos al instante.
  * `Tab`: Útil para navegar hojas de cálculo o formularios.

## Presets de dictado (Personalización de modos)

¡No estás limitado a un solo estilo de dictado! Usando los **Modos de dictado**, puedes crear múltiples **Presets** personalizados (por ejemplo, "Estándar", "Comentarios de código", "Jerga médica") y alternar rápidamente entre ellos usando el menú desplegable en el panel de control.

Esto te permite cambiar el comportamiento y las reglas de formato de tu IA al instante, dependiendo de en qué estés trabajando.

Consulta la guía de [Configuración de modos de dictado](../settings/dictation-modes.md) para instrucciones completas sobre cómo crear, editar y gestionar tus presets personalizados.
