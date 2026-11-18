# Package a BAMS Desktop Windows installer

This guide packages the current `bams.desktop` WPF client as a Windows installer using a self-contained .NET 10 publish and Inno Setup. Run all commands on your Windows development computer, from the workspace folder that contains `bams.desktop` and `bams.server`.

The result is `BAMS-Desktop-Setup-1.0.0-win-x64.exe`, with Start menu and optional desktop shortcuts, an uninstall entry, and no separate .NET installation required on the user's PC. The example installs for the current Windows user without administrator privileges. It targets Windows 11 on an x64-compatible system; validate your actual target Windows editions before distributing.

This document supplies commands and a script to save; it does not install Inno Setup, change the API URL, publish the application, or generate an installer automatically.

## 1. Prepare the build computer

Install the .NET 10 SDK and [Inno Setup from its official site](https://jrsoftware.org/inno-setup/downloads). Use the supported current Inno Setup release; the example uses its standard script format. Check the product's license terms for your intended distribution.

Verify the SDK:

```powershell
dotnet --list-sdks
```

The list must contain a `10.0.x` SDK. Neither Visual Studio nor Inno Setup needs to be installed on end-user PCs.

## 2. Set the deployed API address

Edit `bams.desktop/Constants/ApiConstants.cs` before publishing:

```csharp
public const string ServerBaseAddress = "https://api.example.com";
```

Replace `api.example.com` with your working BAMS API domain. The client appends `api/...` endpoint paths, so use the origin without an additional `/api` path here.

The current project reads this compiled constant when constructing its `HttpClient`. Adding a `.env` or `appsettings.json` file to the installer will not override that address. Changing it currently requires rebuilding and redistributing the client.

Check that HTTPS is reachable from Windows:

```powershell
curl.exe -i https://api.example.com/api/account-types
```

An HTTP 401 JSON response is expected without a token for this protected route. It confirms the endpoint responds; it does not verify login or the full application. Fix certificate, proxy, or connection errors before packaging.

The desktop installer contains only the client. It does not install MySQL, the Linux API, or server migrations. Keep database passwords, JWT signing keys, default staff passwords, and `/etc/bams.env` on the VPS.

## 3. Choose a release version and folder layout

This example uses version `1.0.0`. Keep the publish version, installer version, and output filename consistent.

The folder layout after completing the guide will be:

```text
Bank Account Management System/
  bams.desktop/
  bams.server/
  publish/
    desktop/
      win-x64/
        1.0.0/
          bams.desktop.exe
          bams.desktop.dll
          ...all other published files...
  installer/
    BamsDesktop.iss
  dist/
    BAMS-Desktop-Setup-1.0.0-win-x64.exe
```

Use a new empty publish folder for each release. Do not mix Debug output, server output, or files from previous versions into it. Commit a final installer script if you want reproducible packaging; keep generated publish output and installers out of source control according to your repository's artifact policy.

## 4. Publish a self-contained Windows build

Run this as one command:

```powershell
dotnet publish .\bams.desktop\bams.desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=1.0.0 -p:AssemblyVersion=1.0.0.0 -p:FileVersion=1.0.0.0 -o .\publish\desktop\win-x64\1.0.0
```

Stop if publishing reports an error. A self-contained publish includes the runtime and dependencies for the selected platform. The installer will compress the whole folder, so a single application binary is unnecessary. Trimming is explicitly disabled for this WPF build. See [Microsoft's publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/) for the deployment modes.

Verify the output:

```powershell
Test-Path .\publish\desktop\win-x64\1.0.0\bams.desktop.exe
Test-Path .\publish\desktop\win-x64\1.0.0\bams.desktop.dll
Test-Path .\publish\desktop\win-x64\1.0.0\bams.desktop.runtimeconfig.json
```

All should return `True`. Distribute the complete publish output inside the installer, not just the EXE. Fonts under `Themes/Fonts` are embedded WPF resources by the current project, so the installer does not install system fonts.

Self-contained deployments carry their runtime patch version. Rebuild and redistribute when adopting .NET runtime security updates; updating a user's globally installed runtime does not update the copy bundled with this application.

## 5. Test the published application

Open it from the publish folder, rather than Visual Studio:

```powershell
& ".\publish\desktop\win-x64\1.0.0\bams.desktop.exe"
```

Verify login, password change when required, permissions, opening pages, uploads/downloads, and exports against the intended test API. Close the application before compiling or installing an update.

The API currently rate-limits requests and locks an account for five minutes after five incorrect passwords. Respect HTTP 429 responses and avoid repeated password guesses during packaging checks.

The desktop currently writes diagnostic logs to:

```text
%LOCALAPPDATA%\BAMS\Logs\bams-desktop-YYYYMMDD.log
```

Logging is controlled by `Properties["Debug Log"]` in `App.xaml.cs` and is currently enabled. Review that setting for the intended release. Logs can contain operational context, so review them before sharing them for support.

## 6. Create the Inno Setup script

Create a folder and open a new script file:

```powershell
New-Item -ItemType Directory -Force -Path .\installer
notepad .\installer\BamsDesktop.iss
```

Save the following as `installer/BamsDesktop.iss`:

```ini
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
```

The `[Files]` entry recursively packages the publish output, excluding debugging symbols. File paths are relative to the script location, as described in the [Inno Setup Files reference](https://jrsoftware.org/inno-setup/documentation/topic_filessection.htm).

`PrivilegesRequired=lowest` makes this a [per-user installation](https://jrsoftware.org/inno-setup/documentation/topic_setup_privilegesrequired.htm). Installation defaults to `%LOCALAPPDATA%\Programs\BAMS Desktop`; shortcuts and uninstall registration belong to that user. Other Windows users need their own installation. An all-users installation is a separate packaging choice requiring administrator rights and a Program Files destination; choose the scope before distributing a release.

The `win-x64` publish and Inno Setup's [x64-compatible architecture settings](https://jrsoftware.org/inno-setup/documentation/topic_setup_architecturesinstallin64bitmode.htm) must stay aligned. This is not an ARM64-native package or a 32-bit Windows package. If deploying to ARM64 Windows through x64 emulation, verify the complete application on those devices before claiming support.

The current project has no custom application `.ico` configured, so the example does not reference an icon file. To add branding later, add a real `.ico`, configure the project `ApplicationIcon`, republish, and optionally set Inno Setup's `SetupIconFile`. Do not reference a nonexistent icon or license file just to make the installer script appear complete.

## 7. Compile the installer

Open `installer/BamsDesktop.iss` in the Inno Setup Compiler and choose **Build > Compile**. The compiler bundles the files into the `dist` output directory.

Alternatively, use its command-line compiler. Locate the installed executable first; the folder depends on your Inno Setup version and architecture:

```powershell
Get-Command ISCC.exe -ErrorAction SilentlyContinue
Get-ChildItem "C:\Program Files\Inno Setup*\ISCC.exe", "C:\Program Files (x86)\Inno Setup*\ISCC.exe" -ErrorAction SilentlyContinue
```

For example, if installed at the following path:

```powershell
& "C:\Program Files\Inno Setup 7\ISCC.exe" ".\installer\BamsDesktop.iss"
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed." }
```

Use the actual discovered path if different. The [Inno Setup command-line compiler](https://jrsoftware.org/inno-setup/documentation/topic_compilercmdline.htm) returns exit code `0` on success.

Expected output:

```text
dist\BAMS-Desktop-Setup-1.0.0-win-x64.exe
```

## 8. Verify the installer on a clean PC or VM

Use a supported Windows 11 test machine or VM without the .NET SDK and without separately installed .NET 10 runtimes. This checks whether the self-contained package is actually complete.

Verify the following manually; no test files need to be added to the repository:

1. Install using an ordinary Windows account. The example should not require administrator elevation.
2. Launch from the Start menu and optional desktop shortcut.
3. Confirm the client connects to the deployed HTTPS API, not `localhost`.
4. Test login, required password changes, role-based pages, and normal account operations using test data.
5. Confirm fonts, layout, file dialogs, downloads, and exports work outside the development computer.
6. Check that an API outage produces a readable error rather than terminating the application.
7. Install the next test version over the existing installation, with BAMS closed, and confirm one application entry remains.
8. Uninstall using Windows **Settings > Apps > Installed apps**, then verify installed binaries and shortcuts are removed.

The sample script does not delete `%LOCALAPPDATA%\BAMS\Logs` or documents users explicitly exported elsewhere. It does not modify or delete server-side data. Review retained local logs separately if a machine is being reassigned.

## 9. Distribute the installer

Share the generated installer EXE, not the source repository, `bin/Debug`, or only `bams.desktop.exe`.

Generate a checksum for the exact installer you distribute:

```powershell
Get-FileHash .\dist\BAMS-Desktop-Setup-1.0.0-win-x64.exe -Algorithm SHA256
```

If you Authenticode-sign the application or installer, use your organization's signing certificate and approved signing process. Sign the application before compiling it into the installer, and sign the finished installer before calculating the distributed checksum. Keep signing credentials out of the script and repository. The script above does not configure signing; an unsigned test installer may show an unknown-publisher or reputation warning. Signing alone does not guarantee that Windows reputation prompts disappear.

## 10. Package the next version

For version `1.0.1`:

1. Keep `AppId=Bams.Desktop` and the installation scope/directory stable.
2. Publish to a fresh `publish/desktop/win-x64/1.0.1` directory with `Version=1.0.1`, `AssemblyVersion=1.0.1.0`, and `FileVersion=1.0.1.0`.
3. Change `MyAppVersion` in the installer script to `1.0.1`.
4. Compile, verify an upgrade on a test machine, and distribute the new installer.

The [stable Inno Setup AppId](https://jrsoftware.org/inno-setup/documentation/topic_setup_appid.htm) links upgrades to the existing uninstall registration. This script installs over the existing folder; it does not implement an automatic updater or a downgrade policy. Inspect obsolete files when a release removes or renames dependencies; do not add broad deletion rules against a user-writable install folder without reviewing what they might remove.

## Optional: Smaller framework-dependent packages

For centrally managed PCs, you can publish with `--self-contained false` into a separate folder and distribute a smaller installer. The target machine must then have every framework required by the generated `bams.desktop.runtimeconfig.json`.

The current client output references **Microsoft.NETCore.App**, **Microsoft.WindowsDesktop.App**, and **Microsoft.AspNetCore.App** 10.0. The ASP.NET Core framework reference comes through the desktop project's current OpenAPI dependency. The .NET Desktop Runtime alone therefore does not satisfy all current framework-dependent requirements: provision the matching Windows Desktop and ASP.NET Core runtimes, and check `dotnet --list-runtimes` on the target PC. Recheck the generated runtime configuration whenever dependencies change.

For this project's initial installer, use the self-contained approach above to avoid separate runtime provisioning.

## Troubleshooting

| Problem | Check |
| --- | --- |
| Installer compiler reports no matching source files | Script location, publish version, and the relative `[Files]` path. |
| Installed app requests a .NET runtime | Confirm `--self-contained true` and that the installer included the entire intended publish output. |
| App connects to localhost | Change `ApiConstants.ServerBaseAddress`, republish, and recompile the installer. |
| Certificate or connection error | HTTPS certificate validity, DNS, internet access, VPS service, and proxy configuration. |
| HTTP 429 or temporary login lock | Wait for `Retry-After` or the five-minute account lock to expire. |
| New version appears as a second application | Check stable `AppId`, install scope, Windows user, and destination directory. |
| Old files remain after upgrade | Review removed dependencies; the sample does not delete destination-only files. |
| Application exits immediately | Inspect `%LOCALAPPDATA%\BAMS\Logs`, Windows Application event logs, and clean-machine runtime/publish completeness. |
| Icon is generic | No custom application icon is configured in the current project. |
