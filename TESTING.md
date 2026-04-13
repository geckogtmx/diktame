# dIKta.me V2 — Testing Index

**Last Updated:** 2026-04-13
**Status:** RC2 — final manual E2E testing before launch

---

## At a Glance

| Layer | Coverage | Document |
|-------|----------|----------|
| **Unit Tests** | 1,218 tests, 71 files, 15 modules (xUnit + Moq) | [TESTING_COVERAGE.md](TESTING_COVERAGE.md) |
| **Manual E2E** | ~400 scenarios across 7 journeys + cross-cutting | [MANUAL_TEST_PLAN.md](MANUAL_TEST_PLAN.md) |
| **Helper Scripts** | 10 PowerShell scripts + 2 reference docs | [test-helpers/](test-helpers/) |

---

## Quick Start

```powershell
# Unit tests (all 1,218)
dotnet test DiktaMe.sln

# Start a manual test session
.\test-helpers\New-TestSession.ps1

# Open the checklist
code MANUAL_TEST_PLAN.md

# Validation helpers
.\test-helpers\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"
.\test-helpers\Verify-HistoryDb.ps1
.\test-helpers\Get-AppState.ps1
```

---

## Manual Test Plan — Journey Overview

The manual test plan is organized as **complete user journeys**, each following a realistic path from wizard setup through daily usage.

| Journey | Configuration | Duration | Scenarios |
|---------|--------------|----------|-----------|
| **1. Cloud (Deepgram)** | Deepgram STT + Cloud LLM | ~3h | ~75 |
| **2. Cloud (Gemini)** | Gemini Audio STT + Gemini LLM | ~1.5h | ~15 |
| **3. Local (Whisper+Ollama)** | Whisper STT + Ollama LLM | ~2h | ~25 |
| **4. Hybrid (Skip LLM)** | Cloud STT + No LLM | ~1h | ~10 |
| **5. Settings Verification** | All 9 tabs (with sub-items) + persistence | ~2.5h | ~120 |
| **6. Wallet/Auth/License** | OAuth, wallet proxy, LemonSqueezy license | ~1.5h | ~25 |
| **7. TTS System** | Kokoro, cloud TTS, notifications, ReadSelection | ~1h | ~25 |
| **Cross-Cutting** | Themes, CP, Vision, auto-update, errors, UI | ~2h | ~55 |
| **Audio Feeder** | Automated voice testing (IPC) | ~3-5h | 5 runs |
| | **Total manual** | **~16-18h** | **~400** |

Each journey tests: wizard setup (8 steps), core dictation, all 9 pipeline modes, voice macros, API keys, data/privacy, system integration, and performance.

**Between journeys:** Delete `%APPDATA%\DiktaMe\settings.json` to reset.

---

## Feature Coverage Status

Features with manual test scenarios in MANUAL_TEST_PLAN.md:

| Feature | Covered | Journey/Section |
|---------|---------|-----------------|
| Wizard (8 steps, branching paths) | Yes | J1-J4 setup sections |
| Core dictation (4 STT providers) | Yes | J1-J3 core sections |
| All 9 pipeline modes + Quick Chat | Yes | J1 advanced modes |
| Dictation Presets CRUD (3 built-in) | Yes | J1 + J5.5 |
| Notes settings | Yes | J5.4 (Pipelines > Notes) |
| Chat settings | Yes | J5.3 (AI Engine > Chat) |
| Live model discovery (5 APIs) | Yes | J5.5 |
| Voice Macros | Yes | J1.5 + J5.6 |
| Privacy levels (4 modes) | Yes | J1.7 + J5.7 |
| Hotkeys (9 actions) | Yes | J5.1 (General > Keyboard Shortcuts) |
| Audio ducking | Yes | J5.2 (Audio & Mic > Sound Feedback) |
| Ollama management | Yes | J3.3 |
| Auto-start (Task Scheduler) | Yes | J5.1 (General > Application) |
| Tray icon | Yes | J1.8 |
| Wallet system (sign-in, balance, proxy) | Yes | J6 |
| UI themes (Midnight/Ember/Frost) | Yes | CT.1 |
| Control Panel (waveform, snap, idle roll) | Yes | CT.2-CT.5 |
| Account/Auth (OAuth, wallet, JWT refresh) | Yes | J6 |
| TTS system (Kokoro + 4 cloud providers) | Yes | J7 |
| TTS notifications (toast speak) | Yes | J7.4 |
| ReadSelection mode (Ctrl+Alt+Q) | Yes | J7.3 + CT.6 |
| Vision feature (8 actions, snipping) | Yes | CT.7 |
| Auto-update (Velopack) | Yes | CT.8 |
| LemonSqueezy license activation | Yes | J6.X |
| Refine split (Auto/Voice toggle) | Yes | J1.4 |
| Streaming dictation (Deepgram WebSocket) | Yes | CT.9 |

