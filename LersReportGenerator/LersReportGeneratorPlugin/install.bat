@echo off
title LersReportGeneratorPlugin Installer

echo.
echo ========================================
echo   LersReportGeneratorPlugin Installer
echo ========================================
echo.

:: Check PowerShell
where powershell >nul 2>&1
if errorlevel 1 (
    echo [!] PowerShell not found
    pause
    exit /b 1
)

:: Run install.ps1 with execution policy bypass
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*

if errorlevel 1 (
    echo.
    echo [!] Install failed
    pause
    exit /b 1
)

echo.
pause
