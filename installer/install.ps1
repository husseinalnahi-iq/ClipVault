param(
    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope = 'CurrentUser',
    [switch]$CreateDesktopShortcut
)

$ErrorActionPreference = 'Stop'

$appName = 'ClipVault'
$projectPath = Join-Path $PSScriptRoot '..\src\UI\ClipVault.UI\ClipVault.UI.csproj'
$projectPath = (Resolve-Path $projectPath).Path

function Get-ProjectVersion([string]$csprojPath) {
    [xml]$xml = Get-Content -Path $csprojPath -Raw
    $value = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Project version not found in $csprojPath. Add <Version> to ClipVault.UI.csproj."
    }
    return $value.Trim()
}

$displayVersion = Get-ProjectVersion -csprojPath $projectPath

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

$dotnetPath = Resolve-DotNetCommand
if (-not $dotnetPath) {
    throw ".NET SDK not found. Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host "Publishing $appName..."
& $dotnetPath publish $projectPath -c Release -r win-x64 --self-contained true `
  -p:Version=$displayVersion -p:InformationalVersion=$displayVersion `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE. Installation aborted."
}

$publishDir = Join-Path (Split-Path $projectPath -Parent) 'bin\Release\net8.0-windows\win-x64\publish'
$publishDir = (Resolve-Path $publishDir).Path

if ($Scope -eq 'AllUsers') {
    $installDir = "C:\Program Files\$appName"
    $startMenuDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
    $uninstallRoot = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
} else {
    $installDir = Join-Path $env:LOCALAPPDATA $appName
    $startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
    $uninstallRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
}

$desktopDir = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $startMenuDir "$appName.lnk"
$desktopShortcutPath = Join-Path $desktopDir "$appName.lnk"
$exePath = Join-Path $installDir 'ClipVault.UI.exe'

Write-Host "Installing to $installDir..."
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $installDir -Recurse -Force

Write-Host 'Creating Start Menu shortcut...'
New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = 'ClipVault clipboard manager'
$shortcut.Save()

if ($CreateDesktopShortcut) {
    Write-Host 'Creating Desktop shortcut...'
    $desktopShortcut = $shell.CreateShortcut($desktopShortcutPath)
    $desktopShortcut.TargetPath = $exePath
    $desktopShortcut.WorkingDirectory = $installDir
    $desktopShortcut.IconLocation = "$exePath,0"
    $desktopShortcut.Description = 'ClipVault clipboard manager'
    $desktopShortcut.Save()
}

$uninstallKey = Join-Path $uninstallRoot $appName
if (-not (Test-Path $uninstallKey)) {
    New-Item -Path $uninstallKey -Force | Out-Null
}

$uninstallScript = Join-Path $installDir 'uninstall.ps1'
$sourceUninstallScript = Join-Path $PSScriptRoot 'uninstall.ps1'
Copy-Item -Path $sourceUninstallScript -Destination $uninstallScript -Force

$uninstallCmd = "powershell -ExecutionPolicy Bypass -File `"$uninstallScript`" -Scope $Scope"

Set-ItemProperty -Path $uninstallKey -Name DisplayName -Value $appName
Set-ItemProperty -Path $uninstallKey -Name Publisher -Value 'ClipVault'
Set-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value $displayVersion
Set-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name UninstallString -Value $uninstallCmd

Write-Host ''
Write-Host "$appName installed successfully."
Write-Host "EXE: $exePath"
Write-Host "Start Menu Shortcut: $shortcutPath"
