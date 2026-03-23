# dIKta.me V2 — Manual Test Plan (Journey-Based)

**Date:** 2026-03-23 (Updated)
**Purpose:** Comprehensive end-to-end testing following complete user journeys
**Approach:** Each journey follows one configuration path from setup to completion
**Total Journeys:** 5 core paths + 2 feature journeys + cross-cutting tests + audio feeder
**Total Scenarios:** ~355 (280 original + 75 new feature scenarios)

**Time Breakdown:**
- Journey 1 (Cloud/Deepgram): ~3 hours
- Journey 2 (Gemini): ~1.5 hours
- Journey 3 (Local/Ollama): ~2 hours
- Journey 4 (Hybrid/Skip LLM): ~1 hour
- Journey 5 (Settings): ~2 hours
- Journey 6 (Wallet/Auth): ~1.5 hours
- Journey 7 (TTS): ~1 hour
- Cross-Cutting (incl. themes, CP, ReadSelection): ~1.5 hours
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

### General Settings
- [ ] **1.3.1** Settings → General → Enable trailing space → Dictate → text ends with space
- [ ] **1.3.2** Settings → General → Disable trailing space → Dictate → no space
- [ ] **1.3.3** Settings → General → Additional Key = **Enter** → Dictate → text + Enter
- [ ] **1.3.4** Settings → General → Additional Key = **Tab** → Dictate → text + Tab
- [ ] **1.3.5** Settings → General → Additional Key = **None**

### Audio Settings
- [ ] **1.3.6** Settings → Audio → Enable ducking → Play music → Dictate → music volume drops
- [ ] **1.3.7** Settings → Audio → Disable ducking → Dictate → music unchanged
- [ ] **1.3.8** Settings → Audio → Set max duration = 10s → Hold hotkey 15s → stops at 10s

### Modes Settings (Utility Pipelines)
- [ ] **1.3.9** Settings → Modes → Verify only 3 modes visible: Ask, Refine, Translate
- [ ] **1.3.10** Settings → Modes → Select "Ask" → Edit system prompt → Save
- [ ] **1.3.11** Settings → Modes → Select "Refine" → Edit system prompt → Save
- [ ] **1.3.12** Settings → Modes → Select "Translate" → Edit system prompt → Save
- [ ] **1.3.13** Verify Note and Chat modes NOT shown in Modes page

### Dictation Presets (CRUD)
- [ ] **1.3.14** Settings → Dictation Presets → Verify 4 built-in presets: Standard, Prompt, Professional, Raw
- [ ] **1.3.15** Select "Standard" preset → Edit Cloud system prompt → Save → Verify persists
- [ ] **1.3.16** Select "Standard" preset → Model dropdown shows "(Default)" + 30+ models
- [ ] **1.3.17** Select "Standard" preset → Verify NO Ollama models in Cloud profile dropdown
- [ ] **1.3.18** Select "Standard" preset → Change model to "gpt-4o (OpenAI)" → Save
- [ ] **1.3.19** Click "Add Preset" → Enter name "My Custom Preset" → Save
- [ ] **1.3.20** Select custom preset → Edit prompts → Save → Verify persists
- [ ] **1.3.21** Try to delete built-in preset → Verify delete button disabled
- [ ] **1.3.22** Delete custom preset → Verify removed from sidebar
- [ ] **1.3.23** Verify NO hotkey fields visible (hotkeys managed in Hotkeys tab)
- [ ] **1.3.24** Restart app → Verify custom preset and changes persist

