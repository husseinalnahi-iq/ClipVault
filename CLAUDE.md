# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ClipVault is a Windows-only clipboard manager: .NET 8, WPF + MVVM (CommunityToolkit.Mvvm), SQLite storage, distributed via an Inno Setup installer built by GitHub Actions.

## Commands

```powershell
# Build the app (build the csproj, NOT ClipVault.sln — the sln includes ClipVault.Package.wapproj, which `dotnet build` can't handle)
dotnet build src/UI/ClipVault.UI/ClipVault.UI.csproj -c Release

# Run
dotnet run --project src/UI/ClipVault.UI -c Release

# Build installer locally (needs Inno Setup 6) -> dist/installer/
./installer/build-setup.ps1            # or "Build Setup.bat" (also runs verify-release.ps1)
```

### Tests / QA
There is no unit-test project. QA lives in `.qa/` as console harnesses that print PASS/FAIL:

```powershell
dotnet run --project .qa/RetentionProtectionQa   # drives MainWindowViewModel with in-memory fakes
dotnet run --project .qa/SettingsQa              # WARNING: writes to the REAL %LOCALAPPDATA%\ClipVault DB and toggles the Run-at-startup registry entry
powershell -File .qa/ui_qa.ps1                   # UI Automation smoke test; launches bin/Release/net8.0-windows/ClipVault.UI.exe (build first)
```

To add a test case to RetentionProtectionQa, append a `(name, Func<Task>)` entry to the `tests` list in `Program.cs`.

## Releasing
- Version lives in `src/UI/ClipVault.UI/ClipVault.UI.csproj` (`<Version>`, `<FileVersion>`, `<InformationalVersion>`).
- Push a tag `vX.Y.Z` → `.github/workflows/build.yml` builds on `windows-latest`, runs `installer/build-setup.ps1 -Version X.Y.Z` (self-contained single-file `win-x64` publish + ISCC), and publishes a GitHub Release with `ClipVault-Setup-X.Y.Z.exe`, a portable zip, and `release.json` (SHA256). Pushes to `main`/PRs only build and upload artifacts.
- Release binaries must be built by CI, not locally. `Directory.Build.props` sets `PathMap`/`Deterministic` so compiled output doesn't contain local filesystem paths; keep it.
- `.gitignore` excludes `*.exe`, `*.zip`, `dist/`, `bin/`, `obj/` — never commit installers or build output.
- `Install/Update/Uninstall ClipVault.bat` + `installer/install.ps1` are developer-only build-from-source install flows; end users use the installer from GitHub Releases.

## Architecture

Three layers, dependencies point inward:

- **ClipVault.Core** (`net8.0`): entities (`ClipboardItem`, `Category`), `ClipboardItemType`, and interfaces for repositories (`IClipboardItemRepository`, `ICategoryRepository`, `ISettingsRepository`) and security (`IVaultEncryptionService`, `IPinSecurityService`). No implementations.
- **ClipVault.Infrastructure** (`net8.0`): SQLite repositories (raw `Microsoft.Data.Sqlite`, no ORM), crypto, and `AppDataPaths`. Registered via `AddClipVaultInfrastructure()`.
- **ClipVault.UI** (`net8.0-windows`, WPF + WinForms for the tray icon): Win32-backed services, tray, view models, single `MainWindow`. Registered via `AddClipVaultUiServices()`.

**Startup** (`App.xaml.cs`): builds a `Microsoft.Extensions.Hosting` generic host, runs `SqliteDatabaseInitializer.InitializeAsync()`, resolves `MainWindow` (singleton) and initializes the tray. Closing the window hides to tray; real exit goes through `App.ExitApplication()` (`IsExitRequested`).

**Storage**: `%LOCALAPPDATA%\ClipVault\clipvault.db` (tables `ClipboardItems`, `Categories`, `Settings`) plus an `images\` folder for image clips. There are no migration files — `SqliteDatabaseInitializer` uses `CREATE TABLE IF NOT EXISTS` and then `PRAGMA table_info` checks + `ALTER TABLE ADD COLUMN` (`EnsureClipboardItemsSchemaAsync` / `EnsureCategoriesSchemaAsync`). New columns must be added in both the CREATE statement and the matching Ensure* method so existing user databases upgrade.

**Settings**: key/value rows in the `Settings` table. Keys are constants in `UI/ViewModels/SettingKeys.cs`; the PIN hash is stored under `security.pin.hash` by `PinSecurityService`.

**Security**: the PIN is stored as a PBKDF2-SHA256 hash (210k iterations). Locked clips are encrypted per item with AES-GCM using a key derived from the PIN (`VaultEncryptionService`); salt/nonce/iterations are serialized as JSON into the item's `EncryptionMeta`, ciphertext+tag into `EncryptedPayload`.

**Clipboard capture**: `ClipboardMonitorService` hooks the main window's HWND (`AddClipboardFormatListener` / `WM_CLIPBOARDUPDATE`), fingerprints content to drop duplicates within 700 ms, and raises `ClipboardCaptured`. `GlobalHotkeyService` (default Ctrl+Shift+V, `RegisterHotKey`) and `AutoPasteService` (refocus previous window + send Ctrl+V) use P/Invoke from `Win32/NativeMethods.cs`. `StartupRegistrationService` writes the HKCU Run key.

**MainWindowViewModel** (~1300 lines) owns almost all app logic: item list/filtering per `NavigationTab`, categories, favorites, lock/unlock, settings, hotkey recording, and retention. Retention rule: an item is *protected* if it's a favorite or has a `CategoryId`; `EnforceRetentionAsync` prunes only unprotected items, oldest first, down to the retention limit (100/500/1000). Removing an item's last protection returns it to Recent (`ReturnToRecentIfProtectionRemoved`). `.qa/RetentionProtectionQa` covers this behavior.

`ClipVault.Package` (MSIX `.wapproj`) exists but isn't used by the release pipeline.
