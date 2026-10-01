@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File ".\installer\uninstall.ps1" -Scope CurrentUser
if %errorlevel% neq 0 (
  echo.
  echo Uninstall failed.
  pause
  exit /b %errorlevel%
)

echo.
echo ClipVault uninstalled successfully.
pause
