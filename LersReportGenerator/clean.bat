@echo off
REM Clean build artifacts from all projects
REM Removes bin/, obj/, and .build-hash files

echo.
echo ========================================
echo   Cleaning Build Artifacts
echo ========================================
echo.

REM Check for -v or --verbose flag
set VERBOSE_FLAG=
if "%1"=="-v" set VERBOSE_FLAG=-Verbose
if "%1"=="--verbose" set VERBOSE_FLAG=-Verbose

REM Run PowerShell cleanup script
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0clean.ps1" %VERBOSE_FLAG%

if errorlevel 1 (
    echo.
    echo Failed to clean build artifacts
    pause
    exit /b 1
)

echo.
pause
