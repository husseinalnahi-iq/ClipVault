@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File ".\installer\install.ps1" -Scope CurrentUser -CreateDesktopShortcut
if %errorlevel% neq 0 (
  echo.
  echo Installation failed.
  pause
  exit /b %errorlevel%
)

echo.
echo ClipVault installed successfully.
pause
