# Proveedores de Texto a Voz (TTS)

dIKta.me implementa una estrategia de ejecución multinivel y flexible para la generación de Texto a Voz (TTS), habilitando tanto la inferencia local fuera de línea mediante ONNX como la generación en la nube de alta calidad a través de una interfaz de proveedor unificada.

## Arquitectura de Proveedores

Todos los motores de TTS implementan la interfaz `ITTSProvider`, lo que permite que el servicio `TtsSpeaker` enrute las solicitudes sin problemas.

```csharp
public interface ITTSProvider : IDisposable
{
    string ProviderName { get; }
    bool SupportsStreaming { get; }

    /// <summary>Sintetizar texto a bytes de audio (generación completa).</summary>
    Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Comprobar si el proveedor está listo (modelo cargado / API accesible).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
```

El objeto `TtsResult` encapsula el audio sintetizado junto con los metadatos, incluyendo el arreglo de bytes PCM/WAV, la frecuencia de muestreo, y la latencia de generación.

## Proveedores Disponibles

### KokoroTtsProvider (Local)
El proveedor local predeterminado utiliza `KokoroSharp`, envolviendo los modelos Kokoro-ONNX altamente eficientes (82M de parámetros).
* **Ejecución:** Se ejecuta en proceso con cero dependencias externas.
* **Variantes:** Soporta modelos de precisión `int8` (88 MB), `fp16` (169 MB), y `fp32` (310 MB) a través de `Microsoft.ML.OnnxRuntime`.
* **Latencia:** Genera voz típicamente en 300–500ms.
* **Cumplimiento de Licencia:** El consumo interno de Kokoro del fonetizador `eSpeak-NG` (GPLv3) se aísla de las invocaciones del proceso bajo "mera agregación", requiriendo solo atribución.

### DeepgramTtsProvider (Nube Predeterminado)
Aprovecha la integración existente del SDK .NET de `Deepgram` para ejecutar los modelos Aura-2 TTS utilizando la misma clave API implementada para las operaciones STT del dictado principal. Proporciona 90-200ms TTFB a través de HTTP/WebSockets.

### InworldTtsProvider & OpenAITtsProvider (Nube Premium)
Implementa clientes REST estándar para integraciones HTTP externas utilizando patrones estándar `HttpClient` de ASP.NET. Las claves de autenticación se inyectan dinámicamente mediante el almacenamiento seguro `SecureStorage` (DPAPI).

## Ciclo de Vida y Enrutamiento

La fábrica `TTSProviderFactory` inicializa y almacena en caché las instancias del `ITTSProvider` para prevenir asignaciones redundantes o la recarga constante de los modelos ONNX locales.

Durante las solicitudes de reproducción, el Enrutador `TTSRouter` evalúa la configuración activa `TtsSettings` de la aplicación. Puede retroceder dinámicamente a proveedores alternativos si la API elegida no está disponible o si el modelo local falló al inicializarse.

### Canalización del Reproductor de Audio

El audio se transmite directa y subsecuentemente a través del servicio `TtsPlayerService` aprovechando el componente `WasapiOut` de la librería NAudio. La instancia del reproductor se sincroniza con el atenuador `AudioDucker` para disminuir el volumen del sistema de las aplicaciones en segundo plano durante la reproducción activa. La reproducción se puede interrumpir instantáneamente evaluando los estados de las teclas de acceso rápido o las transiciones de estado de la aplicación (ej., iniciar una nueva instancia de dictado).

### Desinfección de Texto

El formato Markdown en bruto de las respuestas del LLM se desinfecta de forma estricta mediante la clase estática `TextCleaner` antes de su procesamiento. Esto elimina marcadores de cabeceras (`#`), transforma las viñetas de las listas en pausas, expande símbolos comunes (`$` a "dólares"), y trunca agresivamente los límites de salida excesivamente largos antes de la inferencia.
