---
name: winui-fluent-design
description: Guidelines for building beautiful, native Windows UIs with WinUI 3 and Fluent Design principles.
---

# WinUI 3 & Fluent Design

## Mental Model
WinUI is **declarative**. The UI structure lives in XAML, the data lives in ViewModels.
We build for **Windows**, embracing its native metaphors (Acrylic, Mica, Navigation).
The UI must be **responsive** and handle DPI changes gracefully.

## Core Principles
1.  **Declarative UI**: Define layout and appearance in XAML, not code-behind.
2.  **Resource Dictionaries**: Keep styles, templates, and converters in separate files (`App.xaml`, `Themes/`).
3.  **Visual States**: Use `VisualStateManager` for hover, press, and disabled states.
4.  **Theming**: Respect system theme (Light/Dark) automatically. Use theme resources (`{ThemeResource}`).

## Critical Anti-Patterns
- **Code-Behind Logic**: Put business logic in ViewModels. `x:Bind` is your friend.
- **Hardcoded Colors**: Use theme resources (e.g., `ApplicationPageBackgroundThemeBrush`) instead of `#FFFFFF`.
- **Mixing Paradigms**: Stick to MVVM. Don't manipulate UI elements directly from code-behind unless strictly necessary (e.g. animations).
- **Blocking the UI Thread**: Long-running operations must be `await`ed or run on background threads.

## Instructions
1.  **XAML Structure**
    - Use `Grid` for complex layouts, `StackPanel` for simple lists.
    - Leverage `RelativePanel` for adaptive layouts.

2.  **Controls**
    - Prefer standard WinUI controls (`Button`, `TextBox`, `ListView`).
    - Use `NavigationView` for top-level navigation.
    - Use `ContentDialog` for modal interactions.

3.  **Data Binding**
    - prefer `x:Bind` (compiled binding) over `Binding` for performance.
    - Mode defaults to `OneTime`. Use `Mode=OneWay` or `TwoWay` explicitly.

4.  **Assets**
    - Use `Segoe UI Variable` (default font).
    - Use fluent icons (`FontIcon` with `Segoe Fluent Icons`).

## Resources
- **WinUI 3 Gallery**: Reference app for controls and styles.
- **Fluent Design System**: Microsoft's design language documentation.
