# Diagnose which application has registered the hotkeys
# Win32 error 1408 = ERROR_HOTKEY_ALREADY_REGISTERED

Write-Host "Checking for applications that might be using hotkeys..." -ForegroundColor Cyan
Write-Host ""

# Check for common hotkey-using apps
$suspects = @(
    "diktate",      # V1 Python version
    "electron",     # Electron apps (V1 used Electron)
    "autohotkey",   # AutoHotkey scripts
    "powertoys",    # PowerToys keyboard manager
    "sharex",       # ShareX screenshot tool
    "greenshot",    # Greenshot screenshot tool
    "clover",       # Clover keyboard shortcuts
    "keypirinha",   # Keypirinha launcher
    "wox",          # Wox launcher
    "everything"    # Everything search
)

Write-Host "Looking for processes that commonly use global hotkeys:" -ForegroundColor Yellow
foreach ($name in $suspects) {
    $processes = Get-Process -Name $name -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "  FOUND: " -NoNewline -ForegroundColor Red
        Write-Host "$name (PID: $($processes.Id -join ', '))" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "All running processes with windows (potential hotkey users):" -ForegroundColor Yellow
Get-Process | Where-Object { $_.MainWindowTitle -ne "" } |
    Select-Object ProcessName, Id, MainWindowTitle |
    Format-Table -AutoSize

Write-Host ""
Write-Host "To fix error 1408, try:" -ForegroundColor Green
Write-Host "  1. Close any application from the list above" -ForegroundColor White
Write-Host "  2. Restart dIKta.me V2" -ForegroundColor White
Write-Host "  3. If still failing, change hotkeys in Settings to different combinations" -ForegroundColor White
Write-Host ""
Write-Host "Current hotkeys (from default settings):" -ForegroundColor Cyan
Write-Host "  Dictate:   Ctrl+Alt+D" -ForegroundColor Gray
Write-Host "  Refine:    Ctrl+Alt+R" -ForegroundColor Gray
Write-Host "  Ask:       Ctrl+Alt+A" -ForegroundColor Gray
Write-Host "  Translate: Ctrl+Alt+T" -ForegroundColor Gray
Write-Host "  Oops:      Ctrl+Alt+V" -ForegroundColor Gray
Write-Host "  Note:      Ctrl+Alt+N" -ForegroundColor Gray
Write-Host "  Chat:      Ctrl+Alt+C" -ForegroundColor Gray
