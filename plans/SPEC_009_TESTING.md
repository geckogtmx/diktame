# SPEC_009 Testing Protocol: Local Mode End-to-End

> **Purpose**: Repeatable manual test script for SPEC_009_LOCALFLOW changes.
> **Run this after every code change** until all scenarios pass.
> **Mark each check** with `[x]` when verified. Reset all to `[ ]` for next run.
>
> **Execution order matters!** Scenarios are ordered to test progressively:
> 1. First: Ollama **not installed** — test graceful degradation and cloud paths
> 2. Then: Install Ollama, pull model — test full local path
> 3. Finally: Settings switching and second-launch behavior

---

## Prerequisites

Before starting:

1. App builds clean: `dotnet build DiktaMe.sln -c Debug` → 0 errors
2. Tests pass: `dotnet test DiktaMe.sln` → 0 failures
3. Have valid Deepgram + Gemini API keys ready for cloud scenarios

**DO NOT install Ollama yet** — Scenarios 1–3 test without it.

---

## Part A: Without Ollama Installed

> **Before starting Part A**: Uninstall Ollama completely.
> - Windows: Settings → Apps → Ollama → Uninstall
> - Delete leftover folder if present: `%LOCALAPPDATA%\Ollama`
> - Verify it's gone: `curl http://localhost:11434/api/version` → connection refused

---

### Scenario 1: Full Cloud — No Ollama on System (Regression Baseline)

**Tests**: Cloud flow works perfectly on a system with no Ollama at all. This is the most common user profile.

#### Setup

```
1. Close dIKta.me if running
2. Delete: %APPDATA%\DiktaMe\settings.json
3. Delete: %APPDATA%\DiktaMe\models\ (entire folder)
4. Confirm Ollama NOT installed (curl http://localhost:11434/api/version → refused)
```

#### Wizard Flow

| #   | Action                                        | Expected                            | Pass? |
| --- | --------------------------------------------- | ----------------------------------- | ----- |
| 1.1 | Launch app                                    | Wizard window appears (Step 1 of 6) | [✓]   |
| 1.2 | Step 0: Select "I have API Keys" → Next       | Moves to STT step                   | [✓]   |
| 1.3 | Step 1: Select **"Cloud (Deepgram)"** → Next  | Moves to LLM step                   | [✓]   |
| 1.4 | Step 2: Select **"Cloud (Gemini)"** → Next    | Moves to API Keys step              | [✓]   |
| 1.5 | Step 3: Both STT and LLM key sections visible | Enter both keys                     | [✓]   |
| 1.6 | Steps 4-5: Audio test → Finish                | Wizard completes                    | [✓]   |

#### Loading Screen

| #    | Expected                                     | Pass? |
| ---- | -------------------------------------------- | ----- |
| 1.7  | **NO** "Downloading Whisper model..."        | [✓]   |
| 1.8  | "Checking local services..." appears briefly | [✓]   |
| 1.9  | **NO** "Warming up Ollama..."                | [✓]   |
| 1.10 | **NO crash** despite Ollama being absent     | [✓]   |
| 1.11 | Loading completes, main window opens         | [✓]   |

#### Settings Verification

| #    | Check                                | Expected     | Pass? |
| ---- | ------------------------------------ | ------------ | ----- |
| 1.12 | `ActiveProfileName`                  | `"Cloud"`    | [✓]   |
| 1.13 | `ModeProfiles.dictate_0.SttProvider` | `"deepgram"` | [✓]   |
| 1.14 | `ModeProfiles.dictate_0.LlmProvider` | `"gemini"`   | [✓]   |

#### Dictation Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 1.15 | Ctrl+Alt+D → speak → wait | Deepgram → Gemini → text injected (all cloud) | [✓] |

---

### Scenario 2: Hybrid Local STT + Cloud LLM — No Ollama on System

**Tests**: Whisper downloads during wizard STT step, cloud LLM works, no Ollama-related crashes.

#### Setup

