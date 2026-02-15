# Contributing to dIKta.me V2

We welcome contributions to the C# / WinUI 3 codebase!

## 🛠️ Tech Stack

- **Language**: C# 12 (.NET 8)
- **UI Framework**: WinUI 3 (Windows App SDK 1.5)
- **Testing**: xUnit + Moq
- **Architecture**: MVVM (CommunityToolkit.Mvvm)

## ⚡ Quick Start

1.  **Prerequisites**:
    - Visual Studio 2022 (Desktop Development workload).
    - .NET 8 SDK.

2.  **Build**:
    ```powershell
    git clone https://github.com/geckogtmx/diktame.git
    dotnet build DiktaMe.sln
    ```

3.  **Test**:
    ```powershell
    dotnet test
    ```

## 📐 Coding Standards

- **Style**: We follow standard C# conventions (`.editorconfig` is included).
- **Trunk-Based**: Small, frequent commits to `main`.
- **Testing**: All new Business Logic (`DiktaMe.Core`) MUST have unit tests.

## 📂 Project Structure

- `src/DiktaMe.App`: The UI (Views, ViewModels).
- `src/DiktaMe.Core`: The Brain (Logic, Providers, Pipelines).
- `tests/`: xUnit test projects.

## 🤝 Code of Conduct

Be kind. We are building tools to help people communicate.
