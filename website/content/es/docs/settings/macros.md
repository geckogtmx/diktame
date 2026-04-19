# Ajustes de Macros

La pestaña **Macros** proporciona un motor de expansión de texto instantáneo y robusto integrado nativamente en la canalización de inyección de texto de dIKta.me. 

Dado que el formato de dictado a veces puede ser prolijo, los Macros te permiten definir comandos de atajo altamente personalizados que dIKta.me expande automáticamente en bloques de texto complejos, plantillas o datos repetitivos justo en el momento en que se inyectan en tu aplicación activa.

## Cómo funcionan los Macros

Un Fragmento actúa como una macro rápida. Cuando las canalizaciones de Voz a Texto y LLM terminan completamente de procesar, dIKta.me ejecuta un barrido final sobre el texto completado buscando tus Palabras Activadoras (Trigger Words) exactas de los Macros.

Si encuentra una, reemplaza inmediatamente la palabra activadora con el contenido expandido del fragmento y pega el texto final.

> [!TIP]
> **Expansión Post-LLM**: Debido a que los Macros se expanden *después* de que el LLM ha terminado de formatear el texto, se garantiza que el LLM no alucine accidentalmente, formatee mal o reformule tus caracteres de plantilla estrictamente definidos. Es un reemplazo de cadena literal absoluto.

## Creación de Macros

Dentro de la pestaña de ajustes de Macros, puedes agregar, configurar y eliminar Macros al instante.

Cada fragmento requiere dos campos:
*   **Trigger Word (Palabra Activadora)**: La secuencia exacta de caracteres que deseas detectar (ej. `//micorreo`, `@@firma`, `/plantilla1`).
*   **Replacement Content (Contenido de Reemplazo)**: El bloque de texto exacto y literal en el que deseas que se expanda. Soporta plantillas de múltiples líneas, exenciones de responsabilidad legales específicas o URLs complicadas.

### Ejemplo de Dictado
Si creas un fragmento nativo donde:
*   Activador = `:reunion:`
*   Contenido = `Notas de la Reunión:\nAsistentes: \nResumen: \nElementos de Acción: `

Si luego dices en voz alta al micrófono:
> *"Hola equipo, echemos un vistazo a la estructura :reunion:."*

dIKta.me lo formateará sin problemas en tu cursor como:
> *Hola equipo, echemos un vistazo a la estructura Notas de la Reunión:
> Asistentes: 
> Resumen: 
> Elementos de Acción: .*

## Mejores Prácticas
1.  **Activadores Únicos**: Precede siempre tus Palabras Activadoras con puntuación especial (como `:` o `//` o `@@`) para garantizar que dIKta.me no expanda accidentalmente una palabra normal que podrías decir dentro de una oración regular.
2.  **Alternar Macros**: Puedes habilitar o deshabilitar globalmente todo el motor de expansión de Macros de forma segura en la parte superior de la página de Ajustes si no deseas activar plantillas por accidente.
