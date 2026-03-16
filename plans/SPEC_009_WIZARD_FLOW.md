# SPEC_009_WIZARD_FLOW: Complete Wizard Path Matrix

> **Purpose**: Definitive test matrix for ALL wizard paths, including STT, LLM, and TTS combinations.
> **Date**: 2026-03-16
> **Status**: Draft (target-state document — describes wizard AFTER FIX-17 is implemented)
> **Prerequisites**: FIX-17 (TTS wizard step) — currently pending, see `SPEC_009_FIXES.md`
> **Related**: `SPEC_009_FIXES.md` (FIX-17), `SPEC_009_TESTING.md`, `LIVE_TESTING_PLAN.md` Section 1

---

## 1. Wizard Architecture

### 1.1 Steps (8 total, post-FIX-17)

| Step | Page | Purpose |
|------|------|---------|
| 0 | `WizardLanguagePage` | Choose UI language (English / Spanish) |
| 1 | `WizardGetStartedPage` | Choose entry path: Wallet / BYOK (API Keys) / Local |
| 2 | `WizardSttPage` | Choose STT: Cloud (Deepgram) / Local (Whisper ~466MB download) |
| 3 | `WizardLlmPage` | Choose LLM: Cloud (Gemini) / Local (Ollama — install + pull) |
| 4 | `WizardTtsPage` | Choose TTS: Off / Local (Kokoro ~88MB download) / Cloud (Deepgram) |
| 5 | `WizardApiKeysPage` | Enter API keys for cloud providers (skipped if all local) |
| 6 | `WizardTestPage` | Test microphone recording |
| 7 | `WizardReadyPage` | Summary + Finish |

### 1.2 Three Entry Paths (Step 1 forks)

| Path | Name | Steps Visited | Completes Via |
|------|------|---------------|---------------|
| **W** | Wallet | 0 → 1 → exit | `StartWalletAsync()` — opens browser for OAuth |
| **L** | Local | 0 → 1 → exit | `StartLocalAsync()` — configures Whisper + Ollama + Kokoro |
| **A** | BYOK (API Keys) | 0 → 1 → 2 → 3 → 4 → [5] → 6 → 7 | `CompleteWizardAsync()` |

### 1.3 Step-Skip Logic

**API Keys (Step 5)** is skipped when no cloud providers need keys:

```csharp
NeedsApiKeys() => SttChoice == "cloud" || LlmChoice == "cloud" || TtsChoice == "cloud"
```

- If all three are local/off → Step 5 skipped (both `GoNextAsync()` and `GoBack()` auto-skip)
- If any one is cloud → Step 5 shown with relevant key sections visible

### 1.4 Download Pattern (`BeforeLeaveStep`)

Three wizard pages can trigger downloads:

| Page | Download | Size | Trigger |
|------|----------|------|---------|
| `WizardSttPage` (Step 2) | Whisper GGML model | ~466MB (small) | Next click when "Local" selected |
| `WizardLlmPage` (Step 3) | Ollama model pull | ~3.3GB (gemma3:4b) | Next click when "Local" selected |
| `WizardTtsPage` (Step 4) | Kokoro ONNX model | ~88MB (int8) | Next click when "Local" selected |

All follow the same pattern:
1. Radio selection shows preview ("Model will be downloaded when you click Next")
2. Next click triggers `BeforeLeaveStep` callback
3. Next button disabled during download, ProgressBar + status text shown
4. On completion → returns `false`, shows "Ready" status, user clicks Next again
5. Second Next → model check passes → `return true` → advances
6. Back button or radio switch → `CancellationToken` cancels download
7. Failure → error shown, Next re-enabled, user can switch to alternative

---

## 2. Path Matrix (14 valid paths)

### 2.1 Shortcut Paths (exit at Step 1)