### Notes Settings
- [ ] **1.3.25** Settings → Notes → Verify default file path = `%USERPROFILE%\Documents\diktame-notes.md`
- [ ] **1.3.26** Click "Browse" → FileSavePicker appears → Select new path → Path updates
- [ ] **1.3.27** Toggle "LLM Processing" off → Save
- [ ] **1.3.28** Change timestamp format to "dd/MM/yyyy HH:mm" → Live preview updates immediately
- [ ] **1.3.29** Change timestamp format to invalid format → Preview shows error or DateTime.Now
- [ ] **1.3.30** Edit Cloud system prompt → Save
- [ ] **1.3.31** Edit Local system prompt → Save
- [ ] **1.3.32** Click "Reset to Defaults" → All fields revert to defaults
- [ ] **1.3.33** Restart app → Verify all Notes settings persist

### Chat Settings
- [ ] **1.3.34** Settings → Chat → Font size slider: drag to 18pt → Value displays "18"
- [ ] **1.3.35** Window opacity slider: drag to 0.8 → Value displays "0.8"
- [ ] **1.3.36** Theme: select "Light" → Save
- [ ] **1.3.37** Theme: select "Dark" → Save
- [ ] **1.3.38** Theme: select "System" → Save
- [ ] **1.3.39** Toggle "Forget on Close" on → Save
- [ ] **1.3.40** Set max history messages to 50 → Save
- [ ] **1.3.41** Toggle "Show Timestamps" on → Save
- [ ] **1.3.42** Toggle "Enable Markdown" on → Save
- [ ] **1.3.43** Edit Cloud system prompt → Save
- [ ] **1.3.44** Edit Local system prompt → Save
- [ ] **1.3.45** Click "Reset to Defaults" → All fields revert
- [ ] **1.3.46** Restart app → Verify all Chat settings persist

### Settings Persistence
- [ ] **1.3.47** Verify settings.json has correct structure: 4 DictationModes + 5 UtilityPipelines
- [ ] **1.3.48** Verify: `.\test-helpers\Verify-AppSettings.ps1 -SettingPath "Note.UseLlmProcessing"`
- [ ] **1.3.49** Verify: `.\test-helpers\Verify-AppSettings.ps1 -SettingPath "Chat.ForgetOnClose"`

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
- [ ] **1.4.14** Check file at path from Notes settings has timestamp + note
- [ ] **1.4.15** Verify timestamp matches format from Notes settings
- [ ] **1.4.16** If LLM Processing enabled in Notes settings, verify text is formatted
- [ ] **1.4.17** If LLM Processing disabled, verify raw transcription saved
- [ ] **1.4.18** Verify: `.\test-helpers\Verify-FileSystem.ps1 -Path "%USERPROFILE%\Documents\diktame-notes.md" -Type File`

### Oops Mode
- [ ] **1.4.16** Dictate "test text" → injected
- [ ] **1.4.17** Press Ctrl+Alt+V → "test text" re-injected
- [ ] **1.4.18** Restart app → Press Ctrl+Alt+V → No-op (nothing stored)

### Quick Chat
- [ ] **1.4.19** Press Ctrl+Alt+C → QuickChatWindow appears (always-on-top)
- [ ] **1.4.20** Verify font size matches Chat settings (default 14pt or custom)
- [ ] **1.4.21** Verify window opacity matches Chat settings (default 1.0 or custom)
- [ ] **1.4.22** Type "What is the capital of Spain" → Click Send → "Madrid" appears
- [ ] **1.4.23** Click Mic button, say "What is 5 plus 5", release → "10" appears
- [ ] **1.4.24** Verify timestamps shown/hidden per Chat settings
- [ ] **1.4.25** If markdown enabled, type "**bold** and `code`" → Verify formatted
- [ ] **1.4.26** Press Esc → Window closes
- [ ] **1.4.27** If "Forget on Close" enabled → Press Ctrl+Alt+C → No history
- [ ] **1.4.28** If "Forget on Close" disabled → Press Ctrl+Alt+C → History persists
- [ ] **1.4.29** Test max history limit: Send (limit + 5) messages → Only last (limit) shown

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

# Journey 5: Comprehensive Settings Verification

**Configuration:** Any working setup (Cloud or Local)
**Duration:** ~2 hours
**Goal:** Systematically verify all 9 settings tabs and persistence

