# Proveedores de Modelos de Lenguaje Grande (LLM)

La arquitectura de `DiktaMe.Core` trata a los servicios de IA de formato de texto como proveedores intercambiables. Debido a que soportamos modelos masivos en la Nube y modelos Locales ligeros de forma simultánea, el motor de formato confía enteramente en la interfaz `ILLMProvider` para cerrar la brecha.

Si deseas agregar soporte para un nuevo endpoint de Modelo de Lenguaje (como Groq, TogetherAI, o Google Vertex), simplemente necesitas implementar la interfaz `ILLMProvider`.

## La Interfaz

```csharp
public interface ILLMProvider
{
    // El nombre legible por humanos del proveedor (ej. "Ollama Local")
    string Name { get; }

    // Asegura que la clave API exista o que el servidor local esté realmente ejecutándose
    Task<bool> IsReadyAsync();

    // El bucle de ejecución central para el reemplazo de texto de un solo intento
    Task<string> ProcessTextAsync(
        string input, 
        string systemPrompt, 
        CancellationToken cancellationToken);

    // Streams asíncronos de C# 8.0 para emitir respuestas de chat nativamente
    // Requerido para soportar la superposición de Chat Rápido
    IAsyncEnumerable<string> ProcessChatStreamAsync(
        IEnumerable<ChatMessage> history, 
        string systemPrompt, 
        CancellationToken cancellationToken);
}
```

*Ejemplos en el código base*: `AnthropicProvider.cs`, `GeminiProvider.cs`, `OllamaProvider.cs`, `OpenAICompatProvider.cs`

---

## El Enrutador LLM (LLMRouter)

Exactamente igual que en la arquitectura STT, las Vistas y Modelos de Vista nunca instancian directamente a un proveedor. En su lugar, solicitan el singleton `LLMRouter`.

Cuando se activa una canalización de dictado, el `LLMRouter` determina si el usuario está en "Modo Nube (Cloud Mode)" o "Modo Local (Local Mode)" en la superposición principal del Panel de Control.

*   **Modo Nube (Cloud Mode)**: El Enrutador lee el proveedor de API configurado (ej., `Anthropic`), lee el Modelo de Chat seleccionado por el usuario (ej., `claude-3-5-sonnet-20240620`), y pasa la ejecución al `AnthropicProvider`.
*   **Modo Local (Local Mode)**: El Enrutador omite por completo los ajustes BYOK y crea una instancia exclusivamente del `OllamaProvider`.

### Ingestión de Instrucciones (Prompt Ingestion)

A diferencia de STT, que simplemente devuelve texto en crudo, los proveedores LLM requieren **Instrucciones del Sistema (System Prompts)**. 

dIKta.me soporta infinitos modos personalizados, por lo que el `LLMRouter` también es responsable de inyectar el esquema de instrucciones correcto. Cuando se llama al método `ProcessTextAsync()` de un proveedor, el Enrutador se asegura de pasarle nativamente la *Instrucción en la Nube (Cloud Prompt)* o la *Instrucción Local (Local Prompt)* específica adjunta a ese perfil de Modo de Dictado.

## Agregando un Nuevo Proveedor

1.  Crea `MiNuevoLLMProvider.cs` en `src/DiktaMe.Core/LLM/`.
2.  Implementa `ILLMProvider`. Concéntrate en manejar adecuadamente las excepciones HTTP 429 de Límite de Frecuencia (Rate Limits) y HTTP 401 No Autorizado.
3.  Agrega tu proveedor al enumerador `LlmProviderType`.
4.  Regístralo dentro de `LLMProviderFactory.cs`.
5.  *(Inyección de Dependencias)*: Registra tu nueva clase como un servicio Transitorio (Transient) en `App.xaml.cs`.
