@echo off
REM Push API keys from .env.local to Supabase Edge Function secrets.
REM Run this after rotating keys or adding new ones.
REM
REM Usage: push-secrets.cmd
REM
REM Reads: GEMINI_API_KEY, DEEPGRAM_API_KEY, DEV_SECRET from .env.local
REM (Lines starting with # are ignored)

setlocal enabledelayedexpansion

set "ENV_FILE=%~dp0.env.local"

if not exist "%ENV_FILE%" (
    echo ERROR: .env.local not found at %ENV_FILE%
    echo Copy .env.local.example to .env.local and fill in your keys.
    exit /b 1
)

echo Reading keys from .env.local...

set "KEYS_TO_PUSH="
for %%K in (GEMINI_API_KEY DEEPGRAM_API_KEY DEV_SECRET ADMIN_SECRET LEMON_SIGNING_SECRET) do (
    for /f "usebackq tokens=1,* delims==" %%A in ("%ENV_FILE%") do (
        if "%%A"=="%%K" (
            echo   Found %%K
            set "KEYS_TO_PUSH=!KEYS_TO_PUSH! %%K=%%B"
        )
    )
)

if "!KEYS_TO_PUSH!"=="" (
    echo No wallet secrets found in .env.local.
    echo Add GEMINI_API_KEY, DEEPGRAM_API_KEY, etc. to .env.local
    exit /b 1
)

echo.
echo Pushing secrets to Supabase...
call npx supabase secrets set !KEYS_TO_PUSH!

if %errorlevel% equ 0 (
    echo.
    echo Done! Secrets updated on Supabase.
) else (
    echo.
    echo ERROR: Failed to push secrets. Are you logged in? Run: npx supabase login
)
