# dIKta.me V2 — Manual Test Plan (Journey-Based)

**Date:** 2026-04-13 (RC2)
**Purpose:** Comprehensive end-to-end testing following complete user journeys
**Approach:** Each journey follows one configuration path from setup to completion
**Total Journeys:** 5 core paths + 2 feature journeys + cross-cutting tests + audio feeder
**Total Scenarios:** ~400

**Time Breakdown:**
- Journey 1 (Cloud/Deepgram): ~3 hours
- Journey 2 (Gemini): ~1.5 hours
- Journey 3 (Local/Ollama): ~2 hours
- Journey 4 (Hybrid/Skip LLM): ~1 hour
- Journey 5 (Settings): ~2.5 hours
- Journey 6 (Wallet/Auth/License): ~1.5 hours
- Journey 7 (TTS): ~1 hour
- Cross-Cutting (themes, CP, Vision, auto-update, streaming, errors): ~2 hours
- Audio Feeder: ~3-5 hours (setup + runs)
- Bug fixes: ~4-8 hours (estimated)
- **Total:** 22-32 hours

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

**Wizard flow: Language → Get Started → [Features (Wallet) or STT/LLM/TTS (BYOK/Local)] → Test → Ready**
**BYOK/Local paths show lane-filtered options (cloud-only or local-only). API keys entered inline on each step.**

### Wallet Path (Unlicensed)
- [x] **1.1.1** Launch app → Loading screen → Wizard appears
- [x] **1.1.2** Step 0 (Language) → Select UI language
- [x] **1.1.3** Step 1 (Get Started) → Wallet is default and only enabled option. BYOK/Local visible but disabled. "I Have a Key!" red button on left
- [x] **1.1.3a** Click "I Have a Key!" → Activation page → Paste key → Activates → Returns to Get Started with BYOK/Local enabled
- [x] **1.1.3b** (Without key) Click Next → Features page shows Power License benefits (Local AI, BYOK, Vision) + "Get yours now" link
- [x] **1.1.3c** Click Next on Features → Browser opens for sign-in → App loads with Wallet mode active
- [x] **1.1.3d** Close wizard before sign-in → Relaunch → Wizard re-appears (WizardCompleted not set until sign-in)

### BYOK Path (Licensed)
- [x] **1.1.4** Step 1 (Get Started) → Select **BYOK** (enabled with Power License)
- [x] **1.1.5** Step 3 (STT) → Cloud STT only (local hidden) → Deepgram API key field + Test button
- [x] **1.1.5a** Skip without key → Warning appears → Click Next again → Proceeds with warning
- [x] **1.1.6** Step 4 (LLM) → Cloud AI only (local hidden) → Provider dropdown (Gemini/OpenAI/Anthropic/OpenRouter/Requesty) → API key field + Test button
- [x] **1.1.6a** Skip without key → Warning appears → Click Next again → Proceeds with warning
- [x] **1.1.7** Step 5 (TTS) → Off or Cloud TTS (local hidden) → Provider dropdown (Deepgram/OpenAI/Gemini/Inworld) → Shows if key already entered or needs new key
- [x] **1.1.8** Step 7 (Test) → Record 3 seconds of audio → Transcription appears
- [x] **1.1.9** Step 7 (Test) → Can re-record if needed
- [x] **1.1.10** Step 8 (Ready) → Summary shows selected providers
- [x] **1.1.11** Step 8 (Ready) → Click "Start Dictating" → Wizard closes → App loads
- [x] **1.1.12** Verify settings saved: `.\test-helpers\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"`

### Local Path (Licensed)
- [x] **1.1.13** Step 1 (Get Started) → Select **Local** (enabled with Power License)
- [x] **1.1.14** STT page → Only Whisper visible (cloud removed) → Pre-selected → Model download starts if not present
- [x] **1.1.14a** Whisper model downloads successfully → Progress bar → "Model ready"
- [ ] **1.1.14b** If download fails → Error shown → Can retry or go Back
- [x] **1.1.15** LLM page → Only Ollama visible (cloud removed) → Pre-selected → Ollama health check runs
- [ ] **1.1.15a** Ollama running + model present → "Ready" status
- [x] **1.1.15b** Ollama not running → Install/start prompt shown
- [x] **1.1.15c** Model not pulled → Auto-pull with progress bar
- [x] **1.1.16** TTS page → Off or Kokoro visible (cloud removed) → Select Kokoro or Off
- [x] **1.1.16a** Kokoro selected → Model download starts if not present → Progress → "Ready"
- [x] **1.1.17** Test page → Record audio → Whisper transcription appears (local, no cloud)
- [ ] **1.1.18** Ready page → Summary shows Whisper + Ollama (+ Kokoro if selected)
- [x] **1.1.19** Click "Start Dictating" → Wizard closes → App loads fully local
- [x] **1.1.20** Verify offline: disconnect internet → Dictate → Still works

## 1.2: Core Dictation (Deepgram + LLM)

- [x] **1.2.1** Open Notepad
- [x] **1.2.2** Press Ctrl+Alt+D, speak "Hello world", release → text injected
- [x] **1.2.3** Verify text appears in Notepad
- [x] **1.2.4** Check Control Panel shows: Recording → Transcribing → Processing → Idle
- [x] **1.2.5** Verify history.db: `.\test-helpers\Verify-HistoryDb.ps1 -ExpectedMode "dictate"`
- [ ] **1.2.6** Confirm stt_provider = "Deepgram" in history
- [x] **1.2.7** Dictate messy text: "um hello uh world like yeah" → LLM cleans to "Hello world"
- [x] **1.2.8** Settings → Enable **Raw Mode** → Dictate again → Raw transcript injected (no LLM)
- [x] **1.2.9** Settings → Disable Raw Mode

## 1.3: Settings → General Tab

