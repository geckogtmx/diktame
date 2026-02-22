# dIKta.me V2 — Testing Guide

**Status:** Manual testing infrastructure ready
**Last Updated:** 2026-02-19
**For:** Feature-complete V2 before installer creation

---

## Overview

This project uses a **comprehensive multi-phase testing approach**:

1. **414 Automated Unit Tests** (xUnit + Moq) — Already passing ✅
2. **Manual End-to-End Testing** (9 sections, ~125 scenarios) — Ready to execute
3. **Automated Voice Testing** (Audio Feeder + IPC server) — Infrastructure ready

This guide covers **phases 2 & 3**.

---

## Quick Start

### Phase 1: Manual Testing (9.5 hours)

```powershell
# 1. Initialize testing session
.\test-helpers\New-TestSession.ps1

# 2. Open master checklist
code MANUAL_TEST_PLAN.md

# 3. Work through sections 1-9
# - Section 1: Wizard flow (30 min)
# - Section 2: Dictation (1 hour)
# - Section 3: Advanced modes (1.5 hours)
# - Sections 4-9: Audio, Settings, Data, Integration, Errors, Polish

# 4. Run helper scripts as needed
.\test-helpers\Verify-AppSettings.ps1 -SettingPath "WizardCompleted"
.\test-helpers\Verify-HistoryDb.ps1
.\test-helpers\Get-AppState.ps1
```

### Phase 2: Automated Voice Testing (3-5 hours)

```powershell
# 1. Port audio feeder tool (2-3 hours)
# - Invoke-AudioFeeder.ps1 (main orchestration)
# - Download-TestAudio.ps1 (YouTube helper)
# - IpcServer.cs or Start-IpcServer.ps1 (test control)

# 2. Run audio feeder tests (1-3 hours)
.\test-helpers\Download-TestAudio.ps1 -Url "https://youtube.com/watch?v=..."
Start-Process DiktaMe.exe -ArgumentList "--enable-ipc"
.\test-helpers\Invoke-AudioFeeder.ps1 -AudioFile "..." -SubtitleFile "..."

# 3. Verify statistics and accuracy
```

---

## Documentation Structure

| File | Purpose | Time | Content |
|------|---------|------|---------|
| **[LIVE_TESTING_PLAN.md](plans/LIVE_TESTING_PLAN.md)** | Comprehensive strategy (permanent) | — | Full plan with rationale |
| **[MANUAL_TEST_PLAN.md](MANUAL_TEST_PLAN.md)** | Checklist for manual testing | 9.5h | 125 test scenarios with `- [ ]` boxes |
| **[test-helpers/](test-helpers/)** | PowerShell validation scripts | — | 8 core + 3 audio feeder scripts |
| **[test-session-log.md](test-session-log.md)** | Your working copy (gitignored) | — | Timestamped session notes |

---

## Manual Testing Sections

### Section 1: First-Run Experience (30 minutes)
**Goal:** Validate configuration wizard and initial setup
- Delete `%APPDATA%\DiktaMe\settings.json` to simulate first launch
- Navigate all 5 wizard steps
- Verify settings persistence
- **Scenarios:** 15

### Section 2: Core Dictation (1 hour)
**Goal:** Test the primary use case — voice-to-text injection
- Basic dictation with Deepgram, Gemini, Whisper
- With/without LLM cleanup, Raw mode
- Trailing space, additional keys, auto-stop, audio ducking
- Voice snippet expansion
- **Scenarios:** 20

### Section 3: Advanced Modes (1.5 hours)
**Goal:** Validate all 6 workflow modes + Quick Chat
- Refine (Autopilot & Instruction modes)
- Ask (Voice Q&A)
- Translate (EN↔ES)
- Note (Voice → markdown file)
- Oops (Re-inject last text)
- Quick Chat (Floating overlay)
- **Scenarios:** 25

### Section 4: Audio System (30 minutes)
**Goal:** Validate hardware integration
- Device enumeration and selection
- Mute detection
- Audio ducking (volume reduction)
- Auto-stop on max duration
- Error handling (device unplugged)
- **Scenarios:** 10

