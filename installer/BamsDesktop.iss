#define MyAppName "BAMS Desktop"
#define MyAppVersion "1.0.0"
#define MyAppExeName "bams.desktop.exe"

[Setup]
; Keep this identity stable across updates of the same application.
AppId=Bams.Desktop
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=BAMS
DefaultDirName={localappdata}\Programs\BAMS Desktop
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; This example requires Windows 11 or later.
MinVersion=10.0.22000
OutputDir=..\dist
OutputBaseFilename=BAMS-Desktop-Setup-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
; Paths are relative to this script's directory. Include the entire publish folder.
Source: "..\publish\desktop\win-x64\{#MyAppVersion}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch BAMS Desktop"; Flags: nowait postinstall skipifsilent