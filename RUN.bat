@echo off
title NavPathfinder Siege Demo
echo.
echo   NavPathfinder Siege Demo
echo   ========================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo   .NET 8 SDK is required to run this demo.
    echo   Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo   Building from source... (first run takes ~15 seconds)
echo.
dotnet run --project "%~dp0src\NavPathfinder.SiegeDemo.csproj" -c Release

:: If the demo exited with an error, keep the window open so the user can read it
if errorlevel 1 (
    echo.
    echo   The demo exited with an error (code: %errorlevel%).
    echo   If the window was too small, try maximising it and running again.
    echo.
    pause
)
