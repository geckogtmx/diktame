# ![dIKta.me](docs/images/ReadmeHead.png)

# dIKta.me V2

**Speech-to-text dictation for Windows** — C# + WinUI 3 rewrite.

> Formerly *dIKtate*. Full rewrite from Python + Electron to native Windows application.
>
> 📚 **Docs**: [User Guide](docs/user/index.md) • [Developer Guide](docs/dev/index.md) • [Privacy](PRIVACY.md) • [Contributing](CONTRIBUTING.md)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **UI** | WinUI 3 (Fluent Design) |
| **Logic** | C# / .NET 8 |
| **STT** | Cloud (Deepgram, Gemini) + Local (Whisper.net) |
| **LLM** | Gemini, Anthropic, OpenAI, Ollama |
| **Data** | SQLite (Microsoft.Data.Sqlite) |
| **Installer** | Self-contained, trimmed |

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
│       └── System/            # Ollama management
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

**Project Phase:** Feature Complete + Testing ✅

| Stream | Tasks | Status |
|--------|:-----:|--------|
| **A** — Scaffolding | A.0–A.2 | ✅ Complete |
| **B** — Core Engine | B.1–B.5 | ✅ Complete |
| **C** — STT & LLM Providers | C.1–C.7 | ✅ Complete |
| **D** — Pipeline Orchestration | D.1–D.4 | ✅ Complete |
| **E** — Data & Security | E.0–E.3 | ✅ Complete |
| **F** — UI (WinUI 3) | F.1–F.5 | ✅ Complete |
| **G** — Testing & CI/CD | G.1–G.2 | ✅ Complete |
| **I** — Promoted Features | I.1–I.5 | ✅ Complete |
| **H** — Distribution | H.1–H.2 | ⏳ Remaining |

### Metrics

- **Build:** 0 errors, 0 warnings (Release config, `TreatWarningsAsErrors=true`)
- **Tests:** 414 passing (376 in CI unit filter; 1 pre-existing clipboard flake)
- **Coverage:** 74.1% line rate, 52.4% branch rate (Core only; UI layer tested manually)
- **CI/CD:** GitHub Actions green (`Lint ✓ Build ✓ Test ✓ Secret scan ✓ Publish ✓`)
- **Publish Size:** ~173MB uncompressed (x64), ~70MB compressed
- **Code Quality:** Meziantou.Analyzer, NuGetAudit, gitleaks, code coverage tracking

### What's Working

✅ **Recording & Injection:** Push-to-talk with 6 global hotkeys, text injection via clipboard
✅ **Transcription:** Cloud STT (Deepgram, Gemini) + local (Whisper.net optional)
✅ **LLM:** Cloud (OpenAI, Anthropic, Gemini, Ollama) + streaming responses
✅ **All 6 Workflows:** Dictate, Refine, Ask, Translate, Oops, Note
✅ **Settings & Profiles:** Dual-profile system, 16 custom prompts, per-mode providers
✅ **Voice Snippets:** Trigger-based macro expansion (Phase 1)
✅ **Quick Chat:** Floating LLM overlay (text + voice input)
✅ **Audio Ducking:** Auto-volume reduction during recording
✅ **Ollama Management:** Version sensing, health checks, model library UI
✅ **Data:** SQLite history (90-day), session metrics, privacy levels
✅ **Security:** DPAPI secrets, PII scrubber, API key validation
✅ **UI:** WinUI 3 Settings window (10 tabs), Control Panel, Wizard, Notifications

### What's Next (H Stream)

⏳ **Installer:** MSIX or Inno Setup (~70MB compressed)
⏳ **V1 Migration:** Detect V1 settings, migrate to V2 format
⏳ **Website Rebrand:** Update dikta.me for V2 launch

---

**See** [`DEVELOPMENT_ROADMAP.md`](DEVELOPMENT_ROADMAP.md) **for full architecture, task breakdown, and Git strategy.**
