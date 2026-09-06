; Pulsar Inno Setup script (change 2026-09-05-installer-and-portable-packaging, ADR-026)
;
; Build:  ISCC.exe scripts\installer\pulsar.iss /DAppVersion=1.10.0
;         (or rely on GetVersionNumbersString below reading the published exe)
; Input:  artifacts\publish\stage\Pulsar.exe  (dev.ps1 publish output)
; Output: artifacts\publish\Pulsar-v{AppVersion}-Setup.exe
;
; Upgrade semantics (task 2.2):
;   - Overwrite-install never touches %AppData%\Pulsar (user data lives outside {app}).
;   - Uninstall keeps user data by default and says so in the confirmation text.

#ifndef AppVersion
  #define AppVersion GetVersionNumbersString(AddBackslash(SourcePath) + "..\..\artifacts\publish\stage\Pulsar.exe")
#endif

#define MyAppName "Pulsar"
#define MyAppExeName "Pulsar.exe"

[Setup]
AppId={{8A7C1D24-5F3B-4B6E-9A2C-PULSAR0000001}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} v{#AppVersion}
AppPublisher=Pulsar Lab
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; x64 app on x64 OS -> Program Files (64-bit)
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\artifacts\publish
OutputBaseFilename=Pulsar-v{#AppVersion}-Setup
SetupIconFile=..\..\Pulsar\Pulsar\Pulsar.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
; Keep version comparison strict: never downgrade silently
UsePreviousAppDir=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start {#MyAppName} when Windows starts"; Flags: unchecked

[Files]
Source: "..\..\artifacts\publish\stage\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional autostart task: per-user Run entry pointing at the installed exe
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Nothing to delete: user data in {userappdata}\Pulsar is intentionally preserved.

[Messages]
ConfirmUninstall=Are you sure you want to remove %1?%n%nYour settings and saved credentials in %%AppData%%\Pulsar will be KEPT. Delete that folder manually if you want a full wipe.