**Prerequisites:**
- Complete Journey 1, 2, or 3 first (need working API keys)
- App is running and configured

## 5.1: General Settings Tab
- [ ] **5.1.1** Settings → General → Verify trailing space toggle works
- [ ] **5.1.2** Verify additional key dropdown (None/Enter/Tab)
- [ ] **5.1.3** Enable auto-start → Verify Task Scheduler entry
- [ ] **5.1.4** Disable auto-start → Verify entry removed
- [ ] **5.1.5** Change language (if available) → UI updates

## 5.2: AI Engine Tab
- [ ] **5.2.1** Settings → AI Engine → Verify Cloud/Local toggle
- [ ] **5.2.2** Cloud: Select different STT provider → Save
- [ ] **5.2.3** Cloud: Select different LLM provider → Save
- [ ] **5.2.4** Local: Verify Whisper model selection
- [ ] **5.2.5** Local: Verify Ollama connection status

## 5.3: Modes Tab (Utility Pipelines Only)
- [ ] **5.3.1** Settings → Modes → Verify sidebar shows: Ask, Refine, Translate
- [ ] **5.3.2** Verify Note and Chat NOT in list (have dedicated pages)
- [ ] **5.3.3** Select Ask → Edit Cloud prompt → Save → Verify persists
- [ ] **5.3.4** Select Ask → Edit Local prompt → Save → Verify persists
- [ ] **5.3.5** Select Refine → Verify both profiles editable
- [ ] **5.3.6** Select Translate → Verify both profiles editable
- [ ] **5.3.7** Test Ask mode → Verify uses edited prompt

## 5.4: Dictation Presets Tab (CRUD)
- [ ] **5.4.1** Settings → Dictation Presets → Verify 4 built-ins: Standard, Prompt, Professional, Raw
- [ ] **5.4.2** Select Standard → Verify Cloud system prompt field
- [ ] **5.4.3** Select Standard → Verify Local system prompt field
- [ ] **5.4.4** Select Standard → Verify model dropdown (Cloud profile only)
- [ ] **5.4.5** Verify model dropdown shows: "(Default)" + 30+ models from APIs
- [ ] **5.4.6** Verify NO Ollama models in Cloud dropdown (only OpenAI, Anthropic, Gemini, OpenRouter)
- [ ] **5.4.7** Change Cloud model to "gpt-4o (OpenAI)" → Save
- [ ] **5.4.8** Restart app → Verify model selection persists
- [ ] **5.4.9** Click "Add Preset" → Enter "Test Preset" → Save
- [ ] **5.4.10** Select "Test Preset" → Edit prompts → Save
- [ ] **5.4.11** Verify custom preset persists after restart
- [ ] **5.4.12** Select built-in preset → Verify Delete button disabled
- [ ] **5.4.13** Select custom preset → Click Delete → Confirm → Preset removed
- [ ] **5.4.14** Verify NO hotkey fields (managed in Hotkeys tab)
- [ ] **5.4.15** Create 3 custom presets → Verify all saved
- [ ] **5.4.16** Delete all custom presets → Only built-ins remain

## 5.5: Notes Settings Tab
- [ ] **5.5.1** Settings → Notes → Verify default path = `%USERPROFILE%\Documents\diktame-notes.md`
- [ ] **5.5.2** Click "Browse" → FileSavePicker opens (WinUI, not WPF)
- [ ] **5.5.3** Select new file path → Path field updates
- [ ] **5.5.4** Verify "LLM Processing" toggle (default: enabled)
- [ ] **5.5.5** Verify timestamp format field (default: "yyyy-MM-dd HH:mm:ss")
- [ ] **5.5.6** Change timestamp to "dd/MM/yyyy HH:mm" → Live preview updates immediately
- [ ] **5.5.7** Verify preview shows current time in chosen format
- [ ] **5.5.8** Change to invalid format "INVALID" → Preview shows DateTime.Now or error
- [ ] **5.5.9** Verify Cloud system prompt editor (multiline TextBox)
- [ ] **5.5.10** Verify Local system prompt editor (multiline TextBox)
- [ ] **5.5.11** Edit both prompts → Click Save → Verify persists
- [ ] **5.5.12** Click "Reset to Defaults" → All fields revert
- [ ] **5.5.13** Restart app → Verify all changes persist
- [ ] **5.5.14** Test Note mode → Verify uses configured file path and format