**Scope:** All 4 sub-items under the General nav entry (Application, Control Panel, Keyboard Shortcuts, Language). Other tabs (Audio & Mic, AI Engine, Pipelines, Dictation Presets, Macros, Privacy, Account, About) are covered in **§5 Comprehensive Settings Verification**.

### Application sub-item
- [x] **1.3.1** Theme dropdown shows Midnight / Ember / Frost → select each → UI repaints immediately
- [x] **1.3.2** Selected theme persists across app restart
- [ ] **1.3.3** Auto-Start toggle ON → verify Task Scheduler entry created: `.\test-helpers\Verify-AutoStart.ps1`
- [ ] **1.3.4** Auto-Start toggle OFF → Task Scheduler entry removed

### Control Panel sub-item

#### Rows & Stats
- [x] **1.3.5** Show Modes Row toggle OFF → CP modes row hidden → toggle ON → row returns
- [x] **1.3.6** Show Actions Row toggle OFF → CP actions row hidden → ON → returns
- [x] **1.3.7** Show Session Stats toggle OFF → REQ / CHAR / WORDS / WORD/MIN tiles hidden
- [x] **1.3.8** Show Performance Stats toggle OFF → REC / TRNS / PROC / INJ / TOT tiles hidden

#### Bar Position, Layout & Behavior
- [x] **1.3.9** Always On Top toggle ON → CP stays on top of other windows (focus another app, CP visible) → **default should be ON (OBS-007)**
- [x] **1.3.10** Expand Direction → select **Down** → CP expands downward on activity
- [x] **1.3.11** Expand Direction → select **Up** → CP expands upward
- [x] **1.3.12** Bar Position 6-button grid — click each (Top-Left / Top-Center / Top-Right / Bottom-Left / Bottom-Center / Bottom-Right) → CP snaps to that screen anchor
- [x] **1.3.13** Bar Position persists across restart

#### Visual Effects
- [x] **1.3.14** Visual Effects Enable toggle ON → glassmorphic blur active on CP
- [x] **1.3.15** Scope radios appear when enabled → select **Whole App** vs **Top Bar Only**
- [x] **1.3.16** Intensity slider (0–100, step 5) → drag → blur intensity updates live
- [x] **1.3.17** Waveform Style radios → **Wave** / **Bars** / **Off** → each renders as described during recording
- [x] **1.3.18** Visual Effects Enable OFF → blur + waveform controls hidden or inert

#### Idle Roller
- [x] **1.3.19** Idle Roll Enable ON → CP cycles idle layers (status → logo+clock → weather)
- [x] **1.3.20** Show Clock checkbox OFF → clock layer skipped
- [x] **1.3.21** Show Weather checkbox OFF → weather layer skipped
- [x] **1.3.22** Clock Format dropdown → 12h / 24h / with-seconds variants render correctly
- [x] **1.3.23** Hold Duration slider (5–20s) → layer dwell time changes accordingly
- [x] **1.3.24** Idle Roll Enable OFF → CP shows static status only

#### Auto-Collapse
- [x] **1.3.25** Auto-Collapse Enable ON → CP collapses to compact size after idle delay → **default should be ON (OBS-006)**
- [x] **1.3.26** Delay dropdown → change (e.g. 5s / 15s / 30s) → CP collapses after chosen delay
- [x] **1.3.27** Auto-Collapse Enable OFF → CP stays expanded indefinitely

#### Auto-Hide
- [x] **1.3.28** Auto-Hide Enable ON → CP fades/hides after idle delay
- [x] **1.3.29** Delay dropdown → change value → hide timing matches
- [x] **1.3.30** Trigger activity (dictate or hover) → CP reappears

### Keyboard Shortcuts sub-item
- [ ] **1.3.31** Verify 8 hotkey rows visible: Dictate, Refine, Ask, Translate, Oops, Note, Chat, Read Selection
- [ ] **1.3.32** Defaults: Dictate=Ctrl+Alt+D, Refine=Ctrl+Alt+R, Ask=Ctrl+Alt+A, Translate=Ctrl+Alt+T, Oops=Ctrl+Alt+V, Note=Ctrl+Alt+N, Chat=Ctrl+Alt+C, Read Selection=Ctrl+Alt+Q
- [x] **1.3.33** Click **Record** on Dictate → press Ctrl+Shift+D → field updates → Save
- [x] **1.3.34** Press new hotkey Ctrl+Shift+D → dictation fires (old Ctrl+Alt+D no longer active)
- [x] **1.3.35** Click **Reset** on Dictate → reverts to Ctrl+Alt+D
- [ ] **1.3.36** Assign duplicate combo (e.g. set Refine = Ctrl+Alt+D) → warning/error shown or silent conflict (document behavior)
- [x] **1.3.37** Restart app → hotkey changes persist
- [x] **1.3.38** InfoBar at bottom of page is visible and informational
- [ ] **1.3.39** Verify Vision hotkey is NOT in this list (reported drift vs §5 — Vision may live elsewhere or not be wired yet)

### Language sub-item
- [x] **1.3.40** UI Language dropdown → list includes English + Español (Mexico) → select other → restart warning InfoBar appears
- [x] **1.3.41** Restart app → UI renders in selected language
- [x] **1.3.42** Transcription Language dropdown → select different language → dictate → STT transcribes in that language
- [x] **1.3.43** Transcription Language persists across restart

### Persistence (General tab)
- [x] **1.3.44** Change one setting per sub-item → restart → all changes persist
- [x] **1.3.45** Verify settings.json structure intact: `.\test-helpers\Get-AppState.ps1`

## 1.4: Advanced Modes (Deepgram + LLM)

### Refine Mode (Auto/Voice toggle via Control Panel)
- [x] **1.4.1** Verify Control Panel shows Refine mode toggle (Auto/Voice)
- [x] **1.4.2** Set to **Auto** mode → Type "hello wrold" in Notepad, select it
- [x] **1.4.3** Press Ctrl+Alt+R → "hello world" replaces selection (no audio, LLM cleans text)
- [x] **1.4.4** Set to **Voice** mode → Type "make this better", select it
- [x] **1.4.5** Press Ctrl+Alt+R, say "more professional", release → refined text replaces
- [ ] **1.4.6** Press Ctrl+Alt+F (dedicated verbal hotkey) → Same as voice mode
- [ ] **1.4.7** Press Ctrl+Alt+R with no selection → Error notification

