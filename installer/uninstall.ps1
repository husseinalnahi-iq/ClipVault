param(
    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope = 'CurrentUser'
)

$ErrorActionPreference = 'Stop'

$appName = 'ClipVault'

if ($Scope -eq 'AllUsers') {
    $installDir = "C:\Program Files\$appName"
    $startMenuDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
    $uninstallRoot = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
} else {
    $installDir = Join-Path $env:LOCALAPPDATA $appName
    $startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
    $uninstallRoot = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
}

$shortcutPath = Join-Path $startMenuDir "$appName.lnk"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) "$appName.lnk"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

Write-Host "Uninstalling $appName..."

if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
}

if (Test-Path $desktopShortcutPath) {
    Remove-Item $desktopShortcutPath -Force
}

if (Test-Path $runKey) {
    Remove-ItemProperty -Path $runKey -Name $appName -ErrorAction SilentlyContinue
}

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}

$uninstallKey = Join-Path $uninstallRoot $appName
if (Test-Path $uninstallKey) {
    Remove-Item $uninstallKey -Recurse -Force
}

Write-Host "$appName uninstalled."
