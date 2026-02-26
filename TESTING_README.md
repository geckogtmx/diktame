# ✅ Testing Infrastructure Complete

**Date:** 2026-02-25 (Updated)
**Status:** Ready for manual end-to-end testing

---

## What Was Created

### 📋 Master Documents

1. **[plans/LIVE_TESTING_PLAN.md](plans/LIVE_TESTING_PLAN.md)** (Permanent Strategy)
   - Comprehensive 400+ line testing strategy
   - 10 test sections with detailed breakdown
   - Audio feeder migration guide
   - Time estimates (22-34 hours total)
   - Success criteria and deliverables

2. **[TESTING.md](TESTING.md)** (Quick Reference)
   - Overview of all three testing phases
   - Quick start instructions
   - Complete section descriptions
   - File structure summary
   - Next steps

3. **[MANUAL_TEST_PLAN.md](MANUAL_TEST_PLAN.md)** (Checklist)
   - 125 test scenarios with `- [ ]` checkboxes
   - 9 manual sections (Sections 1-9)
   - 1 automated section (Section 10 — Audio Feeder)
   - Each scenario includes: goal, prerequisites, steps, validation
   - Notes section for bugs/observations
   - **Use this as your primary working document**

### 🛠️ PowerShell Helper Scripts (test-helpers/)

**8 Core Validation Scripts:**
- `Verify-AppSettings.ps1` — Read and validate settings.json
- `Verify-HistoryDb.ps1` — Query SQLite history database
- `Verify-SecureStorage.ps1` — Check DPAPI encryption of keys.dat
- `Verify-AutoStart.ps1` — Verify Task Scheduler entry
- `Verify-FileSystem.ps1` — Check file/directory existence
- `Verify-Snippets.ps1` — Validate snippets.json
- `Test-OllamaHealth.ps1` — Check Ollama connectivity
- `Get-AppState.ps1` — Full application state dump

**Session Management:**
- `New-TestSession.ps1` — Initialize timestamped test session

**Audio Feeder (To Be Implemented):**
- `Invoke-AudioFeeder.ps1` — Main automation (port from V1)
- `Download-TestAudio.ps1` — YouTube downloader wrapper
- `Start-IpcServer.ps1` — TCP server for test control

**Documentation:**
- `test-helpers/README.md` — Detailed script documentation with examples

### 📁 File Structure

```
E:\git\diktame\
├── plans/
│   └── LIVE_TESTING_PLAN.md      ← Permanent strategy (400+ lines)
├── TESTING.md                     ← Quick reference (this session's work)
├── TESTING_README.md              ← This file (infrastructure summary)
├── MANUAL_TEST_PLAN.md            ← Checklist (your primary working doc)
├── test-helpers/                  ← PowerShell validation scripts
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
│   ├── (Invoke-AudioFeeder.ps1)    ← To implement
│   ├── (Download-TestAudio.ps1)    ← To implement
│   └── (Start-IpcServer.ps1)       ← To implement
├── tests/fixtures/downloads/       ← YouTube audio + subtitles (gitignored)
└── test-session-log.md            ← Your working copy (gitignored)
```

### 📝 .gitignore Updates

Added:
```
# Manual testing artifacts
test-session-log.md
tests/fixtures/downloads/
*.wav
*.srt
```

---

## Test Sections at a Glance

| Section | Goal | Time | Scenarios |
|---------|------|------|-----------|
| **1. First-Run** | Wizard flow | 30 min | 15 |
| **2. Dictation** | Basic voice-to-text | 1 hour | 20 |
| **3. Advanced Modes** | All 6 modes + Chat | 1.5 hours | 25 |
| **4. Audio System** | Hardware integration | 30 min | 10 |
| **5. Settings** | All 14 tabs | 2.5 hours | 40 |
| **6. Data & Privacy** | Persistence & compliance | 45 min | 12 |
| **7. System Integration** | OS-level features | 1 hour | 15 |
| **8. Error Handling** | Edge cases & recovery | 1.5 hours | 18 |
| **9. Performance** | Non-functional reqs | 30 min | 10 |
| **10. Audio Feeder** | Real voice testing | 2-5 hours | 5 runs |