### Ask Mode
- [x] **1.4.8** Press Ctrl+Alt+A, say "What is 2 plus 2", release
- [x] **1.4.9** Answer "4" appears in notification/UI (not injected to Notepad)
- [x] **1.4.10** Verify history.db mode='ask'

### Translate Mode
- [x] **1.4.11** Press Ctrl+Alt+T, say "Hello how are you", release
- [x] **1.4.12** "Hola cómo estás" injected to Notepad
- [x] **1.4.13** Press Ctrl+Alt+T, say "Hola cómo estás", release
- [x] **1.4.14** "Hello how are you" injected (bidirectional)

### Note Mode
- [x] **1.4.15** Press Ctrl+Alt+N, say "Remember to buy groceries", release
- [x] **1.4.16** Check file at path from Notes settings has timestamp + note
- [x] **1.4.17** Verify timestamp matches format from Notes settings
- [x] **1.4.18** If LLM Processing enabled in Notes settings, verify text is formatted
- [x] **1.4.19** If LLM Processing disabled, verify raw transcription saved
- [x] **1.4.20** Verify: `.\test-helpers\Verify-FileSystem.ps1 -Path "%USERPROFILE%\Documents\diktame-notes.md" -Type File`

### Oops Mode
- [x] **1.4.21** Dictate "test text" → injected
- [x] **1.4.22** Press Ctrl+Alt+V → "test text" re-injected
- [x] **1.4.23** Restart app → Press Ctrl+Alt+V → No-op (nothing stored)

### Quick Chat
- [x] **1.4.24** Press Ctrl+Alt+C → QuickChatWindow appears (always-on-top)
- [x] **1.4.25** Verify font size matches Chat settings (default 14pt)
- [x] **1.4.26** Verify window opacity matches Chat settings (default 0.95)
- [x] **1.4.27** Type "What is the capital of Spain" → Click Send → "Madrid" appears
- [ ] **1.4.28** Click Mic button, say "What is 5 plus 5", release → "10" appears
- [ ] **1.4.29** Verify timestamps shown/hidden per Chat settings (default: shown)
- [ ] **1.4.30** If markdown enabled (default: on), type "**bold** and `code`" → Verify formatted
- [x] **1.4.31** Press Esc → Window closes
- [ ] **1.4.32** If "Forget on Close" enabled → Press Ctrl+Alt+C → No history
- [ ] **1.4.33** If "Forget on Close" disabled (default) → Press Ctrl+Alt+C → History persists
- [ ] **1.4.34** Test max history limit: Send 55 messages → Only last 50 shown (default max=50)

## 1.5: Voice Macros (Cloud Path)

- [x] **1.5.1** Settings → Macros → Add Macro: Trigger="my email", Content="test@example.com"
- [x] **1.5.2** Dictate "Send to my email please" → expands to "Send to test@example.com please"
- [x] **1.5.3** Dictate "MY EMAIL" (uppercase) → expands (case-insensitive)
- [x] **1.5.4** Dictate "my email, thanks" → expands before punctuation
- [x] **1.5.5** Add Macro: Trigger="my phone", Content="555-1234"
- [x] **1.5.6** Dictate "my email and my phone" → both expand
- [x] **1.5.7** Edit Macro → Changes saved
- [x] **1.5.8** Delete Macro → Removed
- [x] **1.5.9** Verify: `.\test-helpers\Verify-Snippets.ps1`

## 1.6: API Keys & Security (Cloud Path)

- [x] **1.6.1** Settings → API Keys → Deepgram key visible (masked)
- [x] **1.6.2** Settings → API Keys → LLM key visible (masked)
- [x] **1.6.3** Settings → API Keys → Click **Test Connection** on Deepgram → Success
- [x] **1.6.4** Settings → API Keys → Click **Test Connection** on LLM → Success
- [x] **1.6.5** Settings → API Keys → Enter invalid key → Test fails with error
- [x] **1.6.6** Verify keys encrypted: `.\test-helpers\Verify-SecureStorage.ps1`
- [x] **1.6.7** Restart app → Keys still work (persisted)

## 1.7: Data & Privacy

**Model (post BUG-024 fix):** two orthogonal controls in Settings → Privacy.
- **Logging Intensity slider** — Ghost / Stats / Balanced / Full. Controls *what rows + metadata* get stored.
- **PII Scrubber toggle** — independent ON/OFF. Controls whether stored text is redacted. Active at Balanced **and** Full. Irrelevant at Stats/Ghost (no text stored).

Sample sentence for every PII step: **"My email is test@example.com and my phone is 555-1234."**

### Full — verbatim metadata, scrubber governs text
- [ ] **1.7.1** Set Logging Intensity = **Full**, PII Scrubber = **OFF** → dictate sample → history.db row has text + raw_transcript both verbatim (email + phone intact)
- [ ] **1.7.2** Keep Full, flip PII Scrubber = **ON** → dictate sample → history.db row exists with full metadata (timings, words, provider) AND email/phone replaced with `[REDACTED]` in text + raw_transcript
- [ ] **1.7.3** Toggle PII Scrubber OFF again → dictate → verbatim again (toggle round-trip works)

### Balanced — scrubber governs text the same way
- [ ] **1.7.4** Set Logging Intensity = **Balanced**, PII Scrubber = **ON** → dictate sample → row has text scrubbed (email + phone → `[REDACTED]`)
- [ ] **1.7.5** Balanced + PII Scrubber = **OFF** → dictate sample → row has text verbatim
- [ ] **1.7.6** Compare rows from 1.7.2 and 1.7.4 — text field should be identical (scrubber behavior is level-agnostic at Balanced and Full)

