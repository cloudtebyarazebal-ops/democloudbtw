$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "Desktop\ExamCoachDesktop.csproj"
$out = Join-Path $root "Desktop\bin\Release\net8.0-windows"
$dest = Join-Path $root "DesktopApp"

Write-Host "Building ExamCoach Desktop (WPF window)..."

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $out "ExamCoachDesktop.exe"
if (-not (Test-Path $exe)) {
    Write-Error "ExamCoachDesktop.exe not found after build."
}

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Path $dest | Out-Null
Copy-Item (Join-Path $out "*") $dest -Recurse -Force

Write-Host "Done: $exe"
Write-Host "Run: $dest\ExamCoachDesktop.exe"
Write-Host "Or:  Start-ExamCoachDesktop.bat"
Write-Host "Note: index.html is browser-only, not the desktop app."
