# dIKta.me V2

**Speech-to-text dictation for Windows** — C# + WinUI 3 rewrite.

> Formerly *dIKtate*. Full rewrite from Python + Electron to native Windows application.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **UI** | WinUI 3 (Fluent Design) |
| **Logic** | C# / .NET 8 |
| **STT** | Cloud (Deepgram, Gemini) + Local (Whisper.net) |
| **LLM** | Gemini, Anthropic, OpenAI, Ollama |
| **Data** | SQLite (Microsoft.Data.Sqlite) |
| **Installer** | Native AOT, < 30MB |

## Solution Structure

```
DiktaMe.sln
├── src/
│   ├── DiktaMe.App/          # WinUI 3 application (UI layer)
│   └── DiktaMe.Core/         # Business logic (class library)
│       ├── Audio/             # NAudio recording, device management
│       ├── STT/               # Speech-to-Text providers
│       ├── LLM/               # LLM providers
│       ├── Pipeline/          # Workflow orchestration
│       ├── Input/             # Hotkeys, text injection
│       ├── Config/            # Settings, profiles, snippets
│       ├── Data/              # SQLite history, metrics
│       ├── Security/          # DPAPI secrets, PII scrubber
│       └── System/            # Capabilities, Ollama management
└── tests/
    └── DiktaMe.Core.Tests/    # xUnit + Moq + FluentAssertions
```

## Workflow Modes

| # | Mode | Hotkey | Description |
|---|------|--------|-------------|
| 1 | **Dictate** | `Ctrl+Alt+D` | Voice → Text injection |
| 2 | **Refine** | `Ctrl+Alt+R` | Selection improvement |
| 3 | **Ask** | `Ctrl+Alt+A` | Voice Q&A |
| 4 | **Translate** | `Ctrl+Alt+T` | EN↔ES bidirectional |
| 5 | **Oops** | `Ctrl+Alt+V` | Re-inject last text |
| 6 | **Note** | `Ctrl+Alt+N` | Voice post-it notes |

## Development

### Prerequisites

- .NET 8 SDK
- Windows 10 (version 2004+) or Windows 11
- Windows App SDK workload: `dotnet workload install maui-windows`

### Build & Test

```bash
dotnet build DiktaMe.sln
dotnet test DiktaMe.sln
```

### Git Conventions

- **Trunk-based** development (commits directly to `main`)
- **Conventional Commits**: `feat(scope): description [TASK_ID]`
- See `DEVELOPMENT_ROADMAP.md` §9 for full strategy

## Status

| Phase | Status |
|-------|--------|
| A.0 — Git Repo Prep | ✅ Complete |
| A.1 — Solution Scaffold | ✅ Complete |
| A.2 — Native AOT Config | ⬜ Pending |
| B.x — Core Engine | ⬜ Pending |
| C.x — STT & LLM Providers | ⬜ Pending |
| D.x — Pipeline Orchestration | ⬜ Pending |

---

*V2 rewrite of [dIKtate](https://github.com/geckogtmx/diktate) — see `DEVELOPMENT_ROADMAP.md` for the full plan.*
