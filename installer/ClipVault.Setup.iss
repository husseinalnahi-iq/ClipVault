#ifndef AppName
  #define AppName "ClipVault"
#endif

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif

#ifndef AppPublisher
  #define AppPublisher "ClipVault"
#endif

#ifndef AppExeName
  #define AppExeName "ClipVault.UI.exe"
#endif

#ifndef PublishDir
  #define PublishDir "..\src\UI\ClipVault.UI\bin\Release\net8.0-windows\win-x64\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist\installer"
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "ClipVault-Setup"
#endif

[Setup]
AppId={{E0DF59AA-8A50-4A5F-A529-6AB7DF5D7F66}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\src\UI\ClipVault.UI\Assets\Clipboard2.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
