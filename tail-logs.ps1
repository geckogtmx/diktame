# Tail the dIKta.me log file in real-time
# Run this AFTER launching the app to see logs as they're written

$logDir = "$env:APPDATA\DiktaMe\logs"
$today = Get-Date -Format "yyyyMMdd"
$logFile = "$logDir\diktame_$today.log"

Write-Host "Watching log file: $logFile" -ForegroundColor Cyan
Write-Host "Launch the app now, and logs will appear below..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop watching" -ForegroundColor Gray
Write-Host ""

# Wait for log file to exist
while (-not (Test-Path $logFile)) {
    Start-Sleep -Milliseconds 500
    Write-Host "." -NoNewline
}

Write-Host ""
Write-Host "Log file found! Streaming..." -ForegroundColor Green
Write-Host ""

# Tail the log file (like Unix tail -f)
Get-Content $logFile -Wait -Tail 0
