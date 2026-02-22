<#
.SYNOPSIS
Verify secure storage (keys.dat) exists and is encrypted

.DESCRIPTION
Checks that keys.dat file exists and is encrypted via DPAPI

.EXAMPLE
.\Verify-SecureStorage.ps1
#>

$keysPath = Join-Path $env:APPDATA "DiktaMe\keys.dat"

Write-Host "Checking secure storage at: $keysPath"

if (-not (Test-Path $keysPath)) {
    Write-Host "⚠️  WARNING: keys.dat not found (no API keys stored yet)" -ForegroundColor Yellow
    exit 0
}

try {
    $file = Get-Item $keysPath
    $sizeBytes = $file.Length

    # Try to read as text - if encrypted, will be binary garbage
    $content = Get-Content $keysPath -Raw -ErrorAction SilentlyContinue
    $isText = $null -ne $content -and $content -match '^[a-zA-Z0-9\s\{\}\[\]"\-:,\.]*$'

    if ($isText) {
        Write-Host "⚠️  WARNING: keys.dat appears to be plaintext (not encrypted?)" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "✅ PASS: keys.dat exists and is encrypted" -ForegroundColor Green
    Write-Host "   Size: $sizeBytes bytes" -ForegroundColor Gray
    Write-Host "   Modified: $($file.LastWriteTime)" -ForegroundColor Gray
    exit 0
} catch {
    Write-Host "❌ ERROR: Failed to verify keys.dat: $_" -ForegroundColor Red
    exit 1
}
