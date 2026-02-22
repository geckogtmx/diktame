<#
.SYNOPSIS
Test Ollama server health and connectivity

.DESCRIPTION
Checks if Ollama is running, gets version, and lists available models

.PARAMETER Endpoint
Ollama endpoint URL (default: http://localhost:11434)

.EXAMPLE
.\Test-OllamaHealth.ps1
.\Test-OllamaHealth.ps1 -Endpoint "http://localhost:11434"
#>

param(
    [string]$Endpoint = "http://localhost:11434"
)

Write-Host "Testing Ollama at: $Endpoint"

try {
    # Test connectivity
    Write-Host "  Checking connection..." -NoNewline
    $versionResponse = Invoke-WebRequest -Uri "$Endpoint/api/version" -TimeoutSec 5 -ErrorAction SilentlyContinue

    if ($versionResponse.StatusCode -eq 200) {
        $versionData = $versionResponse.Content | ConvertFrom-Json
        Write-Host " ✅ PASS" -ForegroundColor Green
        Write-Host "  Version: $($versionData.version)" -ForegroundColor Green
    } else {
        Write-Host " ❌ FAIL" -ForegroundColor Red
        Write-Host "  Status: $($versionResponse.StatusCode)" -ForegroundColor Red
        exit 1
    }

    # Get model list
    Write-Host "  Fetching model list..." -NoNewline
    $tagsResponse = Invoke-WebRequest -Uri "$Endpoint/api/tags" -TimeoutSec 5 -ErrorAction SilentlyContinue

    if ($tagsResponse.StatusCode -eq 200) {
        $tagsData = $tagsResponse.Content | ConvertFrom-Json
        Write-Host " ✅ PASS" -ForegroundColor Green
        Write-Host "  Models: $($tagsData.models.Count) installed" -ForegroundColor Green

        if ($tagsData.models) {
            Write-Host "  Installed models:" -ForegroundColor Gray
            $tagsData.models | ForEach-Object {
                $size = if ($_.size) { " ($($_.size / 1GB)GB)" } else { "" }
                Write-Host "    - $($_.name)$size" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host " ⚠️  WARNING: Could not fetch model list" -ForegroundColor Yellow
    }

    exit 0
} catch {
    Write-Host "❌ ERROR: Ollama not responding at $Endpoint" -ForegroundColor Red
    Write-Host "  Make sure Ollama is running: ollama serve" -ForegroundColor Yellow
    Write-Host "  Error: $_" -ForegroundColor Gray
    exit 1
}
