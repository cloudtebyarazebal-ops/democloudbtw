param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$OutputHtml = (Join-Path $PSScriptRoot "index.html"),
    [string]$OutputJson = (Join-Path $PSScriptRoot "steps-data.json"),
    [string]$ManifestPath = (Join-Path $PSScriptRoot "steps-manifest.json"),
    [switch]$UpdateManifest
)

$ErrorActionPreference = "Stop"

if ($UpdateManifest) {
    & (Join-Path $PSScriptRoot "Update-Manifest.ps1") -ProjectRoot $ProjectRoot -ManifestPath $ManifestPath
}

$manifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$steps = New-Object System.Collections.Generic.List[object]
$moduleStepMap = @{}

foreach ($s in $manifest.setup) {
    $step = [ordered]@{
        id       = $s.id
        phase    = $s.phase
        module   = $(if ($s.module) { $s.module } else { "m1" })
        title    = $s.title
        vsHint   = $s.vsHint
        terminal = $(if ($s.terminal) { $s.terminal } else { "" })
        code     = ""
    }
    $steps.Add($step)
    $mid = $step.module
    if (-not $moduleStepMap.ContainsKey($mid)) { $moduleStepMap[$mid] = New-Object System.Collections.Generic.List[int] }
    $moduleStepMap[$mid].Add($steps.Count - 1)
}

foreach ($f in $manifest.files) {
    $fullPath = Join-Path $ProjectRoot ($f.path -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path $fullPath)) {
        Write-Warning "Missing: $fullPath"
        continue
    }
    $code = [IO.File]::ReadAllText($fullPath, [Text.Encoding]::UTF8)
    $id = "file-" + ($f.path -replace '[\\/\.]', '-')
    $step = [ordered]@{
        id       = $id
        phase    = $f.phase
        module   = $(if ($f.module) { $f.module } else { "m1" })
        title    = $f.path
        vsHint   = $f.vsHint
        terminal = ""
        code     = $code
    }
    $steps.Add($step)
    $mid = $step.module
    if (-not $moduleStepMap.ContainsKey($mid)) { $moduleStepMap[$mid] = New-Object System.Collections.Generic.List[int] }
    $moduleStepMap[$mid].Add($steps.Count - 1)
}

$modules = @()
$moduleSource = $manifest.modules
if (-not $moduleSource) {
    $configPath = Join-Path $PSScriptRoot "coach-config.json"
    $moduleSource = (Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json).modules
}
foreach ($m in $moduleSource) {
    $indices = @()
    if ($moduleStepMap.ContainsKey($m.id)) {
        $indices = @($moduleStepMap[$m.id].ToArray())
    }
    $modules += [ordered]@{
        id          = $m.id
        title       = $m.title
        minutes     = [int]$m.minutes
        description = $m.description
        stepIndices = $indices
    }
}

$stepsArray = @($steps.ToArray())
$data = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    steps       = $stepsArray
    modules     = $modules
}

$dataJson = $data | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($OutputJson, $dataJson, [Text.UTF8Encoding]::new($false))

$stepsJson = ($steps | ConvertTo-Json -Depth 6 -Compress)
$modulesJson = ($modules | ConvertTo-Json -Depth 6 -Compress)

$templatePath = Join-Path $PSScriptRoot "coach-template.html"
$template = [IO.File]::ReadAllText($templatePath, [Text.Encoding]::UTF8)
$html = $template.Replace('__STEPS_JSON__', $stepsJson).Replace('__MODULES_JSON__', $modulesJson)
[IO.File]::WriteAllText($OutputHtml, $html, [Text.UTF8Encoding]::new($false))

Write-Host "Generated: $OutputHtml ($($steps.Count) steps)"
Write-Host "Generated: $OutputJson"
