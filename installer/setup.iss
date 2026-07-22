; OrbsWin Inno Setup Script
; Compile with: iscc installer\setup.iss

#define MyAppName "OrbsWin"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "OrbsWin"
#define MyAppExeName "OrbsWin.exe"
#define MyAppURL "https://github.com/nillohitdebnath/OrbsWin"

[Setup]
AppId={{A1B2C3D4-1234-5678-9ABC-ORBSWINAPPID}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; No admin rights required for a per-user tray app
PrivilegesRequired=lowest
OutputDir=..\dist\installer
OutputBaseFilename=OrbsWinSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startupicon"; Description: "&Launch OrbsWin when Windows starts"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "..\dist\OrbsWin.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional: registered here only if user checks "Launch on startup" during install.
; Your in-app Settings toggle should remain the source of truth after install -
; this just seeds the initial state if the user opts in up front.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OrbsWin"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OrbsWin"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up user config on uninstall - ask yourself first if you actually want this;
; many users expect settings to survive an uninstall/reinstall. Commented out by default.
; Type: filesandordirs; Name: "{userappdata}\OrbsWin"