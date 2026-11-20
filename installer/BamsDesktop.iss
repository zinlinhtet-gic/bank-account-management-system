#define MyAppName "BAMS Desktop"
; Override at compile time: ISCC.exe /DMyAppVersion=1.0.1 BamsDesktop.iss
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppId "Bams.Desktop"
#define MyAppExeName "bams.desktop.exe"

[Setup]
; Keep this identity stable across updates of the same application.
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
AppPublisher=BAMS
DefaultDirName={localappdata}\Programs\BAMS Desktop
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Upgrade: reuse previous install folder and task choices; skip folder page.
UsePreviousAppDir=yes
DisableDirPage=auto
UsePreviousTasks=yes
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
; Close running BAMS before overwriting files, relaunch not forced.
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

; Upgrade overwrites program files only. User data lives outside {app}
; (%LOCALAPPDATA%\BAMS\Logs, server-side DB) and is never touched by install,
; upgrade, or uninstall. Do NOT add [InstallDelete]/[UninstallDelete] entries for it.

[Code]
function GetInstalledVersion(var Version: String): Boolean;
var
  Key: String;
begin
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';
  Result := RegQueryStringValue(HKCU, Key, 'DisplayVersion', Version) or
            RegQueryStringValue(HKLM, Key, 'DisplayVersion', Version);
end;

function InitializeSetup(): Boolean;
var
  Installed: String;
  InstalledPacked, NewPacked: Int64;
begin
  Result := True;
  if GetInstalledVersion(Installed) and
     StrToVersion(Installed, InstalledPacked) and
     StrToVersion('{#MyAppVersion}', NewPacked) then
  begin
    { Block downgrade. Same version = repair/reinstall, allowed. }
    if ComparePackedVersion(InstalledPacked, NewPacked) > 0 then
    begin
      if not WizardSilent then
        MsgBox('A newer version of {#MyAppName} (' + Installed + ') is already installed.' + #13#10 +
               'This installer is version {#MyAppVersion}. Setup will exit.', mbInformation, MB_OK);
      Result := False;
    end;
  end;
end;