### Section 5: Settings Management (2 hours)
**Goal:** Validate all 10 settings tabs
- General, AI Engine, Modes, Audio, Privacy
- API Keys, Ollama, Snippets, Control Panel Config, About
- Test all toggles, dropdowns, sliders
- Verify persistence across restart
- **Scenarios:** 35

### Section 6: Data & Privacy (45 minutes)
**Goal:** Validate persistence, history, and privacy compliance
- Privacy levels (Ghost/Stats/Balanced/Full)
- PII scrubbing
- 90-day history pruning
- Settings persistence
- Corrupt file recovery
- **Scenarios:** 12

### Section 7: System Integration (1 hour)
**Goal:** Validate OS-level integrations
- Global hotkey registration
- Hotkey conflicts and re-registration
- Text injection and clipboard handling
- Auto-start via Task Scheduler
- Tray icon behavior
- App minimize/exit
- **Scenarios:** 15

### Section 8: Error Handling (1.5 hours)
**Goal:** Validate graceful degradation and error recovery
- Invalid/missing API keys
- STT/LLM providers offline
- Whisper model not downloaded
- Network timeouts
- Corrupt files (recovery)
- Empty/silent audio
- App restart during operations
- **Scenarios:** 18

### Section 9: Performance & Polish (30 minutes)
**Goal:** Validate non-functional requirements
- Startup time (<3s in cloud mode)
- Memory footprint (<80MB idle)
- Recording latency (<100ms visual feedback)
- Pipeline end-to-end latency (<5s)
- Control Panel real-time updates
- Settings responsiveness
- UI polish and branding consistency
- **Scenarios:** 10

---

## Audio Feeder Automation (Section 10)

**Goal:** Validate transcription quality with diverse real voices and accents

### How It Works

1. **Download** — `Download-TestAudio.ps1` fetches YouTube video + captions via `yt-dlp`
2. **Slice** — `Invoke-AudioFeeder.ps1` parses SRT subtitle file, creates 8-10s phrases
3. **Play** — Audio playback through speakers via NAudio or Windows media APIs
4. **Control** — IPC server (TCP `127.0.0.1:5005`) receives:
   - `START` → Begin recording
   - `STOP` → Stop recording
   - `STATUS` → Poll for state (idle/recording/transcribing/processing/error)
   - `PING` → Health check
5. **Measure** — Track success rate, latency, accuracy

### Test Scenarios

| Video | Accent | Duration | Phrases | Expected Accuracy |
|-------|--------|----------|---------|-------------------|
| TED Talk | American | 10-15m | 50-100 | >90% |
| Podcast | Casual | 5-10m | 30-60 | 80-90% |
| BBC Documentary | British | 5m | 30 | >85% |
| Sports Commentary | Fast Speech | 3-5m | 20-30 | 75-85% |
| Interview/Debate | Multi-Speaker | 5m | 30 | 85-90% |

### IPC Server Requirements

**Implement in C# or PowerShell:**
```csharp
// Option A: C# TCP server in DiktaMe.Core.Testing.IpcServer
// Option B: PowerShell TCP listener in Start-IpcServer.ps1
// Option C: Named pipe server (Windows-native)

// Security: Only enabled with --enable-ipc flag
// Port: 127.0.0.1:5005 (configurable)
// Commands: START, STOP, STATUS, PING
```

---

## PowerShell Helper Scripts

**8 Core Validation Scripts:**

1. **Verify-AppSettings.ps1** — Read and validate settings.json
2. **Verify-HistoryDb.ps1** — Query history.db for recent entries
3. **Verify-SecureStorage.ps1** — Check keys.dat encryption
4. **Verify-AutoStart.ps1** — Verify Task Scheduler entry
5. **Verify-FileSystem.ps1** — Check file/directory existence
6. **Verify-Snippets.ps1** — Validate snippets.json
7. **Test-OllamaHealth.ps1** — Check Ollama connectivity
8. **Get-AppState.ps1** — Full app state dump for debugging

**Session Management:**

9. **New-TestSession.ps1** — Initialize testing session with timestamp

**Audio Feeder (To Implement):**

10. **Invoke-AudioFeeder.ps1** — Main orchestration (port from V1)
11. **Download-TestAudio.ps1** — YouTube downloader wrapper
12. **Start-IpcServer.ps1** — TCP server for automation

See [test-helpers/README.md](test-helpers/README.md) for detailed documentation.

