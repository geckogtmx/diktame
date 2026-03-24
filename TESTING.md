# dIKta.me V2 — Testing Index

**Last Updated:** 2026-03-23
**Status:** Pre-release — manual E2E testing in progress

---

## At a Glance

| Layer | Coverage | Document |
|-------|----------|----------|
| **Unit Tests** | 1010 tests, 60 files, 13 modules (xUnit + Moq) | [TESTING_COVERAGE.md](TESTING_COVERAGE.md) |
| **Manual E2E** | ~280 scenarios across 5 journeys + cross-cutting | [MANUAL_TEST_PLAN.md](MANUAL_TEST_PLAN.md) |
| **Helper Scripts** | 9 PowerShell validation scripts | [test-helpers/](test-helpers/) |

---

## Quick Start

```powershell
# Unit tests (all 1010)
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
| **5. Settings Verification** | All 9 settings tabs + persistence | ~2h | ~95 |
| **Cross-Cutting** | Error handling, persistence, UI polish | ~1h | ~25 |
| **Audio Feeder** | Automated voice testing (IPC) | ~3-5h | 5 runs |
| | **Total manual** | **~10.5h** | **~280** |

Each journey tests: wizard setup, core dictation, all 6 modes, voice macros, API keys, data/privacy, system integration, and performance.

**Between journeys:** Delete `%APPDATA%\DiktaMe\settings.json` to reset.

---

## Feature Coverage Status

Features with manual test scenarios in MANUAL_TEST_PLAN.md:

| Feature | Covered | Journey/Section |
|---------|---------|-----------------|
| Wizard (8 steps, 14 paths) | Yes | J1-J4 setup sections |
| Core dictation (3 STT providers) | Yes | J1-J3 core sections |
| All 6 modes + Quick Chat | Yes | J1 advanced modes |
| Dictation Presets CRUD | Yes | J1 + J5.4 |
| Notes settings | Yes | J1 + J5.5 |
| Chat settings | Yes | J1 + J5.6 |
| Live model discovery (5 APIs) | Yes | J5.4 |
| Voice macros | Yes | J1.5 |
| Privacy levels (4 modes) | Yes | J1.7 + J5.9 |
| Hotkeys (7 actions) | Yes | J5.8 |
| Audio ducking | Yes | J5.7 |
| Ollama management | Yes | J3.3 + J5.11 |
| Auto-start (Task Scheduler) | Yes | J5.1 |
| Tray icon | Yes | J1.8 |
| **Wallet system (sign-in, balance, billing)** | **No** | Needs new journey |
| **UI themes (Midnight/Ember/Frost)** | **No** | Needs cross-cutting section |
| **Control Panel (waveform, snap, idle roll)** | **No** | Needs cross-cutting section |
| **Account/Auth (OAuth, avatar, JWT refresh)** | **No** | Needs new journey |
| **TTS system (Kokoro, cloud providers)** | **No** | Needs new journey |
| **TTS notifications (toast speak)** | **No** | Needs cross-cutting section |
| **ReadSelection mode (Ctrl+Alt+S)** | **No** | Needs cross-cutting section |

---

## Helper Scripts

| Script | Purpose |
|--------|---------|
| `Verify-AppSettings.ps1` | Read and validate settings.json |
| `Verify-HistoryDb.ps1` | Query SQLite history.db |
| `Verify-SecureStorage.ps1` | Check DPAPI encryption of keys.dat |
| `Verify-AutoStart.ps1` | Verify Task Scheduler entry |
| `Verify-FileSystem.ps1` | Check file/directory existence |
| `Verify-Snippets.ps1` | Validate snippets.json |
| `Test-OllamaHealth.ps1` | Check Ollama connectivity |
| `Get-AppState.ps1` | Full application state dump |
| `New-TestSession.ps1` | Initialize timestamped test session |

See [test-helpers/README.md](test-helpers/README.md) for usage examples.

---

## Unit Test Modules (1010 tests)

| Module | Tests | Files | Coverage |
|--------|-------|-------|----------|
| TTS | 184 | 10 | 77% (10/13 classes) |
| Config | 118 | 8 | 75% (9/12) |
| STT | 95 | 6 | 56% (5/9) |
| Pipeline | 89 | 4 | 100% (8/8) |
| Data | 82 | 5 | 83% (5/6) |
| LLM | 69 | 3 | 71% (5/7) |
| Audio | 62 | 6 | 83% (5/6) |
| Account | 46 | 6 | 75% (3/4) |
| Input | 44 | 4 | 100% (4/4) |
| System | 41 | 2 | 67% (2/3) |
| Security | 26 | 3 | 100% (3/3) |
| Weather | 13 | 1 | 100% (1/1) |
| Root | 10 | 2 | — |

Full details: [TESTING_COVERAGE.md](TESTING_COVERAGE.md)

---

## File Structure

```
E:\git\diktame\
├── TESTING.md                  ← This index
├── MANUAL_TEST_PLAN.md         ← Journey-based checklist (~280 scenarios)
├── TESTING_COVERAGE.md         ← Unit test metrics (1010 tests, 60 files)
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
│   └── New-TestSession.ps1
├── test-session-log.md         ← Working notes (gitignored)
└── plans/archive/
    └── LIVE_TESTING_PLAN.md    ← Original strategy doc (archived)
```

---

## Pre-Release Testing Priority

1. **Run unit tests** — `dotnet test DiktaMe.sln` (1010 tests, 0 failures expected)
2. **Journey 1** (Cloud/Deepgram) — validates the primary happy path
3. **Journey 3** (Local/Ollama) — validates fully offline workflow
4. **Journey 5** (Settings) — validates all 9 tabs + persistence
5. **Cross-cutting** — error handling, clipboard, tray icon
6. **Missing features** — wallet, auth, TTS, themes, control panel (add to MANUAL_TEST_PLAN.md)