## 5.6: Chat Settings Tab
- [ ] **5.6.1** Settings → Chat → Verify font size slider (10-24pt, default 14pt)
- [ ] **5.6.2** Drag font size to 18 → Value displays "18"
- [ ] **5.6.3** Verify window opacity slider (0.5-1.0, default 1.0)
- [ ] **5.6.4** Drag opacity to 0.7 → Value displays "0.7"
- [ ] **5.6.5** Verify theme dropdown: System, Light, Dark
- [ ] **5.6.6** Select "Light" → Save
- [ ] **5.6.7** Verify "Forget on Close" toggle (privacy mode)
- [ ] **5.6.8** Enable "Forget on Close" → Save
- [ ] **5.6.9** Verify max history messages NumberBox (default 100, 0=unlimited)
- [ ] **5.6.10** Set max history to 25 → Save
- [ ] **5.6.11** Verify "Show Timestamps" toggle
- [ ] **5.6.12** Verify "Enable Markdown" toggle
- [ ] **5.6.13** Edit Cloud system prompt → Save
- [ ] **5.6.14** Edit Local system prompt → Save
- [ ] **5.6.15** Click "Reset to Defaults" → All revert
- [ ] **5.6.16** Save custom config → Restart → Verify persists
- [ ] **5.6.17** Open Quick Chat → Verify font size matches settings
- [ ] **5.6.18** Open Quick Chat → Verify opacity matches settings
- [ ] **5.6.19** Close Quick Chat → If "Forget on Close", verify history cleared
- [ ] **5.6.20** Test max history: Send 30 messages → Only last 25 shown (from 5.6.10)

## 5.7: Audio Settings Tab
- [ ] **5.7.1** Settings → Audio → Verify input device dropdown
- [ ] **5.7.2** Verify sample rate dropdown (16kHz/48kHz)
- [ ] **5.7.3** Verify max recording duration NumberBox
- [ ] **5.7.4** Set duration to 5 seconds → Save
- [ ] **5.7.5** Dictate for 10 seconds → Recording stops at 5 seconds
- [ ] **5.7.6** Verify "Enable Audio Ducking" toggle
- [ ] **5.7.7** Enable ducking → Set target volume to 20% → Save
- [ ] **5.7.8** Play music → Dictate → Music drops to ~20% volume
- [ ] **5.7.9** Disable ducking → Music unaffected during dictation

## 5.8: Hotkeys Settings Tab
- [ ] **5.8.1** Settings → Hotkeys → Verify all 7 hotkey fields visible
- [ ] **5.8.2** Verify default Dictate hotkey = Ctrl+Alt+D
- [ ] **5.8.3** Change Dictate to Ctrl+Shift+D → Save → Test → Works
- [ ] **5.8.4** Verify Refine, Ask, Translate, Note, Oops, Chat hotkeys
- [ ] **5.8.5** Change all hotkeys → Save → Test each → All work
- [ ] **5.8.6** Restart app → Verify all hotkeys persist
- [ ] **5.8.7** Try duplicate hotkey → Verify error/warning
- [ ] **5.8.8** Reset to defaults → All revert to Ctrl+Alt+X

