# UI Architecture & MVVM

The `DiktaMe.App` project holds the entire presentation layer of the application. It is completely isolated from the business logic encapsulated inside the `DiktaMe.Core` project library natively.

Because we are building a modern WinUI 3 (Windows App SDK) application, we strictly enforce the **Model-View-ViewModel (MVVM)** architectural pattern throughout the codebase using the `CommunityToolkit.Mvvm` NuGet package.

## Core Concepts

Understanding this pattern is critical to contributing UI workflows to dIKta.me.

### 1. Views (.xaml / .xaml.cs)
A View is strictly responsible for rendering the UI and handling pure visual events (like Windows animations or resizing logic). 
Views **should not** perform any business logic.

*   **Location**: `src/DiktaMe.App/Views/` or `src/DiktaMe.App/Pages/`
*   **Binding**: Views always bind `x:Bind` exclusively to a dedicated ViewModel interface.

*Example excerpt of `ControlPanelPage.xaml`:*
```xml
<Button Command="{x:Bind ViewModel.StartDictationCommand}" Content="Dictate" />
```

### 2. ViewModels (.cs)
A ViewModel bridges the gap between the Core Services and the displayed View. It does the heavy lifting: managing state, formatting strings for the UI, and exposing `ICommand`s that act on button clicks.

*   **Location**: `src/DiktaMe.App/ViewModels/`
*   **Inheritance**: Almost all ViewModels inherit from `ObservableObject`.

By utilizing the Community Toolkit source generators, you should decorate private fields with `[ObservableProperty]` and methods with `[RelayCommand]`. The C# compiler will automatically generate the public properties and boilerplate `INotifyPropertyChanged` firing events for you in the background.

*Example excerpt of `SettingsViewModel.cs`:*
```csharp
[ObservableProperty]
private bool _isDarkModeEnabled;

[RelayCommand]
private async Task SaveSettingsAsync()
{
    await _settingsService.UpdateDarkModeAsync(IsDarkModeEnabled);
}
```

### 3. Dependency Injection (DI)
The `App.xaml.cs` acts as the root composer. We utilize the standard `Microsoft.Extensions.DependencyInjection` framework to register everything inside the application container at startup.

If your ViewModel requires access to the UI Dispatcher, Settings, or the Audio Pipeline, you **must injection it** via the constructor. Do not use singleton references arbitrarily.

```csharp
public ControlPanelViewModel(
    IDictationPipeline dictationPipeline,
    ISettingsService settingsService)
{
    _dictationPipeline = dictationPipeline;
    _settingsService = settingsService;
}
```

## UI Structure

The dIKta.me interface consists of three primary interaction surfaces:

1.  **System Tray**: Handled by `H.NotifyIcon`. It serves as the silent anchor point while the application is backgrounded.
2.  **Control Panel**: The floating, `AlwaysOnTop` quick-access HUD utilizing an acrylic window backdrop.
3.  **Settings Window**: The comprehensive, multi-tab `NavigationView` interface for complex provider routing and configuration updates.

When modifying UI components, be sure to verify your changes match our Fluent Design aesthetic and that text updates are appropriately migrated via the Windows `x:Uid` localization resources.
