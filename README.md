> **Language / Idioma:** English | [Español](README.es.md)

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
| **STT** | Cloud (Deepgram, Gemini) + Local (Whisper.net with Vulkan GPU) |
| **TTS** | Cloud (Deepgram, Inworld, OpenAI) + Local (Kokoro-ONNX) |
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
│       ├── TTS/               # Text-to-Speech providers
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
| 7 | **Read Selection** | `Ctrl+Alt+Q` | Text-to-Speech playback |

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

| Stream / Spec | Tasks | Status |
|---------------|:-----:|--------|
| **A** — Scaffolding | A.0–A.2 | ✅ Complete |
| **B** — Core Engine | B.1–B.5 | ✅ Complete |
| **C** — STT & LLM Providers | C.1–C.7 | ✅ Complete |
| **D** — Pipeline Orchestration | D.1–D.4 | ✅ Complete |
| **E** — Data & Security | E.0–E.3 | ✅ Complete |
| **F** — UI (WinUI 3) | F.1–F.5 | ✅ Complete |
| **G** — Testing & CI/CD | G.1–G.2 | ✅ Complete |
| **I** — Promoted Features | I.1–I.5 | ✅ Complete |
| **J** — CRUD Dictation Modes | J.1–J.7 | ✅ Complete |
| **K** — OAuth & Wallet | K.1–K.12 | ✅ Complete |
| **L** — Deepgram Streaming | L.1–L.5 | ✅ Complete |
| **SPEC_003** — TTS | Phase A–G | ✅ Complete |
| **SPEC_007** — Chat Upgrade | 14/14 tasks | ✅ Complete |
| **SPEC_009** — Local/Wizard | 15 fixes | ✅ Complete |
| **SPEC_011** — Ollama Management| API + UI | ✅ Complete |
| **SPEC_002** — Vision | Screenshot/Video/OCR + AI | ✅ Complete |
| **SPEC_008** — Wallet System | Cloud credits + billing | ✅ Complete |
| **UI Revamp** — Themes & Polish | 6 phases | ✅ Complete |
| **H** — Distribution | H.1–H.2 | ⏳ Remaining |

### Metrics

- **Build:** 0 errors, 0 warnings (Release config, `TreatWarningsAsErrors=true`)
- **Tests:** 1,014 passing locally (470+ in CI unit filter; DPAPI/Clipboard skipped on runners)
- **Coverage:** 74.1% line rate, 52.4% branch rate (Core only; UI layer tested manually)
- **CI/CD:** GitHub Actions green (`Lint ✓ Build ✓ Test ✓ Secret scan ✓ Publish ✓`)
- **Publish Size:** ~173MB uncompressed (x64), ~70MB compressed
- **Code Quality:** Meziantou.Analyzer, NuGetAudit, gitleaks, code coverage tracking

### What's Working

✅ **Recording & Injection:** Push-to-talk with global hotkeys, text injection via clipboard
✅ **Transcription:** Cloud STT (Deepgram, Gemini) + local Vulkan GPU (Whisper.net)
✅ **TTS (Text-to-Speech):** Read Selection mode (`Ctrl+Alt+Q`), Kokoro local + cloud fallback
✅ **LLM:** Cloud (OpenAI, Anthropic, Gemini) + local Ollama with provider caching & auto-start
✅ **All 7 Workflows:** Dictate, Refine, Ask, Translate, Oops, Note, Read Selection
✅ **Settings & Profiles:** Dual-profile system, 16 custom prompts, per-mode providers
✅ **Voice Snippets:** Trigger-based macro expansion (Phase 1)
✅ **Quick Chat:** Floating LLM overlay (text + voice input)
✅ **Ollama Management:** Version sensing, health checks, model library UI, keep-alive
✅ **Data:** SQLite history (90-day), session metrics, privacy levels
✅ **Security:** DPAPI secrets, PII scrubber, API key validation
✅ **UI:** WinUI 3 Settings window (10 tabs), Control Panel, First-Run Wizard, Notifications
✅ **Vision:** Screenshot/video/OCR/table extraction with AI analysis (`Ctrl+Alt+S`)
✅ **Wallet:** Cloud credit system with LemonSqueezy + Ko-fi payment adapters
✅ **Website Rebrand:** Comprehensive user docs and marketing on dikta.me

### What's Next

⏳ **License Gate & Installer:** Power License system + Inno Setup distribution
⏳ **Connectors Plugin:** Voice to Obsidian, webhooks, Discord (SPEC_013/015)
⏳ **Refinemmarly:** Grammarly-like grammar checking via your own LLM (SPEC_016)
⏳ **Meeting Intelligence:** Local-first meeting notes & synthesis (SPEC_001/015)
⏳ **Memory Layer:** Semantic recall that improves with use (SPEC_014/015)
⏳ **Chaviz Orchestrator:** Conversational voice agent (SPEC_017)

---

## Built by

[Eduardo Garcia-Torres](https://www.linkedin.com/in/eduardogarciatorres/) — marketing & business executive from Mexico with 20+ years in IT consulting, digital media, and project management. Not a software engineer by training. dIKta.me is his first desktop application, built from scratch with C#, WinUI 3, and AI coding tools. The product decisions come from two decades of building businesses and shipping products — not a CS degree. [Full story](CONTRIBUTING.md#about-the-builder).

## License

[MIT](LICENSE) — use it, fork it, build on it.

---

**See** [`DEVELOPMENT_ROADMAP.md`](DEVELOPMENT_ROADMAP.md) **for full architecture, task breakdown, and Git strategy.**
