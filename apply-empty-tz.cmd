@echo off
chcp 65001 >nul
setlocal

if "%~1"=="" (
    echo Использование: apply-empty-tz.cmd "путь\к\тз.txt" [папка_проекта]
    echo.
    echo Создаёт MVC-проект ^(если папки нет^) и заполняет его под ТЗ.
    echo По умолчанию папка: %~dp0NewExamProject
    exit /b 1
)

set "TZ=%~1"
set "TARGET=%~2"
if "%TARGET%"=="" set "TARGET=%~dp0NewExamProject"

cd /d "%~dp0"

echo Целевой проект: %TARGET%
dotnet run --project "ExamCoach\tools\AdaptTest\AdaptTest.csproj" -- "%TZ%" --apply --init --project-root="%TARGET%"
if errorlevel 1 exit /b 1

echo.
echo Сборка...
dotnet build "%TARGET%"
if errorlevel 1 exit /b 1

echo.
echo Готово. Запуск:
echo   cd /d "%TARGET%"
echo   dotnet run
echo Логин: admin / admin123

endlocal