```
1. Close dIKta.me
2. Delete: %APPDATA%\DiktaMe\settings.json
3. Delete: %APPDATA%\DiktaMe\models\ (entire folder)
4. Ollama still NOT installed
5. Have a valid Gemini API key ready
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 2.1 | Launch app | Wizard appears (Step 1 of 7) — Language selection | [✓] |
| 2.2 | Step 0 (Language): Select English → Next | Moves to onboarding step | [✓] |
| 2.3 | Step 1 (Get Started): Select **"I have API Keys"** → Next | Moves to STT step | [✓] |
| 2.4 | Step 2 (STT): Select **"Local (Whisper)"** | Download panel shows "Model will be downloaded when you click Next" | [✓] |
| 2.5 | Click **Next** | Download starts, Next button **disabled**, progress bar fills | [✓] |
| 2.6 | Download completes (~466 MB) | Status shows **"Whisper model ready"**, auto-advances to LLM step | [✓] |
| 2.8 | Step 3 (LLM): Select **"Cloud (Gemini)"** → Next | Moves to API Keys step | [✓] |
| 2.9 | Step 4 (API Keys): STT section **hidden**, LLM section **visible** | Enter Gemini key | [✓] |
| 2.10 | Enter Gemini API key → Next | Moves to Audio Test | [✓] |
| 2.11 | Step 5 (Audio Test): Select mic, record test → Next | Moves to Ready | [✓] |
| 2.12 | Step 6 (Ready): Summary shows "Whisper" + "Gemini" → Finish | Wizard completes | [✓] |

#### Wizard Download Cancellation Test (Optional)

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 2.13 | On STT step: select "Local", click Next to start download | Progress bar updating | [✓] |
| 2.14 | Click **Back** mid-download | Download cancels, returns to previous step | [✓] |
| 2.15 | Return to STT step, select "Local", click Next again | Download resumes from scratch (partial file cleaned up) | [✓] |

#### Loading Screen

| # | Expected | Pass? |
|---|----------|-------|
| 2.16 | **NO** "Downloading Whisper model..." (already downloaded in wizard) | [✓] |
| 2.17 | "Checking local services..." appears briefly | [✓] |
| 2.18 | **NO** "Warming up Ollama..." (LLM is cloud, Ollama not installed) | [✓] |
| 2.19 | **NO crash** despite Ollama being absent | [✓] |
| 2.20 | Loading completes, main window opens | [✓] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| 2.21 | `ActiveProfileName` | `"Cloud"` (LLM choice drives this) | [✓] |
| 2.22 | `ModeProfiles.dictate_0.SttProvider` | `"whisper"` | [✓] |
| 2.23 | `ModeProfiles.dictate_0.LlmProvider` | `"gemini"` | [✓] |
| 2.24 | Whisper model file exists | `%APPDATA%\DiktaMe\models\ggml-small.bin` (~466 MB) | [✓] |

#### Dictation Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 2.25 | Ctrl+Alt+D → speak → wait | Whisper transcribes locally, Gemini processes via cloud, text injected | [✓] |
| 2.26 | Check logs | No Ollama calls, Gemini API used for LLM | [✓] |

---

### Scenario 3: User Picks Local LLM — But Ollama Not Installed

**Tests**: What happens when a user selects "Local (Ollama)" but Ollama isn't on the system. This is the exact friction scenario we're trying to improve.

#### Setup

```
1. Close dIKta.me
2. Delete: %APPDATA%\DiktaMe\settings.json
3. Delete: %APPDATA%\DiktaMe\models\ (entire folder)
4. Ollama still NOT installed
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 3.1 | Launch app → wizard | Wizard appears | [ ] |
| 3.2 | Step 0: "I have API Keys" → Next | | [ ] |
| 3.3 | Step 1: Select **"Local (Whisper)"** → Next | | [ ] |
| 3.4 | Step 2: Select **"Local (Ollama)"** → Next | Wizard proceeds (no blocking yet) | [ ] |
| 3.5 | Step 3: API Keys page | Both sections **hidden** (both local) | [ ] |
| 3.6 | Steps 4-5: Audio test → Finish | Wizard completes | [ ] |

#### Loading Screen

| # | Expected | Pass? |
|---|----------|-------|
| 3.7 | Whisper model downloads with progress | [ ] |
| 3.8 | "Checking local services..." appears | [ ] |
| 3.9 | **NO** "Warming up Ollama..." (Ollama check returns Offline) | [ ] |
| 3.10 | **NO crash** — loading continues gracefully | [ ] |
| 3.11 | Loading completes, main window opens | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| 3.12 | `ActiveProfileName` | `"Local"` | [ ] |
| 3.13 | `ModeProfiles.dictate_0.SttProvider` | `"whisper"` | [ ] |
| 3.14 | `ModeProfiles.dictate_0.LlmProvider` | `"ollama"` | [ ] |

