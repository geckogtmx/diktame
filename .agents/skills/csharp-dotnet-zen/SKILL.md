---
name: csharp-dotnet-zen
description: Comprehensive guide for modern C# 12 and .NET 8 development, focusing on maintainability, performance, and best practices.
---

# C# & .NET Zen

## Mental Model
C# is a language of **clarity and structure**.
We prioritize **compile-time safety** over runtime flexibility.
Modern .NET is **fast, cross-platform, and lean**.

## Core Principles
1.  **Modern Syntax**: embrace C# 12 features (primary constructors, collection expressions).
2.  **Immutability by Default**: Use `readonly` structs, records, and `IReadOnlyList` where possible.
3.  **Async/Await Correctness**: `async Task` all the way down. Never `.Result` or `.Wait()`.
4.  **Dependency Injection**: Loose coupling via constructor injection.

## Critical Anti-Patterns
- **The "Async Void"**: `async void` crashes the process on exception. Use `async Task` (except event handlers).
- **The "Massive Class"**: Classes > 500 lines are a smell. Partial classes are for generated code, not splitting logic.
- **The "Generic Exception"**: `catch (Exception ex)` without rethrowing or specific handling.
- **String Concatenation**: Use `IO.Path.Combine`, interpolated strings `$"{}"`, or `StringBuilder`.

## Instructions
1.  **Project Structure**
    - Use `src/` and `tests/` folders.
    - Treat warnings as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).

2.  **Coding Standards**
    - **Namespace**: File-scoped namespaces (`namespace MyNamespace;`).
    - **Nullability**: Enable nullable reference types (`<Nullable>enable</Nullable>`).
    - **Var vs Type**: Use `var` when the type is obvious (`var stream = new FileStream(...)`), explicit type when not (`int count = GetCount()`).
    - **Naming**: `_camelCase` private fields, `PascalCase` properties/methods, `IInterface` prefix.

3.  **Performance**
    - Use `Span<T>` and `Memory<T>` for buffer manipulation.
    - Prefer `ArrayPool` for large temporary buffers.
    - Profile before optimizing.

4.  **LINQ**
    - Prefer explicit loops for critical hot paths.
    - Use `List<T>.ForEach` or `foreach` instead of `Select` for side effects.

## Ecosystem
- **Logging**: Serilog (structured logging).
- **Testing**: xUnit + Moq + FluentAssertions.
- **Mapping**: AutoMapper (sparingly) or manual mapping.
