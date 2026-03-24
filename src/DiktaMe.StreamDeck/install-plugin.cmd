@echo off
setlocal

set "PLUGIN_ID=me.dikta.streamdeck.sdPlugin"
set "BUILD_DIR=%~dp0bin\Release\%PLUGIN_ID%"
set "BUILD_EXE=%BUILD_DIR%\DiktaMe.StreamDeck.exe"
set "INSTALL_DIR=%APPDATA%\Elgato\StreamDeck\Plugins\%PLUGIN_ID%"
set "STREAMDECK_EXE=%PROGRAMFILES%\Elgato\StreamDeck\StreamDeck.exe"

echo ============================================
echo  DiktaMe Stream Deck Plugin Installer
echo ============================================
echo.

:: 1. Check that the Release build exists
if not exist "%BUILD_EXE%" (
    echo ERROR: Release build not found at:
    echo   %BUILD_EXE%
    echo.
    echo Run "dotnet publish -c Release" first.
    exit /b 1
)

echo [1/5] Release build found.

:: 2. Kill StreamDeck.exe if running
echo [2/5] Stopping Stream Deck...
taskkill /f /im StreamDeck.exe >nul 2>&1
if %errorlevel% equ 0 (
    echo       Stream Deck process terminated.
) else (
    echo       Stream Deck was not running.
)

:: 3. Wait for process to fully exit
echo [3/5] Waiting 2 seconds...
timeout /t 2 /nobreak >nul

:: 4. Remove old plugin folder and copy new one
echo [4/5] Installing plugin...
if exist "%INSTALL_DIR%" (
    rmdir /s /q "%INSTALL_DIR%"
    if exist "%INSTALL_DIR%" (
        echo ERROR: Could not remove old plugin folder. Is a file locked?
        exit /b 1
    )
)

xcopy "%BUILD_DIR%" "%INSTALL_DIR%" /e /i /q >nul
if %errorlevel% neq 0 (
    echo ERROR: Failed to copy plugin files.
    exit /b 1
)

echo       Plugin installed to:
echo       %INSTALL_DIR%

:: 5. Restart Stream Deck
echo [5/5] Starting Stream Deck...
if exist "%STREAMDECK_EXE%" (
    start "" "%STREAMDECK_EXE%"
    echo       Stream Deck started.
) else (
    echo WARNING: Stream Deck executable not found at:
    echo   %STREAMDECK_EXE%
    echo   Please start Stream Deck manually.
)

echo.
echo ============================================
echo  Plugin installed successfully!
echo ============================================

endlocal
