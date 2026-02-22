<#
.SYNOPSIS
Verify voice snippets configuration

.DESCRIPTION
Checks snippets.json file structure and validates snippet count

.EXAMPLE
.\Verify-Snippets.ps1
#>

$snippetsPath = Join-Path $env:APPDATA "DiktaMe\snippets.json"

Write-Host "Checking snippets at: $snippetsPath"

if (-not (Test-Path $snippetsPath)) {
    Write-Host "⚠️  WARNING: snippets.json not found (no snippets stored yet)" -ForegroundColor Yellow
    exit 0
}

try {
    $content = Get-Content $snippetsPath -Raw
    $snippets = $content | ConvertFrom-Json

    if ($snippets -is [array]) {
        $count = $snippets.Count
    } else {
        $count = 1
    }

    Write-Host "✅ PASS: snippets.json is valid JSON" -ForegroundColor Green
    Write-Host "   Snippet count: $count / 100" -ForegroundColor Gray

    if ($count -gt 100) {
        Write-Host "⚠️  WARNING: Snippet count exceeds limit (100)" -ForegroundColor Yellow
        exit 1
    }

    if ($count -gt 0) {
        Write-Host "   Snippets:" -ForegroundColor Gray
        $snippets | ForEach-Object {
            Write-Host "     - '$($_.Trigger)' → '$($_.Content)'" -ForegroundColor Gray
        }
    }

    exit 0
} catch {
    Write-Host "❌ ERROR: Failed to parse snippets.json: $_" -ForegroundColor Red
    exit 1
}