### Stats — counts only, scrubber inert
- [ ] **1.7.7** Set Logging Intensity = **Stats** → dictate "test" → row exists with word_count + timings but text + raw_transcript are NULL or empty
- [ ] **1.7.8** Flip PII Scrubber in either direction → no effect on Stats rows (control is inert at this level; UI may show it disabled or note the no-op)

### Ghost — no row at all
- [ ] **1.7.9** Set Logging Intensity = **Ghost** → dictate "test" → no new history.db row (row count unchanged from before)
- [ ] **1.7.10** Scrubber state irrelevant at Ghost (same as Stats above)

### Wipe + verification
- [ ] **1.7.11** Click **Wipe All Data** → confirm → history.db row count drops to 0
- [ ] **1.7.12** Verify: `.\test-helpers\Verify-HistoryDb.ps1`

### UX sanity (after BUG-024 fix copy lands)
- [ ] **1.7.13** PII Scrubber description text reads explicitly "Works at Balanced and Full; at Stats/Ghost text isn't stored at all" (or equivalent — confirm EN + ES copy)

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
- [ ] **2.1.2** Step 0 (Language) → Select language
- [ ] **2.1.3** Step 1 (Get Started) → With Power License: **BYOK** → Select it
- [ ] **2.1.4** STT page → Cloud STT (Deepgram key) → Enter Deepgram key or skip
- [ ] **2.1.5** LLM page → Select **Gemini** from provider dropdown → Enter Gemini API key
- [ ] **2.1.6** TTS page → Select Off or cloud provider
- [ ] **2.1.7** Test page → Record audio → Transcription appears
- [ ] **2.1.9** Step 7 (Ready) → Summary shows Gemini Audio + Gemini LLM
- [ ] **2.1.10** Complete wizard

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
- [ ] **3.1.3** Step 0 (Language) → Select language
- [ ] **3.1.4** Step 1 (Get Started) → With Power License: **Local** option enabled → Select it
- [ ] **3.1.5** STT page → Only Whisper visible (cloud hidden) → Pre-selected → Download starts if needed
- [ ] **3.1.6** LLM page → Only Ollama visible (cloud hidden) → Pre-selected → Ollama check + model pull
- [ ] **3.1.7** TTS page → Off or Kokoro visible (cloud hidden) → Select Kokoro or Off
- [ ] **3.1.8** Test page → Record audio → Whisper transcription appears
- [ ] **3.1.9** Ready page → Summary shows Whisper + Ollama
- [ ] **3.1.10** Click "Start Dictating" → Wizard closes → App loads

## 3.2: Core Functionality (Local Path)

- [x] **3.2.1** Dictate "Hello world" → Text injected
- [x] **3.2.2** Verify history.db stt_provider = "Whisper"
- [x] **3.2.3** Verify history.db llm_provider = "Ollama"
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
- [ ] **4.1.2** Step 0 (Language) → Select language
- [ ] **4.1.3** Step 1 (Get Started) → With Power License: **BYOK** → Select it
- [ ] **4.1.4** STT page → Cloud STT → Enter Deepgram key
- [ ] **4.1.5** LLM page → Skip without entering key (warning shown, proceed)
- [ ] **4.1.6** TTS page → Select Off
- [ ] **4.1.7** Test page → Record audio → Raw transcription appears (no LLM cleanup)
- [ ] **4.1.8** Ready page → Summary shows Deepgram + No LLM
- [ ] **4.1.9** Complete wizard

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

# Journey 5: Comprehensive Settings Verification

**Configuration:** Any working setup (Cloud or Local)
**Duration:** ~2 hours
**Goal:** Systematically verify all 9 settings tabs and persistence

**Prerequisites:**
- Complete Journey 1, 2, or 3 first (need working API keys)
- App is running and configured

## 5.1: General Tab (4 sub-items: Application, Control Panel, Keyboard Shortcuts, Language)

### Application sub-item
- [ ] **5.1.1** Settings → General → Application → Verify theme selector (Midnight/Ember/Frost)
- [ ] **5.1.2** Select each theme → UI updates immediately
- [ ] **5.1.3** Verify trailing space toggle
- [ ] **5.1.4** Verify additional key dropdown (None/Enter/Tab)
- [ ] **5.1.5** Enable auto-start → Verify Task Scheduler entry: `.\test-helpers\Verify-AutoStart.ps1`
- [ ] **5.1.6** Disable auto-start → Verify entry removed

### Control Panel sub-item
- [ ] **5.1.7** Settings → General → Control Panel → Verify "Show on Startup" toggle
- [ ] **5.1.8** Enable "Show on Startup" → Restart → Panel visible
- [ ] **5.1.9** Disable "Show on Startup" → Restart → Panel hidden
- [ ] **5.1.10** Verify "Always on Top" toggle
- [ ] **5.1.11** Verify auto-collapse, waveform, snap position settings

### Keyboard Shortcuts sub-item
- [ ] **5.1.12** Settings → General → Keyboard Shortcuts → Verify 9 hotkey fields visible
- [ ] **5.1.13** Verify defaults: Dictate=Ctrl+Alt+D, Refine=Ctrl+Alt+R, Ask=Ctrl+Alt+A, Translate=Ctrl+Alt+T, Oops=Ctrl+Alt+V, Note=Ctrl+Alt+N, Chat=Ctrl+Alt+C, ReadSelection=Ctrl+Alt+Q, Vision=Ctrl+Alt+S
- [ ] **5.1.14** Change Dictate to Ctrl+Shift+D → Save → Test → Works
- [ ] **5.1.15** Restart app → Verify changed hotkey persists
- [ ] **5.1.16** Try duplicate hotkey → Verify error/warning
- [ ] **5.1.17** Reset to defaults → All revert

### Language sub-item
- [ ] **5.1.18** Settings → General → Language → Change language → UI updates

