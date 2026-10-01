# ClipVault

[![Build & Release](https://github.com/husseinalnahi-iq/ClipVault/actions/workflows/build.yml/badge.svg)](https://github.com/husseinalnahi-iq/ClipVault/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/husseinalnahi-iq/ClipVault)](https://github.com/husseinalnahi-iq/ClipVault/releases/latest)

A Windows clipboard manager built with .NET 8 / WPF. ClipVault keeps a searchable history of everything you copy, lets you organize clips into categories, pin favorites, and protect the vault with a PIN and encryption.

## Install

1. Go to the **[latest release](https://github.com/husseinalnahi-iq/ClipVault/releases/latest)**.
2. Download **`ClipVault-Setup-<version>.exe`** and run it.
   - No admin rights needed — it installs for your user only.
   - No .NET install needed — the runtime is bundled.
3. If Windows SmartScreen shows *"Windows protected your PC"*, click **More info → Run anyway**. (The installer isn't code-signed yet; it's built publicly by [GitHub Actions](https://github.com/husseinalnahi-iq/ClipVault/actions) from this source.)

Prefer no installer? Download **`ClipVault-Portable-<version>-win-x64.zip`**, extract it anywhere, and run `ClipVault.UI.exe`.

To uninstall: **Settings → Apps → Installed apps → ClipVault → Uninstall**.

**Requirements:** Windows 10 or 11, 64-bit.

## Features
- Clipboard history with search, favorites, and categories
- Global hotkey to open the vault and auto-paste into the active window
- PIN lock with auto-lock timeout and encrypted storage
- Duplicate filtering and configurable retention limits
- System tray integration and optional run-at-startup
- Local SQLite storage (`%LOCALAPPDATA%`) — nothing leaves your machine

## Project structure
```
src/
  Core/ClipVault.Core                     Domain entities and abstractions
  Infrastructure/ClipVault.Infrastructure SQLite repositories, encryption, PIN security
  UI/ClipVault.UI                         WPF app (MVVM), services, tray, hotkeys
  UI/ClipVault.Package                    MSIX packaging project
installer/                                Inno Setup script and install/build scripts
.qa/                                      Small QA console harnesses
```

## Build & run
Requirements: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build ClipVault.sln -c Release
dotnet run --project src/UI/ClipVault.UI -c Release
```

## Releasing
Push a version tag (e.g. `git tag v1.0.3 && git push origin v1.0.3`). GitHub Actions builds the installer and portable zip and publishes them as a GitHub Release.

## Building the installer locally
Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php). Run `Build Setup.bat`; output goes to `dist/installer/`. See [SHARE_CLIPVAULT.md](SHARE_CLIPVAULT.md) for the release process.

## License
[MIT](LICENSE)