## 5.9: Privacy Settings Tab
- [ ] **5.9.1** Settings → Privacy → Verify 4 levels: Full, Balanced, Stats, Ghost
- [ ] **5.9.2** Select "Full" → Dictate → Verify verbatim in history.db
- [ ] **5.9.3** Select "Balanced" → Dictate "test@example.com" → Verify [REDACTED]
- [ ] **5.9.4** Select "Stats" → Dictate → Verify only count stored
- [ ] **5.9.5** Select "Ghost" → Dictate → Verify no history entry
- [ ] **5.9.6** Click "Wipe Data" → Confirm → Verify history.db cleared
- [ ] **5.9.7** Verify: `.\test-helpers\Verify-HistoryDb.ps1`

## 5.10: API Keys Tab
- [ ] **5.10.1** Settings → API Keys → Verify Cloud providers listed
- [ ] **5.10.2** Verify keys are masked (******)
- [ ] **5.10.3** Click "Show" → Key visible
- [ ] **5.10.4** Click "Hide" → Key masked again
- [ ] **5.10.5** Click "Test Connection" on Deepgram → Success
- [ ] **5.10.6** Click "Test Connection" on LLM → Success
- [ ] **5.10.7** Enter invalid key → Test → Error message clear
- [ ] **5.10.8** Update key → Save → Restart → Verify works
- [ ] **5.10.9** Verify: `.\test-helpers\Verify-SecureStorage.ps1`

## 5.11: Ollama Settings Tab
- [ ] **5.11.1** Settings → Ollama → Verify health check (if Ollama running)
- [ ] **5.11.2** Verify Ollama version displayed
- [ ] **5.11.3** Verify model library lists installed models
- [ ] **5.11.4** Select different model → Save
- [ ] **5.11.5** Test dictation → Verify new model used
- [ ] **5.11.6** Stop Ollama → Health check shows offline
- [ ] **5.11.7** Start Ollama → Health check shows online

## 5.12: Snippets Settings Tab
- [ ] **5.12.1** Settings → Snippets → Add snippet: "myemail" → "test@example.com"
- [ ] **5.12.2** Dictate "Send to myemail" → Expands correctly
- [ ] **5.12.3** Edit snippet → Changes persist
- [ ] **5.12.4** Delete snippet → Removed
- [ ] **5.12.5** Add 5 snippets → All saved
- [ ] **5.12.6** Restart app → All snippets persist
- [ ] **5.12.7** Verify: `.\test-helpers\Verify-Snippets.ps1`

## 5.13: Control Panel Settings Tab
- [ ] **5.13.1** Settings → Control Panel → Verify "Show on Startup" toggle
- [ ] **5.13.2** Enable "Show on Startup" → Restart → Panel visible
- [ ] **5.13.3** Disable "Show on Startup" → Restart → Panel hidden
- [ ] **5.13.4** Verify "Always on Top" toggle
- [ ] **5.13.5** Enable "Always on Top" → Control Panel stays foreground
- [ ] **5.13.6** Verify "Show Status" toggle (shows current state)

## 5.14: About Tab
- [ ] **5.14.1** Settings → About → Verify app version displayed
- [ ] **5.14.2** Verify copyright/license info
- [ ] **5.14.3** Verify links (GitHub, docs) are clickable
- [ ] **5.14.4** Click GitHub link → Opens in browser

## 5.15: Settings Persistence & Migration
- [ ] **5.15.1** Make changes to all 14 tabs → Save all
- [ ] **5.15.2** Restart app → Verify all changes persist
- [ ] **5.15.3** Check settings.json structure:
  - 4 DictationModes (standard, prompt, professional, raw)
  - 5 UtilityPipelines (ask, refine, translate, note, chat)
- [ ] **5.15.4** Verify: `.\test-helpers\Get-AppState.ps1`
- [ ] **5.15.5** Delete settings.json → Restart → Verify defaults populate
- [ ] **5.15.6** Verify migration creates 4 modes + 5 pipelines automatically

## 5.16: Journey 5 Complete ✅

**Summary:** All 9 settings tabs verified, CRUD operations tested, persistence confirmed

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

