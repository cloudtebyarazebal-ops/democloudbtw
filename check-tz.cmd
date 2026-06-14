@echo off
chcp 65001 >nul
setlocal

if "%~1"=="" (
    echo Использование: check-tz.cmd "путь\к\тз.pdf" или .txt
    exit /b 1
)

cd /d "%~dp0"
dotnet run --project "ExamCoach\tools\AdaptTest\AdaptTest.csproj" -- "%~1"

endlocal
