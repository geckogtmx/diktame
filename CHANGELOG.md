# Changelog

All notable changes to dIKta.me are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-04-XX

Complete rewrite from Python/Electron (V1) to C#/.NET 8/WinUI 3.
Single native Windows process, self-contained installer with auto-updates, local-first architecture.

### Added

**8 Workflow Modes**
- Dictate — voice to text, injected at cursor position
- Refine Auto — select text, AI improves it in-place
- Refine Voice — select text + speak instructions for targeted edits
- Ask — voice Q&A with answer injected at cursor
- Translate — EN/ES bidirectional, auto-detect language
- Note — voice memos appended to markdown file with timestamps
- Oops — re-inject last output (undo safety net)
- Read Selection — highlight text, hear it read aloud via TTS

**Multi-Provider AI Engine**
- STT: Deepgram Nova-3, Gemini Audio (cloud) / Whisper.net ONNX with GPU support (local)
- LLM: Gemini, OpenAI, Anthropic, OpenRouter (cloud) / Ollama with any model (local)
- TTS: Deepgram, OpenAI, Inworld, Gemini (cloud) / Kokoro ONNX ~88 MB (local)
- Vision: Gemini multimodal (cloud) / minicpm-v via Ollama (local)
- Deepgram streaming STT with token-by-token injection

**Quick Chat**
- Floating overlay window with text and voice input
- Multi-turn conversation with image attachment support
- Provider-aware routing (local or cloud)

**Vision Module**
- 6-action modal: Save, Clipboard, Chat, Note, OCR, Table extraction
- Local/Cloud toggle per action
- Multimodal chat with image context across all 4 LLM providers

**Text-to-Speech**
- Local-first via Kokoro ONNX (free, private, offline)
- Cloud fallback: Deepgram, OpenAI, Gemini, Inworld voices
- Optional response read-back on Ask, Translate, and Chat modes
- On-demand model download with progress UI

**Wallet System**
- Cloud AI via managed wallet — no API keys needed to start
- $1 promotional credit on sign-up (~13,000 words of dictation)
- Append-only SQLite ledger with balance sync
- Top-up via Ko-fi / LemonSqueezy payment webhooks
- Supabase Edge Functions for managed STT/LLM proxying

**Full Version ($20, one-time)**
- Unlocks local STT, LLM, TTS, and bring-your-own API keys
- RSA-2048 signed keys, offline validation, no phone-home
- DPAPI-encrypted storage, survives app restarts

**Account & Auth**
- OAuth sign-in via Google and GitHub (Supabase Auth)
- Deep-link protocol handler (`diktame://auth`)
- JWT auto-refresh (5-minute timer, 10-minute threshold)
- Profile avatar upload and sync

**User Interface**
- 3 color themes: Midnight (dark), Ember (warm), Frost (light)
- Runtime theme switching (no restart needed)
- Inter typography, glassmorphic card styling
- Control panel with auto-collapse, voice waveform, 6-position snap
- 3-layer idle animation: status, logo+clock, weather
- System tray with context menu and dynamic tooltip
- First-run wizard: language, path selection, STT/LLM/TTS setup, API keys, test

**Configuration**
- 16 custom prompt slots with dual Cloud/Local profiles
- CRUD dictation modes (4 built-in + unlimited user-created)
- Per-mode provider and model selection
- Audio ducking (auto-lower other apps during dictation)
- Configurable hotkeys, sound feedback, +Key behavior
- Voice snippets with trigger-word expansion
- 4-level privacy controls with PII scrubbing

**Internationalization**
- Full bilingual UI: English + Spanish
- Bilingual UI (EN + ES)

**Data & Security**
- SQLite history with 90-day auto-pruning
- Session metrics and per-provider latency tracking
- DPAPI encryption for API keys and license
- PII scrubber (email, phone, credit card, SSN, API key patterns)
- Settings persistence with atomic writes and schema migration

**Testing & CI**
- 1,134 unit tests across 60+ test files
- 11-step CI pipeline: restore, lint, build, test, threshold check, secret scan, vulnerability audit, deprecated packages, publish, size guard, Velopack packaging
- Meziantou.Analyzer for culture bugs and string comparison safety
- Velopack auto-update: delta updates via GitHub Releases, background download, apply on restart
- GitHub Release auto-created on `v*` tags with installer + update packages attached

### Changed

- Architecture: Python + Electron (V1) to C# + .NET 8 + WinUI 3 (V2)
- Packaging: Electron app to self-contained native Windows executable
- Installer: Velopack one-click install with automatic delta updates (replaced Inno Setup)
- Startup: <3 seconds in cloud mode
- Memory: 50-80 MB idle (vs ~300 MB Electron)
- License: proprietary (V1) to MIT open source (V2)

### Architecture

- Target: .NET 8, Windows App SDK 1.6, WinUI 3
- Platform: Windows 10 1809+ (build 17763) and Windows 11
- Build: IL-trimmed, self-contained x64
- Solution: `DiktaMe.App` (WinUI 3) + `DiktaMe.Core` (class library) + `DiktaMe.Core.Tests` (xUnit)
- Plugin architecture designed for V2.1 modules (Connectors, Meetings, Memory)

[2.0.0]: https://github.com/geckogtmx/diktame/releases/tag/v2.0.0
