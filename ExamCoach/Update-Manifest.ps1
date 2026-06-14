param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ManifestPath = (Join-Path $PSScriptRoot "steps-manifest.json"),
    [string]$ConfigPath = (Join-Path $PSScriptRoot "coach-config.json"),
    [switch]$DryRun,
    [switch]$ValidateManifest
)

$ErrorActionPreference = "Stop"

function Write-Log([string]$Level, [string]$Message) {
    Write-Host ("[{0}] {1}" -f $Level, $Message)
}

function Exit-WithError([string]$Message, [int]$Code = 1) {
    Write-Error $Message
    exit $Code
}

function Get-UniqueCount([object[]]$Values) {
    if (-not $Values -or $Values.Count -eq 0) { return 0 }
    return @($Values | Select-Object -Unique).Count
}

function Test-ManifestObject(
    [object]$Manifest,
    [string]$RootPath
) {
    $issues = New-Object System.Collections.Generic.List[string]

    if (-not $Manifest) {
        $issues.Add("Manifest object is null.")
        return $issues
    }

    if (-not $Manifest.modules) {
        $issues.Add("Manifest.modules is missing.")
    }
    if (-not $Manifest.files) {
        $issues.Add("Manifest.files is missing.")
    }

    $allowedPhases = @(
        '0. Start', '1. Models', '2. Database', '3. Services',
        '4. ViewModels', '5. Program', '6. Controllers', '7. Views',
        '8. Static', '9. Other'
    )

    $moduleIds = @()
    if ($Manifest.modules) {
        $moduleIds = @($Manifest.modules | ForEach-Object { $_.id })
        if ((Get-UniqueCount $moduleIds) -ne $moduleIds.Count) {
            $issues.Add("Duplicate module ids found in modules.")
        }
    }

    $knownModules = @{}
    foreach ($id in $moduleIds) {
        if ($id) { $knownModules[$id] = $true }
    }

    $paths = @()
    foreach ($file in @($Manifest.files)) {
        if (-not $file.path) { $issues.Add("File entry has empty path."); continue }
        $paths += $file.path

        if (-not $file.phase) { $issues.Add("File '$($file.path)' has empty phase.") }
        elseif ($allowedPhases -notcontains $file.phase) { $issues.Add("File '$($file.path)' has unknown phase '$($file.phase)'.") }

        if (-not $file.module) { $issues.Add("File '$($file.path)' has empty module.") }
        elseif (-not $knownModules.ContainsKey($file.module)) { $issues.Add("File '$($file.path)' references unknown module '$($file.module)'.") }

        if (-not $file.vsHint) { $issues.Add("File '$($file.path)' has empty vsHint.") }

        $fullPath = Join-Path $RootPath ($file.path -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path $fullPath)) {
            $issues.Add("File '$($file.path)' does not exist in ProjectRoot.")
        }
    }

    if ((Get-UniqueCount $paths) -ne $paths.Count) {
        $issues.Add("Duplicate file paths found in files.")
    }

    return $issues
}

if (-not (Test-Path $ProjectRoot)) {
    Exit-WithError "ProjectRoot not found: $ProjectRoot" 2
}
if (-not (Test-Path $ConfigPath)) {
    Exit-WithError "Config file not found: $ConfigPath" 2
}

$config = Get-Content $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Get-PhaseForPath([string]$RelativePath) {
    $p = $RelativePath -replace '\\', '/'
    switch -Regex ($p) {
        '^KodShopWeb\.csproj$' { return '0. Start' }
        '^appsettings\.json$' { return '0. Start' }
        '^Docs/' { return '1. Models' }
        '^Models/' { return '1. Models' }
        '^Data/' { return '2. Database' }
        '^Services/' { return '3. Services' }
        '^ViewModels/' { return '4. ViewModels' }
        '^Program\.cs$' { return '5. Program' }
        '^Controllers/' { return '6. Controllers' }
        '^Views/' { return '7. Views' }
        '^wwwroot/' { return '8. Static' }
        default { return '9. Other' }
    }
}

