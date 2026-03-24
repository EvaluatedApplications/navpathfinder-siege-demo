@echo off
title NavPathfinder Siege Demo

:: Verify executable exists
if not exist "%~dp0NavPathfinder.Demo.exe" (
    echo.
    echo   ERROR: NavPathfinder.Demo.exe not found next to this batch file.
    echo.
    pause
    exit /b 1
)

cd /d "%~dp0"
NavPathfinder.Demo.exe

:: If the demo exited with an error, keep the window open so the user can read it
if errorlevel 1 (
    echo.
    echo   The demo exited with an error (code: %errorlevel%).
    echo   If the window was too small, try maximising it and running again.
    echo.
    pause
)
