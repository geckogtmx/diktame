# dIKta.me V2 — Live Testing Plan

**Status:** Draft
**Created:** 2026-02-19
**Purpose:** Comprehensive manual and automated testing plan for feature-complete V2
**Prerequisites:** All development streams (A–G, I) complete; installer (H) deferred to post-testing

---

## 1. Overview

The dIKta.me V2 application is feature-complete with 414 unit tests covering all core functionality. However, manual end-to-end testing is required to validate:

- ✅ Full user workflows from first launch through daily usage
- ✅ UI interactions and visual polish
- ✅ Real hardware integration (audio devices, clipboard, keyboard injection)
- ✅ Cross-cutting concerns (settings persistence, tray interactions, error recovery)
- ✅ Transcription quality with diverse real voices and accents

This plan provides a **comprehensive, workflow-based testing script** organized in three phases:

1. **Manual Testing** (Sections 1-9): ~125 test scenarios with markdown checklists
2. **Automated Voice Testing** (Section 10): Audio feeder tool with real YouTube audio
3. **Polish & Bug Fixes**: Iterate until all scenarios pass

**Testing Environment:** Development build (not installed) — running from Visual Studio or published output in `bin/` directory. The installer will be created AFTER manual testing and polish are complete.

---

## 2. Test Organization

### 2.1 Test Files

