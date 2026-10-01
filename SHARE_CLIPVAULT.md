# ClipVault Release and Sharing Guide

## Canonical Release Policy
- Share installer artifacts only from `dist/installer`.
- Do not share any root-level `ClipVault-Setup*.exe`.
- Preferred file to share with users: `dist/installer/ClipVault-Setup.exe` (latest alias).
- Versioned release artifact: `dist/installer/ClipVault-Setup-<version>.exe`.

## What to Share With End Users
- Installer-only update flow for normal users.
- End users should install/update by running the latest installer from `dist/installer`.
- End users should not use `Update ClipVault.bat`.

## Build a Release Installer
1. Install prerequisites:
- .NET 8 SDK
- Inno Setup 6 (`ISCC.exe`)
2. Run `Build Setup.bat` from project root.
3. Collect outputs from `dist/installer`:
- `ClipVault-Setup-<version>.exe` (immutable versioned artifact)
- `ClipVault-Setup.exe` (latest alias)
- `release.json` (version, filename, build timestamp, SHA256)
- `LATEST.txt` (quick text summary)

## Verify Before Sharing
1. Run:
```powershell
powershell -ExecutionPolicy Bypass -File .\installer\verify-release.ps1 -RequireReleaseMetadata
```
2. Confirm:
- No root-level `ClipVault-Setup*.exe` exists.
- `dist/installer/ClipVault-Setup.exe` exists.
- `release.json` version matches `src/UI/ClipVault.UI/ClipVault.UI.csproj` version.

## Hash Verification (Recommended)
Use the SHA256 from `dist/installer/release.json`:
```powershell
Get-FileHash .\dist\installer\ClipVault-Setup.exe -Algorithm SHA256
```
Compare with `release.json.sha256` before publishing.

## Developer-Only Script Install / Update
- `Install ClipVault.bat` and `Update ClipVault.bat` are developer workflows.
- They rebuild from source and require .NET 8 SDK.
- Keep these for local/dev scenarios only, not for normal-user release instructions.