function Get-ModuleForPath([string]$RelativePath) {
    $p = $RelativePath -replace '\\', '/'
    switch -Regex ($p) {
        '^KodShopWeb\.csproj$' { return 'm1' }
        '^appsettings\.json$' { return 'm1' }
        '^Docs/' { return 'm1' }
        '^Models/' { return 'm1' }
        '^Data/' { return 'm1' }
        '^Services/ImportService\.cs$' { return 'm1' }
        '^Views/Import/' { return 'm1' }
        '^Program\.cs$' { return 'm2' }
        '^ViewModels/' { return 'm2' }
        '^Services/AuthService\.cs$' { return 'm2' }
        '^Services/UserAccess\.cs$' { return 'm2' }
        '^Controllers/AccountController\.cs$' { return 'm2' }
        '^Views/_ViewImports\.cshtml$' { return 'm2' }
        '^Views/_ViewStart\.cshtml$' { return 'm2' }
        '^Views/Shared/_Layout\.cshtml$' { return 'm2' }
        '^Views/Shared/_ValidationScriptsPartial\.cshtml$' { return 'm2' }
        '^Views/Account/' { return 'm2' }
        '^Views/Products/Index\.cshtml$' { return 'm2' }
        '^Services/ProductService\.cs$' { return 'm3' }
        '^Controllers/ProductsController\.cs$' { return 'm3' }
        '^Views/Products/Edit\.cshtml$' { return 'm3' }
        '^wwwroot/js/products\.js$' { return 'm3' }
        '^wwwroot/css/shop\.css$' { return 'm3' }
        '^Services/OrderService\.cs$' { return 'm4' }
        '^Controllers/OrdersController\.cs$' { return 'm4' }
        '^Views/Orders/' { return 'm4' }
        '^Views/Home/Error\.cshtml$' { return 'm4' }
        '^Services/' { return 'm3' }
        '^Controllers/' { return 'm4' }
        '^Views/' { return 'm4' }
        '^wwwroot/' { return 'm3' }
        default { return 'm1' }
    }
}

function Get-DefaultVsHint([string]$RelativePath) {
    $p = $RelativePath -replace '\\', '/'
    $name = [IO.Path]::GetFileName($p)
    switch -Regex ($p) {
        '^KodShopWeb\.csproj$' { return 'Check PackageReference items in csproj.' }
        '^appsettings\.json$' { return 'Add ConnectionStrings and Shop section.' }
        '^Models/' { return "Add $name in Models folder." }
        '^Data/AppDbContext\.cs$' { return 'EF Core DbContext.' }
        '^Data/DbSeeder\.cs$' { return 'Seed test users, products and sample order (PU/BU).' }
        '^Services/' { return "Add service $name." }
        '^ViewModels/' { return 'Forms for login, product, order.' }
        '^Program\.cs$' { return 'Replace Program.cs - DI, auth, routes.' }
        '^Controllers/AccountController\.cs$' { return 'Login, Guest, Logout actions.' }
        '^Controllers/ProductsController\.cs$' { return 'Product list and CRUD.' }
        '^Controllers/OrdersController\.cs$' { return 'Orders and Import controllers.' }
        '^Views/_ViewImports\.cshtml$' { return 'Global usings for Razor.' }
        '^Views/_ViewStart\.cshtml$' { return 'Default layout.' }
        '^Views/Shared/_Layout\.cshtml$' { return 'Header, nav, user role badge.' }
        '^Views/Account/' { return 'Login form.' }
        '^Views/Products/Index\.cshtml$' { return 'Product table + GET toolbar (server-side filter/sort).' }
        '^Views/Products/Edit\.cshtml$' { return 'Add/Edit product form.' }
        '^Views/Orders/' { return 'Orders list/form with status, pickup, total sum.' }
        '^Views/Import/' { return 'Import page.' }
        '^Views/Home/Error\.cshtml$' { return 'Error page.' }
        '^wwwroot/css/' { return 'Styles per style guide.' }
        '^wwwroot/js/products\.js$' { return 'Debounced GET submit — server-side search/filter/sort via URL.' }
        '^Docs/ER-Diagram\.html$' { return 'ER-diagram source; export to Docs/ER-Diagram.pdf for exam.' }
        '^Docs/ER-Diagram\.pdf$' { return 'ER-diagram PDF for module 1 submission.' }
        default { return "Create or update $p in Visual Studio." }
    }
}

$existingHints = @{}
$oldManifest = $null
if (Test-Path $ManifestPath) {
    $oldManifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($f in $oldManifest.files) {
        $key = ($f.path -replace '\\', '/')
        $existingHints[$key] = $f.vsHint
    }
}

