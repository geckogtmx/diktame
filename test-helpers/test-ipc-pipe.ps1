# test-ipc-pipe.ps1 — Manual E2E test for LocalApiServer named pipe IPC
# Usage: powershell -ExecutionPolicy Bypass -File test-helpers\test-ipc-pipe.ps1
# Prerequisites: DiktaMe app must be running.

param(
    [string]$PipeName = "DiktaMe.V2.Api",
    [int]$TimeoutMs = 5000
)

$ErrorActionPreference = "Stop"

Write-Host "Connecting to pipe '$PipeName'..." -ForegroundColor Cyan

$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $PipeName, [System.IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect($TimeoutMs)
} catch {
    Write-Host "ERROR: Could not connect to pipe. Is DiktaMe running?" -ForegroundColor Red
    exit 1
}

Write-Host "Connected!" -ForegroundColor Green

$writer = New-Object System.IO.StreamWriter($pipe)
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($pipe)

function Read-Event {
    $line = $reader.ReadLine()
    if ($line) {
        Write-Host "<< $line" -ForegroundColor Yellow
    }
    return $line
}

function Send-Command {
    param([string]$Json)
    Write-Host ">> $Json" -ForegroundColor Green
    $writer.WriteLine($Json)
}

# ── Read initial snapshot (state, settings, modes) ───────────────────────
Write-Host "`n=== Initial Snapshot ===" -ForegroundColor Cyan
for ($i = 0; $i -lt 3; $i++) {
    Read-Event | Out-Null
}

# ── Query modes ──────────────────────────────────────────────────────────
Write-Host "`n=== Query: Modes ===" -ForegroundColor Cyan
Send-Command '{"action":"query","target":"modes"}'
Read-Event | Out-Null

# ── Query settings ───────────────────────────────────────────────────────
Write-Host "`n=== Query: Settings ===" -ForegroundColor Cyan
Send-Command '{"action":"query","target":"settings"}'
Read-Event | Out-Null

# ── Toggle RawModeOverride ───────────────────────────────────────────────
Write-Host "`n=== Toggle: RawModeOverride ===" -ForegroundColor Cyan
Send-Command '{"action":"toggle","setting":"RawModeOverride"}'
Start-Sleep -Milliseconds 500
Read-Event | Out-Null  # settings event broadcast

# ── Toggle it back ───────────────────────────────────────────────────────
Write-Host "`n=== Toggle: RawModeOverride (restore) ===" -ForegroundColor Cyan
Send-Command '{"action":"toggle","setting":"RawModeOverride"}'
Start-Sleep -Milliseconds 500
Read-Event | Out-Null

# ── Interactive mode: listen for events ──────────────────────────────────
Write-Host "`n=== Listening for events (press Ctrl+C to exit) ===" -ForegroundColor Cyan
Write-Host "Try pressing your dictation hotkey or sending:" -ForegroundColor DarkGray
Write-Host '  {"action":"trigger","pipeline":"dictate"}' -ForegroundColor DarkGray

try {
    while ($pipe.IsConnected) {
        $line = $reader.ReadLine()
        if ($line) {
            $timestamp = Get-Date -Format "HH:mm:ss.fff"
            Write-Host "[$timestamp] << $line" -ForegroundColor Yellow
        } else {
            Write-Host "Pipe disconnected." -ForegroundColor Red
            break
        }
    }
} catch {
    Write-Host "`nDisconnected." -ForegroundColor DarkGray
} finally {
    $pipe.Dispose()
}