- [ ] **6.4.1** Settings -> Account -> Display name shown correctly
- [ ] **6.4.2** Avatar shown (if uploaded on website)
- [ ] **6.4.3** Click "Sign Out" -> Confirms -> Signed out, tokens cleared
- [ ] **6.4.4** Restart app -> No longer signed in

## 6.5: Journey 6 Complete

---

# Journey 7: TTS System (Text-to-Speech)

**Configuration:** Any working setup + TTS enabled
**Duration:** ~1 hour
**Goal:** Validate local (Kokoro) and cloud TTS providers, notification TTS

**Prerequisites:**
- Working dictation setup (Journey 1 or 3 completed)

## 7.1: TTS Provider Setup

- [ ] **7.1.1** Settings -> TTS -> Verify provider selection (Off / Kokoro / Deepgram / OpenAI / Gemini)
- [ ] **7.1.2** Select **Kokoro** (local) -> Verify model download UI if model not present
- [ ] **7.1.3** Kokoro model download completes -> Status shows "Ready"
- [ ] **7.1.4** Select **Deepgram** (cloud) -> Requires API key
- [ ] **7.1.5** Select **OpenAI** (cloud) -> Requires API key
- [ ] **7.1.6** Select **Off** -> TTS disabled globally

## 7.2: TTS Playback

- [ ] **7.2.1** Enable TTS (Kokoro) -> Dictate "Hello world" -> Text injected AND spoken aloud
- [ ] **7.2.2** Verify audio ducking during TTS playback (if ducking enabled)
- [ ] **7.2.3** TTS playback completes -> Audio ducking restores
- [ ] **7.2.4** Enable TTS (cloud provider) -> Dictate -> Cloud TTS plays back
- [ ] **7.2.5** Compare latency: Kokoro (local) vs Cloud TTS

## 7.3: ReadSelection Mode (Ctrl+Alt+S)

- [ ] **7.3.1** Select text in Notepad ("The quick brown fox jumps over the lazy dog")
- [ ] **7.3.2** Press Ctrl+Alt+S -> Selected text is read aloud via TTS
- [ ] **7.3.3** Verify audio plays to completion
- [ ] **7.3.4** Press Ctrl+Alt+S with no selection -> Error notification or no-op
- [ ] **7.3.5** Select very long text (500+ chars) -> TTS handles without crash

## 7.4: Notification TTS

- [ ] **7.4.1** Use Ask mode ("What is 2+2") -> Answer "4" spoken aloud via toast TTS
- [ ] **7.4.2** Use Translate mode -> Translation spoken aloud
- [ ] **7.4.3** Verify suppressTts prevents double-speak (notification + pipeline TTS)
- [ ] **7.4.4** Disable TTS -> Ask mode -> Answer shown in toast but NOT spoken

## 7.5: TTS Settings Persistence

- [ ] **7.5.1** Configure TTS provider + voice settings -> Save
- [ ] **7.5.2** Restart app -> TTS settings persist
- [ ] **7.5.3** Verify tts_played_ms logged in history.db for TTS dictations

## 7.6: Journey 7 Complete

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

## CT.6: ReadSelection (Ctrl+Alt+S)

- [ ] **CT.6.1** Select text in any app -> Press Ctrl+Alt+S -> Text read aloud
- [ ] **CT.6.2** No TTS configured -> Error notification
- [ ] **CT.6.3** Empty selection -> No-op or error

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
- [ ] Journey 5: Comprehensive Settings Verification (9 tabs)
- [ ] Journey 6: Wallet System + Account/Auth
- [ ] Journey 7: TTS System
- [ ] Cross-Cutting: Themes, Control Panel, ReadSelection
- [ ] Audio Feeder Automation

**Total Time:** ___ hours
**Bugs Found:** ___ critical, ___ important, ___ minor
**Success Rate:** ___% scenarios passed
**Total Scenarios:** ~355

---

**Ready for installer creation when all journeys complete!** 🎯
