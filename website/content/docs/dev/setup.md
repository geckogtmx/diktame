# Environment Setup

Welcome to the dIKta.me V2 codebase! If you are migrating over from the V1 Python/Electron repository, **please read this entire guide carefully.** 

To build and run dIKta.me V2 locally, you must have the Microsoft ecosystem tools properly configured on your machine.

## Prerequisites

1.  **Operating System**: Windows 10 (version 2004 or higher) or Windows 11. 
2.  **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
3.  **IDE**: [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Community, Professional, or Enterprise). *Do not use VS Code as your primary compiler for WinUI 3, as the XAML Hot Reload and designer tooling are significantly better in VS2022.*

## Visual Studio 2022 Workloads

When installing or modifying Visual Studio 2022, you **must** select the following workloads from the Visual Studio Installer:

*   ☑ **.NET desktop development**
*   ☑ **Windows application development**
    *   *Ensure the Optional component `Windows App SDK C++ Templates` is checked if you plan to touch bridging code, though it is not strictly required for standard C# compilation.*

Alternatively, you can install the required MAUI/WinUI workloads directly from your terminal:
```powershell
dotnet workload install maui-windows
```

## Running the Application

### 1. Restore Dependencies
Open PowerShell or your preferred terminal in the repository root and restore the NuGet packages:
```powershell
dotnet restore DiktaMe.sln
```

### 2. Configure the Build
The application must be built targeting `x64` architecture. Avoid `Any CPU` configurations due to native interop requirements (like ONNX Runtime and Whisper.net Vulkan DLLs).

```powershell
dotnet build DiktaMe.sln -c Debug -p:Platform=x64
```

### 3. Launching
You can run the application directly from Visual Studio by setting `DiktaMe.App` as the **Startup Project** and clicking the Green Play button (Unpackaged).

If you prefer terminal debugging:
```powershell
dotnet run --project src/DiktaMe.App/DiktaMe.App.csproj -c Debug -p:Platform=x64
```

---

## Code Quality Standards

dIKta.me enforces strict code quality measures natively in the build pipeline. **You cannot commit code that generates warnings.**

1.  **TreatWarningsAsErrors is True**: Any compiler warning (like an unused variable or a nullable reference type mismatch) will immediately fail the `.NET Build` GitHub Action.
2.  **.editorconfig**: The repository root contains a strict formatting config. Visual Studio will automatically format your code to match these styles (e.g., file-scoped namespaces, required braces, `_camelCase` private fields).
3.  **Analyzers**: We utilize `Meziantou.Analyzer` on every build. Ensure your IDE is highlighting these suggestions.

## Running Tests

Before submitting a Pull Request, you must verify the Core business logic is intact.
We use **xUnit**, **Moq**, and **FluentAssertions**.

```powershell
dotnet test DiktaMe.sln
```

*Note: UI logic (Views and ViewModels in `DiktaMe.App`) is tested manually. Do not attempt to write unit tests that require an active WinUI `DispatcherQueue` footprint.*
