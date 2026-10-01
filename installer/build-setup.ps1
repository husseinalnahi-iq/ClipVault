param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version,
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot '..\src\UI\ClipVault.UI\ClipVault.UI.csproj'
$projectPath = (Resolve-Path $projectPath).Path

$issPath = Join-Path $PSScriptRoot 'ClipVault.Setup.iss'
$issPath = (Resolve-Path $issPath).Path

$publishDir = Join-Path (Split-Path $projectPath -Parent) "bin\$Configuration\net8.0-windows\$Runtime\publish"
$outputDir = Join-Path $PSScriptRoot '..\dist\installer'

function Resolve-DotNetCommand {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        return $dotnet.Source
    }

    $fallbacks = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "${env:ProgramFiles(x86)}\dotnet\dotnet.exe"
    )

    return $fallbacks | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Resolve-IsccCommand([string]$requestedPath) {
    if (-not [string]::IsNullOrWhiteSpace($requestedPath)) {
        if (Test-Path $requestedPath) {
            return (Resolve-Path $requestedPath).Path
        }
        throw "Provided -IsccPath does not exist: $requestedPath"
    }

    $byCommand = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue),
        (Get-Command ISCC -ErrorAction SilentlyContinue)
    ) | Where-Object { $_ -and (Test-Path $_.Source) } | Select-Object -First 1
    if ($byCommand) {
        return $byCommand.Source
    }

    $candidatePaths = New-Object System.Collections.Generic.List[string]
    $candidatePaths.Add("$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe")
    $candidatePaths.Add("$env:ProgramFiles\Inno Setup 6\ISCC.exe")
    $candidatePaths.Add("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")

    $roots = @(
        "$env:ProgramFiles(x86)",
        "$env:ProgramFiles",
        "$env:LOCALAPPDATA\Programs"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path $_) } | Select-Object -Unique

    foreach ($root in $roots) {
        $innoDirs = Get-ChildItem -Path $root -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue
        foreach ($dir in $innoDirs) {
            $candidatePaths.Add((Join-Path $dir.FullName 'ISCC.exe'))
        }
    }

    $registryKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )
    foreach ($key in $registryKeys) {
        $installLocation = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).InstallLocation
        if (-not [string]::IsNullOrWhiteSpace($installLocation)) {
            $candidatePaths.Add((Join-Path $installLocation 'ISCC.exe'))
        }
    }

    $resolved = $candidatePaths | Select-Object -Unique | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($resolved) {
        return (Resolve-Path $resolved).Path
    }

    $searched = ($candidatePaths | Select-Object -Unique | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n - "
    throw @"
Inno Setup compiler (ISCC.exe) not found.
Install Inno Setup 6 first: https://jrsoftware.org/isdl.php

Searched locations:
 - $searched

If ISCC.exe is installed in a custom path, run:
  .\installer\build-setup.ps1 -IsccPath "C:\Path\To\ISCC.exe"
"@
}

function Get-ProjectVersion([string]$csprojPath) {
    [xml]$xml = Get-Content -Path $csprojPath -Raw
    $value = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Project version not found in $csprojPath. Add <Version> to ClipVault.UI.csproj."
    }
    return $value.Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -csprojPath $projectPath
}

if ($Version -notmatch '^[0-9A-Za-z\.\-_]+$') {
    throw "Invalid version '$Version'. Use letters, numbers, dots, hyphens, or underscores."
}

$dotnetPath = Resolve-DotNetCommand
if (-not $dotnetPath) {
    throw ".NET SDK not found. Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host "Publishing ClipVault (version $Version)..."
& $dotnetPath publish $projectPath -c $Configuration -r $Runtime --self-contained true `
    -p:Version=$Version -p:InformationalVersion=$Version `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishDir = (Resolve-Path $publishDir).Path
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$outputDir = (Resolve-Path $outputDir).Path

$isccPath = Resolve-IsccCommand -requestedPath $IsccPath

$versionedBaseFilename = "ClipVault-Setup-$Version"
$versionedSetupPath = Join-Path $outputDir "$versionedBaseFilename.exe"
$aliasSetupPath = Join-Path $outputDir 'ClipVault-Setup.exe'

Write-Host "Compiling setup with ISCC: $isccPath"
& $isccPath "/DPublishDir=$publishDir" "/DAppVersion=$Version" "/DOutputDir=$outputDir" "/DOutputBaseFilename=$versionedBaseFilename" $issPath | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "ISCC compile failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $versionedSetupPath)) {
    throw "Installer build completed but output was not found: $versionedSetupPath"
}

Copy-Item -Path $versionedSetupPath -Destination $aliasSetupPath -Force

$hash = (Get-FileHash -Path $versionedSetupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$builtAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$releaseMetadata = [ordered]@{
    app = 'ClipVault'
    version = $Version
    installer_file = [IO.Path]::GetFileName($versionedSetupPath)
    installer_alias = [IO.Path]::GetFileName($aliasSetupPath)
    sha256 = $hash
    built_at_utc = $builtAtUtc
}

$releaseJsonPath = Join-Path $outputDir 'release.json'
$latestTxtPath = Join-Path $outputDir 'LATEST.txt'
$releaseMetadata | ConvertTo-Json | Set-Content -Path $releaseJsonPath -Encoding UTF8
@(
    "version=$Version"
    "installer_file=$([IO.Path]::GetFileName($versionedSetupPath))"
    "installer_alias=$([IO.Path]::GetFileName($aliasSetupPath))"
    "sha256=$hash"
    "built_at_utc=$builtAtUtc"
) | Set-Content -Path $latestTxtPath -Encoding UTF8

Write-Host ''
Write-Host "Versioned installer: $versionedSetupPath"
Write-Host "Latest installer alias: $aliasSetupPath"
Write-Host "Release metadata: $releaseJsonPath"