| File | Purpose | Tracked in Git? |
|------|---------|:---------------:|
| **`MANUAL_TEST_PLAN.md`** | Master checklist (9 manual sections + 1 automated) | ✅ Yes |
| **`test-helpers/*.ps1`** | PowerShell validation scripts (8 core + 3 audio feeder) | ✅ Yes |
| **`tests/fixtures/downloads/`** | YouTube audio + subtitle files for automated testing | ❌ No (gitignored) |
| **`test-session-log.md`** | Working copy with notes (user's scratch space) | ❌ No (gitignored) |
| **`src/DiktaMe.Core/Testing/IpcServer.cs`** | TCP server for test automation (optional) | ✅ Yes |

### 2.2 PowerShell Helpers

#### Core Validation Scripts (8)

1. **`Verify-AppSettings.ps1`** — Reads `settings.json` and checks for expected values
   ```powershell
   .\Verify-AppSettings.ps1 -SettingPath "GeneralSettings.Language" -ExpectedValue "en"
   # Output: ✅ PASS or ❌ FAIL with actual value
   ```

2. **`Verify-HistoryDb.ps1`** — Queries `history.db` for most recent entry
   ```powershell
   .\Verify-HistoryDb.ps1 -ExpectedMode "dictate"
   # Output: Entry details + timestamp + PASS/FAIL
   ```

3. **`Verify-SecureStorage.ps1`** — Confirms `keys.dat` exists and is encrypted
   ```powershell
   .\Verify-SecureStorage.ps1
   # Output: File exists, size, encrypted status
   ```

4. **`Verify-AutoStart.ps1`** — Checks Task Scheduler registration
   ```powershell
   .\Verify-AutoStart.ps1
   # Output: Task exists, enabled status, trigger details
   ```

5. **`Verify-FileSystem.ps1`** — Checks for expected files/directories
   ```powershell
   .\Verify-FileSystem.ps1 -Path "%APPDATA%\DiktaMe\logs" -Type Directory
   # Output: Exists, size, file count
   ```

6. **`Verify-Snippets.ps1`** — Validates `snippets.json` structure
   ```powershell
   .\Verify-Snippets.ps1
   # Output: Count, structure valid, max limit check
   ```

7. **`Test-OllamaHealth.ps1`** — Checks Ollama connectivity and version
   ```powershell
   .\Test-OllamaHealth.ps1
   # Output: Online, version, models available
   ```

8. **`Get-AppState.ps1`** — Dumps full app state for debugging
   ```powershell
   .\Get-AppState.ps1
   # Output: All settings, history entries, file locations, Ollama status
   ```

#### Audio Feeder Scripts (3)

9. **`Invoke-AudioFeeder.ps1`** — Automated voice testing (ported from V1's `audio_feeder.py`)
   - Plays audio through speakers while controlling app via IPC
   - Supports smart mode (subtitle-based) and dumb mode (fixed chunks)
   - Tracks success rate, latency, failures

10. **`Download-TestAudio.ps1`** — YouTube downloader wrapper
    ```powershell
    .\Download-TestAudio.ps1 -Url "https://youtube.com/watch?v=..." -OutputDir "tests/fixtures/downloads"
    # Uses yt-dlp to download audio + subtitles
    ```

11. **`Start-IpcServer.ps1`** — TCP server for test automation (if not built into app)
    - Listens on `127.0.0.1:5005`
    - Commands: `START`, `STOP`, `STATUS`, `PING`
    - Only enabled with `--enable-ipc` flag (security)

#### Session Management

12. **`New-TestSession.ps1`** — Initialize fresh session log
    ```powershell
    .\New-TestSession.ps1
    # Creates test-session-log.md with timestamp header
    ```

---

## 3. Manual Testing Sections (1–9)

### Section 1: First-Run Experience (Wizard Flow)
**Goal:** Validate configuration wizard and initial setup
**Time Estimate:** 30 minutes
**Scenarios:** ~15

**Prerequisites:** Delete `%APPDATA%\DiktaMe\settings.json` to simulate first launch

**Test Coverage:**
- Clean state detection (WizardCompleted=false)
- Wizard UI navigation (5 steps)
- STT/LLM provider selection
- Test recording validation
- Settings persistence after wizard completion
- Transition to main app (Loading screen → Control Panel or Quick Chat)

**Key Scenarios:**
1. First launch shows wizard (not main app)
2. Step 1 (Welcome) displays branding and "Build Your Stack" explanation
3. Step 2 (STT choice): Cloud vs Local options work
4. Step 3 (LLM choice): Cloud vs Ollama vs Skip options work
5. Step 4 (Test recording): Record 3s audio, transcription displayed
6. Step 5 (Ready): Summary shows selected providers
7. "Start Dictating" button saves `WizardCompleted=true` and transitions
8. Next launch skips wizard (shows main app directly)
9. Can reset wizard by deleting settings.json

**Validation:**
- Visual: Wizard steps flow correctly
- Helper: `.\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"`

---

### Section 2: Core Dictation Workflows
**Goal:** Test the primary use case — voice-to-text injection
**Time Estimate:** 1 hour
**Scenarios:** ~20

#### 2A. Basic Dictation (Ctrl+Alt+D)

**Test Coverage:**
- Record → Transcribe → Inject into Notepad
- Cloud STT (Deepgram/Gemini)
- Local STT (Whisper)
- With/without LLM cleanup
- Raw mode (LLM bypass)
- Trailing space toggle
- Additional key (+Enter, +Tab)
- Max duration auto-stop
- Audio ducking behavior

**Key Scenarios:**
1. Open Notepad, press Ctrl+Alt+D, speak "Hello world", release → text injected
2. Same test with Cloud STT (Deepgram) → verify in history
3. Same test with Cloud STT (Gemini Audio) → verify in history
4. Same test with Local STT (Whisper) → verify in history
5. Enable LLM cleanup → speak messy text → cleaned version injected
6. Raw mode enabled → LLM skipped, raw transcript injected
7. Trailing space enabled → text ends with space
8. Trailing space disabled → text ends without space
9. Additional key = Enter → text injected + Enter pressed
10. Additional key = Tab → text injected + Tab pressed
11. Hold hotkey for 65 seconds → auto-stop at 60s max
12. Audio ducking enabled → other apps volume reduced during recording
13. Audio ducking disabled → other apps volume unchanged

**Validation:**
- Visual: Text appears in Notepad
- Helper: `.\Verify-HistoryDb.ps1 -ExpectedMode "dictate"`
- Manual: Check Control Panel for latency stats

#### 2B. Dictation with Snippets

**Test Coverage:**
- Trigger expansion during injection
- Case-insensitive matching
- Punctuation handling
- Multi-snippet in one phrase

**Key Scenarios:**
1. Add snippet: Trigger="my email", Content="test@example.com"
2. Dictate "Send it to my email please" → expands to "Send it to test@example.com please"
3. Dictate "MY EMAIL" (uppercase) → expands (case-insensitive)
4. Dictate "my email, thanks" → expands before punctuation
5. Add second snippet: Trigger="my phone", Content="555-1234"
6. Dictate "my email and my phone" → both expand

**Validation:**
- Visual: Expanded text in Notepad
- Helper: `.\Verify-Snippets.ps1`

---

### Section 3: Advanced Modes
**Goal:** Validate all 6 workflow modes
**Time Estimate:** 1.5 hours
**Scenarios:** ~25

#### 3A. Refine Mode (Ctrl+Alt+R)

**Test Coverage:**
- Autopilot (no voice input) → selection cleanup
- Instruction mode (hold + speak) → selection + command
- Fallback behavior (no selection → Ask mode)

**Key Scenarios:**
1. Type messy text in Notepad, select it, press Ctrl+Alt+R → cleaned version replaces selection
2. Type "hello wrold", select it, hold Ctrl+Alt+R, say "fix spelling", release → "hello world" replaces
3. Press Ctrl+Alt+R (no selection) → shows error or Ask mode fallback
4. Hold Ctrl+Alt+R, say "make this more formal" (no selection) → Ask mode answer displayed

**Validation:**
- Visual: Selection replaced with refined text
- Helper: `.\Verify-HistoryDb.ps1 -ExpectedMode "refine"`

#### 3B. Ask Mode (Ctrl+Alt+A)

**Test Coverage:**
- Voice Q&A → answer displayed (not injected)
- Result visible in Control Panel or notification

**Key Scenarios:**
1. Press Ctrl+Alt+A, say "What is the capital of France", release → "Paris" displayed (not injected)
2. Check Control Panel or notification for answer
3. Same test with complex question → multi-sentence answer

**Validation:**
- Visual: Answer shown in UI
- Helper: `.\Verify-HistoryDb.ps1 -ExpectedMode "ask"`

#### 3C. Translate Mode (Ctrl+Alt+T)

**Test Coverage:**
- EN → ES translation
- ES → EN translation
- Auto-language detection

**Key Scenarios:**
1. Press Ctrl+Alt+T, say "Hello how are you", release → "Hola cómo estás" injected
2. Press Ctrl+Alt+T, say "Hola cómo estás", release → "Hello how are you" injected
3. Auto-detection works (no language specified in settings)

**Validation:**
- Visual: Translated text in Notepad
- Helper: `.\Verify-HistoryDb.ps1 -ExpectedMode "translate"`

#### 3D. Note Mode (Ctrl+Alt+N)

**Test Coverage:**
- Voice note → markdown file append
- Timestamp headers
- Directory creation if missing

**Key Scenarios:**
1. Press Ctrl+Alt+N, say "Remember to buy milk", release → appended to `diktame-notes.md`
2. Check file has `## 2026-02-19 14:30` timestamp header
3. Delete notes file, repeat test → file created
4. Delete parent directory, repeat test → directory + file created

**Validation:**
- Helper: `.\Verify-FileSystem.ps1 -Path "%USERPROFILE%\Documents\diktame-notes.md" -Type File`
- Manual: Open file and verify timestamp + text

#### 3E. Oops Mode (Ctrl+Alt+V)

**Test Coverage:**
- Re-inject last text
- Works after Dictate/Refine/Translate
- Empty state handling (first use, after restart)

**Key Scenarios:**
1. Dictate "test text" → injected
2. Press Ctrl+Alt+V → "test text" re-injected
3. Restart app, press Ctrl+Alt+V → no-op (nothing stored)
4. Refine "hello" → "Hello" injected
5. Press Ctrl+Alt+V → "Hello" re-injected (last injected text)

**Validation:**
- Visual: Same text re-injected
- Manual: Verify text matches last injection

#### 3F. Quick Chat (Ctrl+Alt+C)

**Test Coverage:**
- Window appears (always-on-top)
- Text input → LLM response
- Voice input (Mic button) → transcribe → response
- Esc closes window
- Stateless (no history between invocations)

**Key Scenarios:**
1. Press Ctrl+Alt+C → QuickChatWindow appears
2. Type "Hello" in input field, click Send → LLM response displayed
3. Click Mic button, say "What is 2+2", release → "4" response displayed
4. Press Esc → window closes
5. Press Ctrl+Alt+C again → window opens fresh (no previous conversation)
6. Click outside window → window closes (or stays open, depending on design)

**Validation:**
- Visual: Window behavior correct
- Helper: `.\Verify-HistoryDb.ps1 -ExpectedMode "chat"`

---

### Section 4: Audio System
**Goal:** Validate hardware integration
**Time Estimate:** 30 minutes
**Scenarios:** ~10

**Test Coverage:**
- Device enumeration and selection
- Recording lifecycle
- Mute detection (hardware mute state changes)
- Audio ducking (other apps volume reduced during recording)
- Auto-stop on max duration
- Device unplugged during recording

**Key Scenarios:**
1. Settings → Audio → Device dropdown lists all input devices
2. Select different device → recording uses new device
3. Mute microphone → mute indicator appears in UI
4. Unmute microphone → indicator disappears
5. Enable audio ducking, play music, dictate → music volume reduced during recording
6. Disable audio ducking, play music, dictate → music volume unchanged
7. Set max duration to 10s, hold hotkey for 15s → recording stops at 10s
8. Unplug microphone during recording → error handled gracefully

**Validation:**
- Visual: Device selection works, mute indicator correct
- Manual: Listen to audio playback during ducking test
- Helper: `.\Verify-AppSettings.ps1 -SettingPath "AudioSettings.MaxRecordingDuration" -ExpectedValue "10"`

---

### Section 5: Settings Management
**Goal:** Validate all 10 settings tabs
**Time Estimate:** 2 hours
**Scenarios:** ~35

#### 5A. General Settings
- Language selection (EN/ES)
- Auto-start toggle
- Sound feedback toggle
- Trailing space toggle
- Additional key behavior (None/Enter/Tab/Space)

#### 5B. AI Engine Settings
- STT provider switching (Cloud ↔ Local)
- LLM provider switching (Cloud ↔ Ollama ↔ Skip)
- Capability summary display (which providers available)

#### 5C. Modes Settings
- Configure all 8 modes across 2 profiles (16 configs)
- Model selection per mode
- Custom prompt editing (16 slots)
- Profile switching (Local ↔ Cloud)

#### 5D. Audio Settings
- Device selection
- Max recording duration slider
- Audio ducking enable/disable + level (0-100%)

#### 5E. Privacy Settings
- Privacy level slider (Ghost/Stats/Balanced/Full)
- PII scrubber toggle
- One-click data wipe (confirm dialog)

#### 5F. API Keys Settings
- Add/edit/delete keys for all providers (Deepgram, Gemini, OpenAI, Anthropic, etc.)
- Test connection button per provider
- Secure storage (keys.dat encryption)

#### 5G. Ollama Settings
- Model library list (installed models)
- Health check status (online/offline/version)
- Version compatibility warnings
- Fallback model selection

#### 5H. Snippets Settings
- Add/edit/delete snippets
- 100-snippet limit enforcement
- Trigger validation (no duplicates)

#### 5I. Control Panel Config
- Toggle visibility of HUD rows:
  - Modes row
  - Actions row
  - Session stats row
  - Performance stats row

#### 5J. About Page
- Version info display (app version, .NET version)
- Credits and links (GitHub, website)

**Key Scenarios (35 total across all tabs):**
1. General → Change language to Spanish → UI updates
2. General → Enable auto-start → Task Scheduler entry created
3. General → Enable sound feedback → beep on completion
4. AI Engine → Switch STT to Local (Whisper) → dictation uses Whisper
5. AI Engine → Switch LLM to Ollama → processing uses Ollama
6. Modes → Dictate mode → Change model to gpt-4o → dictation uses gpt-4o
7. Modes → Dictate mode → Edit custom prompt → prompt saved
8. Modes → Switch profile to Local → mode configs change
9. Audio → Change max duration to 30s → auto-stop at 30s
10. Audio → Enable ducking at 50% → volume reduced to 50%
11. Privacy → Set to Ghost → history.db has no new entries
12. Privacy → Set to Full → history.db has verbatim text
13. Privacy → Click "Wipe Data" → confirmation dialog → history cleared
14. API Keys → Add Deepgram key → test connection → success
15. API Keys → Invalid key → test connection → failure
16. Ollama → Health check → shows online + version
17. Ollama → Offline → shows offline warning
18. Snippets → Add 5 snippets → all saved
19. Snippets → Try to add 101st snippet → blocked with error
20. Snippets → Edit snippet → changes saved
21. Snippets → Delete snippet → removed from list
22. Control Panel Config → Hide Modes row → row disappears from HUD
23. Control Panel Config → Show all rows → all visible
24. About → Version number displayed correctly
25. About → GitHub link opens browser

(... continue for all 35 scenarios)

**Validation:**
- Helper: `.\Verify-AppSettings.ps1` for each setting
- Helper: `.\Verify-SecureStorage.ps1` for API keys
- Helper: `.\Verify-Snippets.ps1` for snippet CRUD
- Helper: `.\Test-OllamaHealth.ps1` for Ollama status
- Manual: Visual confirmation of UI changes

---

### Section 6: Data & Privacy
**Goal:** Validate persistence, history, and privacy compliance
**Time Estimate:** 45 minutes
**Scenarios:** ~12

**Test Coverage:**
- SQLite history logging (all privacy levels)
- 90-day auto-pruning
- PII scrubbing in Balanced mode
- Ghost mode (zero storage)
- Metrics collection and display
- Session stats aggregation
- Note file persistence
- Settings persistence across restarts

**Key Scenarios:**
1. Privacy = Full → Dictate "My email is test@example.com" → history.db has verbatim text
2. Privacy = Balanced → Same test → history.db has "[REDACTED]" instead of email
3. Privacy = Stats → Same test → history.db has counts only, no text
4. Privacy = Ghost → Same test → history.db has no new entry
5. Insert 100-day-old record in history.db → restart app → record pruned (90-day limit)
6. Dictate 10 phrases → Control Panel shows session count = 10
7. Restart app → Control Panel shows today's count (not session count)
8. Note mode → Write note → file persists across app restart
9. Change setting → restart app → setting persisted
10. Corrupt settings.json (invalid JSON) → app recovers with defaults
11. Corrupt history.db (invalid schema) → app recovers or shows error
12. Delete keys.dat → app treats as no API keys configured

**Validation:**
- Helper: `.\Verify-HistoryDb.ps1` for history entries
- Helper: `.\Verify-AppSettings.ps1` for persistence
- Helper: `.\Verify-SecureStorage.ps1` for keys.dat
- Manual: Check file contents for PII scrubbing

---

### Section 7: System Integration
**Goal:** Validate OS-level integrations
**Time Estimate:** 1 hour
**Scenarios:** ~15

**Test Coverage:**
- Global hotkey registration
- Hotkey conflicts (already taken)
- Hotkey re-registration (change in settings)
- Text injection (Clipboard paste method)
- Clipboard save/restore
- Selection capture (Ctrl+C simulation)
- Auto-start via Task Scheduler
- Tray icon context menu
- Tray icon state changes (Idle/Recording/Processing/Error)
- App minimize to tray
- App exit from tray

**Key Scenarios:**
1. Register Ctrl+Alt+D → hotkey works
2. Register Ctrl+Alt+X (already taken by another app) → error notification
3. Change Dictate hotkey to Ctrl+Shift+D → new hotkey works, old doesn't
4. Dictate text → clipboard restored to original content after injection
5. Copy "test" to clipboard, dictate "hello" → clipboard still has "test" after
6. Refine mode → Ctrl+C captures selection correctly
7. Enable auto-start in Settings → Task Scheduler entry created
8. Tray icon right-click → context menu appears
9. Tray icon shows Idle state (gray/green icon)
10. Start dictation → tray icon shows Recording state (red icon)
11. Processing → tray icon shows Processing state (blue/yellow icon)
12. Error → tray icon shows Error state (red X icon)
13. Close main window → app minimizes to tray (doesn't exit)
14. Tray menu → Exit → app exits completely
15. Task Scheduler → Run task → app starts on boot

**Note:** Auto-start testing validates the Settings toggle and Task Scheduler registration. Full startup behavior from Task Scheduler will be validated after installer is built.

**Validation:**
- Helper: `.\Verify-AutoStart.ps1`
- Manual: Task Manager → check process still running after close
- Manual: Task Scheduler UI → verify entry

---

### Section 8: Error Handling & Edge Cases
**Goal:** Validate graceful degradation and error recovery
**Time Estimate:** 1.5 hours
**Scenarios:** ~18

**Test Coverage:**
- API key invalid/missing
- STT provider offline
- LLM provider offline
- Ollama server not running
- Whisper model not downloaded
- Network timeout during API call
- Corrupt settings.json (recovery)
- Corrupt history.db (recovery)
- Empty audio recording
- Silent audio (no speech)
- Clipboard locked by another app
- No text selected in Refine autopilot
- Wizard interrupted mid-flow
- App restart during recording

**Key Scenarios:**
1. Delete Deepgram API key → dictate → error notification, fallback to local?
2. Invalid API key format → test connection → validation error
3. Disconnect internet → cloud STT → timeout error + fallback
4. Stop Ollama server → LLM call → offline error + fallback
5. Whisper model not downloaded → local STT → download prompt or error
6. Cloud API timeout (simulate slow network) → error after 30s timeout
7. Corrupt settings.json (extra comma) → app recovers with defaults + backup created
8. Corrupt history.db (delete a table) → app rebuilds schema or shows error
9. Dictate with no speech (silence) → empty transcript, no injection
10. Dictate ambient noise (no words) → garbled transcript or empty
11. Lock clipboard with another app → injection fails gracefully
12. Refine autopilot with no selection → error notification
13. Exit app mid-wizard (step 3) → next launch resumes wizard at step 3
14. Kill app process during recording → next launch recovers (no stuck state)
15. Disk full during settings save → error notification, settings not corrupted
16. Disk full during history insert → error logged, app continues
17. Network disconnects during LLM streaming → partial response or timeout
18. Ollama version too old (< 0.1.0) → compatibility warning + fallback

**Validation:**
- Visual: Error notifications appear with actionable messages
- Helper: `.\Get-AppState.ps1` after each error to check state
- Manual: Check logs in `%APPDATA%\DiktaMe\logs\` for error details

---

### Section 9: Performance & Polish
**Goal:** Validate non-functional requirements
**Time Estimate:** 30 minutes
**Scenarios:** ~10

**Test Coverage:**
- Startup time (<3s in cloud mode)
- Memory footprint (<80MB idle)
- Recording latency (visual feedback)
- Pipeline end-to-end latency
- Control Panel real-time updates
- Settings window responsiveness
- Dark mode support (if applicable)
- Window focus handling
- Icon and branding consistency

**Key Scenarios:**
1. Launch app → measure startup time → <3s to tray icon
2. Task Manager → check memory usage idle → <80MB
3. Press hotkey → visual feedback appears <100ms
4. Dictate short phrase → total latency (record + STT + LLM + inject) <5s
5. Control Panel updates in real-time during dictation (Recording → Transcribing → Processing → Idle)
6. Settings window opens quickly (<500ms)
7. Settings tabs switch instantly
8. Dark mode (if supported) → all UI elements render correctly
9. Dictate in Notepad → focus returns to Notepad after injection
10. All icons (tray, window, buttons) use consistent branding (dIKta.me logo)

**Validation:**
- Manual: Stopwatch for timing measurements
- Manual: Task Manager for memory usage
- Visual: UI responsiveness and polish

---

## 4. Automated Voice Testing (Section 10)

### Section 10: Audio Feeder Automation
**Goal:** Validate transcription quality with diverse real voices and accents
**Time Estimate:** 2 hours setup + 1-3 hours runs
**Scenarios:** ~5 test runs

### 4.1 Overview

The **Audio Feeder** tool automates dictation testing by:
1. Downloading YouTube videos with captions/transcripts (yt-dlp)
2. Slicing audio based on subtitle timestamps into realistic phrases (8-10 seconds)
3. Playing audio through system speakers while controlling the app via IPC
4. Comparing transcription output to expected subtitle text (manual spot-check)
5. Tracking success rates, latency per phrase, and failures

**Key Features:**
- **Smart Mode:** Uses subtitles to create phrase-length test cases (8-10s each)
- **Dumb Mode:** Fixed-length chunks (10s default) for continuous audio
- **Real voices:** Different accents, speaking styles, background noise
- **Reproducible:** Same audio inputs → comparable results across runs
- **Automated:** Can run 100+ phrases unattended (20-60 minutes per run)
- **Statistics:** Success rate, avg latency per phrase, ETA tracking, interruption handling (Ctrl+C)

### 4.2 Migration from V1

**Source:** `E:\git\diktate\python\tools\audio_feeder.py` (787 lines)

**Components to Port:**

1. **IPC Protocol** — App must expose TCP server (or named pipe) to accept test commands:
   - **Commands:**
     - `START` → begins recording (returns `OK` or `BUSY`)
     - `STOP` → stops recording (returns `OK`)
     - `STATUS` → returns current state (`idle`, `recording`, `transcribing`, `processing`, `error`)
     - `PING` → health check (returns `PONG`)
   - **Implementation Options:**
     - **Option A:** C# TCP server in `DiktaMe.Core.Testing.IpcServer` (always listening on `127.0.0.1:5005`)
     - **Option B:** PowerShell TCP listener in `Start-IpcServer.ps1` (separate process)
     - **Option C:** Named pipe server (Windows-native IPC)
   - **Security:** Only enabled with `--enable-ipc` command-line flag (prevent abuse)

2. **Audio Playback** — Play WAV files through speakers with precise timing:
   - **Option A:** NAudio in PowerShell (via Add-Type or separate C# helper)
   - **Option B:** Windows Media Player COM object (simpler but less control)
   - **Option C:** `soundplayer` .NET class (limited to WAV)

3. **Subtitle Parsing** — Parse SRT files to extract timestamps and text:
   - PowerShell native (regex-based)
   - Or C# helper class with SRT parser

4. **YouTube Download** — Wrapper around `yt-dlp`:
   ```powershell
   yt-dlp --extract-audio --audio-format wav --write-auto-sub --sub-lang en --output "%(title)s.%(ext)s" $Url
   ```

5. **Statistics Tracking** — Count success/failure, calculate ETA, print summary

**Ported Files:**

| V1 File | V2 File | Purpose |
|---------|---------|---------|
| `audio_feeder.py` | `Invoke-AudioFeeder.ps1` | Main orchestration script |
| N/A | `Download-TestAudio.ps1` | YouTube download wrapper |
| N/A | `IpcServer.cs` or `Start-IpcServer.ps1` | TCP server for test automation |

### 4.3 Test Scenarios

#### Scenario 1: TED Talk (Clear, Formal English)
- **Source:** TED talk video with English captions
- **Duration:** 10-15 minutes
- **Phrases:** ~50-100 (8-10s each in smart mode)
- **Expected:** High accuracy (95%+), low latency (<3s per phrase)
- **Validation:** Manual spot-check of 10 random phrases

#### Scenario 2: Podcast (Casual, Background Music)
- **Source:** Podcast with background music/intro
- **Duration:** 5-10 minutes
- **Phrases:** ~30-60
- **Expected:** Lower accuracy (80-90%), some music interference
- **Validation:** Check history.db for transcriptions

#### Scenario 3: British Accent (Accent Diversity)
- **Source:** BBC documentary or British YouTuber
- **Duration:** 5 minutes
- **Phrases:** ~30
- **Expected:** Good accuracy (90%+), accent handled well
- **Validation:** Spot-check British-specific pronunciations

#### Scenario 4: Fast Speech (Sports Commentary)
- **Source:** Sports highlight with rapid commentary
- **Duration:** 3-5 minutes
- **Phrases:** ~20-30
- **Expected:** Moderate accuracy (75-85%), some words missed
- **Validation:** Check for dropped words

#### Scenario 5: Multi-Speaker (Interview/Debate)
- **Source:** Interview or debate with 2+ speakers
- **Duration:** 5 minutes
- **Phrases:** ~30
- **Expected:** Good accuracy (85-90%), speaker changes handled
- **Validation:** Check speaker attribution (if supported)

### 4.4 Usage Example

```powershell
# 1. Download test audio
.\Download-TestAudio.ps1 -Url "https://youtube.com/watch?v=..." -OutputDir "tests/fixtures/downloads"

# 2. Start app with IPC enabled
Start-Process DiktaMe.exe -ArgumentList "--enable-ipc"

# 3. Run audio feeder (smart mode, 20 phrases)
.\Invoke-AudioFeeder.ps1 `
  -AudioFile "tests/fixtures/downloads/ted_talk.wav" `
  -SubtitleFile "tests/fixtures/downloads/ted_talk.en.srt" `
  -Count 20 `
  -StartAt 0

# 4. Review statistics
# Output:
# ======================================================
# TEST SUMMARY
# ======================================================
# Total Phrases: 20
# [OK] Success:  18
# [X] Failed:    2
# [-] Skipped:   0
# Duration:      5.2 minutes (312s)
# Avg per phrase: 15.6s
# Success rate:   90.0%
# ======================================================

# 5. Check history.db for transcriptions
.\Verify-HistoryDb.ps1
```

### 4.5 Success Criteria

- ✅ Tool successfully feeds audio to V2 app via IPC
- ✅ Transcription accuracy visible in app output (manual spot-check of 10 phrases)
- ✅ Statistics tracking works (success rate, latency, ETA)
- ✅ Can pause/resume tests (Ctrl+C graceful shutdown)
- ✅ Reproducible results across runs (same audio → similar accuracy)
- ✅ TED talk scenario achieves 90%+ success rate
- ✅ Diverse accents tested (British, Indian, Australian)

---

## 5. Testing Workflow

### 5.1 Starting a Session

1. Open `MANUAL_TEST_PLAN.md` in your editor (VS Code, Obsidian, etc.)
2. Optionally run `.\New-TestSession.ps1` to generate `test-session-log.md` (working copy)
3. Work through sections sequentially (1 → 2 → ... → 10)
4. Check off `- [ ]` items as you complete them
5. Note any bugs/issues in the session log or GitHub Issues

### 5.2 Pausing and Resuming

- Your markdown editor saves checkbox state automatically (` - [ ]` → `- [x]`)
- Session log is gitignored (won't pollute commits)
- Can resume anytime — unchecked boxes show remaining work
- If needed, re-run `.\New-TestSession.ps1` to start fresh

### 5.3 Validation Methods

| Validation Type | Method | Example |
|-----------------|--------|---------|
| **Visual** | Observe UI behavior | "Window appears", "Text injected" |
| **File** | Check file existence/contents | "settings.json has WizardCompleted=true" |
| **Helper Script** | Run PowerShell validation | `.\Verify-AppSettings.ps1 ...` |
| **Database** | Query SQLite history.db | `.\Verify-HistoryDb.ps1 ...` |
| **System** | Check Task Scheduler, Registry, etc. | `.\Verify-AutoStart.ps1` |

### 5.4 Bug Tracking

When you find a bug:
1. **Document in session log:** Add note with `❌ BUG:` prefix
2. **Create GitHub Issue:** If critical, create issue immediately
3. **Continue testing:** Don't block on bugs (mark as known failure, move on)
4. **Fix in batch:** After testing session, prioritize and fix bugs

**Example Session Log Entry:**
```markdown
- [x] Dictate with Deepgram → ✅ PASS
- [x] Dictate with Gemini → ❌ BUG: Timeout after 30s (Issue #42)
- [ ] Dictate with Whisper → (pending)
```

---

## 6. File Structure

```
E:\git\diktame\
├── plans/
│   └── LIVE_TESTING_PLAN.md         ← This document (tracked in git)
├── MANUAL_TEST_PLAN.md              ← Master checklist (tracked in git)
├── test-helpers/                    ← PowerShell validation scripts (tracked)
│   ├── Verify-AppSettings.ps1
│   ├── Verify-HistoryDb.ps1
│   ├── Verify-SecureStorage.ps1
│   ├── Verify-AutoStart.ps1
│   ├── Verify-FileSystem.ps1
│   ├── Verify-Snippets.ps1
│   ├── Test-OllamaHealth.ps1
│   ├── Get-AppState.ps1
│   ├── New-TestSession.ps1
│   ├── Invoke-AudioFeeder.ps1       ← Automated voice testing (ported from V1)
│   ├── Download-TestAudio.ps1       ← YouTube downloader wrapper (yt-dlp)
│   └── Start-IpcServer.ps1          ← IPC server (if not built into app)
├── tests/fixtures/downloads/        ← YouTube audio + subtitle files (gitignored)
├── test-session-log.md              ← Your working copy (gitignored)
└── src/DiktaMe.Core/Testing/        ← IPC server for test automation (optional)
    └── IpcServer.cs                 ← TCP server (START/STOP/STATUS commands)
```

---

## 7. Deliverables

### Phase 1: Manual Testing Infrastructure

1. ✅ **`MANUAL_TEST_PLAN.md`** with ~125 test scenarios (9 manual + 1 automated)
2. ✅ **8 core validation helper scripts** in `test-helpers/`
3. ✅ **Session management:** `New-TestSession.ps1` + `.gitignore` entry
4. ✅ **Documentation:** This plan document (`LIVE_TESTING_PLAN.md`)

### Phase 2: Audio Feeder Automation

5. ✅ **`Invoke-AudioFeeder.ps1`** — Ported from V1's `audio_feeder.py`
6. ✅ **`Download-TestAudio.ps1`** — YouTube downloader wrapper
7. ✅ **IPC Server** — `IpcServer.cs` or `Start-IpcServer.ps1`
8. ✅ **`.gitignore`** entries for `tests/fixtures/downloads/`

### Phase 3: Testing Execution

9. ✅ **Section 1-9 Complete:** All manual scenarios tested, bugs logged
10. ✅ **Section 10 Complete:** Audio feeder runs complete, accuracy measured
11. ✅ **Bug Fixes:** All critical bugs fixed, non-critical prioritized
12. ✅ **Polish:** UI tweaks, performance tuning, edge case handling

### Phase 4: Post-Testing

13. ✅ **Installer (Task H.1):** After polish is complete, create installer
14. ✅ **Final QA:** Smoke test installer on clean machine
16. ✅ **Release Prep:** Update README, CHANGELOG, website

---

## 8. Time Estimates

| Phase | Activity | Time |
|-------|----------|------|
| **Setup** | Create test plan + helpers | 4-6 hours |
| **Manual Testing** | Sections 1-9 execution | 9-10 hours |
| **Audio Feeder** | Port tool + IPC server | 2-3 hours |
| **Audio Feeder** | Run test scenarios | 1-3 hours |
| **Bug Fixes** | Fix critical bugs (estimate) | 4-8 hours |
| **Polish** | UI tweaks, performance | 2-4 hours |
| **Total** | **22-34 hours** | ~1 week |

**Assumptions:**
- 1 person (you) doing all testing
- Can spread across multiple days/sessions
- Bugs are typical (not showstoppers)
- Audio feeder porting is straightforward

---

## 9. Success Criteria

### Manual Testing (Sections 1-9)
- ✅ All 125 scenarios executed at least once
- ✅ 95%+ scenarios pass (120+ / 125)
- ✅ All critical bugs fixed (blocking issues)
- ✅ Non-critical bugs documented (GitHub Issues)
- ✅ Performance targets met (startup <3s, memory <80MB)

### Automated Testing (Section 10)
- ✅ Audio feeder tool successfully controls app via IPC
- ✅ TED talk scenario achieves 90%+ transcription success rate
- ✅ Diverse accents tested (3+ different accents/speakers)
- ✅ Statistics tracking accurate (success rate, latency)
- ✅ Tool is reproducible (same audio → similar results)

### Overall Polish
- ✅ UI is visually consistent (branding, icons, fonts)
- ✅ Error messages are actionable (tell user what to do)
- ✅ No crashes or data loss in normal usage
- ✅ Settings persist correctly across restarts
- ✅ Privacy levels work as documented (Ghost/Stats/Balanced/Full)

---

## 10. Next Steps

1. **Review this plan** — Confirm approach and scope
2. **Create `MANUAL_TEST_PLAN.md`** — Detailed checklist with all 125 scenarios
3. **Implement helper scripts** — 8 core + 3 audio feeder scripts
4. **Port audio feeder** — Migrate V1's `audio_feeder.py` to PowerShell
5. **Build IPC server** — TCP server in C# or PowerShell
6. **Execute manual tests** — Work through Sections 1-9
7. **Run audio feeder** — Execute Section 10 scenarios
8. **Fix bugs** — Prioritize and resolve issues
9. **Polish** — UI tweaks, performance tuning
10. **Build installer** — Task H.1 (post-testing)

---

**Document Status:** Draft
**Next Review:** After plan approval
**Owner:** @geckogtmx
