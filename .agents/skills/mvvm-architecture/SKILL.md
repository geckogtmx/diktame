---
name: mvvm-architecture
description: Patterns for robust MVVM implementation using the CommunityToolkit.Mvvm library.
---

# MVVM Architecture

## Mental Model
The **ViewModel** is the model of the View. It has no reference to the View.
The **View** knows about the ViewModel (via DataContext).
The **Model** knows nothing about the View or ViewModel.

## Core Principles
1.  **CommunityToolkit.Mvvm**: Use the source generators (`[ObservableProperty]`, `[RelayCommand]`).
2.  **State Management**: ViewModels hold the state. Views reflect it.
3.  **Commands**: User actions are commands (`IRelayCommand`).
4.  **Messaging**: Use `WeakReferenceMessenger` for loose coupling between ViewModels.

## Critical Anti-Patterns
- **God ViewModels**: Split large ViewModels into smaller, focused services or child ViewModels.
- **View References in VM**: Never pass a UI element (e.g., `Button`, `TextBox`) to a ViewModel.
- **Calling View Methods**: Use events or messages if the VM needs to trigger a View action (e.g., scroll to bottom).
- **Logic in Converters**: Converters should be purely for display formatting, not business rules.

## Instructions
1.  **ViewModel Setup**
    - Inherit from `ObservableObject`.
    - Partial classes are required for source generators.

    ```csharp
    public partial class MyViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [RelayCommand]
        private void Save() { ... }
    }
    ```

2.  **Dependency Injection**
    - Inject services (e.g., `INavigationService`, `IDataService`) into ViewModel constructors.
    - Register ViewModels as `Transient` or `Singleton` in `App.xaml.cs`.

3.  **Navigation**
    - Use a navigation service to switch Views.
    - Pass parameters via navigation arguments, processed by the receiving ViewModel.

4.  **Validation**
    - Use `ObservableValidator` for input validation if needed.