## 5.2: Audio & Mic Tab (2 sub-items: Microphone & Recording, Sound Feedback)

### Microphone & Recording sub-item
- [ ] **5.2.1** Settings → Audio & Mic → Microphone & Recording → Verify input device dropdown
- [ ] **5.2.2** Verify sample rate dropdown (16kHz/48kHz)
- [ ] **5.2.3** Verify max recording duration NumberBox
- [ ] **5.2.4** Set duration to 5 seconds → Save
- [ ] **5.2.5** Dictate for 10 seconds → Recording stops at 5 seconds

### Sound Feedback sub-item
- [ ] **5.2.6** Settings → Audio & Mic → Sound Feedback → Verify audio ducking toggle
- [ ] **5.2.7** Enable ducking → Set target volume to 20% → Save
- [ ] **5.2.8** Play music → Dictate → Music drops to ~20% volume
- [ ] **5.2.9** Disable ducking → Music unaffected during dictation

## 5.3: AI Engine Tab (7 sub-items: API Keys, Speech to Text, Language Model, Text to Speech, Chat, Vision, System Monitor)

### API Keys sub-item
- [ ] **5.3.1** Settings → AI Engine → API Keys → Verify cloud providers listed
- [ ] **5.3.2** Verify keys are masked (******)
- [ ] **5.3.3** Click "Show" → Key visible → Click "Hide" → Key masked
- [ ] **5.3.4** Click "Test Connection" on each provider → Success
- [ ] **5.3.5** Enter invalid key → Test → Error message clear
- [ ] **5.3.6** Update key → Save → Restart → Verify works
- [ ] **5.3.7** Verify: `.\test-helpers\Verify-SecureStorage.ps1`

### Speech to Text sub-item
- [ ] **5.3.8** Settings → AI Engine → Speech to Text → Verify cloud/local toggle
- [ ] **5.3.9** Cloud: Select different STT provider (Deepgram/Gemini Audio) → Save
- [ ] **5.3.10** Local: Verify Whisper model selection

### Language Model sub-item
- [ ] **5.3.11** Settings → AI Engine → Language Model → Verify provider selection
- [ ] **5.3.12** Verify model dropdown
- [ ] **5.3.13** Local: Verify Ollama connection status + model list

### Text to Speech sub-item
- [ ] **5.3.14** Settings → AI Engine → Text to Speech → Verify provider selection (Off/Kokoro/Deepgram/OpenAI/Gemini/Inworld)
- [ ] **5.3.15** Select Kokoro → Verify voice, speed, volume settings
- [ ] **5.3.16** Verify notification prefs: SpeakAskResponses, SpeakChatResponses, SpeakTranslations, SpeakNotifications toggles
- [ ] **5.3.17** Toggle each notification pref → Save → Verify persists

### Chat sub-item
- [ ] **5.3.18** Settings → AI Engine → Chat → Verify font size (default 14pt)
- [ ] **5.3.19** Verify window opacity (default 0.95)
- [ ] **5.3.20** Verify always-on-top toggle (default: on)
- [ ] **5.3.21** Verify "Forget on Close" toggle (default: off)
- [ ] **5.3.22** Verify max history messages (default 50, 0=unlimited)
- [ ] **5.3.23** Verify "Show Timestamps" toggle (default: on)
- [ ] **5.3.24** Verify "Enable Markdown" toggle (default: on)
- [ ] **5.3.25** Verify Web Search toggle for Gemini grounding (default: off)
- [ ] **5.3.26** Edit system prompt → Save → Restart → Verify persists
- [ ] **5.3.27** Open Quick Chat → Verify font size and opacity match settings

### Vision sub-item
- [ ] **5.3.28** Settings → AI Engine → Vision → Verify local/cloud model selection
- [ ] **5.3.29** Verify vision model settings persist after restart

### System Monitor sub-item
- [ ] **5.3.30** Settings → AI Engine → System Monitor → Verify resource usage display

## 5.4: Pipelines Tab (7 sub-items: Ask, Refine (Auto), Refine (Verbal), Translate, Notes, Vision, Speak (TTS))

- [ ] **5.4.1** Settings → Pipelines → Verify 7 sub-items visible in sidebar
- [ ] **5.4.2** Select Ask → Edit Cloud prompt → Save → Verify persists
- [ ] **5.4.3** Select Ask → Edit Local prompt → Save → Verify persists
- [ ] **5.4.4** Select Refine (Auto) → Verify both profiles editable
- [ ] **5.4.5** Select Refine (Verbal) → Verify both profiles editable
- [ ] **5.4.6** Select Translate → Verify both profiles editable
- [ ] **5.4.7** Select Notes → Verify file path, LLM processing toggle, timestamp format
- [ ] **5.4.8** Notes → Change timestamp to "dd/MM/yyyy HH:mm" → Live preview updates
- [ ] **5.4.9** Notes → Click "Browse" → FileSavePicker opens → Select new path
- [ ] **5.4.10** Select Vision → Verify vision pipeline settings
- [ ] **5.4.11** Select Speak (TTS) → Verify TTS pipeline settings
- [ ] **5.4.12** Test Ask mode → Verify uses edited prompt

## 5.5: Dictation Presets Tab (CRUD)

- [ ] **5.5.1** Settings → Dictation Presets → Verify 3 built-ins: Standard, Prompt, Professional
- [ ] **5.5.2** Select Standard → Verify Cloud system prompt field
- [ ] **5.5.3** Select Standard → Verify Local system prompt field
- [ ] **5.5.4** Select Standard → Verify model dropdown (Cloud profile only)
- [ ] **5.5.5** Verify model dropdown shows: "(Default)" + 30+ models from APIs
- [ ] **5.5.6** Verify NO Ollama models in Cloud dropdown (only OpenAI, Anthropic, Gemini, OpenRouter)
- [ ] **5.5.7** Change Cloud model → Save → Restart → Verify model selection persists
- [ ] **5.5.8** Click "Add Preset" → Enter "Test Preset" → Save
- [ ] **5.5.9** Select "Test Preset" → Edit prompts → Save → Verify persists after restart
- [ ] **5.5.10** Select built-in preset → Verify Delete button disabled
- [ ] **5.5.11** Select custom preset → Click Delete → Confirm → Preset removed
- [ ] **5.5.12** Verify NO hotkey fields (managed in General > Keyboard Shortcuts)

