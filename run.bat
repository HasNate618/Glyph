@echo off
REM Quick helper to show Glyph log location and run the app

setlocal enabledelayedexpansion

echo.
echo ====== Glyph Launcher ======
echo.

set "logdir=%APPDATA%\Glyph\logs"
echo Logs will be written to:
echo   %logdir%
echo.

if exist "%logdir%" (
    echo Recent logs:
    for /f "delims=" %%f in ('dir /b /o-d "%logdir%\*.log" 2^>nul') do (
        echo   - %%f
    )
) else (
    echo [Logs directory will be created on first run]
)

echo.
echo Starting Glyph...
echo   Press Ctrl+Shift+Space to activate the overlay
echo   Type: r then c (Chrome) or r then n (Notepad)
echo   Press Esc to cancel
echo.

cd /d "%~dp0.."
dotnet run --project src/Glyph.App/Glyph.App.csproj

pause
