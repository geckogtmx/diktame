# dIKta.me V2 — Manual Test Plan (Journey-Based)

**Date:** 2026-02-19 (Revised)
**Purpose:** Comprehensive end-to-end testing following complete user journeys
**Approach:** Each journey follows one configuration path from setup to completion
**Total Journeys:** 4 core paths + cross-cutting tests
**Time Estimate:** 12-16 hours total

---

## 🎯 Testing Philosophy

This test plan is organized by **complete user journeys** rather than fragmented feature tests. Each journey follows a realistic user path from wizard setup through daily usage, ensuring all components work together end-to-end.

**Between journeys:** Delete `%APPDATA%\DiktaMe\settings.json` to reset the app and test a different configuration path.

---

## How to Use This Plan

1. **Pick a journey** (start with Journey 1)
2. **Follow it completely** from wizard to final verification
3. **Check off items** as you complete them: `- [ ]` → `- [x]`
4. **Document bugs** in the Notes section at the bottom
5. **Reset and repeat** for the next journey

**Helper Scripts:** Run from `test-helpers/` directory when referenced

---

# Journey 1: Cloud STT (Deepgram) + Cloud LLM Path

**Configuration:** Deepgram STT + OpenAI/Gemini/Anthropic LLM
**Duration:** ~3 hours
**Goal:** Validate complete cloud-based workflow with API key management

## 1.1: First-Run Setup (Deepgram Path)

**Prerequisites:**
```powershell
Remove-Item "$env:APPDATA\DiktaMe\settings.json" -Force -ErrorAction SilentlyContinue
```

- [x] **1.1.1** Launch app → Loading screen → Wizard appears
- [x] **1.1.2** Step 1 (Welcome) → dIKta.me branding + "Build Your Stack" text
- [x] **1.1.3** Step 2 (STT) → Select **Cloud** option
- [x] **1.1.4** Step 2 (STT) → Select **Deepgram** from cloud providers
- [x] **1.1.5** Step 3 (LLM) → Select **Cloud** option
- [x] **1.1.6** Step 3 (LLM) → Select **OpenAI** (or Gemini/Anthropic)
- [ ] **1.1.7** Step 3 (LLM) → Enter API key for selected LLM provider
- [ ] **1.1.8** Step 4 (Test) → Record 3 seconds of audio
- [ ] **1.1.9** Step 4 (Test) → Transcription appears (Deepgram transcribed)
- [ ] **1.1.10** Step 4 (Test) → Can re-record if needed
- [ ] **1.1.11** Step 5 (Ready) → Summary shows Deepgram + LLM provider
- [ ] **1.1.12** Step 5 (Ready) → Click "Start Dictating" → Wizard closes
- [ ] **1.1.13** Verify settings saved: `.\test-helpers\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"`

## 1.2: Core Dictation (Deepgram + LLM)

- [ ] **1.2.1** Open Notepad
- [ ] **1.2.2** Press Ctrl+Alt+D, speak "Hello world", release → text injected
- [ ] **1.2.3** Verify text appears in Notepad
- [ ] **1.2.4** Check Control Panel shows: Recording → Transcribing → Processing → Idle
- [ ] **1.2.5** Verify history.db: `.\test-helpers\Verify-HistoryDb.ps1 -ExpectedMode "dictate"`
- [ ] **1.2.6** Confirm stt_provider = "Deepgram" in history
- [ ] **1.2.7** Dictate messy text: "um hello uh world like yeah" → LLM cleans to "Hello world"
- [ ] **1.2.8** Settings → Enable **Raw Mode** → Dictate again → Raw transcript injected (no LLM)
- [ ] **1.2.9** Settings → Disable Raw Mode

## 1.3: Settings Configuration (Cloud Path)

- [ ] **1.3.1** Settings → General → Enable trailing space → Dictate → text ends with space
- [ ] **1.3.2** Settings → General → Disable trailing space → Dictate → no space
- [ ] **1.3.3** Settings → General → Additional Key = **Enter** → Dictate → text + Enter
- [ ] **1.3.4** Settings → General → Additional Key = **Tab** → Dictate → text + Tab
- [ ] **1.3.5** Settings → General → Additional Key = **None**
- [ ] **1.3.6** Settings → Audio → Enable ducking → Play music → Dictate → music volume drops
- [ ] **1.3.7** Settings → Audio → Disable ducking → Dictate → music unchanged
- [ ] **1.3.8** Settings → Audio → Set max duration = 10s → Hold hotkey 15s → stops at 10s
- [ ] **1.3.9** Verify settings persist: Restart app → Settings still correct

