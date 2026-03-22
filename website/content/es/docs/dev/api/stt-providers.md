# Proveedores de Voz a Texto (STT)

La arquitectura de `DiktaMe.Core` trata a todos los servicios de IA externos como complementos (plugins) intercambiables en caliente a través de interfaces estrictamente definidas. 

Si deseas agregar soporte para un nuevo servicio de Voz a Texto (como Azure Speech o Google Cloud Speech-to-Text), simplemente necesitas crear una clase que cumpla con nuestra interfaz `ISTTProvider` y registrarla dentro del marco de Inyección de Dependencias.

## Las Interfaces

Hay dos interfaces principales que detallan lo que un servicio STT es capaz de hacer.

### 1. `ISTTProvider` (Reconocimiento por Lotes - Batch)

Esta es la base. Cualquier clase que implemente esto es capaz de recibir un arreglo de bytes monolítico de audio WAV y devolver una única transcripción finalizada.

```csharp
public interface ISTTProvider
{
    // El nombre legible por humanos del proveedor (ej. "Whisper.net Local")
    string Name { get; }
    
    // Comprueba si se adquirieron las claves API o los modelos fuera de línea están listos
    Task<bool> IsReadyAsync();

    // El bucle de ejecución central
    Task<string> TranscribeAsync(byte[] wavData, string language, CancellationToken cancellationToken);
}
```

*Ejemplos en el código base*: `WhisperProvider.cs`

### 2. `IStreamingSTTProvider` (Reconocimiento En Vivo)

Si un proveedor soporta la ingestión en tiempo real por WebSocket (donde las transcripciones llegan secuencialmente mientras el usuario aún está hablando), debe implementar esta interfaz que amplía el `ISTTProvider` base.

```csharp
public interface IStreamingSTTProvider : ISTTProvider
{
    // Streams asíncronos de C# 8.0 para emitir el texto a medida que llega por la red
    IAsyncEnumerable<string> TranscribeStreamAsync(
        IAsyncEnumerable<byte[]> audioStream, 
        string language, 
        CancellationToken cancellationToken);
}
```

*Ejemplos en el código base*: `DeepgramProvider.cs`, `GeminiAudioProvider.cs`

---

## El Enrutador STT (`STTRouter`)

dIKta.me no codifica implementaciones de proveedores en la lógica de la vista. En cambio, los ViewModels solicitan el singleton `STTRouter`.

El `STTRouter` actúa como un director de tráfico. Su tarea es leer la configuración `$App.Settings.ActiveSttProvider` del usuario, acceder al contenedor DI a través de `ISTTProviderFactory`, y devolver dinámicamente el proveedor correcto.

### Mecanismo de Reserva (Fallback Mechanism)

Dado que admitimos la modalidad Trae Tu Propia Clave (BYOK), el `STTRouter` tiene un mecanismo de seguridad vital integrado en su lógica de enrutamiento. 

Si un usuario selecciona `Deepgram` como su proveedor pero en realidad no ha guardado una clave API, la comprobación `IsReadyAsync()` fallará. Cuando esto sucede, el Enrutador recurrirá **silenciosamente** al `WhisperProvider` (ejecución local) para que la tecla de dictado del usuario no termine bloqueando la aplicación.

## Agregando un Nuevo Proveedor

1.  Crea `MiNuevoSttProvider.cs` en `src/DiktaMe.Core/STT/`.
2.  Implementa `ISTTProvider`.
3.  Agrega tu proveedor al enumerador de selección `SttProviderType`.
4.  Regístralo lógicamente dentro de la declaración switch en `STTProviderFactory.cs`. 
5.  *(Inyección de Dependencias)*: Registra tu nueva clase como un servicio Transitorio (Transient) en `App.xaml.cs`.
