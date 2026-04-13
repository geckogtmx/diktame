@echo off
REM ============================================================
REM  dIKta.me V2 — Installer Build Script
REM  1. Publishes win-x64 release via publish-release.cmd
REM  2. Compiles Inno Setup installer
REM ============================================================

setlocal

echo.
echo ========================================
echo  dIKta.me V2 — Building Installer
echo ========================================
echo.

REM --- Step 1: Publish release ---
echo [1/2] Publishing release build...
pushd ..
call publish-release.cmd
if errorlevel 1 (
    echo.
    echo ERROR: Publish failed!
    popd
    exit /b 1
)
popd

REM --- Step 2: Build installer ---
echo.
echo [2/2] Building Inno Setup installer...

REM Try standard Inno Setup 6 install paths
set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
) else if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
) else (
    REM Try PATH
    where ISCC.exe >nul 2>&1
    if errorlevel 1 (
        echo ERROR: Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php
        exit /b 1
    )
    set "ISCC=ISCC.exe"
)

"%ISCC%" diktame-setup.iss
if errorlevel 1 (
    echo.
    echo ERROR: Installer build failed!
    exit /b 1
)

echo.
echo ========================================
echo  Done!
echo ========================================
echo.
dir output\*.exe
echo.

endlocal