$scanRoots = @(
    @{ Path = ''; Patterns = @('KodShopWeb.csproj', 'appsettings.json', 'Program.cs') },
    @{ Path = 'Models'; Patterns = @('*.cs') },
    @{ Path = 'Data'; Patterns = @('*.cs') },
    @{ Path = 'Services'; Patterns = @('*.cs') },
    @{ Path = 'ViewModels'; Patterns = @('*.cs') },
    @{ Path = 'Controllers'; Patterns = @('*.cs') },
    @{ Path = 'Views'; Patterns = @('*.cshtml'); Recursive = $true },
    @{ Path = 'wwwroot/css'; Patterns = @('*.css') },
    @{ Path = 'wwwroot/js'; Patterns = @('*.js') },
    @{ Path = 'Docs'; Patterns = @('*.html', '*.pdf') }
)

$fileOrder = New-Object System.Collections.Generic.List[string]
$seen = @{}

foreach ($root in $scanRoots) {
    $base = if ($root.Path) { Join-Path $ProjectRoot $root.Path } else { $ProjectRoot }
    if (-not (Test-Path $base)) { continue }

    foreach ($pattern in $root.Patterns) {
        $searchPath = if ($root.Path) { Join-Path $ProjectRoot $root.Path } else { $ProjectRoot }
        $option = if ($root.Recursive) { [IO.SearchOption]::AllDirectories } else { [IO.SearchOption]::TopDirectoryOnly }
        $files = [IO.Directory]::GetFiles($searchPath, $pattern, $option)
        foreach ($full in ($files | Sort-Object)) {
            $rel = $full.Substring($ProjectRoot.Length).TrimStart('\', '/') -replace '\\', '/'
            if ($rel -match '^(bin|obj|\.vs)/') { continue }
            if ($seen.ContainsKey($rel)) { continue }
            $seen[$rel] = $true
            [void]$fileOrder.Add($rel)
        }
    }
}

$phaseRank = @{
    '0. Start' = 0; '1. Models' = 1; '2. Database' = 2; '3. Services' = 3
    '4. ViewModels' = 4; '5. Program' = 5; '6. Controllers' = 6
    '7. Views' = 7; '8. Static' = 8; '9. Other' = 9
}
$moduleRank = @{ m1 = 0; m2 = 1; m3 = 2; m4 = 3 }

$fileEntries = @()
foreach ($rel in $fileOrder) {
    $hint = if ($existingHints.ContainsKey($rel)) { $existingHints[$rel] } else { Get-DefaultVsHint $rel }
    $fileEntries += [ordered]@{
        path   = $rel
        phase  = Get-PhaseForPath $rel
        module = Get-ModuleForPath $rel
        vsHint = $hint
    }
}
$fileEntries = $fileEntries | Sort-Object { $phaseRank[$_.phase] }, { $moduleRank[$_.module] }, { $_.path }

$manifest = [ordered]@{
    setup   = @($config.setup)
    modules = @($config.modules)
    files   = @($fileEntries)
}

$issues = Test-ManifestObject -Manifest $manifest -RootPath $ProjectRoot
if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Host ("[ERROR] {0}" -f $issue)
    }
    Exit-WithError ("Manifest validation failed with {0} issue(s)." -f $issues.Count) 3
}

if ($ValidateManifest) {
    Write-Log "INFO" ("Manifest validation passed ({0} files)." -f $fileEntries.Count)
}

$oldPaths = @()
if ($oldManifest -and $oldManifest.files) {
    $oldPaths = @($oldManifest.files | ForEach-Object { $_.path })
}
$newPaths = @($fileEntries | ForEach-Object { $_.path })
$added = @($newPaths | Where-Object { $oldPaths -notcontains $_ })
$removed = @($oldPaths | Where-Object { $newPaths -notcontains $_ })
$unchanged = @($newPaths | Where-Object { $oldPaths -contains $_ })

Write-Log "INFO" ("Scan summary: total={0}, added={1}, removed={2}, unchanged={3}" -f $newPaths.Count, $added.Count, $removed.Count, $unchanged.Count)

if ($DryRun) {
    Write-Log "INFO" ("DryRun enabled. Manifest was not written: {0}" -f $ManifestPath)
    exit 0
}

$manifestDir = Split-Path $ManifestPath -Parent
if ($manifestDir -and -not (Test-Path $manifestDir)) {
    New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
}

$json = $manifest | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($ManifestPath, $json, [Text.UTF8Encoding]::new($false))
Write-Log "INFO" ('Updated manifest: ' + $ManifestPath + ' (' + $fileEntries.Count + ' files)')
