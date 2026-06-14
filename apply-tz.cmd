@echo off
chcp 65001 >nul
setlocal

if "%~1"=="" (
    echo Использование: apply-tz.cmd "путь\к\тз.pdf" или .txt
    echo Перед запуском закройте KodShopWeb.
    exit /b 1
)

cd /d "%~dp0"

echo Применение ТЗ: %~1
dotnet run --project "ExamCoach\tools\AdaptTest\AdaptTest.csproj" -- "%~1" --apply
if errorlevel 1 exit /b 1

echo.
echo Сборка и запуск...
dotnet build
if errorlevel 1 exit /b 1

echo.
echo Готово. Запуск: dotnet run
echo Логин: admin / admin123

endlocal
