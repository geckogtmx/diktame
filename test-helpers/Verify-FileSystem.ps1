<#
.SYNOPSIS
Verify files and directories exist

.DESCRIPTION
Checks that expected files or directories exist in the file system

.PARAMETER Path
Path to check (supports environment variables like %APPDATA%)

.PARAMETER Type
Type to check: File or Directory

.EXAMPLE
.\Verify-FileSystem.ps1 -Path "%APPDATA%\DiktaMe\settings.json" -Type File
.\Verify-FileSystem.ps1 -Path "%APPDATA%\DiktaMe\logs" -Type Directory
#>

param(
    [string]$Path = (Join-Path $env:APPDATA "DiktaMe"),
    [ValidateSet("File", "Directory")]
    [string]$Type = "Directory"
)

# Expand environment variables
$expandedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

Write-Host "Checking $Type at: $expandedPath"

try {
    if (Test-Path $expandedPath) {
        $item = Get-Item $expandedPath

        if ($Type -eq "File" -and -not $item.PSIsContainer) {
            Write-Host "✅ PASS: File exists" -ForegroundColor Green
            Write-Host "   Size: $($item.Length) bytes" -ForegroundColor Gray
            Write-Host "   Modified: $($item.LastWriteTime)" -ForegroundColor Gray
            exit 0
        } elseif ($Type -eq "Directory" -and $item.PSIsContainer) {
            $fileCount = (Get-ChildItem $expandedPath -Recurse -File | Measure-Object).Count
            $dirCount = (Get-ChildItem $expandedPath -Recurse -Directory | Measure-Object).Count
            Write-Host "✅ PASS: Directory exists" -ForegroundColor Green
            Write-Host "   Files: $fileCount, Subdirs: $dirCount" -ForegroundColor Gray
            exit 0
        } else {
            Write-Host "❌ FAIL: Item exists but is not $Type" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "⚠️  WARNING: $Type not found" -ForegroundColor Yellow
        exit 0
    }
} catch {
    Write-Host "❌ ERROR: Failed to check path: $_" -ForegroundColor Red
    exit 1
}