#### Dictation Test (Degraded Mode)

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 3.15 | Ctrl+Alt+D → speak → wait | Whisper STT works (transcription happens) | [ ] |
| 3.16 | LLM processing step | Fails gracefully — error toast, **no crash** | [ ] |
| 3.17 | Check logs | Ollama connection error logged, no unhandled exceptions | [ ] |

> **Note**: This scenario documents current behavior (graceful failure).
> Future work (wizard-time Ollama validation) will add inline guidance before the user gets here.

---

## Part B: Install Ollama

> **Transition step**: Install Ollama now before continuing to Part C.
>
> ```
> 1. Download Ollama from https://ollama.com/download/windows
> 2. Run OllamaSetup.exe — follow default install
> 3. Verify running: curl http://localhost:11434/api/version → {"version":"..."}
> 4. Pull model: ollama pull gemma3  (wait for ~3 GB download)
> 5. Verify model: ollama list → shows gemma3
> ```

---

## Part C: With Ollama Installed + Model Pulled

---

### Scenario 4: Full Local (Fresh Install — The Golden Path)

**Tests**: Wizard defaults, ActiveProfileName, Whisper download, Ollama warmup, end-to-end dictation. This is the primary "it works" scenario.

#### Setup

```
1. Close dIKta.me if running
2. Delete: %APPDATA%\DiktaMe\settings.json
3. Delete: %APPDATA%\DiktaMe\models\ (entire folder)
4. Verify Ollama running: curl http://localhost:11434/api/version
5. Verify gemma3 pulled: ollama list
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 4.1 | Launch app | Wizard window appears (Step 1 of 6) | [ ] |
| 4.2 | Step 0: Select "I have API Keys" → Next | Moves to STT step | [ ] |
| 4.3 | Step 1: Select **"Local (Whisper)"** → Next | Moves to LLM step | [ ] |
| 4.4 | Step 2: Select **"Local (Ollama)"** → Next | Moves to API Keys step | [ ] |
| 4.5 | Step 3: API Keys page | Both sections **hidden** (both local) | [ ] |
| 4.6 | Step 3: Click Next | Moves to Audio Test | [ ] |
| 4.7 | Step 4: Select mic, click Record | Records 3s, shows "Recording captured: X KB" | [ ] |
| 4.8 | Step 4: Click Next | Moves to Ready | [ ] |
| 4.9 | Step 5: Summary card | Shows "Speech-to-Text: Whisper" and "Language Model: Ollama" | [ ] |
| 4.10 | Step 5: Click **Finish** | Wizard closes, loading screen appears | [ ] |

#### Loading Screen

| # | Expected | Pass? |
|---|----------|-------|
| 4.11 | Status text shows **"Downloading Whisper model..."** | [ ] |
| 4.12 | Progress updates with percentage (e.g. "Downloading Whisper model... 42%") | [ ] |
| 4.13 | Download completes (~466 MB for small model) | [ ] |
| 4.14 | Status text shows **"Checking local services..."** | [ ] |
| 4.15 | Status text shows **"Warming up Ollama model..."** | [ ] |
| 4.16 | Loading completes, main window opens | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| 4.17 | Open `%APPDATA%\DiktaMe\settings.json` | File exists | [ ] |
| 4.18 | `ActiveProfileName` | `"Local"` | [ ] |
| 4.19 | `OllamaModel` | `"gemma3"` | [ ] |
| 4.20 | `WhisperModel` | `"small"` | [ ] |
| 4.21 | `ModeProfiles.dictate_0.SttProvider` | `"whisper"` | [ ] |
| 4.22 | `ModeProfiles.dictate_0.LlmProvider` | `"ollama"` | [ ] |
| 4.23 | File exists: `%APPDATA%\DiktaMe\models\ggml-small.bin` | ~466 MB | [ ] |

#### Dictation Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 4.24 | Open a text editor (Notepad), click into text area | Cursor active | [ ] |
| 4.25 | Press **Ctrl+Alt+D**, speak a sentence, wait for auto-stop | Recording starts (sound plays) | [ ] |
| 4.26 | Wait for pipeline to complete | Text appears in Notepad | [ ] |
| 4.27 | Check Serilog logs | No cloud API errors, Whisper transcribed, Ollama processed | [ ] |

#### Settings UI Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 4.28 | Open Settings → AI Engine tab | Page loads without crash | [ ] |
| 4.29 | STT dropdown | Shows "Local" selected | [ ] |
| 4.30 | LLM dropdown | Shows "Local" selected | [ ] |
| 4.31 | Whisper section | **Visible** with "Small (~466 MB, recommended)" selected | [ ] |
| 4.32 | Deepgram section | **Hidden** | [ ] |
| 4.33 | Capability summary | Shows "STT: Local Whisper  |  LLM: Ollama (gemma3)" | [ ] |

---

### Scenario 5: Hybrid — Cloud STT + Local LLM

**Tests**: No Whisper download, Ollama warms up, ActiveProfileName = Local

#### Setup

```
1. Close dIKta.me
2. Delete: %APPDATA%\DiktaMe\settings.json
3. Have a valid Deepgram API key ready
4. Ollama running with gemma3 pulled
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 5.1 | Launch app → wizard | Wizard appears | [ ] |
| 5.2 | Step 0: "I have API Keys" → Next | | [ ] |
| 5.3 | Step 1: Select **"Cloud (Deepgram)"** → Next | | [ ] |
| 5.4 | Step 2: Select **"Local (Ollama)"** → Next | | [ ] |
| 5.5 | Step 3: API Keys page | STT key section **visible**, LLM key section **hidden** | [ ] |
| 5.6 | Enter Deepgram key → Next | | [ ] |
| 5.7 | Steps 4-5: Audio test → Finish | Wizard completes | [ ] |

