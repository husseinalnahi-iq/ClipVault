param(
    [switch]$RequireReleaseMetadata
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$csprojPath = (Resolve-Path (Join-Path $repoRoot 'src\UI\ClipVault.UI\ClipVault.UI.csproj')).Path
$issPath = (Resolve-Path (Join-Path $repoRoot 'installer\ClipVault.Setup.iss')).Path
$installScriptPath = (Resolve-Path (Join-Path $repoRoot 'installer\install.ps1')).Path
$distInstallerDir = Join-Path $repoRoot 'dist\installer'
$aliasInstallerPath = Join-Path $distInstallerDir 'ClipVault-Setup.exe'
$releaseJsonPath = Join-Path $distInstallerDir 'release.json'

$errors = New-Object System.Collections.Generic.List[string]

function Get-ProjectVersion([string]$path) {
    [xml]$xml = Get-Content -Path $path -Raw
    $value = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }
    return $value.Trim()
}

function Assert-Contains([string]$content, [string]$needle, [string]$errorMessage) {
    if ($content -notmatch [Regex]::Escape($needle)) {
        $errors.Add($errorMessage)
    }
}

$projectVersion = Get-ProjectVersion -path $csprojPath
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    $errors.Add("Missing <Version> in $csprojPath")
}

$rootInstallers = Get-ChildItem -Path $repoRoot -Filter 'ClipVault-Setup*.exe' -File -ErrorAction SilentlyContinue
if ($rootInstallers) {
    $names = ($rootInstallers | Select-Object -ExpandProperty Name) -join ', '
    $errors.Add("Root-level installer(s) found: $names. Release artifacts must only be under dist/installer.")
}

if (-not (Test-Path $aliasInstallerPath)) {
    $errors.Add("Missing canonical alias installer: $aliasInstallerPath")
}

$issContent = Get-Content -Path $issPath -Raw
Assert-Contains -content $issContent -needle 'AppVersion={#AppVersion}' -errorMessage 'ISS must use AppVersion define: AppVersion={#AppVersion}'
Assert-Contains -content $issContent -needle 'OutputBaseFilename={#OutputBaseFilename}' -errorMessage 'ISS must use OutputBaseFilename define: OutputBaseFilename={#OutputBaseFilename}'

$installScriptContent = Get-Content -Path $installScriptPath -Raw
Assert-Contains -content $installScriptContent -needle 'Get-ProjectVersion' -errorMessage 'install.ps1 must resolve version from csproj via Get-ProjectVersion.'
if ($installScriptContent -match "DisplayVersion\s+-Value\s+'[^']+'") {
    $errors.Add('install.ps1 uses a hardcoded uninstall DisplayVersion; it must be dynamic.')
}

if (Test-Path $releaseJsonPath) {
    $release = Get-Content -Path $releaseJsonPath -Raw | ConvertFrom-Json
    if ($projectVersion -and $release.version -ne $projectVersion) {
        $errors.Add("release.json version ($($release.version)) does not match csproj version ($projectVersion).")
    }

    $versionedPath = Join-Path $distInstallerDir $release.installer_file
    if (-not (Test-Path $versionedPath)) {
        $errors.Add("Versioned installer from release.json is missing: $versionedPath")
    }
} elseif ($RequireReleaseMetadata) {
    $errors.Add("Missing release metadata file: $releaseJsonPath")
}

if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host 'Release verification failed:' -ForegroundColor Red
    foreach ($err in $errors) {
        Write-Host " - $err" -ForegroundColor Red
    }
    exit 1
}

Write-Host 'Release verification passed.' -ForegroundColor Green
Write-Host "Project version: $projectVersion"
Write-Host "Canonical installer: $aliasInstallerPath"
