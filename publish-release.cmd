@echo off
REM ============================================================
REM  dIKta.me V2 — Release Publish Script
REM  Builds a self-contained, trimmed WinUI 3 application
REM ============================================================

setlocal

set CONFIGURATION=Release
set OUTPUT_DIR=publish
set PROJECT=src\DiktaMe.App\DiktaMe.App.csproj

echo.
echo ========================================
echo  dIKta.me V2 — Release Build
echo ========================================
echo.

REM Clean previous output
if exist "%OUTPUT_DIR%" (
    echo Cleaning previous publish output...
    rmdir /s /q "%OUTPUT_DIR%"
)

REM --- Build x64 ---
echo.
echo [1/1] Publishing x64 (self-contained, trimmed)...
echo.
dotnet publish "%PROJECT%" ^
    -c %CONFIGURATION% ^
    -r win-x64 ^
    -o "%OUTPUT_DIR%\win-x64" ^
    --self-contained true ^
    /p:Platform=x64

if errorlevel 1 (
    echo.
    echo ERROR: x64 publish failed!
    exit /b 1
)

REM --- Build ARM64 (uncomment when needed) ---
REM echo.
REM echo [2/2] Publishing ARM64 (self-contained, trimmed)...
REM echo.
REM dotnet publish "%PROJECT%" ^
REM     -c %CONFIGURATION% ^
REM     -r win-arm64 ^
REM     -o "%OUTPUT_DIR%\win-arm64" ^
REM     --self-contained true ^
REM     /p:Platform=ARM64

REM --- Report sizes ---
echo.
echo ========================================
echo  Build Complete — Output Sizes
echo ========================================
echo.

echo x64 output:
for /f "tokens=3" %%a in ('dir "%OUTPUT_DIR%\win-x64" /s /-c ^| findstr "File(s)"') do echo   Total: %%a bytes
for %%f in ("%OUTPUT_DIR%\win-x64\DiktaMe.App.exe") do echo   DiktaMe.App.exe: %%~zf bytes

echo.
echo Output directory: %OUTPUT_DIR%\win-x64\
echo.
echo To test: run "%OUTPUT_DIR%\win-x64\DiktaMe.App.exe"
echo.

endlocal
