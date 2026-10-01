# ClipVault

A Windows clipboard manager built with .NET 8 / WPF. ClipVault keeps a searchable history of everything you copy, lets you organize clips into categories, pin favorites, and protect the vault with a PIN and encryption.

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

## Building the installer
Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php). Run `Build Setup.bat`; output goes to `dist/installer/`. See [SHARE_CLIPVAULT.md](SHARE_CLIPVAULT.md) for the release process.

## License
[MIT](LICENSE)
