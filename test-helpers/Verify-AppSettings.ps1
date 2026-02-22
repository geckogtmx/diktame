<#
.SYNOPSIS
Verify app settings values in settings.json

.DESCRIPTION
Reads %APPDATA%\DiktaMe\settings.json and checks for expected values using dot notation path

.PARAMETER SettingPath
Dot notation path to setting (e.g., "GeneralSettings.Language" or "WizardCompleted")

.PARAMETER ExpectedValue
Expected value to compare against

.EXAMPLE
.\Verify-AppSettings.ps1 -SettingPath "WizardCompleted" -ExpectedValue "true"
.\Verify-AppSettings.ps1 -SettingPath "GeneralSettings.Language" -ExpectedValue "en"
#>

param(
    [string]$SettingPath = "WizardCompleted",
    [string]$ExpectedValue = $null
)

$settingsPath = Join-Path $env:APPDATA "DiktaMe\settings.json"

if (-not (Test-Path $settingsPath)) {
    Write-Host "❌ FAIL: settings.json not found at $settingsPath" -ForegroundColor Red
    exit 1
}

try {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

    # Navigate through dot notation path
    $value = $settings
    foreach ($key in $SettingPath.Split(".")) {
        if ($null -eq $value) {
            Write-Host "❌ FAIL: Path $SettingPath not found" -ForegroundColor Red
            exit 1
        }
        $value = $value.$key
    }

    if ($null -eq $ExpectedValue) {
        Write-Host "✅ PASS: $SettingPath = $value" -ForegroundColor Green
        exit 0
    }

    if ($value -eq $ExpectedValue) {
        Write-Host "✅ PASS: $SettingPath = $value (expected: $ExpectedValue)" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "❌ FAIL: $SettingPath = $value (expected: $ExpectedValue)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ ERROR: Failed to read settings: $_" -ForegroundColor Red
    exit 1
}
