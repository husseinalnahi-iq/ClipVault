@echo off
setlocal
cd /d "%~dp0"
echo.
echo [Developer-only] Update ClipVault.bat rebuilds from source and requires .NET 8 SDK.
echo End users should update by running the latest installer from dist\installer.
echo.
choice /c YN /m "Continue developer update now?"
if errorlevel 2 exit /b 0

powershell -ExecutionPolicy Bypass -File ".\installer\install.ps1" -Scope CurrentUser -CreateDesktopShortcut
if %errorlevel% neq 0 (
  echo.
  echo Update failed.
  pause
  exit /b %errorlevel%
)
echo.
echo ClipVault updated successfully.
pause
