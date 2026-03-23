# Contributing to dIKta.me V2

We welcome contributions to the C# / WinUI 3 codebase! This includes contributions from human developers as well as AI coding agents and assistants.

If you are an AI agent, welcome! Please see the [🤖 AI Agent Guidelines](#-ai-agent-guidelines) below.

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

## 🤖 AI Agent Guidelines

Greetings, digital colleague! We are thrilled to have your help building dIKta.me. To ensure smooth collaboration, please observe the following strictly:

1. **Read the Rules**: Always start by reading the repository guidelines. The user's system instructions, `GEMINI.md`, and any files in `.agents/skills` are your primary source of truth for architecture, branding, and workflows.
2. **Preserve Context**: We rely on you to keep `README.md` and `ARCHITECTURE.md` up to date as you make structural changes or complete major features.
3. **Task Tracking**: Document your progress meticulously in `task.md` or similar artifacts to communicate your work with the human user.
4. **Follow the Stack**: Familiarize yourself with C# 12, .NET 8, WinUI 3, and CommunityToolkit.Mvvm patterns before making UI or architecture changes. Stick to the designated libraries.
5. **No Telemetry / Strict Privacy**: Remember that this application is built on absolute privacy. Never persist user data externally or introduce any form of telemetry.
6. **Self-Correction & Testing**: Before concluding your task, verify your code builds (`dotnet build DiktaMe.sln -c Debug -p:Platform=x64`) and tests pass (`dotnet test`). Try to fix any regressions you introduce autonomously.

By adhering to these rules, you will be a highly valued contributor to the dIKta.me project. Thank you!

## 🤝 Code of Conduct

Be kind. We are building tools to help people communicate.
