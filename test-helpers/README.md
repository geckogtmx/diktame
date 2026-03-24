# Test Helpers — dIKta.me V2 Manual Testing

PowerShell scripts for validating application state during manual testing.

## Quick Start

```powershell
# Initialize a new testing session
.\New-TestSession.ps1

# Verify a setting
.\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"

# Check history database
.\Verify-HistoryDb.ps1

# Dump full app state
.\Get-AppState.ps1

# Check Ollama health
.\Test-OllamaHealth.ps1
```

## Scripts

### Core Validation (8 scripts)

#### 1. **Verify-AppSettings.ps1** — Read and validate settings.json
```powershell
.\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"
.\Verify-AppSettings.ps1 -SettingPath "GeneralSettings.Language" -ExpectedValue "en"
```

#### 2. **Verify-HistoryDb.ps1** — Query history.db for recent entries
```powershell
.\Verify-HistoryDb.ps1                                    # Show latest entry
.\Verify-HistoryDb.ps1 -ExpectedMode "dictate"          # Filter by mode
.\Verify-HistoryDb.ps1 -ExpectedMode "ask" -MaxAgeSeconds 60  # Last 60 seconds
```

#### 3. **Verify-SecureStorage.ps1** — Check keys.dat encryption
```powershell
.\Verify-SecureStorage.ps1  # Verify keys are encrypted via DPAPI
```

#### 4. **Verify-AutoStart.ps1** — Check Task Scheduler auto-start task
```powershell
.\Verify-AutoStart.ps1  # Verify dIKta.me task exists and is enabled
```

#### 5. **Verify-FileSystem.ps1** — Check file/directory existence
```powershell
.\Verify-FileSystem.ps1 -Path "%APPDATA%\DiktaMe\settings.json" -Type File
.\Verify-FileSystem.ps1 -Path "%APPDATA%\DiktaMe\logs" -Type Directory
```

#### 6. **Verify-Macros.ps1** — Validate Macros.json
```powershell
.\Verify-Macros.ps1  # Check Macro count and structure
```

#### 7. **Test-OllamaHealth.ps1** — Check Ollama connectivity and models
```powershell
.\Test-OllamaHealth.ps1
.\Test-OllamaHealth.ps1 -Endpoint "http://localhost:11434"
```

#### 8. **Get-AppState.ps1** — Full application state dump
```powershell
.\Get-AppState.ps1  # Shows all files, settings, history, logs, Ollama status
```

### Session Management (1 script)

#### 9. **New-TestSession.ps1** — Create test session log
```powershell
.\New-TestSession.ps1  # Creates test-session-log.md with timestamp
```

### Audio Feeder (3 scripts — to be implemented)

#### 10. **Invoke-AudioFeeder.ps1** — Automated voice testing
- Port of V1's `audio_feeder.py`
- Downloads YouTube videos, plays audio, controls app via IPC
- Smart mode (subtitle-based phrases) or dumb mode (fixed chunks)
- Tracks transcription accuracy and latency

#### 11. **Download-TestAudio.ps1** — YouTube downloader wrapper
```powershell
.\Download-TestAudio.ps1 -Url "https://youtube.com/watch?v=..." -OutputDir "tests/fixtures/downloads"
```

#### 12. **Start-IpcServer.ps1** — TCP server for test automation
- Listens on `127.0.0.1:5005`
- Commands: `START`, `STOP`, `STATUS`, `PING`
- Required for audio feeder automation

## Exit Codes

All scripts return:
- **0** = Success/PASS
- **1** = Failure/FAIL

This allows chaining scripts together in batch files or CI/CD pipelines.

## Requirements

- **PowerShell 5.1+** (Windows 10/11)
- **System.Data.SQLite** (for HistoryDb script)
  ```powershell
  # Install if needed:
  Install-Package System.Data.SQLite
  ```

## Usage in Test Plan

The `MANUAL_TEST_PLAN.md` contains references to these scripts:

```markdown
- [ ] **1.15** Verify settings.json → Helper: `.\Verify-AppSettings.ps1 ...`
- [ ] **2A.3** Same test with Deepgram → Verify in history.db → Helper: `.\Verify-HistoryDb.ps1`
```

## Example Test Workflow

```powershell
# 1. Start a new session
.\New-TestSession.ps1

# 2. Run app (manually or via script)
Start-Process "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\DiktaMe.exe"

# 3. Test dictation (manually in Notepad)
# ... perform manual test steps ...

# 4. Verify results
.\Verify-HistoryDb.ps1 -ExpectedMode "dictate"
.\Verify-AppSettings.ps1 -SettingPath "ActiveProfile" -ExpectedValue "cloud"

# 5. Check app state for debugging
.\Get-AppState.ps1

# 6. Continue with next test section
```

## Notes

- All scripts operate **read-only** (no modifications to app state)
- Scripts output colored text (✅ PASS, ❌ FAIL, ⚠️ WARNING)
- Environment variables like `%APPDATA%` are automatically expanded
- SQLite queries use `System.Data.SQLite` .NET provider

## Troubleshooting

**"System.Data.SQLite not found"**
```powershell
Install-Package System.Data.SQLite
```

**"Ollama not responding"**
```powershell
# Make sure Ollama is running:
ollama serve
```

**"Task Scheduler task not found"**
- Auto-start task is only created after enabling in Settings
- Run the app and go to Settings → General → Enable auto-start

## Contributing

When adding new validation scripts:
1. Use same naming convention: `Verify-*.ps1` or `Test-*.ps1`
2. Include help documentation with `.SYNOPSIS`, `.DESCRIPTION`, `.EXAMPLE`
3. Output colored status: `✅ PASS`, `❌ FAIL`, `⚠️ WARNING`
4. Return exit code 0 or 1
5. Add to this README

---

**Last Updated:** 2026-02-19
**Part of:** LIVE_TESTING_PLAN.md