## 1.4: Advanced Modes (Deepgram + LLM)

### Refine Mode
- [ ] **1.4.1** Type "hello wrold" in Notepad, select it
- [ ] **1.4.2** Press Ctrl+Alt+R (autopilot) → "hello world" replaces selection
- [ ] **1.4.3** Type "make this better", select it
- [ ] **1.4.4** Hold Ctrl+Alt+R, say "more professional", release → refined text replaces
- [ ] **1.4.5** Press Ctrl+Alt+R with no selection → Error notification

### Ask Mode
- [ ] **1.4.6** Press Ctrl+Alt+A, say "What is 2 plus 2", release
- [ ] **1.4.7** Answer "4" appears in notification/UI (not injected to Notepad)
- [ ] **1.4.8** Verify history.db mode='ask'

### Translate Mode
- [ ] **1.4.9** Press Ctrl+Alt+T, say "Hello how are you", release
- [ ] **1.4.10** "Hola cómo estás" injected to Notepad
- [ ] **1.4.11** Press Ctrl+Alt+T, say "Hola cómo estás", release
- [ ] **1.4.12** "Hello how are you" injected (bidirectional)

### Note Mode
- [ ] **1.4.13** Press Ctrl+Alt+N, say "Remember to test snippets", release
- [ ] **1.4.14** Check `%USERPROFILE%\Documents\diktame-notes.md` has timestamp + note
- [ ] **1.4.15** Verify: `.\test-helpers\Verify-FileSystem.ps1 -Path "%USERPROFILE%\Documents\diktame-notes.md" -Type File`

### Oops Mode
- [ ] **1.4.16** Dictate "test text" → injected
- [ ] **1.4.17** Press Ctrl+Alt+V → "test text" re-injected
- [ ] **1.4.18** Restart app → Press Ctrl+Alt+V → No-op (nothing stored)

### Quick Chat
- [ ] **1.4.19** Press Ctrl+Alt+C → QuickChatWindow appears (always-on-top)
- [ ] **1.4.20** Type "What is the capital of Spain" → Click Send → "Madrid" appears
- [ ] **1.4.21** Click Mic button, say "What is 5 plus 5", release → "10" appears
- [ ] **1.4.22** Press Esc → Window closes
- [ ] **1.4.23** Press Ctrl+Alt+C again → Window opens fresh (no history)

## 1.5: Voice Snippets (Cloud Path)

- [ ] **1.5.1** Settings → Snippets → Add snippet: Trigger="my email", Content="test@example.com"
- [ ] **1.5.2** Dictate "Send to my email please" → expands to "Send to test@example.com please"
- [ ] **1.5.3** Dictate "MY EMAIL" (uppercase) → expands (case-insensitive)
- [ ] **1.5.4** Dictate "my email, thanks" → expands before punctuation
- [ ] **1.5.5** Add snippet: Trigger="my phone", Content="555-1234"
- [ ] **1.5.6** Dictate "my email and my phone" → both expand
- [ ] **1.5.7** Edit snippet → Changes saved
- [ ] **1.5.8** Delete snippet → Removed
- [ ] **1.5.9** Verify: `.\test-helpers\Verify-Snippets.ps1`

## 1.6: API Keys & Security (Cloud Path)

- [ ] **1.6.1** Settings → API Keys → Deepgram key visible (masked)
- [ ] **1.6.2** Settings → API Keys → LLM key visible (masked)
- [ ] **1.6.3** Settings → API Keys → Click **Test Connection** on Deepgram → Success
- [ ] **1.6.4** Settings → API Keys → Click **Test Connection** on LLM → Success
- [ ] **1.6.5** Settings → API Keys → Enter invalid key → Test fails with error
- [ ] **1.6.6** Verify keys encrypted: `.\test-helpers\Verify-SecureStorage.ps1`
- [ ] **1.6.7** Restart app → Keys still work (persisted)

## 1.7: Data & Privacy (Cloud Path)

