# Developer Setup

## Prerequisites

- **Windows 10 (2004+) or Windows 11**: Required for WinUI 3.
- **Visual Studio 2022** (Community or higher):
    - Workload: **.NET Desktop Development**
    - Individual Component: **Windows App SDK C# Templates**
- **.NET 8 SDK**: `dotnet --version` should be `8.0.xxx`.

## Getting Started

1.  **Clone the Repo**:
    ```bash
    git clone https://github.com/geckogtmx/diktame.git
    cd diktame
    ```

2.  **Restore Dependencies**:
    ```bash
    dotnet restore DiktaMe.sln
    ```

3.  **Build**:
    ```bash
    dotnet build DiktaMe.sln
    ```

4.  **Run**:
    Open `DiktaMe.sln` in Visual Studio and press **F5**, or:
    ```bash
    dotnet run --project src/DiktaMe.App/DiktaMe.App.csproj
    ```

## Development Workflow

- **Trunk-Based**: We commit directly to `main`.
- **Formatting**: We use `dotnet format` in CI (and locally).
- **Tests**: Run `dotnet test` before pushing.
