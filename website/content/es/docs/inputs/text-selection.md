# Selección de Texto

El texto resaltado en cualquier aplicación de Windows es una entrada válida para dIKta.me. Selecciona algo de texto, pulsa un atajo, y la selección se convierte en materia prima para una pipeline de IA — reescrita, respondida, traducida o leída en voz alta.

![Texto resaltado en un editor de Windows](/images/docs/input-text-selection.png)

> [!TIP]
> La selección de texto es la entrada más silenciosa. La app lee tu portapapeles, opera sobre él y lo restaura después de inyectar el resultado — así que nada en tu flujo normal con el portapapeles cambia.

## Cómo capturar una selección de texto

Selecciona texto en cualquier app (Word, Slack, VS Code, un navegador — lo que sea), luego pulsa uno de los atajos sensibles a la selección:

| Atajo | Acción | Qué ocurre |
|---|---|---|
| `Ctrl+Alt+R` | [Refinar](../features/refine.md) | Reescribe la selección in situ — limpieza, reformateo, estilo |
| `Ctrl+Alt+A` | [Preguntar](../features/ask.md) | Usa la selección como contexto para una pregunta hablada |
| `Ctrl+Alt+T` | [Traducir](../features/translate.md) | Traduce la selección a tu idioma destino |
| `Ctrl+Alt+F` | Leer Selección | Lee el texto seleccionado en voz alta vía TTS |

Todos los atajos son reasignables en [Configuración → Atajos de Teclado](../settings/hotkeys.md).

## Cómo funciona por dentro

1. Pulsas un atajo de selección.
2. dIKta.me copia brevemente tu selección al portapapeles (guardando tu contenido original).
3. El texto seleccionado fluye por la pipeline de IA que elegiste.
4. El resultado se inyecta donde está tu cursor y se restaura tu portapapeles original.

Como dIKta.me usa el portapapeles estándar de Windows, **la selección de texto funciona en todas las apps que soporten copiar/pegar** — sin plugins, sin integraciones.

## Variantes de Refinar

Refinar es la salida principal para la selección de texto, y viene en dos sabores:

- **Refinar (Auto)** — limpieza automática de estilo/gramática, sin necesidad de instrucción por voz.
- **Refinar (Verbal)** — hablas una instrucción (*"hazlo más formal"*, *"conviértelo en una lista con viñetas"*) y la IA la aplica a la selección.

Configura ambos en [Configuración → Pipelines](../settings/pipelines.md).

## Local vs. nube

Las salidas de selección de texto se ejecutan sobre el LLM que configures globalmente (o por preset). Elige Gemini / OpenAI / Anthropic / OpenRouter en la nube, u Ollama con cualquier modelo compatible localmente. Consulta [Configuración → Motor IA](../settings/ai-engine.md).