**Total Manual:** 10 hours (Sections 1-9)
**Total Automated:** 3-5 hours (Section 10 setup + runs)
**Total with Fixes:** 19-29 hours

---

## How to Use This Infrastructure

### Phase 1: Manual Testing (9-10 hours)

```powershell
# 1. Start a session
.\test-helpers\New-TestSession.ps1

# 2. Open the checklist
code MANUAL_TEST_PLAN.md

# 3. Work through Sections 1-9
# - Read each section's goal and prerequisites
# - Perform manual test steps
# - Check off items: - [ ] → - [x]
# - Use helper scripts when referenced

# 4. Run validation as you go
.\test-helpers\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"
.\test-helpers\Verify-HistoryDb.ps1
.\test-helpers\Get-AppState.ps1

# 5. Document bugs in test-session-log.md
```

### Phase 2: Audio Feeder Automation (2-3 hours to implement, 1-3 hours to run)

```powershell
# 1. Port three scripts from V1 (2-3 hours)
# - Invoke-AudioFeeder.ps1
# - Download-TestAudio.ps1
# - IPC server (C# or PowerShell)

# 2. Download test audio (YouTube)
.\test-helpers\Download-TestAudio.ps1 -Url "https://..." -OutputDir "tests/fixtures/downloads"

# 3. Run app with IPC enabled
Start-Process DiktaMe.exe -ArgumentList "--enable-ipc"

# 4. Execute audio feeder tests
.\test-helpers\Invoke-AudioFeeder.ps1 -AudioFile "..." -SubtitleFile "..."

# 5. Review statistics and accuracy
```

### Phase 3: Bug Fixes & Polish (4-8 hours)

- Document all bugs found in test-session-log.md
- Prioritize critical (blocking) vs important vs nice-to-have
- Fix bugs in batches
- Re-test affected scenarios
- Polish UI, performance, etc.

---

## Success Criteria

**Manual Testing (Sections 1-9):**
- ✅ All 125 scenarios executed at least once
- ✅ 95%+ pass rate (120+ / 125)
- ✅ All critical bugs fixed
- ✅ Performance targets met (<3s startup, <80MB memory)

**Automated Testing (Section 10):**
- ✅ Audio feeder successfully controls app via IPC
- ✅ TED talk scenario achieves 90%+ accuracy
- ✅ Diverse accents tested (3+ different speakers)
- ✅ Statistics tracking works correctly

**Overall Quality:**
- ✅ UI is visually consistent
- ✅ Error messages are actionable
- ✅ No crashes or data loss
- ✅ Settings persist across restarts
- ✅ Privacy levels work as documented

---

## Key Innovation: Audio Feeder

Instead of manually recording dozens of voice samples, the **Audio Feeder** tool:
1. Downloads real YouTube videos with captions
2. Slices audio into realistic phrases (8-10 seconds)
3. Plays audio through speakers while controlling the app via IPC
4. Tracks transcription accuracy against expected text
5. Tests diverse accents and speaking styles

This validates:
- Real-world voice quality (not perfect lab conditions)
- Different accents (British, Indian, Australian)
- Various speaking rates (slow, fast, normal)
- Background noise handling
- Multi-speaker scenarios (podcasts, interviews)

---

## Important Notes

### Before Starting Tests

1. **Build the app:** `dotnet build DiktaMe.sln -c Release` (0 errors expected)
2. **Run existing tests:** `dotnet test DiktaMe.sln` (414 tests should pass)
3. **Delete old state:** `del %APPDATA%\DiktaMe\settings.json` (for wizard testing)
4. **Have Notepad ready:** For text injection tests

### During Testing

- **Don't skip steps** — Even if something seems obvious, test it (bugs hide in obvious places)
- **Document everything** — Note unusual behavior, performance, UI glitches
- **Use helper scripts** — They'll save you time and reduce manual checking
- **Stop and fix critical bugs** — Don't continue if something breaks core functionality
- **Pause anytime** — Test sections are independent, can resume later

### Artifacts Created

All testing artifacts are **gitignored** and won't appear in commits:
- `test-session-log.md` — Your notes
- `tests/fixtures/downloads/` — Downloaded audio files
- `.wav`, `.srt` files — YouTube audio + subtitles

---

## Next Actions