| # | Path | Description | Steps | Downloads | Key Settings Written |
|---|------|-------------|-------|-----------|---------------------|
| **W1** | Wallet | OAuth login | 0→1→exit | None | `WizardCompleted=true`, browser opens for login, `AuthMode` set by callback |
| **L1** | Local | Fully offline | 0→1→exit | Deferred to LoadingViewModel | `WizardCompleted=true`, `ActiveProfileName="Local"`, all ModeProfiles `SttProvider="whisper"` + `LlmProvider="ollama"`, `Tts.Enabled=true` + `Tts.Provider="kokoro"` |

**L1 Loading Screen** (deferred downloads):
1. "Downloading Whisper model..." (~466MB with progress)
2. "Checking local services..." (Ollama health check)
3. "Warming up Ollama model..." (VRAM load)
4. Kokoro model downloads lazily on first TTS call (not during loading)

### 2.2 BYOK Paths (full wizard, Steps 0-7)

| # | STT | LLM | TTS | API Keys Step | Downloads in Wizard | Key Settings |
|---|-----|-----|-----|---------------|---------------------|-------------|
| **A1** | Cloud | Cloud | Off | Show (STT+LLM keys) | None | `ActiveProfile="Cloud"`, `Tts.Enabled=false` |
| **A2** | Cloud | Cloud | Local | Show (STT+LLM keys) | Kokoro 88MB | `ActiveProfile="Cloud"`, `Tts.Enabled=true`, `Tts.Provider="kokoro"` |
| **A3** | Cloud | Cloud | Cloud | Show (STT+LLM keys) | None | `ActiveProfile="Cloud"`, `Tts.Enabled=true`, `Tts.Provider="deepgram"` |
| **A4** | Cloud | Local | Off | Show (STT key only) | Ollama pull | `ActiveProfile="Local"`, `Tts.Enabled=false` |
| **A5** | Cloud | Local | Local | Show (STT key only) | Ollama pull + Kokoro 88MB | `ActiveProfile="Local"`, `Tts.Enabled=true`, `Tts.Provider="kokoro"` |
| **A6** | Cloud | Local | Cloud | Show (STT key — same as TTS) | Ollama pull | `ActiveProfile="Local"`, `Tts.Enabled=true`, `Tts.Provider="deepgram"` |
| **A7** | Local | Cloud | Off | Show (LLM key only) | Whisper 466MB | `ActiveProfile="Cloud"`, `Tts.Enabled=false` |
| **A8** | Local | Cloud | Local | Show (LLM key only) | Whisper 466MB + Kokoro 88MB | `ActiveProfile="Cloud"`, `Tts.Enabled=true`, `Tts.Provider="kokoro"` |
| **A9** | Local | Cloud | Cloud | Show (LLM+TTS share Deepgram key) | Whisper 466MB | `ActiveProfile="Cloud"`, `Tts.Enabled=true`, `Tts.Provider="deepgram"` |
| **A10** | Local | Local | Off | **Skipped** | Whisper 466MB + Ollama pull | `ActiveProfile="Local"`, `Tts.Enabled=false` |
| **A11** | Local | Local | Local | **Skipped** | Whisper 466MB + Ollama pull + Kokoro 88MB | `ActiveProfile="Local"`, `Tts.Enabled=true`, `Tts.Provider="kokoro"` |
| **A12** | Local | Local | Cloud | Show (TTS key only) | Whisper 466MB + Ollama pull | `ActiveProfile="Local"`, `Tts.Enabled=true`, `Tts.Provider="deepgram"` |

**Note on A12**: Edge case — both STT and LLM are local, but cloud TTS needs a Deepgram key. `NeedsApiKeys()` returns `true` because `TtsChoice == "cloud"`. API Keys page shows Deepgram key section for TTS.

**Note on A6/A9**: Cloud TTS uses Deepgram which shares the same key as Deepgram STT. When STT is cloud (A3, A6), the Deepgram key is already entered for STT — no duplicate entry needed. When STT is local but TTS is cloud (A9, A12), the Deepgram key section appears for TTS use.

---

## 3. Detailed Test Scenarios

### Scenario W1: Wallet Path

