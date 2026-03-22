# Arquitectura UI y MVVM

El proyecto `DiktaMe.App` contiene toda la capa de presentación de la aplicación. Está completamente aislado de la lógica de negocio, la cual está encapsulada de forma nativa dentro de la biblioteca central `DiktaMe.Core`.

Debido a que estamos construyendo una aplicación moderna de WinUI 3 (Windows App SDK), imponemos estrictamente el patrón arquitectónico **Modelo-Vista-Modelo de Vista (MVVM)** en todo el código base utilizando el excelente paquete NuGet `CommunityToolkit.Mvvm`.

## Conceptos Básicos

Entender este patrón es fundamental para contribuir y aportar flujos de trabajo de interfaz de usuario a dIKta.me.

### 1. Vistas (Views - .xaml / .xaml.cs)
Una Vista es estrictamente responsable de renderizar la interfaz de usuario en pantalla y manejar los eventos visuales puros (como animaciones nativas de Windows o lógica de redimensionamiento fluido). 
Las Vistas **no deben** realizar de ninguna manera ninguna lógica de negocio compleja.

*   **Ubicación**: `src/DiktaMe.App/Views/` o `src/DiktaMe.App/Pages/`
*   **Enlace (Binding)**: Las Vistas siempre se vinculan `x:Bind` exclusivamente a una sola interfaz de Modelo de Vista (ViewModel) dedicada.

*Extracto de ejemplo de `ControlPanelPage.xaml`:*
```xml
<Button Command="{x:Bind ViewModel.StartDictationCommand}" Content="Dictate" />
```

### 2. Modelos de Vista (ViewModels - .cs)
Un Modelo de Vista ayuda a cerrar la enorme brecha existente entre los complejos Servicios Core y lo que finalmente muestra la Vista dócilmente. Realiza casi todo el trabajo pesado: maneja con soltura el estado en vivo, formatea debidamente las cadenas para la interfaz, y expone `ICommand`s genéricos que reaccionan pertinentemente a los clics de botones.

*   **Ubicación**: `src/DiktaMe.App/ViewModels/`
*   **Herencia**: Casi todos los ViewModels actuales heredan directamente de `ObservableObject`.

Al utilizar inteligentemente los generadores de código fuente del Community Toolkit, debes siempre decorar rigurosamente los campos privados con `[ObservableProperty]` y los métodos ejecutables con `[RelayCommand]`. El astuto compilador de C# creará por sí solo y en las sombras las propiedades públicas esperadas y el vasto código boilerplate necesario para activar debida y limpiamente los eventos `INotifyPropertyChanged`.

*Ejemplo en `SettingsViewModel.cs`:*
```csharp
[ObservableProperty]
private bool _isDarkModeEnabled;

[RelayCommand]
private async Task SaveSettingsAsync()
{
    await _settingsService.UpdateDarkModeAsync(IsDarkModeEnabled);
}
```

### 3. Inyección de Dependencias (DI)
El archivo `App.xaml.cs` funge como el compositor principal predeterminado. Aprovechamos el versátil framework estándar de `Microsoft.Extensions.DependencyInjection` provisto por el SDK para registrar ágilmente todo el ecosistema y dependencias dentro del robusto contenedor de la aplicación desde justo el momento del arranque.

Si por alguna razón evidente tu valioso ViewModel local requiere acceso puntual al Dispatcher de la UI principal, a la Clase Global Singleton de Ajustes, o tal vez a la intrincada Canalización de Procesamiento de Audio, **estás férreamente obligado a inyectarlo** mediante el constructor respectivo. Jamás instancies arbitrariamente referencias usando clásicos singletons anticuados.

```csharp
public ControlPanelViewModel(
    IDictationPipeline dictationPipeline,
    ISettingsService settingsService)
{
    _dictationPipeline = dictationPipeline;
    _settingsService = settingsService;
}
```

## Estructura de la Interfaz de Usuario

La veloz interfaz de dIKta.me se subdivide esencialmente en tres áreas o superficies principales de interacción:

1.  **System Tray (Bandeja del Sistema)**: Diligentemente manejada por `H.NotifyIcon`. Opera en silencio invariable conformando el punto crítico de control y anclaje persistente de la aplicación integral siempre estacionada como servicio de segundo plano.
2.  **Control Panel (Panel de Control Principal)**: El llamativo módulo suspendido en el aire `AlwaysOnTop` que figura hábilmente de un material acrílico simulado.
3.  **Settings Window (Ventana Principal de Ajustes de Configuración)**: La ventana completa, extensa, y minuciosa de múltiples componentes del clásico tipo `NavigationView` que aloja holgadamente todas las configuraciones posibles que personalizan radical y permanentemente tu app y entorno local.

Cuando inevitablemente planees añadir algún detalle o modernizar los esquemas interactivos de la UI actual de la aplicación de usuario, asegúrate debidamente que tus cambios se atengan de forma integral estrictamente a nuestra elogiada plantilla visual del genial "Diseño Fluido (Fluent Design)" ideado por Microsoft y que cualquier agregado o eliminación relacionada con las cadenas de descripciones de texto permanezcan adecuadamente referenciadas apoyándose invariablemente en la robusta red nativa original que maneja recursos localizados para componentes individuales XAML empleando fielmente identificadores válidos tipo `x:Uid`.
