# Setup KodShopWeb + ExamCoach workspace
param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = "Stop"
$Root = (Resolve-Path $Root).Path
$ExamCoachDir = Join-Path $Root "ExamCoach"
$ExamCoachRepo = "https://github.com/cloudtebyarazebal-ops/ExamCoach.git"

Write-Host "=== KodShopWeb + ExamCoach setup ===" -ForegroundColor Cyan
Write-Host "Root: $Root"

if (-not (Test-Path (Join-Path $Root "KodShopWeb.csproj"))) {
    throw "KodShopWeb.csproj not found in $Root"
}

if (-not (Test-Path $ExamCoachDir)) {
    Write-Host "Cloning ExamCoach..." -ForegroundColor Yellow
    git clone $ExamCoachRepo $ExamCoachDir
}
elseif (Test-Path (Join-Path $ExamCoachDir "tools\AdaptTest\AdaptTest.csproj")) {
    Write-Host "ExamCoach already installed." -ForegroundColor Green
    if (Test-Path (Join-Path $ExamCoachDir ".git")) {
        Write-Host "Updating ExamCoach (git pull)..." -ForegroundColor Yellow
        git -C $ExamCoachDir pull --ff-only
    }
}
else {
    Write-Host "ExamCoach folder broken - recloning..." -ForegroundColor Yellow
    Remove-Item $ExamCoachDir -Recurse -Force
    git clone $ExamCoachRepo $ExamCoachDir
}

Write-Host "Building ExamCoach..." -ForegroundColor Yellow
dotnet build (Join-Path $ExamCoachDir "ExamCoach.sln") -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building KodShopWeb..." -ForegroundColor Yellow
dotnet build (Join-Path $Root "KodShopWeb.csproj") -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Check TZ:  .\check-tz.cmd `"C:\path\to\tz.txt`""
Write-Host "  Apply TZ:  .\apply-tz.cmd `"C:\path\to\tz.txt`""
Write-Host "  Run app:   dotnet run"