- [ ] **1.7.1** Settings → Privacy → Set to **Full** (verbatim logging)
- [ ] **1.7.2** Dictate "My email is test@example.com" → Check history.db has verbatim
- [ ] **1.7.3** Settings → Privacy → Set to **Balanced** (PII scrubbed)
- [ ] **1.7.4** Dictate "My email is test@example.com" → Check history.db has [REDACTED]
- [ ] **1.7.5** Settings → Privacy → Set to **Stats** (counts only)
- [ ] **1.7.6** Dictate "test" → Check history.db has no text, only count
- [ ] **1.7.7** Settings → Privacy → Set to **Ghost** (zero storage)
- [ ] **1.7.8** Dictate "test" → Check history.db has no new entry
- [ ] **1.7.9** Settings → Privacy → Click **Wipe Data** → Confirm → history.db cleared
- [ ] **1.7.10** Verify: `.\test-helpers\Verify-HistoryDb.ps1`

## 1.8: System Integration (Cloud Path)

- [ ] **1.8.1** Settings → General → Enable auto-start → Task Scheduler entry created
- [ ] **1.8.2** Verify: `.\test-helpers\Verify-AutoStart.ps1`
- [ ] **1.8.3** Tray icon right-click → Menu appears
- [ ] **1.8.4** Tray icon shows Idle state (green/gray)
- [ ] **1.8.5** Dictate → Tray icon shows Recording state (red)
- [ ] **1.8.6** Processing → Tray icon shows Processing state (blue/yellow)
- [ ] **1.8.7** Copy "test" to clipboard → Dictate "hello" → Clipboard still has "test"
- [ ] **1.8.8** Close main window → App minimizes to tray (doesn't exit)
- [ ] **1.8.9** Tray → Exit → App exits completely

## 1.9: Performance (Cloud Path)

- [ ] **1.9.1** Restart app → Time from launch to tray icon: <3 seconds
- [ ] **1.9.2** Task Manager → Memory usage idle: <80MB
- [ ] **1.9.3** Press Ctrl+Alt+D → Visual feedback <100ms
- [ ] **1.9.4** Dictate short phrase → Total latency <5s (record + STT + LLM + inject)
- [ ] **1.9.5** Settings window opens <500ms
- [ ] **1.9.6** All icons/branding consistent (dIKta.me logo)

## 1.10: Journey 1 Complete ✅

**Summary:** Document any bugs found, total time spent, overall impressions

---

# Journey 2: Cloud STT (Gemini Audio) + Cloud LLM Path

**Configuration:** Gemini Audio STT + Gemini LLM
**Duration:** ~1.5 hours
**Goal:** Validate Gemini-only cloud workflow

**Prerequisites:**
```powershell
Remove-Item "$env:APPDATA\DiktaMe\settings.json" -Force -ErrorAction SilentlyContinue
```

## 2.1: Setup (Gemini Audio Path)

- [ ] **2.1.1** Launch app → Wizard appears
- [ ] **2.1.2** Step 2 (STT) → Select **Cloud** → Select **Gemini Audio**
- [ ] **2.1.3** Step 3 (LLM) → Select **Cloud** → Select **Gemini**
- [ ] **2.1.4** Step 3 (LLM) → Enter Gemini API key
- [ ] **2.1.5** Step 4 (Test) → Record audio → Gemini transcription appears
- [ ] **2.1.6** Step 5 (Ready) → Summary shows Gemini Audio + Gemini LLM
- [ ] **2.1.7** Complete wizard

## 2.2: Core Functionality (Gemini Path)

- [ ] **2.2.1** Dictate "Hello world" → Text injected
- [ ] **2.2.2** Verify history.db stt_provider = "GeminiAudio"
- [ ] **2.2.3** Verify history.db llm_provider = "Gemini"
- [ ] **2.2.4** Test all 6 modes (Dictate, Refine, Ask, Translate, Note, Oops)
- [ ] **2.2.5** Test Quick Chat (text + voice input)
- [ ] **2.2.6** Verify API key persists: Restart app → Still works

## 2.3: Gemini-Specific Tests

- [ ] **2.3.1** Settings → API Keys → Test Gemini connection → Success
- [ ] **2.3.2** Dictate long phrase (30+ seconds) → Handles correctly
- [ ] **2.3.3** Check latency: Gemini STT + LLM pipeline <6s total
- [ ] **2.3.4** Privacy: Balanced mode → PII scrubbing works

## 2.4: Journey 2 Complete ✅

---

# Journey 3: Local STT (Whisper) + Local LLM (Ollama) Path

**Configuration:** Whisper (local) + Ollama (local)
**Duration:** ~2 hours
**Goal:** Validate fully offline/local workflow

**Prerequisites:**
```powershell
Remove-Item "$env:APPDATA\DiktaMe\settings.json" -Force -ErrorAction SilentlyContinue
# Ensure Ollama is running: ollama serve
```

## 3.1: Setup (Local Path)

- [ ] **3.1.1** Verify Ollama running: `.\test-helpers\Test-OllamaHealth.ps1`
- [ ] **3.1.2** Launch app → Wizard appears
- [ ] **3.1.3** Step 2 (STT) → Select **Local** → Whisper option appears
- [ ] **3.1.4** Step 2 (STT) → Confirm Whisper model download (if needed)
- [ ] **3.1.5** Step 3 (LLM) → Select **Ollama**
- [ ] **3.1.6** Step 3 (LLM) → Confirm localhost:11434 detected
- [ ] **3.1.7** Step 3 (LLM) → Select model from installed models
- [ ] **3.1.8** Step 4 (Test) → Record audio → Whisper transcription appears
- [ ] **3.1.9** Step 5 (Ready) → Summary shows Whisper + Ollama
- [ ] **3.1.10** Complete wizard

## 3.2: Core Functionality (Local Path)

- [ ] **3.2.1** Dictate "Hello world" → Text injected
- [ ] **3.2.2** Verify history.db stt_provider = "Whisper"
- [ ] **3.2.3** Verify history.db llm_provider = "Ollama"
- [ ] **3.2.4** Test all 6 modes work locally (no internet required)
- [ ] **3.2.5** Disconnect internet → Dictate → Still works (fully offline)
- [ ] **3.2.6** Reconnect internet

## 3.3: Ollama Management

- [ ] **3.3.1** Settings → Ollama → Health check shows online + version
- [ ] **3.3.2** Settings → Ollama → Model library lists installed models
- [ ] **3.3.3** Stop Ollama (`ollama stop`) → Dictate → Error notification + fallback
- [ ] **3.3.4** Start Ollama → Dictate → Works again
- [ ] **3.3.5** Settings → Ollama → Switch model → Dictate uses new model

## 3.4: Whisper Performance

- [ ] **3.4.1** Dictate short phrase (5s) → Check latency <3s
- [ ] **3.4.2** Dictate long phrase (30s) → Check latency <8s
- [ ] **3.4.3** Verify Whisper model size (Turbo vs Base vs Large)
- [ ] **3.4.4** Check CUDA usage (if GPU available)

## 3.5: Journey 3 Complete ✅

---

# Journey 4: Hybrid Path (Cloud STT + Skip LLM)

**Configuration:** Deepgram STT + No LLM (Skip)
**Duration:** ~1 hour
**Goal:** Validate raw transcription without LLM processing

**Prerequisites:**
```powershell
Remove-Item "$env:APPDATA\DiktaMe\settings.json" -Force -ErrorAction SilentlyContinue
```

## 4.1: Setup (Skip LLM Path)

- [ ] **4.1.1** Launch app → Wizard appears
- [ ] **4.1.2** Step 2 (STT) → Select **Cloud** → Deepgram
- [ ] **4.1.3** Step 3 (LLM) → Select **Skip** (no LLM)
- [ ] **4.1.4** Step 4 (Test) → Record audio → Raw transcription appears
- [ ] **4.1.5** Step 5 (Ready) → Summary shows Deepgram + No LLM
- [ ] **4.1.6** Complete wizard

## 4.2: Core Functionality (No LLM)

- [ ] **4.2.1** Dictate "um hello uh world like yeah" → Raw text injected (not cleaned)
- [ ] **4.2.2** Verify history.db llm_provider = null or "None"
- [ ] **4.2.3** Dictate mode works (raw transcript)
- [ ] **4.2.4** Note mode works (raw notes)
- [ ] **4.2.5** Translate mode disabled or skipped (requires LLM)
- [ ] **4.2.6** Ask mode disabled or skipped (requires LLM)
- [ ] **4.2.7** Refine mode disabled or skipped (requires LLM)
- [ ] **4.2.8** Quick Chat disabled or shows error (requires LLM)

## 4.3: Journey 4 Complete ✅

---

# Cross-Cutting Tests (All Journeys)

**These tests apply regardless of configuration**

## Error Handling & Edge Cases

- [ ] **E.1** Invalid API key → Test connection fails with clear error
- [ ] **E.2** Disconnect internet (cloud path) → STT timeout → Error notification
- [ ] **E.3** Stop Ollama (local path) → LLM fails → Error notification
- [ ] **E.4** Corrupt settings.json → App recovers with defaults + backup
- [ ] **E.5** Dictate silence (no speech) → Empty transcript, no injection
- [ ] **E.6** Dictate ambient noise → Garbled or empty transcript
- [ ] **E.7** Clipboard locked by another app → Injection fails gracefully
- [ ] **E.8** Refine with no selection → Error notification
- [ ] **E.9** Kill app during recording → Restart recovers (no stuck state)
- [ ] **E.10** Disk full → Error notification, app continues
- [ ] **E.11** Unplug microphone during recording → Error handled gracefully
- [ ] **E.12** Exit wizard mid-way → Restart resumes at same step

## Settings Persistence

- [ ] **P.1** Change 5 different settings → Restart → All persist
- [ ] **P.2** Add snippet → Restart → Snippet still there
- [ ] **P.3** Change hotkey → Restart → New hotkey works
- [ ] **P.4** Enable auto-start → Restart → Task still registered

## UI/UX Polish

- [ ] **U.1** All windows use consistent dIKta.me branding
- [ ] **U.2** Icons are crisp and consistent
- [ ] **U.3** Fonts are readable and consistent
- [ ] **U.4** Dark mode (if supported) renders correctly
- [ ] **U.5** No visual glitches or layout issues
- [ ] **U.6** Error messages are actionable and clear
- [ ] **U.7** Loading states are visible and smooth

---

# Section 10: Automated Voice Testing (Audio Feeder)

**Prerequisites:** After completing at least Journey 1

## 10.1: Setup Audio Feeder

- [ ] **10.1.1** Port `audio_feeder.py` to `Invoke-AudioFeeder.ps1` (2-3 hours)
- [ ] **10.1.2** Create `Download-TestAudio.ps1` wrapper
- [ ] **10.1.3** Build IPC server (C# or PowerShell on port 5005)
- [ ] **10.1.4** Download test video: `.\test-helpers\Download-TestAudio.ps1 -Url "..."`
- [ ] **10.1.5** Launch app with `--enable-ipc` flag
- [ ] **10.1.6** Test IPC: Send `PING` → Receive `PONG`

## 10.2: Audio Feeder Test Runs

- [ ] **10.2.1** TED Talk (Clear English) → 20 phrases → >90% accuracy
- [ ] **10.2.2** Podcast (Background Music) → 30 phrases → 80-90% accuracy
- [ ] **10.2.3** British Accent (BBC) → 30 phrases → >85% accuracy
- [ ] **10.2.4** Fast Speech (Sports) → 20 phrases → 75-85% accuracy
- [ ] **10.2.5** Multi-Speaker (Interview) → 30 phrases → 85-90% accuracy

## 10.3: Validation

- [ ] **10.3.1** Audio feeder prints statistics (Total, Success, Failed, Success Rate)
- [ ] **10.3.2** Spot-check 10 random transcriptions vs expected
- [ ] **10.3.3** Average latency per phrase <10s
- [ ] **10.3.4** Can pause (Ctrl+C) and resume
- [ ] **10.3.5** Results reproducible (same audio → similar accuracy)

---

# Notes & Bugs

## Bugs Found

Format: `- [ ] [ID] Description`

```
(Add bugs here as you find them)
```

## Observations

```
(General notes about behavior, performance, polish)
```

## Follow-up Items

```
(Specific tests to re-run or edge cases to investigate)
```

---

# Testing Summary

**Journeys Completed:**
- [ ] Journey 1: Cloud (Deepgram) + LLM ✅
- [ ] Journey 2: Cloud (Gemini Audio) + Gemini LLM ✅
- [ ] Journey 3: Local (Whisper) + Ollama ✅
- [ ] Journey 4: Hybrid (Cloud STT + Skip LLM) ✅
- [ ] Cross-Cutting Tests ✅
- [ ] Audio Feeder Automation ✅

**Total Time:** ___ hours
**Bugs Found:** ___ critical, ___ important, ___ minor
**Success Rate:** ___% scenarios passed

---

**Ready for installer creation when all journeys complete!** 🎯