---

## Testing Workflow

### Starting a Session

```powershell
# 1. Initialize
.\test-helpers\New-TestSession.ps1

# 2. Open checklist
code MANUAL_TEST_PLAN.md

# 3. Work through sections, checking off items
# - Mark complete: - [x]
# - Mark pending: - [ ]

# 4. Add notes to test-session-log.md as you go
```

### Validation Methods

| Type | Method | Example |
|------|--------|---------|
| **Visual** | Observe UI behavior | "Window appears", "Text injected" |
| **File** | Check file existence/contents | `.\Verify-FileSystem.ps1` |
| **Database** | Query SQLite history.db | `.\Verify-HistoryDb.ps1` |
| **Settings** | Read settings.json | `.\Verify-AppSettings.ps1` |
| **System** | Task Scheduler, Registry | `.\Verify-AutoStart.ps1` |
| **App State** | Full dump for debugging | `.\Get-AppState.ps1` |

### Pausing & Resuming

- Markdown editor saves checklist state automatically
- `test-session-log.md` is gitignored (won't commit)
- Can resume anytime — unchecked boxes show remaining work

### Bug Tracking

When you find a bug:
1. Document in `test-session-log.md` with `❌ BUG:` prefix
2. Create GitHub Issue (if critical)
3. Continue testing (don't block)
4. Fix bugs in batch after session

---

## Success Criteria

### Manual Testing (Sections 1-9)
- ✅ All 125 scenarios executed at least once
- ✅ 95%+ scenarios pass (120+ / 125)
- ✅ All critical bugs fixed
- ✅ Non-critical bugs documented (GitHub Issues)
- ✅ Performance targets met (<3s startup, <80MB memory)

### Automated Testing (Section 10)
- ✅ Audio feeder successfully controls app via IPC
- ✅ TED talk scenario achieves 90%+ success rate
- ✅ Diverse accents tested (3+ different speakers)
- ✅ Statistics tracking accurate (success rate, latency)
- ✅ Tool is reproducible (same audio → similar results)

### Overall
- ✅ UI is visually consistent (branding, icons, fonts)
- ✅ Error messages are actionable
- ✅ No crashes or data loss in normal usage
- ✅ Settings persist correctly across restarts
- ✅ Privacy levels work as documented

---

## Time Estimates

| Phase | Activity | Duration |
|-------|----------|----------|
| Setup | Create infrastructure | ✅ **Done** |
| Manual (1-9) | Execute 125 scenarios | 9-10 hours |
| Audio Feeder | Port V1 tool + IPC | 2-3 hours |
| Audio Feeder | Run test scenarios | 1-3 hours |
| Bug Fixes | Fix critical/important | 4-8 hours |
| Polish | UI tweaks, performance | 2-4 hours |
| **Total** | | **18-28 hours** |

---

## Next Steps

1. ✅ **Created:**
   - `MANUAL_TEST_PLAN.md` with 125 scenarios
   - 8 PowerShell validation helper scripts
   - `test-helpers/README.md` documentation
   - Updated `.gitignore` for test artifacts

2. **To Implement (Section 10):**
   - Port `audio_feeder.py` to `Invoke-AudioFeeder.ps1`
   - Create `Download-TestAudio.ps1` (YouTube wrapper)
   - Build IPC server (C# or PowerShell)
   - Test with real YouTube audio

3. **To Execute:**
   - Run manual tests (Sections 1-9)
   - Document bugs and observations
   - Fix critical issues
   - Run automated voice tests (Section 10)
   - Final polish before installer

---

## Files

```
E:\git\diktame\
├── plans/
│   └── LIVE_TESTING_PLAN.md         ← Comprehensive strategy
├── TESTING.md                        ← This file
├── MANUAL_TEST_PLAN.md               ← Checklist (125 scenarios)
├── test-helpers/                     ← PowerShell validation scripts
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
│   ├── Invoke-AudioFeeder.ps1         ← (To implement)
│   ├── Download-TestAudio.ps1         ← (To implement)
│   └── Start-IpcServer.ps1            ← (To implement)
└── test-session-log.md               ← Your working copy (gitignored)
```

---

**Ready to test!** Open `MANUAL_TEST_PLAN.md` and begin Section 1. 🎯
