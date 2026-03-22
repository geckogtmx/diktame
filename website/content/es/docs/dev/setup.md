# Configuración del Entorno (Environment Setup)

¡Bienvenido al código base de dIKta.me V2! Si estás migrando desde el repositorio V1 original de Python/Electron, **por favor, lee esta guía por completo cuidadosamente.** 

Para compilar y ejecutar dIKta.me V2 localmente, debes tener las herramientas del ecosistema de Microsoft configuradas correctamente en tu máquina de desarrollo.

## Requisitos Previos

1.  **Sistema Operativo**: Windows 10 (versión 2004 o superior) o Windows 11. 
2.  **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
3.  **IDE**: [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Community, Professional, o Enterprise). *No uses VS Code como tu compilador principal para WinUI 3, ya que la Recarga Activa (Hot Reload) de XAML y las herramientas de diseño visual son significativamente mejores en VS2022.*

## Cargas de trabajo de Visual Studio 2022

Al instalar o modificar Visual Studio 2022, **debes** seleccionar obligatoriamente las siguientes cargas de trabajo del Instalador de Visual Studio:

*   ☑ **.NET desktop development (Desarrollo de escritorio de .NET)**
*   ☑ **Windows application development (Desarrollo de aplicaciones de Windows)**
    *   *Asegúrate de que el componente opcional `Windows App SDK C++ Templates` esté marcado si planeas tocar el código fuente puente, aunque no es estrictamente necesario para la compilación estándar de C#.*

Alternativamente, puedes instalar las cargas de trabajo requeridas de MAUI/WinUI directamente desde tu terminal interactiva:
```powershell
dotnet workload install maui-windows
```

## Ejecutando la Aplicación

### 1. Restaurar Dependencias
Abre PowerShell o tu terminal preferida en la raíz del directorio del repositorio y restaura los paquetes NuGet:
```powershell
dotnet restore DiktaMe.sln
```

### 2. Configurar la Compilación
La aplicación debe compilarse apuntando a la arquitectura `x64`. Evita las configuraciones `Any CPU` debido a estrictos requerimientos de interoperabilidad nativa (como las DLL de ONNX Runtime y la ejecución de Whisper.net Vulkan).

```powershell
dotnet build DiktaMe.sln -c Debug -p:Platform=x64
```

### 3. Lanzamiento
Puedes ejecutar la aplicación de Windows directamente desde Visual Studio configurando `DiktaMe.App` como tu **Startup Project (Proyecto de Inicio)** principal y haciendo clic en el botón Verde de Reproducir (Unpackaged / Sin empaquetar).

Si prefieres la depuración rápida por terminal:
```powershell
dotnet run --project src/DiktaMe.App/DiktaMe.App.csproj -c Debug -p:Platform=x64
```

---

## Estándares de Calidad del Código

dIKta.me aplica estrictas medidas de calidad de código de forma nativa en la canalización de la compilación. **No puedes confirmar (commit) código fuente que genere advertencias.**

1.  **TreatWarningsAsErrors es True**: Cualquier advertencia menor del compilador (como una variable olvidada no utilizada o una falta de coincidencia de tipo de referencia que acepte valores NULL) fallará inmediatamente la Acción de GitHub `.NET Build` publicamente.
2.  **.editorconfig**: La raíz del repositorio contiene una configuración de tipografía y formato estricta. Visual Studio formateará automáticamente tu archivo de código para que coincida con estos estilos (ej., namespaces con alcance de archivo, llaves obligatorias requeridas, campos privados usando `_camelCase`).
3.  **Analizadores**: Utilizamos activamente `Meziantou.Analyzer` en cada compilación compilada. Asegúrate de que tu IDE resalte debidamente estas prácticas sugerencias.

## Ejecutando Pruebas (Tests)

Antes de enviar una Solicitud de Extracción (Pull Request formal), debes comprobar y verificar exhaustivamente que la lógica empresarial Core (Núcleo) esté totalmente intacta.
Usamos los frameworks **xUnit**, **Moq** y **FluentAssertions**.

```powershell
dotnet test DiktaMe.sln
```

*Nota: La lógica central de la interfaz de usuario (Las Vistas y Modelos de Vista ubicados en `DiktaMe.App`) se prueba exclusivamente de forma manual. No intentes en absoluto escribir pruebas unitarias que requieran o dependan de una huella activa del `DispatcherQueue` directo de la interfaz WinUI.*
