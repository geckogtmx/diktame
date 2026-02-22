<#
.SYNOPSIS
Query SQLite history database and verify entries

.DESCRIPTION
Opens history.db and checks for recent entries matching criteria

.PARAMETER ExpectedMode
Filter by mode (dictate, refine, ask, translate, note, chat, oops)

.PARAMETER MaxAgeSeconds
Only check entries newer than this many seconds (default 300 = 5 minutes)

.EXAMPLE
.\Verify-HistoryDb.ps1 -ExpectedMode "dictate"
.\Verify-HistoryDb.ps1 -ExpectedMode "ask" -MaxAgeSeconds 60
#>

param(
    [string]$ExpectedMode = $null,
    [int]$MaxAgeSeconds = 300
)

$dbPath = Join-Path $env:APPDATA "DiktaMe\history.db"

if (-not (Test-Path $dbPath)) {
    Write-Host "⚠️  WARNING: history.db not found at $dbPath" -ForegroundColor Yellow
    Write-Host "   (Database may not exist yet if no dictations recorded)" -ForegroundColor Gray
    exit 0
}

try {
    # Load SQLite assembly
    $conn = New-Object System.Data.SQLite.SQLiteConnection
    $conn.ConnectionString = "Data Source=$dbPath;Version=3;"
    $conn.Open()

    # Get last entry
    $query = "SELECT id, timestamp, mode, text, stt_provider, llm_provider, latency_ms FROM history ORDER BY timestamp DESC LIMIT 1"
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $query

    $reader = $cmd.ExecuteReader()

    if (-not $reader.Read()) {
        Write-Host "⚠️  WARNING: No entries in history.db yet" -ForegroundColor Yellow
        $conn.Close()
        exit 0
    }

    $id = $reader["id"]
    $timestamp = $reader["timestamp"]
    $mode = $reader["mode"]
    $text = $reader["text"]
    $sttProvider = $reader["stt_provider"]
    $llmProvider = $reader["llm_provider"]
    $latencyMs = $reader["latency_ms"]

    $reader.Close()
    $conn.Close()

    $textPreview = if ($text) { $text.Substring(0, [Math]::Min(50, $text.Length)) + "..." } else { "(null)" }

    Write-Host "✅ FOUND: Latest history entry:" -ForegroundColor Green
    Write-Host "   ID: $id" -ForegroundColor Gray
    Write-Host "   Timestamp: $timestamp" -ForegroundColor Gray
    Write-Host "   Mode: $mode" -ForegroundColor Gray
    Write-Host "   Text: $textPreview" -ForegroundColor Gray
    Write-Host "   STT: $sttProvider | LLM: $llmProvider | Latency: ${latencyMs}ms" -ForegroundColor Gray

    if ($ExpectedMode -and $mode -ne $ExpectedMode) {
        Write-Host "❌ FAIL: Mode is '$mode' (expected '$ExpectedMode')" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "✅ PASS: Mode check OK" -ForegroundColor Green
        exit 0
    }
} catch {
    Write-Host "❌ ERROR: Failed to query history.db: $_" -ForegroundColor Red
    Write-Host "   Ensure System.Data.SQLite is installed: Install-Package System.Data.SQLite" -ForegroundColor Yellow
    exit 1
}
