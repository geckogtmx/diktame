# Refinar (Refine)

Mientras que el Dictado se usa para generar texto *nuevo*, la canalización de **Refinar (Refine)** se usa para editar y manipular texto *existente* directamente en su lugar.

Cuando activas Refinar, dIKta.me copia el texto que has resaltado, lo envía a una IA junto con tus instrucciones, y reemplaza instantáneamente tu texto original con la versión reescrita.

## Cómo usar Refinar

Hay dos formas principales de usar la canalización de Refinar, determinadas por la opción "Voice Instruction" (Instrucción de Voz) en `Settings -> General`.

### 1. Modo Piloto Automático (Predeterminado)
En el modo Piloto Automático, Refinar actúa como un transformador de texto de 1 clic utilizando una instrucción del sistema predefinida.

1. Resalta un bloque de texto en cualquier aplicación.
2. Presiona la tecla de **Refinar** (Predeterminado: `Ctrl + Alt + R`).
3. El texto seleccionado se captura al instante, es procesado por el LLM utilizando el perfil activo de Refinar, y es reemplazado.

*Caso de Uso de Ejemplo*: Configuras tu instrucción del sistema de Refinar como `"Corrige todos los errores de ortografía y gramática, pero mantén el tono original."` Ahora, cada vez que resaltes el borrador de un correo electrónico y presiones `Ctrl+Alt+R`, será revisado y corregido al instante.

### 2. Modo Instrucción de Voz
En el modo Instrucción de Voz, dictas dinámicamente *cómo* quieres que cambie el texto.

1. Resalta un bloque de texto.
2. Mantén presionada la tecla de **Refinar** (`Ctrl + Alt + R`).
3. Di una instrucción (ej., *"Haz que esto suene más profesional"*, *"Traduce esto al español"*, *"Resume esto en 3 viñetas"*).
4. Suelta la tecla.

dIKta.me transcribirá tu instrucción, la combinará con el texto que resaltaste, y pedirá al LLM que aplique tu instrucción al texto antes de reemplazarlo.

> [!TIP]
> **Respaldo a Preguntar (Ask)**: Si estás en el modo Instrucción de Voz, pero olvidas resaltar algún texto antes de hablar, dIKta.me cambiará automáticamente a la canalización de **Preguntar (Ask)** e intentará responder tu instrucción directamente.

## Cómo captura el texto seleccionado dIKta.me
dIKta.me no emplea API intrusivas de lectura de pantalla para ver lo que has resaltado. En su lugar, guarda temporalmente tu portapapeles, simula rápidamente presionar `Ctrl + C` (Copiar) para capturar la selección, y luego restaura el contenido original de tu portapapeles.

Debido a que usa `Ctrl + C` y `Ctrl + V`, Refinar funciona universalmente en casi todas las aplicaciones de Windows.

## Configurando la instrucción de Refinar

Al igual que el Dictado, Refinar opera usando Perfiles configurados en la pestaña de **Modos (Modes)** de la ventana de ajustes.

Para cambiar cómo se comporta el modo Piloto Automático, o para darle a la IA contexto de fondo para tus Instrucciones de Voz:
1. Abre el Panel de Control y haz clic en el engranaje de **Ajustes**.
2. Navega a la pestaña de **Modos (Modes)**.
3. Selecciona la canalización **Refinar (Refine)**.
4. Modifica la Instrucción del Sistema para tu perfil Cloud (Nube) o Local.
