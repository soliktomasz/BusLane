; BusLane Setup Script for Inno Setup
; Generates a Windows installer for the BusLane application

[Setup]
AppName=BusLane
AppVersion={{APP_VERSION}}
AppPublisher=BusLane
AppPublisherURL=https://github.com/tomaszsolik/BusLane
AppSupportURL=https://github.com/tomaszsolik/BusLane/issues
AppUpdatesURL=https://github.com/tomaszsolik/BusLane/releases
DefaultDirName={autopf}\BusLane
DefaultGroupName=BusLane
AllowNoIcons=yes
OutputBaseFilename=BusLane-{{APP_VERSION}}-win-x64-setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\BusLane.exe
ChangesAssociations=yes
DisableDirPage=no
DisableProgramGroupPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BusLane"; Filename: "{app}\BusLane.exe"
Name: "{group}\Uninstall BusLane"; Filename: "{uninstallexe}"
Name: "{autodesktop}\BusLane"; Filename: "{app}\BusLane.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\BusLane"; Filename: "{app}\BusLane.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\BusLane.exe"; Description: "{cm:LaunchProgram,BusLane}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\BusLane"
Type: filesandordirs; Name: "{commonappdata}\BusLane"

[Registry]
Root: HKLM; Subkey: "Software\BusLane"; ValueType: string; ValueName: "Version"; ValueData: "{{APP_VERSION}}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\BusLane"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\BusLane"; ValueType: string; ValueName: "Publisher"; ValueData: "BusLane"; Flags: uninsdeletekey

; File association for .buslane files (if applicable in the future)
; Root: HKCR; Subkey: ".buslane"; ValueType: string; ValueData: "BusLane.File"; Flags: uninsdeletevalue
; Root: HKCR; Subkey: "BusLane.File"; ValueType: string; ValueData: "BusLane Project"; Flags: uninsdeletekey
; Root: HKCR; Subkey: "BusLane.File\DefaultIcon"; ValueType: string; ValueData: "{app}\BusLane.exe,0"
; Root: HKCR; Subkey: "BusLane.File\shell\open\command"; ValueType: string; ValueData: """{app}\BusLane.exe"" ""%1"""
