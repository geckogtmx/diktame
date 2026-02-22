@echo off
REM Quick script to run Debug build with console output
echo Deleting old settings to trigger wizard...
del "%APPDATA%\DiktaMe\settings.json" 2>nul

echo.
echo Building Debug version...
dotnet build E:\git\diktame\DiktaMe.sln -c Debug

echo.
echo Launching app with console window...
echo Watch this window for logs!
echo.
echo ========================================
echo.

E:\git\diktame\src\DiktaMe.App\bin\x86\Debug\net8.0-windows10.0.19041.0\DiktaMe.App.exe

echo.
echo ========================================
echo App closed.
pause