### Immediately

1. ✅ **Infrastructure:** All testing documents and scripts are ready
2. ✅ **MANUAL_TEST_PLAN.md:** Use this as your primary checklist (needs updating for new tabs)
3. ✅ **Helper Scripts:** Copy to test-helpers/ and make executable

### Before Section 1

1. **Review TESTING.md** — Quick overview
2. **Build the app** — Ensure it compiles with 0 errors
3. **Run unit tests** — 521 tests should pass
4. **Delete settings.json** — Reset to first-run state
5. **Update MANUAL_TEST_PLAN.md** — Add test scenarios for:
   - Dictation Presets page (CRUD operations, model selection)
   - Notes page (file path, timestamp, LLM processing)
   - Chat page (UI settings, forget-on-close, prompts)

### During Testing (Sections 1-9)

- Work systematically through each section
- Use helper scripts liberally
- Document bugs with `❌ BUG:` prefix
- Create GitHub Issues for critical bugs

### After Section 9

- Review all bugs found
- Prioritize fixes (critical → important → nice-to-have)
- Fix bugs in batch
- Re-test affected scenarios

### For Section 10 (Audio Feeder)

- Port three scripts from V1 (`E:\git\diktate\python\tools\audio_feeder.py`)
- Build IPC server (TCP on 127.0.0.1:5005)
- Download test videos using `yt-dlp`
- Run automated tests with diverse video sources

---

## Support

**Questions about testing?**
- Read [TESTING.md](TESTING.md) for quick reference
- Read [test-helpers/README.md](test-helpers/README.md) for script documentation
- Check [plans/LIVE_TESTING_PLAN.md](plans/LIVE_TESTING_PLAN.md) for comprehensive strategy

**Issues with helper scripts?**
- Each script has built-in error handling
- Run `.\Get-AppState.ps1` to dump full state for debugging
- Scripts output colored status: ✅ PASS, ❌ FAIL, ⚠️ WARNING

---

## Timeline

**Manual Testing:** 9-10 hours
- Can split across multiple sessions
- Sections are independent
- Can pause and resume anytime

**Audio Feeder Setup:** 2-3 hours
- Port V1 tool (~1 hour)
- Build IPC server (~1-2 hours)

**Audio Feeder Runs:** 1-3 hours
- 5 test scenarios with different video sources
- Each run takes 20-60 minutes
- Can run unattended

**Bug Fixes:** 4-8 hours (estimate)
- Depends on severity and number of bugs
- Prioritize critical blocking issues
- Re-test after fixes

**Total:** 18-28 hours over ~1-2 weeks

---

## Success!

When all tests pass and bugs are fixed, you'll be ready for:
- **Installer Creation** (Task H.1)
- **V1 Migration Testing** (Task H.2)
- **Final Release** (v2.0.0)

Good luck! 🚀

---

**Created:** 2026-02-19
**Updated:** 2026-02-25 (Stream J + Notes/Chat pages complete)
**Part of:** dIKta.me V2 Testing Infrastructure
**Status:** Infrastructure ready, MANUAL_TEST_PLAN.md needs updating for new features

---

## Updates (2026-02-25)

**New Features Added (NOT YET TESTED):**
- ⏳ CRUD Dictation Presets page (create, edit, delete, reorder presets)
- ⏳ Notes settings page (file path, timestamp format, LLM processing, prompts)
- ⏳ Chat settings page (font size, opacity, theme, forget-on-close, prompts)
- ⏳ Live model discovery from APIs (OpenAI, Anthropic, Gemini, OpenRouter, Ollama)
- ⏳ Per-preset model selection (Cloud profiles only)

**Testing Impact:**
- Settings tabs expanded: 10 → 14 (+4 tabs: Dictation Presets, Notes, Chat, Modes split)
- Estimated additional test scenarios: +5-10 scenarios
- Estimated additional testing time: +30-60 minutes
- Unit tests expanded: 414 → 521 (+107 tests for new features)

**Action Required:**
- Update MANUAL_TEST_PLAN.md Section 5 (Settings) with new scenarios
- Test Dictation Presets CRUD functionality
- Test Notes page file picker and live preview
- Test Chat page UI settings
- Verify model dropdown populates correctly from APIs
