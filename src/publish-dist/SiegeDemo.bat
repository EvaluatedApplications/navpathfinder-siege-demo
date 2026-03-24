@echo off
title NavPathfinder Siege Demo

:: Unblock files downloaded from the internet
echo Preparing...
powershell -NoProfile -Command "Get-ChildItem -Path '%~dp0runtime' -Recurse | Unblock-File" >nul 2>&1

:: Verify runtime directory exists
if not exist "%~dp0runtime\" (
    echo.
    echo   ERROR: "runtime" folder not found next to this batch file.
    echo   Expected location: %~dp0runtime
    echo.
    pause
    exit /b 1
)

:: Verify executable exists
if not exist "%~dp0runtime\NavPathfinder.Demo.exe" (
    echo.
    echo   ERROR: NavPathfinder.Demo.exe not found in runtime folder.
    echo.
    pause
    exit /b 1
)

cd /d "%~dp0runtime"
NavPathfinder.Demo.exe

:: If the demo exited with an error, keep the window open so the user can read it
if errorlevel 1 (
    echo.
    echo   The demo exited with an error (code: %errorlevel%).
    echo   If the window was too small, try maximising it and running again.
    echo.
    pause
)
