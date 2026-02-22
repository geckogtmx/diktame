<#
.SYNOPSIS
Dump full application state for debugging

.DESCRIPTION
Collects and displays all relevant app state information for troubleshooting

.EXAMPLE
.\Get-AppState.ps1
#>

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║             dIKta.me V2 - Application State Dump              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

$appDataPath = Join-Path $env:APPDATA "DiktaMe"

# 1. Directory Check
Write-Host "`n[1] File System State" -ForegroundColor Yellow
Write-Host "────────────────────────────────────────────────────────────────"

$directories = @(
    @{ Name = "AppData"; Path = $appDataPath }
    @{ Name = "Settings"; Path = (Join-Path $appDataPath "settings.json") }
    @{ Name = "History DB"; Path = (Join-Path $appDataPath "history.db") }
    @{ Name = "Secure Keys"; Path = (Join-Path $appDataPath "keys.dat") }
    @{ Name = "Snippets"; Path = (Join-Path $appDataPath "snippets.json") }
    @{ Name = "Logs"; Path = (Join-Path $appDataPath "logs") }
    @{ Name = "Notes File"; Path = (Join-Path $env:USERPROFILE "Documents\diktame-notes.md") }
)

foreach ($item in $directories) {
    $exists = Test-Path $item.Path
    $status = if ($exists) { "✅" } else { "❌" }
    Write-Host "$status $($item.Name): $($item.Path)" -ForegroundColor Gray

    if ($exists -and (Get-Item $item.Path).PSIsContainer -eq $false) {
        $file = Get-Item $item.Path
        Write-Host "   Size: $($file.Length) bytes, Modified: $($file.LastWriteTime)" -ForegroundColor DarkGray
    }
}

# 2. Settings Check
Write-Host "`n[2] Application Settings" -ForegroundColor Yellow
Write-Host "────────────────────────────────────────────────────────────────"

$settingsPath = Join-Path $appDataPath "settings.json"
if (Test-Path $settingsPath) {
    try {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        Write-Host "✅ Settings loaded successfully" -ForegroundColor Green

        Write-Host "  Wizard Completed: $($settings.WizardCompleted)" -ForegroundColor Gray
        Write-Host "  Language: $($settings.GeneralSettings.Language)" -ForegroundColor Gray
        Write-Host "  Active Profile: $($settings.ActiveProfile)" -ForegroundColor Gray
        Write-Host "  Privacy Level: $($settings.PrivacySettings.Level)" -ForegroundColor Gray

        if ($settings.SttSettings) {
            Write-Host "  STT Provider: $($settings.SttSettings.Provider)" -ForegroundColor Gray
        }
        if ($settings.LlmSettings) {
            Write-Host "  LLM Provider: $($settings.LlmSettings.Provider)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "❌ Failed to read settings: $_" -ForegroundColor Red
    }
} else {
    Write-Host "⚠️  Settings not found" -ForegroundColor Yellow
}

# 3. History Database
Write-Host "`n[3] History Database" -ForegroundColor Yellow
Write-Host "────────────────────────────────────────────────────────────────"

$dbPath = Join-Path $appDataPath "history.db"
if (Test-Path $dbPath) {
    try {
        $conn = New-Object System.Data.SQLite.SQLiteConnection
        $conn.ConnectionString = "Data Source=$dbPath;Version=3;"
        $conn.Open()

        # Count entries
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) as count FROM history"
        $count = $cmd.ExecuteScalar()

        Write-Host "✅ Database OK" -ForegroundColor Green
        Write-Host "   Total entries: $count" -ForegroundColor Gray

        # Get latest entry
        $cmd.CommandText = "SELECT timestamp, mode FROM history ORDER BY timestamp DESC LIMIT 1"
        $reader = $cmd.ExecuteReader()
        if ($reader.Read()) {
            Write-Host "   Latest: $($reader['mode']) @ $($reader['timestamp'])" -ForegroundColor Gray
        }
        $reader.Close()

        $conn.Close()
    } catch {
        Write-Host "⚠️  Could not read database: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  History database not found" -ForegroundColor Yellow
}

# 4. Logs
Write-Host "`n[4] Application Logs" -ForegroundColor Yellow
Write-Host "────────────────────────────────────────────────────────────────"

$logsPath = Join-Path $appDataPath "logs"
if (Test-Path $logsPath) {
    $logFiles = Get-ChildItem $logsPath -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 3
    if ($logFiles) {
        Write-Host "✅ Logs found" -ForegroundColor Green
        foreach ($log in $logFiles) {
            Write-Host "   $($log.Name) - $($log.Length) bytes" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠️  No log files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  Logs directory not found" -ForegroundColor Yellow
}

# 5. Ollama Health
Write-Host "`n[5] Ollama Status" -ForegroundColor Yellow
Write-Host "────────────────────────────────────────────────────────────────"

try {
    $response = Invoke-WebRequest -Uri "http://localhost:11434/api/version" -TimeoutSec 2 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        $version = $response.Content | ConvertFrom-Json
        Write-Host "✅ Ollama is running" -ForegroundColor Green
        Write-Host "   Version: $($version.version)" -ForegroundColor Gray
    }
} catch {
    Write-Host "⚠️  Ollama not responding (offline or not running)" -ForegroundColor Yellow
}

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                      End of State Dump                         ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