## 5.6: Macros Tab

- [ ] **5.6.1** Settings → Macros → Add Macro: "myemail" → "test@example.com"
- [ ] **5.6.2** Dictate "Send to myemail" → Expands correctly
- [ ] **5.6.3** Edit Macro → Changes persist
- [ ] **5.6.4** Delete Macro → Removed
- [ ] **5.6.5** Add 5 Macros → All saved
- [ ] **5.6.6** Restart app → All Macros persist
- [ ] **5.6.7** Verify: `.\test-helpers\Verify-Snippets.ps1`

## 5.7: Privacy Tab

- [ ] **5.7.1** Settings → Privacy → Verify 4 levels: Full, Balanced, Stats, Ghost
- [ ] **5.7.2** Select "Full" → Dictate → Verify verbatim in history.db
- [ ] **5.7.3** Select "Balanced" → Dictate "test@example.com" → Verify [REDACTED]
- [ ] **5.7.4** Select "Stats" → Dictate → Verify only count stored
- [ ] **5.7.5** Select "Ghost" → Dictate → Verify no history entry
- [ ] **5.7.6** Click "Wipe Data" → Confirm → Verify history.db cleared
- [ ] **5.7.7** Verify: `.\test-helpers\Verify-HistoryDb.ps1`

## 5.8: Account Tab

- [ ] **5.8.1** Settings → Account → Verify Power License section visible
- [ ] **5.8.2** If unlicensed: verify "Buy" button + license key TextBox + "Activate" button
- [ ] **5.8.3** Enter invalid license key → Activate → Clear error message
- [ ] **5.8.4** Enter valid license key → Activate → Green checkmark + "Unlocked" status
- [ ] **5.8.5** Verify "Deactivate" link visible when licensed
- [ ] **5.8.6** If signed out: verify sign-in InfoBar with button
- [ ] **5.8.7** Click "Sign In" → Browser opens → Complete OAuth → App shows signed-in state
- [ ] **5.8.8** Verify email + status text displayed
- [ ] **5.8.9** Verify wallet balance displayed (formatted)
- [ ] **5.8.10** Verify "Use Wallet Proxy" toggle
- [ ] **5.8.11** Verify "Buy Credits" button + "Manage Account" link
- [ ] **5.8.12** Verify transaction history list
- [ ] **5.8.13** Sign out → Confirm → Tokens cleared
- [ ] **5.8.14** Restart app → Still signed in (if not signed out)

## 5.9: About Tab

- [ ] **5.9.1** Settings → About → Verify app version displayed
- [ ] **5.9.2** Verify copyright/license info
- [ ] **5.9.3** Verify links are clickable → Open in browser

## 5.10: Settings Persistence & Migration

- [ ] **5.10.1** Make changes across all 9 tabs → Save all
- [ ] **5.10.2** Restart app → Verify all changes persist
- [ ] **5.10.3** Check settings.json structure:
  - 3 DictationModes (Standard, Prompt, Professional)
  - 7 UtilityPipelines (ask, refine_auto, refine_instruction, refine, translate, note, chat)
- [ ] **5.10.4** Verify: `.\test-helpers\Get-AppState.ps1`
- [ ] **5.10.5** Delete settings.json → Restart → Verify defaults populate
- [ ] **5.10.6** Verify migration creates 3 modes + 7 pipelines automatically

## 5.11: Journey 5 Complete

**Summary:** All 9 settings tabs with sub-items verified, CRUD operations tested, persistence confirmed

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
- [ ] **P.2** Add Voice Macro → Restart → Macro still there
- [ ] **P.3** Change hotkey → Restart → New hotkey works
- [ ] **P.4** Enable auto-start → Restart → Task still registered

## UI/UX Polish

- [ ] **U.1** All windows use consistent dIKta.me branding
- [ ] **U.2** Icons are crisp and consistent
- [ ] **U.3** Fonts are readable and consistent
- [ ] **U.4** All 3 themes render correctly (Midnight, Ember, Frost)
- [ ] **U.5** No visual glitches or layout issues
- [ ] **U.6** Error messages are actionable and clear
- [ ] **U.7** Loading states are visible and smooth

---

# Journey 6: Wallet System + Account/Auth

**Configuration:** Cloud path with Wallet sign-in (OAuth via website)
**Duration:** ~1.5 hours
**Goal:** Validate wallet-based billing, OAuth sign-in, account features

**Prerequisites:**
- Internet connection required
- A dIKta.me account (or ability to create one at the website)

## 6.1: Account Sign-In (OAuth)

- [ ] **6.1.1** Settings -> Account -> Click "Sign In" -> Browser opens dIKta.me website
- [ ] **6.1.2** Complete OAuth sign-in on website -> Deeplink redirects back to app
- [ ] **6.1.3** App shows signed-in state: display name, email, avatar (if set)
- [ ] **6.1.4** Verify JWT token stored: settings show auth mode
- [ ] **6.1.5** Restart app -> Still signed in (token persisted)
- [ ] **6.1.6** Wait 1+ hour -> Token auto-refreshes (no sign-in prompt)

## 6.2: Wallet Balance & Billing

- [ ] **6.2.1** Verify wallet balance displayed in Control Panel or Settings
- [ ] **6.2.2** Switch to Wallet auth mode (if not auto-detected)
- [ ] **6.2.3** Dictate "Hello world" -> Text injected via wallet-routed STT
- [ ] **6.2.4** Check wallet balance decremented after dictation
- [ ] **6.2.5** Dictate 5 more phrases -> Balance continues to decrement
- [ ] **6.2.6** Verify history.db shows wallet-routed entries

