@echo off
chcp 65001 >nul
setlocal

:: Проверка прав администратора
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Требуются права администратора!
    echo Запустите от имени администратора.
    pause
    exit /b 1
)

set SERVICE_NAME=LersReportProxy
set SCRIPT_DIR=%~dp0

echo.
echo === Установка службы %SERVICE_NAME% ===
echo Порт: 5377
echo.

:: Проверяем, установлена ли служба - если да, сначала удаляем
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo Обнаружена существующая служба, удаляем...
    echo.
    powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1" -Uninstall
    echo.
    timeout /t 2 /nobreak >nul
)

:: Устанавливаем
echo Установка...
echo.
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1"

echo.
pause