---

## Helper Scripts

| Script | Purpose |
|--------|---------|
| `Verify-AppSettings.ps1` | Read and validate settings.json |
| `Verify-HistoryDb.ps1` | Query SQLite history.db |
| `Verify-SecureStorage.ps1` | Check DPAPI encryption of keys.dat |
| `Verify-AutoStart.ps1` | Verify Task Scheduler entry |
| `Verify-FileSystem.ps1` | Check file/directory existence |
| `Verify-Snippets.ps1` | Validate voice macros (snippets.json) |
| `Test-OllamaHealth.ps1` | Check Ollama connectivity |
| `Get-AppState.ps1` | Full application state dump |
| `New-TestSession.ps1` | Initialize timestamped test session |
| `test-ipc-pipe.ps1` | Manual E2E test for LocalApiServer named pipe IPC |

**Reference docs:** `AUDIO_FEEDER.md`, `POTATO_COUCH.md`

See [test-helpers/README.md](test-helpers/README.md) for usage examples.

---

## Unit Test Modules (1,218 tests)

| Module | Tests | Files | Coverage |
|--------|-------|-------|----------|
| TTS | 200 | 11 | 85% (11/13 classes) |
| Config | 157 | 12 | 92% (11/12) |
| Pipeline | 96 | 4 | 100% (8/8) |
| STT | 95 | 6 | 56% (5/9) |
| LLM | 90 | 3 | 86% (6/7) |
| Data | 84 | 5 | 83% (5/6) |
| Audio | 62 | 6 | 83% (5/6) |
| System | 50 | 3 | 67% (2/3) |
| Account | 45 | 5 | 83% (5/6) |
| Input | 44 | 4 | 100% (4/4) |
| Security | 25 | 4 | 100% (4/4) |
| Plugin | 24 | 4 | 100% (4/4) |
| Weather | 13 | 1 | 100% (1/1) |
| Vision | 10 | 1 | 100% (1/1) |
| Root | 10 | 2 | — |

Full details: [TESTING_COVERAGE.md](TESTING_COVERAGE.md)

---

## File Structure

```
E:\git\diktame\
├── TESTING.md                  ← This index
├── MANUAL_TEST_PLAN.md         ← Journey-based checklist (~400 scenarios)
├── TESTING_COVERAGE.md         ← Unit test metrics (1,218 tests, 71 files)
├── test-helpers/               ← PowerShell validation scripts
│   ├── README.md
│   ├── Verify-AppSettings.ps1
│   ├── Verify-HistoryDb.ps1
│   ├── Verify-SecureStorage.ps1
│   ├── Verify-AutoStart.ps1
│   ├── Verify-FileSystem.ps1
│   ├── Verify-Snippets.ps1
│   ├── Test-OllamaHealth.ps1
│   ├── Get-AppState.ps1
│   ├── New-TestSession.ps1
│   ├── test-ipc-pipe.ps1
│   ├── AUDIO_FEEDER.md
│   └── POTATO_COUCH.md
├── test-session-log.md         ← Working notes (gitignored)
└── plans/archive/
    └── LIVE_TESTING_PLAN.md    ← Original strategy doc (archived)
```

---

## Pre-Release Testing Priority

1. **Run unit tests** — `dotnet test DiktaMe.sln` (1,218 tests, 0 failures expected)
2. **Journey 1** (Cloud/Deepgram) — validates the primary happy path
3. **Journey 3** (Local/Ollama) — validates fully offline workflow
4. **Journey 5** (Settings) — validates all 9 tabs with sub-items + persistence
5. **Journey 6** (Wallet/Auth/License) — validates billing and account features
6. **Journey 7** (TTS) — validates local + cloud text-to-speech
7. **Cross-cutting** — themes, control panel, vision, auto-update, error handling
