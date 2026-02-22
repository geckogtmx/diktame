# Launch DiktaMe with console output visible
# This attaches a console to the WinUI 3 app so you can see Serilog output

$exePath = "E:\git\diktame\src\DiktaMe.App\bin\x64\Release\net8.0-windows10.0.19041.0\DiktaMe.App.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Error: App not built yet. Run this first:" -ForegroundColor Red
    Write-Host "  dotnet build DiktaMe.sln -c Release `"-p:Platform=x64`"" -ForegroundColor Yellow
    exit 1
}

Write-Host "Launching dIKta.me with console output..." -ForegroundColor Green
Write-Host "Watch this window for logs from Serilog.Console" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop the app" -ForegroundColor Yellow
Write-Host ""

# Start the process and keep console attached
& $exePath

Write-Host ""
Write-Host "App closed." -ForegroundColor Gray