#### Setup
```
1. Close dIKta.me if running
2. Delete %APPDATA%\DiktaMe\settings.json
3. Delete %APPDATA%\DiktaMe\models\ (entire folder)
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| W1.1 | Launch app | Wizard appears, Step 0 (Language) | [ ] |
| W1.2 | Select English → Next | Step 1 (GetStarted) | [ ] |
| W1.3 | Select "Test with free Wallet credits" → Next | Browser opens for OAuth login | [ ] |
| W1.4 | Wizard window closes | LoadingWindow appears | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| W1.5 | `WizardCompleted` | `true` | [ ] |
| W1.6 | No STT/LLM/TTS settings changed | Defaults preserved | [ ] |

---

### Scenario L1: Local Shortcut Path (Fully Offline)

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Delete %APPDATA%\DiktaMe\models\ (entire folder)
4. Ensure Ollama is running with gemma3:4b pulled
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| L1.1 | Launch app | Wizard appears, Step 0 (Language) | [ ] |
| L1.2 | Select English → Next | Step 1 (GetStarted) | [ ] |
| L1.3 | Select "Local" → Next | Wizard completes, LoadingWindow appears | [ ] |

#### Loading Screen

| # | Expected | Pass? |
|---|----------|-------|
| L1.4 | "Downloading Whisper model..." with progress | [ ] |
| L1.5 | Whisper download completes (~466MB) | [ ] |
| L1.6 | "Checking local services..." | [ ] |
| L1.7 | "Warming up Ollama model..." | [ ] |
| L1.8 | Loading completes, main window opens | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| L1.9 | `ActiveProfileName` | `"Local"` | [ ] |
| L1.10 | `ModeProfiles.dictate_0.SttProvider` | `"whisper"` | [ ] |
| L1.11 | `ModeProfiles.dictate_0.LlmProvider` | `"ollama"` | [ ] |
| L1.12 | `Tts.Enabled` | `true` | [ ] |
| L1.13 | `Tts.Provider` | `"kokoro"` | [ ] |

#### Functional Tests

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| L1.14 | Ctrl+Alt+D → speak → wait | Whisper STT + Ollama LLM → text injected | [ ] |
| L1.15 | Control Panel TTS toggle state | Shows "Local" (Kokoro enabled) | [ ] |
| L1.16 | Ctrl+Alt+A → ask a question | Answer displayed + spoken via Kokoro (if SpeakAskResponses=true) | [ ] |

---

### Scenario A1: BYOK — Cloud/Cloud/Off (Baseline)

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Have Deepgram + Gemini API keys ready
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A1.1 | Launch → Language → English → Next | Step 1 (GetStarted) | [ ] |
| A1.2 | Select "I have API Keys" → Next | Step 2 (STT) | [ ] |
| A1.3 | Select "Cloud (Deepgram)" → Next | Step 3 (LLM) | [ ] |
| A1.4 | Select "Cloud (Gemini)" → Next | Step 4 (TTS) | [ ] |
| A1.5 | Select "Off" → Next | Step 5 (API Keys) — both STT+LLM sections visible | [ ] |
| A1.6 | Enter Deepgram + Gemini keys → Next | Step 6 (Test) | [ ] |
| A1.7 | Record test audio → Next | Step 7 (Ready) | [ ] |
| A1.8 | Click Finish | Wizard completes, LoadingWindow appears | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A1.9 | `ActiveProfileName` | `"Cloud"` | [ ] |
| A1.10 | `ModeProfiles.dictate_0.SttProvider` | `"deepgram"` | [ ] |
| A1.11 | `ModeProfiles.dictate_0.LlmProvider` | `"gemini"` | [ ] |
| A1.12 | `Tts.Enabled` | `false` | [ ] |

#### Functional Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A1.13 | Ctrl+Alt+D → speak | Deepgram STT + Gemini LLM → text injected, no TTS | [ ] |

---

### Scenario A2: BYOK — Cloud/Cloud/Local (Kokoro)

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Delete %APPDATA%\DiktaMe\models\tts\ (Kokoro models)
4. Have Deepgram + Gemini API keys ready
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A2.1 | Language → BYOK → Cloud STT → Cloud LLM → Next | Step 4 (TTS) | [ ] |
| A2.2 | Select "Local (Kokoro)" | Download panel: "Model will be downloaded when you click Next" (~88MB) | [ ] |
| A2.3 | Click Next | Download starts, Next disabled, progress bar fills | [ ] |
| A2.4 | Download completes | Status: "Kokoro model ready" | [ ] |
| A2.5 | Click Next again | Step 5 (API Keys) — STT+LLM key sections visible | [ ] |
| A2.6 | Enter keys → Test → Finish | Wizard completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A2.7 | `Tts.Enabled` | `true` | [ ] |
| A2.8 | `Tts.Provider` | `"kokoro"` | [ ] |
| A2.9 | Kokoro model file | `%APPDATA%\DiktaMe\models\tts\kokoro-quant-convinteger.onnx` (~88MB) | [ ] |

#### Functional Test

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A2.10 | Control Panel TTS state | "Local" (Kokoro) | [ ] |

---

### Scenario A10: BYOK — Local/Local/Off (API Keys Skipped)

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Delete %APPDATA%\DiktaMe\models\ (entire folder)
4. Ollama running with gemma3:4b pulled
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A10.1 | Language → BYOK → Local STT (Whisper download ~466MB) | Whisper downloads with progress | [ ] |
| A10.2 | Next → Local LLM (Ollama check + pull if needed) | Ollama ready / pulls model | [ ] |
| A10.3 | Next → TTS: Select "Off" → Next | **Step 5 (API Keys) SKIPPED** — jumps to Step 6 (Test) | [ ] |
| A10.4 | Test → Ready → Finish | Wizard completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A10.5 | `ActiveProfileName` | `"Local"` | [ ] |
| A10.6 | `Tts.Enabled` | `false` | [ ] |
| A10.7 | API Keys step was skipped | No key prompts shown | [ ] |

---

### Scenario A11: BYOK — Local/Local/Local (Fully Offline via BYOK)

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Delete %APPDATA%\DiktaMe\models\ (entire folder)
4. Ollama running with gemma3:4b pulled
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A11.1 | Language → BYOK → Local STT | Whisper downloads | [ ] |
| A11.2 | Next → Local LLM | Ollama check passes | [ ] |
| A11.3 | Next → TTS: Select "Local (Kokoro)" → Next | Kokoro downloads (~88MB) with progress | [ ] |
| A11.4 | Download completes → Next | **Step 5 (API Keys) SKIPPED** — jumps to Step 6 (Test) | [ ] |
| A11.5 | Test → Ready → Finish | Wizard completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A11.6 | `ActiveProfileName` | `"Local"` | [ ] |
| A11.7 | All ModeProfiles `SttProvider` | `"whisper"` | [ ] |
| A11.8 | All ModeProfiles `LlmProvider` | `"ollama"` | [ ] |
| A11.9 | `Tts.Enabled` | `true` | [ ] |
| A11.10 | `Tts.Provider` | `"kokoro"` | [ ] |
| A11.11 | Whisper model | `ggml-small.bin` exists | [ ] |
| A11.12 | Kokoro model | `kokoro-quant-convinteger.onnx` exists in `models\tts\` | [ ] |

---

### Scenario A12: BYOK — Local/Local/Cloud (Edge Case: API Keys for TTS only)

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Ollama running with gemma3:4b pulled
4. Have a valid Deepgram API key ready
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A12.1 | Language → BYOK → Local STT (Whisper download) | Whisper downloads | [ ] |
| A12.2 | Next → Local LLM (Ollama check) | Ollama ready | [ ] |
| A12.3 | Next → TTS: Select "Cloud (Deepgram)" → Next | Step 5 (API Keys) — **NOT skipped** | [ ] |
| A12.4 | API Keys page shows Deepgram key section | STT key section hidden (STT is local), Deepgram shown for TTS | [ ] |
| A12.5 | Enter Deepgram key → Next → Test → Finish | Wizard completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A12.6 | `ActiveProfileName` | `"Local"` (driven by LLM choice) | [ ] |
| A12.7 | `Tts.Enabled` | `true` | [ ] |
| A12.8 | `Tts.Provider` | `"deepgram"` | [ ] |
| A12.9 | Deepgram API key stored | In SecureStorage (keys.dat) | [ ] |

**Note**: This is the key edge case for `NeedsApiKeys()`. Previously, local STT + local LLM would skip API Keys entirely. With cloud TTS, the step must show.

---

### Scenario A7: BYOK — Local STT / Cloud LLM / Off TTS

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Have a valid Gemini API key ready
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A7.1 | Language → BYOK → Local STT (Whisper download ~466MB) | Whisper downloads | [ ] |
| A7.2 | Next → Cloud LLM (Gemini) → Next | Step 4 (TTS) | [ ] |
| A7.3 | Select "Off" → Next | Step 5 (API Keys) — LLM key section visible | [ ] |
| A7.4 | Enter Gemini key → Next → Test → Finish | Wizard completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A7.5 | `ActiveProfileName` | `"Cloud"` (LLM is cloud) | [ ] |
| A7.6 | `ModeProfiles.dictate_0.SttProvider` | `"whisper"` | [ ] |
| A7.7 | `ModeProfiles.dictate_0.LlmProvider` | `"gemini"` | [ ] |
| A7.8 | `Tts.Enabled` | `false` | [ ] |

---

### Scenario A4: BYOK — Cloud STT / Local LLM / Off TTS

#### Setup
```
1. Close dIKta.me
2. Delete %APPDATA%\DiktaMe\settings.json
3. Ollama running with gemma3:4b pulled
4. Have a valid Deepgram API key ready
```

#### Wizard Flow

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| A4.1 | Language → BYOK → Cloud STT → Next | Step 3 (LLM) | [ ] |
| A4.2 | Local LLM (Ollama check + pull) → Next | Step 4 (TTS) | [ ] |
| A4.3 | Select "Off" → Next | Step 5 (API Keys) — STT key section visible | [ ] |
| A4.4 | Enter Deepgram key → Next → Test → Finish | Wizard completes | [ ] |

#### Settings Verification

| # | Check | Expected | Pass? |
|---|-------|----------|-------|
| A4.5 | `ActiveProfileName` | `"Local"` (LLM is local) | [ ] |
| A4.6 | `ModeProfiles.dictate_0.SttProvider` | `"deepgram"` | [ ] |
| A4.7 | `ModeProfiles.dictate_0.LlmProvider` | `"ollama"` | [ ] |
| A4.8 | `Tts.Enabled` | `false` | [ ] |

---

## 4. Edge Cases

### 4.1 Download Cancellation

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| E1 | Start Whisper download → click Back mid-download | Download cancels, returns to previous step, partial file cleaned up | [ ] |
| E2 | Start Ollama pull → click Back mid-pull | Pull cancels, returns to STT step | [ ] |
| E3 | Start Kokoro download → click Back mid-download | Download cancels, returns to LLM step, partial file cleaned up | [ ] |
| E4 | Start Kokoro download → switch radio to "Off" | Download cancels, download panel hides | [ ] |
| E5 | Start Kokoro download → switch radio to "Cloud" | Download cancels, panel shows cloud info | [ ] |

### 4.2 Wizard Interruption

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| E6 | Close wizard window mid-download (Whisper) | App exits cleanly, partial file cleaned up | [ ] |
| E7 | Close wizard window mid-download (Kokoro) | App exits cleanly, partial file cleaned up | [ ] |
| E8 | Close wizard at Step 4 (TTS) | Next launch: wizard resumes (WizardCompleted=false) | [ ] |
| E9 | Kill app process during Ollama pull | Next launch: wizard resumes, Ollama pull restarts | [ ] |

### 4.3 Second Launch (Models Cached)

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| E10 | Complete L1 (Local) → close → relaunch | No wizard (WizardCompleted=true), no downloads, Ollama warmup only | [ ] |
| E11 | Complete A11 (all local BYOK) → close → relaunch | No wizard, no downloads, Ollama warmup, Kokoro ready for first TTS call | [ ] |
| E12 | Complete A2 (cloud+Kokoro) → close → relaunch | No wizard, no downloads, no Ollama, Kokoro ready | [ ] |

### 4.4 Control Panel Override After Wizard

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| E13 | Complete A1 (TTS=Off) → cycle TTS toggle to Local | TTS enabled, Kokoro downloads on first TTS call | [ ] |
| E14 | Complete A11 (TTS=Local) → cycle TTS toggle to Off | TTS disabled, Kokoro model stays on disk | [ ] |
| E15 | Complete A11 (TTS=Local) → cycle to Cloud → cycle to Off | TTS cycling works correctly, settings persisted on each change | [ ] |

### 4.5 Ollama Not Installed (TTS-specific)

| # | Action | Expected | Pass? |
|---|--------|----------|-------|
| E16 | Ollama not installed → BYOK → Cloud STT → Cloud LLM → Local TTS | TTS download works independently of Ollama state | [ ] |
| E17 | Ollama not installed → BYOK → Local STT → pick Local LLM (blocked) → switch to Cloud → Local TTS | TTS step works after LLM fallback to cloud | [ ] |

---

## 5. API Keys Step Visibility Matrix

The API Keys page (Step 5) shows/hides sections based on which providers need keys:

| STT | LLM | TTS | API Keys Step | Deepgram Section | Gemini Section |
|-----|-----|-----|---------------|------------------|----------------|
| Cloud | Cloud | Off | Show | STT key | LLM key |
| Cloud | Cloud | Local | Show | STT key | LLM key |
| Cloud | Cloud | Cloud | Show | STT+TTS key (same) | LLM key |
| Cloud | Local | Off | Show | STT key | Hidden |
| Cloud | Local | Local | Show | STT key | Hidden |
| Cloud | Local | Cloud | Show | STT+TTS key (same) | Hidden |
| Local | Cloud | Off | Show | Hidden | LLM key |
| Local | Cloud | Local | Show | Hidden | LLM key |
| Local | Cloud | Cloud | Show | TTS key | LLM key |
| Local | Local | Off | **Skip** | — | — |
| Local | Local | Local | **Skip** | — | — |
| Local | Local | Cloud | Show | TTS key | Hidden |

**Key insight**: Deepgram key is needed for either STT (Deepgram) or TTS (Deepgram) — same key covers both. The section label may need to say "Deepgram (STT + TTS)" when both use Deepgram, or just "Deepgram" when only one does.

---

## 6. Cross-References

| Document | Section | Relationship |
|----------|---------|-------------|
| `SPEC_009_FIXES.md` | FIX-17 | Implementation spec for the TTS wizard step |
| `SPEC_009_TESTING.md` | Scenarios 1-8 | Pre-TTS test scenarios — need TTS additions |
| `LIVE_TESTING_PLAN.md` | Section 1 | First-Run testing — should reference this matrix |
| `MANUAL_TEST_PLAN.md` | Section 1 | Checklist version — should align with this matrix |
| `SPEC_009_LOCALFLOW.md` | Phase E | Integration verification — needs TTS scenarios |

---

## 7. Run Log

### Run 1 — Date: ___________

| Scenario | Result | Notes |
|----------|--------|-------|
| W1: Wallet | | |
| L1: Local (fully offline) | | |
| A1: Cloud/Cloud/Off | | |
| A2: Cloud/Cloud/Local TTS | | |
| A10: Local/Local/Off (skip API Keys) | | |
| A11: Local/Local/Local (fully offline BYOK) | | |
| A12: Local/Local/Cloud TTS (API Keys for TTS only) | | |
| A4: Cloud/Local/Off | | |
| A7: Local/Cloud/Off | | |
| Edge cases E1-E17 | | |

**Priority order**: L1 → A1 → A11 → A12 → A10 → A2 → remaining

**Blockers found**:
-

**Fixes applied**:
-