## 6.3: Wallet Edge Cases

- [ ] **6.3.1** Sign out -> Wallet mode unavailable -> Falls back to BYOK
- [ ] **6.3.2** Invalid/expired token -> App shows sign-in prompt (not crash)
- [ ] **6.3.3** Zero balance -> Dictation fails with clear "insufficient credits" message
- [ ] **6.3.4** Network error during wallet call -> Graceful error notification

## 6.4: Account Profile

- [ ] **6.4.1** Settings → Account → Email + status text shown correctly
- [ ] **6.4.2** Wallet balance displayed (formatted)
- [ ] **6.4.3** Transaction history shows recent entries
- [ ] **6.4.4** Click "Sign Out" → Confirms → Signed out, tokens cleared
- [ ] **6.4.5** Restart app → No longer signed in

## 6.5: License Activation (LemonSqueezy)

- [ ] **6.5.1** Settings → Account → Power License section visible
- [ ] **6.5.2** Enter valid LemonSqueezy license key → Click Activate → Status shows "Unlocked"
- [ ] **6.5.3** Enter invalid key → Click Activate → Clear error message
- [ ] **6.5.4** Restart app → License still active (persisted in SecureStorage)
- [ ] **6.5.5** Deactivate license → License cleared
- [ ] **6.5.6** Without license → Wizard shows BYOK/Local as disabled with info text, Wallet is default
- [ ] **6.5.7** With license → Wizard BYOK/Local options enabled and selectable
- [ ] **6.5.8** Disconnect internet → License still valid (30-day offline grace period)

## 6.6: Journey 6 Complete

---

# Journey 7: TTS System (Text-to-Speech)

**Configuration:** Any working setup + TTS enabled
**Duration:** ~1 hour
**Goal:** Validate local (Kokoro) and cloud TTS providers, notification TTS

**Prerequisites:**
- Working dictation setup (Journey 1 or 3 completed)

## 7.1: TTS Provider Setup

- [x] **7.1.1** Settings → AI Engine → Text to Speech → Verify provider selection (Off / Kokoro / Deepgram / OpenAI / Gemini / Inworld)
- [x] **7.1.2** Select **Kokoro** (local) → Verify model download UI if model not present
- [x] **7.1.3** Kokoro model download completes → Status shows "Ready"
- [ ] **7.1.4** Select **Deepgram** (cloud) → Requires API key
- [ ] **7.1.5** Select **OpenAI** (cloud) → Requires API key
- [ ] **7.1.6** Select **Gemini** (cloud) → Requires API key
- [ ] **7.1.7** Select **Inworld** (cloud) → Requires API key
- [ ] **7.1.8** Select **Off** → TTS disabled globally

## 7.2: TTS Playback

- [ ] **7.2.1** Enable TTS (Kokoro) -> Dictate "Hello world" -> Text injected AND spoken aloud
- [ ] **7.2.2** Verify audio ducking during TTS playback (if ducking enabled)
- [ ] **7.2.3** TTS playback completes -> Audio ducking restores
- [ ] **7.2.4** Enable TTS (cloud provider) -> Dictate -> Cloud TTS plays back
- [ ] **7.2.5** Compare latency: Kokoro (local) vs Cloud TTS

## 7.3: ReadSelection Mode (Ctrl+Alt+Q)

- [ ] **7.3.1** Select text in Notepad ("The quick brown fox jumps over the lazy dog")
- [ ] **7.3.2** Press Ctrl+Alt+Q → Selected text is read aloud via TTS
- [ ] **7.3.3** Verify audio plays to completion
- [ ] **7.3.4** Press Ctrl+Alt+Q with no selection → Error notification or no-op
- [ ] **7.3.5** Select very long text (500+ chars) → TTS handles without crash

## 7.4: Notification TTS

- [ ] **7.4.1** Use Ask mode ("What is 2+2") -> Answer "4" spoken aloud via toast TTS
- [ ] **7.4.2** Use Translate mode -> Translation spoken aloud
- [ ] **7.4.3** Verify suppressTts prevents double-speak (notification + pipeline TTS)
- [ ] **7.4.4** Disable TTS -> Ask mode -> Answer shown in toast but NOT spoken

## 7.5: TTS Notification Preferences

- [ ] **7.5.1** Settings → AI Engine → Text to Speech → Enable SpeakAskResponses
- [ ] **7.5.2** Use Ask mode ("What is 2+2") → Answer "4" spoken aloud
- [ ] **7.5.3** Disable SpeakAskResponses → Use Ask mode → Answer NOT spoken
- [ ] **7.5.4** Enable SpeakTranslations → Translate "Hello" → Translation spoken aloud
- [ ] **7.5.5** Enable SpeakChatResponses → Quick Chat → Response spoken aloud
- [ ] **7.5.6** Enable SpeakNotifications → Trigger an error → Error spoken aloud

## 7.6: TTS Settings Persistence

- [x] **7.6.1** Configure TTS provider + voice settings + notification prefs → Save
- [x] **7.6.2** Restart app → TTS settings persist
- [ ] **7.6.3** Verify tts_played_ms logged in history.db for TTS dictations

## 7.7: Journey 7 Complete

---

# Cross-Cutting: UI Themes

**These tests apply to any configured journey**

## CT.1: Theme Switching

- [ ] **CT.1.1** Settings -> Themes -> Select **Midnight** -> UI updates to dark blue palette
- [ ] **CT.1.2** Select **Ember** -> UI updates to warm/orange palette
- [ ] **CT.1.3** Select **Frost** -> UI updates to cool/light palette
- [ ] **CT.1.4** Theme applies to: Settings window, Control Panel, Quick Chat
- [ ] **CT.1.5** Restart app -> Theme persists
- [ ] **CT.1.6** Glassmorphic effects visible on settings cards and Control Panel

---

# Cross-Cutting: Control Panel Features

