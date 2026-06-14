@echo off
chcp 65001 >nul
set "APP=%~dp0DesktopApp\ExamCoachDesktop.exe"
if not exist "%APP%" set "APP=%~dp0Desktop\bin\Release\net8.0-windows\ExamCoachDesktop.exe"
if not exist "%APP%" (
    echo ExamCoach Desktop не собран.
    echo Запустите: powershell -ExecutionPolicy Bypass -File "%~dp0Build-ExamCoachDesktop.ps1"
    pause
    exit /b 1
)
start "" "%APP%"
