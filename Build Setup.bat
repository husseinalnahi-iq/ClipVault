@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File ".\installer\build-setup.ps1" %*
if %errorlevel% neq 0 (
  echo.
  echo Setup build failed.
  pause
  exit /b %errorlevel%
)

powershell -NoProfile -ExecutionPolicy Bypass -File ".\installer\verify-release.ps1" -RequireReleaseMetadata
if %errorlevel% neq 0 (
  echo.
  echo Release verification failed.
  pause
  exit /b %errorlevel%
)

for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$r = Get-Content '.\dist\installer\release.json' -Raw ^| ConvertFrom-Json; Write-Output $r.version"`) do set RELEASE_VERSION=%%i
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$r = Get-Content '.\dist\installer\release.json' -Raw ^| ConvertFrom-Json; Join-Path (Resolve-Path '.\dist\installer').Path $r.installer_file"`) do set RELEASE_PATH=%%i

echo.
echo Setup build completed successfully.
echo Version: %RELEASE_VERSION%
echo Installer: %RELEASE_PATH%
echo Alias: %cd%\dist\installer\ClipVault-Setup.exe
pause