## CT.2: Control Panel Auto-Collapse

- [ ] **CT.2.1** Control Panel in idle state -> Collapses to compact size (~170px)
- [ ] **CT.2.2** Start recording -> CP expands to full size (~420px)
- [ ] **CT.2.3** Recording ends -> CP collapses back after idle timeout

## CT.3: Voice Waveform

- [ ] **CT.3.1** Start recording -> Waveform visualization appears in CP
- [ ] **CT.3.2** Settings -> CP -> Waveform style: **Wave** -> Smooth wave display
- [ ] **CT.3.3** Waveform style: **Bars** -> Bar-style visualization
- [ ] **CT.3.4** Waveform style: **Off** -> No waveform shown

## CT.4: Snap-to-Position

- [ ] **CT.4.1** CP -> Snap to **Top-Left** -> Panel moves to top-left corner
- [ ] **CT.4.2** Snap to **Top-Right** -> Panel moves to top-right corner
- [ ] **CT.4.3** Snap to **Bottom-Left** -> Panel moves to bottom-left corner
- [ ] **CT.4.4** Snap to **Bottom-Right** -> Panel moves to bottom-right corner
- [ ] **CT.4.5** Snap to **Top-Center** -> Panel moves to top-center
- [ ] **CT.4.6** Snap to **Bottom-Center** -> Panel moves to bottom-center
- [ ] **CT.4.7** Restart app -> Panel position persists

## CT.5: Idle Animation (Cylinder Roll)

- [ ] **CT.5.1** CP idle -> Layer 1: Status text displayed
- [ ] **CT.5.2** Wait -> Layer 2: Logo + clock displayed (rolls in)
- [ ] **CT.5.3** Wait -> Layer 3: Weather display (temperature + conditions)
- [ ] **CT.5.4** Animation cycles through all 3 layers
- [ ] **CT.5.5** Start recording -> Animation stops, recording UI shown
- [ ] **CT.5.6** Recording ends -> Animation resumes after idle timeout

---

# Cross-Cutting: ReadSelection Mode

## CT.6: ReadSelection (Ctrl+Alt+Q)

- [x] **CT.6.1** Select text in any app → Press Ctrl+Alt+Q → Text read aloud
- [ ] **CT.6.2** No TTS configured → Error notification
- [ ] **CT.6.3** Empty selection → No-op or error

---

# Cross-Cutting: Vision Feature

## CT.7: Vision (Ctrl+Alt+S)

- [ ] **CT.7.1** Press Ctrl+Alt+S → Snipping overlay appears (full-screen)
- [ ] **CT.7.2** Draw selection region → VisionActionWindow appears with thumbnail
- [ ] **CT.7.3** Verify radio buttons: Quick (Local) / Detailed (Cloud) / None
- [ ] **CT.7.4** Verify 8 action buttons: Edit, Save, Clipboard, Chat, Note, OCR, Color, Record
- [ ] **CT.7.5** Click **Save** → Screenshot saved to file + clipboard
- [ ] **CT.7.6** Click **Clipboard** (no query) → Image copied to clipboard
- [ ] **CT.7.7** Type text query + Click **Clipboard** → AI analysis text copied to clipboard
- [ ] **CT.7.8** Click **Chat** → QuickChatWindow opens with image attached
- [ ] **CT.7.9** Click **Note** → Vision description saved to notes file
- [ ] **CT.7.10** Click **OCR** → Extracted text copied to clipboard
- [ ] **CT.7.11** Click **Color** → Color palette extracted
- [ ] **CT.7.12** Click **Record** → Screen recording starts
- [ ] **CT.7.13** Click **Edit** → Annotation mode
- [ ] **CT.7.14** Click mic button → Dictate voice query → Query transcribed
- [ ] **CT.7.15** Toggle Quick (Local) → Run action → Uses local model
- [ ] **CT.7.16** Toggle Detailed (Cloud) → Run action → Uses cloud model
- [ ] **CT.7.17** Press Esc → Vision window closes

---

# Cross-Cutting: Auto-Update (Velopack)

## CT.8: Auto-Update

- [ ] **CT.8.1** Launch app → Verify update check runs (check logs for "UpdateService")
- [ ] **CT.8.2** If update available → Notification shown
- [ ] **CT.8.3** Download update → Apply on restart
- [ ] **CT.8.4** Settings → About → Verify current version displayed
- [ ] **CT.8.5** If no update → No notification (silent)

---

# Cross-Cutting: Streaming Dictation

## CT.9: Streaming Dictation (Deepgram WebSocket)

- [ ] **CT.9.1** Settings → General → Application → Verify "Streaming" toggle exists (default: off)
- [ ] **CT.9.2** Enable streaming → Dictate → Text appears progressively as you speak
- [ ] **CT.9.3** Release hotkey → Text finalized
- [ ] **CT.9.4** Disable streaming → Dictate → Text appears all at once after release (batch mode)
- [ ] **CT.9.5** Streaming with no internet → Graceful error

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
- [ ] Journey 1: Cloud (Deepgram) + LLM
- [ ] Journey 2: Cloud (Gemini Audio) + Gemini LLM
- [ ] Journey 3: Local (Whisper) + Ollama
- [ ] Journey 4: Hybrid (Cloud STT + Skip LLM)
- [ ] Journey 5: Comprehensive Settings Verification (9 tabs + sub-items)
- [ ] Journey 6: Wallet System + Account/Auth + License
- [ ] Journey 7: TTS System + Notification Prefs
- [ ] Cross-Cutting: Themes (CT.1), CP (CT.2-5), ReadSelection (CT.6), Vision (CT.7), Auto-Update (CT.8), Streaming (CT.9)
- [ ] Audio Feeder Automation

**Total Time:** ___ hours
**Bugs Found:** ___ critical, ___ important, ___ minor
**Success Rate:** ___% scenarios passed
**Total Scenarios:** ~400

---

**Ready for installer creation when all journeys complete!** 🎯
