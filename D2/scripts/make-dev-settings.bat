@echo off
setlocal enabledelayedexpansion

REM Go to repo root (relative to script location)
cd /d "%~dp0.."

echo.
echo === Setting up appsettings.Development.json files ===

for /r %%f in (appsettings.Example.json) do (
    set "example=%%f"
    set "dir=%%~dpf"
    set "dev=%%~dpnxf"
    set "dev=!dev:Example=Development!"
    if not exist "!dev!" (
        copy "!example!" "!dev!" >nul
        echo Created: !dev!
    )
)

echo.
echo Done. You can now run the solution locally.
pause