#### Loading Screen

| # | Expected | Pass? |
|---|----------|-------|
| 5.8 | **NO** "Downloading Whisper model..." (STT is cloud) | [ ] |
| 5.9 | Status shows "Checking local services..." | [ ] |
| 5.10 | Status shows **"Warming up Ollama model..."** | [ ] |
| 5.11 | Loading completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| 5.12 | `ActiveProfileName` | `"Local"` (LLM is local) | [ ] |
| 5.13 | `ModeProfiles.dictate_0.SttProvider` | `"deepgram"` | [ ] |
| 5.14 | `ModeProfiles.dictate_0.LlmProvider` | `"ollama"` | [ ] |
| 5.15 | No Whisper model downloaded | `%APPDATA%\DiktaMe\models\` folder empty or absent | [ ] |

#### Dictation Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 5.16 | Ctrl+Alt+D → speak → wait | Deepgram transcribes via cloud, Ollama processes locally, text injected | [ ] |

---

### Scenario 6: Settings UI Mode Switching

**Tests**: Toggle sync, section visibility, settings persistence

#### Setup

```
Start from any completed wizard state (e.g., after Scenario 5)
Ollama still running
```

#### STT Toggle

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 6.1 | Settings → AI Engine → change STT to **"Local"** | Whisper section appears, Deepgram section hides | [ ] |
| 6.2 | Check `settings.json` → `ModeProfiles.dictate_0.SttProvider` | `"whisper"` | [ ] |
| 6.3 | Change STT back to **"Cloud"** | Deepgram section appears, Whisper section hides | [ ] |
| 6.4 | Check `settings.json` → `ModeProfiles.dictate_0.SttProvider` | `"deepgram"` | [ ] |

#### LLM Toggle

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 6.5 | Change LLM to **"Local (Ollama)"** | Capability summary updates to show "Ollama" | [ ] |
| 6.6 | Check `settings.json` → `ActiveProfileName` | `"Local"` | [ ] |
| 6.7 | Check `settings.json` → `ModeProfiles.dictate_0.LlmProvider` | `"ollama"` | [ ] |
| 6.8 | Change LLM to **"Cloud (Gemini)"** | Summary updates | [ ] |
| 6.9 | Check `settings.json` → `ActiveProfileName` | `"Cloud"` | [ ] |
| 6.10 | Change LLM to **"Skip LLM"** | Summary shows "Disabled" | [ ] |
| 6.11 | Check `settings.json` → `ActiveProfileName` | `"Cloud"` | [ ] |
| 6.12 | Check `settings.json` → `ModeProfiles.dictate_0.LlmProvider` | `"none"` | [ ] |

#### Whisper Model Picker

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| 6.13 | Set STT to "Local" → Whisper section visible | ComboBox shows "Small (~466 MB, recommended)" | [ ] |
| 6.14 | Change to **"Medium (~1.5 GB)"** | | [ ] |
| 6.15 | Check `settings.json` → `WhisperModel` | `"medium"` | [ ] |
| 6.16 | Change to **"Tiny (~75 MB)"** | | [ ] |
| 6.17 | Check `settings.json` → `WhisperModel` | `"tiny"` | [ ] |
| 6.18 | Change back to **"Small"** | | [ ] |
| 6.19 | Check `settings.json` → `WhisperModel` | `"small"` | [ ] |

---

### Scenario 7: Second Launch (Model Already Downloaded)

**Tests**: Startup skips download when model exists, Ollama still warms up

#### Setup

```
Complete Scenario 4 first (settings.json has local config, model file exists)
Close and relaunch the app
```

| # | Expected | Pass? |
|---|----------|-------|
| 7.1 | No wizard (WizardCompleted = true) | [ ] |
| 7.2 | Loading screen: **NO** "Downloading Whisper model..." (already exists) | [ ] |
| 7.3 | Loading screen: **"Warming up Ollama model..."** appears | [ ] |
| 7.4 | App loads to main window | [ ] |
| 7.5 | Ctrl+Alt+D works immediately (no cold start delay) | [ ] |

---

### Scenario 8: Ollama Service Stopped (Process Killed)

**Tests**: App handles Ollama being installed but not running

#### Setup

```
1. Complete Scenario 4 (local mode configured, model exists)
2. Stop Ollama: taskkill /f /im ollama.exe (or close from system tray)
3. Verify stopped: curl http://localhost:11434/api/version → refused
4. Relaunch dIKta.me
```

| # | Expected | Pass? |
|---|----------|-------|
| 8.1 | Loading screen: no crash during "Checking local services..." | [ ] |
| 8.2 | **NO** "Warming up Ollama..." (Ollama offline, check returned Offline) | [ ] |
| 8.3 | App loads to main window | [ ] |
| 8.4 | Ctrl+Alt+D → speak → Whisper transcribes | STT works (independent of LLM) | [ ] |
| 8.5 | LLM step fails gracefully | Error toast, **no crash** | [ ] |

> After testing: restart Ollama (`ollama serve`) for remaining scenarios if needed.

---

## Quick Reference: File Paths

| Item | Path |
|------|------|
| Settings file | `%APPDATA%\DiktaMe\settings.json` |
| Whisper models | `%APPDATA%\DiktaMe\models\` |
| Whisper small model | `%APPDATA%\DiktaMe\models\ggml-small.bin` (~466 MB) |
| Ollama version cache | `%APPDATA%\DiktaMe\ollama_last_version.txt` |
| Log files | Check Serilog output (console/file depending on config) |

## Quick Reference: Key Settings Fields

```json
{
  "ActiveProfileName": "Local",       // "Local" or "Cloud" — driven by LLM choice
  "OllamaModel": "gemma3",            // Default local LLM model
  "WhisperModel": "small",            // Default local STT model size
  "ModeProfiles": {
    "dictate_0": {
      "SttProvider": "whisper",        // "whisper" or "deepgram"
      "LlmProvider": "ollama",         // "ollama", "gemini", or "none"
      "UseLlm": true
    }
  }
}
```

---

## Run Log

Use this section to record test run results.

### Run 1 — Date: ___________

**Part A (No Ollama)**:

| Scenario | Result | Notes |
|----------|--------|-------|
| 1. Full Cloud (no Ollama on system) | | |
| 2. Hybrid Local STT + Cloud LLM (no Ollama) | | |
| 3. Local LLM selected but Ollama missing | | |

**Part B (Install Ollama)**: Installed? [ ] Version: ______ gemma3 pulled? [ ]

**Part C (With Ollama)**:

| Scenario | Result | Notes |
|----------|--------|-------|
| 4. Full Local (golden path) | | |
| 5. Hybrid Cloud STT + Local LLM | | |
| 6. Settings UI switching | | |
| 7. Second launch (model cached) | | |
| 8. Ollama service stopped | | |

**Blockers found**:
-

**Fixes applied**:
-
