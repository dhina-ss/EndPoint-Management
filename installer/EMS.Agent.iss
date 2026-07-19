; ------------------------------------------------------------------
; EMS Agent - Windows Service installer
;
; Build (from the repository root):
;   1. dotnet publish EMS.Agent -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
;   2. "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\EMS.Agent.iss
;   -> installer\output\EMSAgentSetup-<version>.exe
; ------------------------------------------------------------------

#define MyAppName "EMS Agent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Jubilant Enterprises"
#define MyAppExeName "EMS.Agent.exe"
#define MyServiceName "EMSAgent"
#define MyServiceDisplayName "EMS Endpoint Agent"
#define PublishDir "..\EMS.Agent\bin\Release\net8.0\win-x64\publish"

[Setup]
; Never change AppId between versions - it is how upgrades find the install.
AppId={{B7E63F52-1A4D-4E0B-9C2F-6D8A24E7C511}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\EMS Agent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputDir=output
OutputBaseFilename=EMSAgentSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Keep an existing (possibly customized) config on upgrade.
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
; Everything else from the publish folder, minus dev/debug artifacts.
; With a single-file publish this matches nothing, hence skipifsourcedoesntexist.
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
    Excludes: "{#MyAppExeName},appsettings.json,appsettings.Development.json,*.pdb"; \
    Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
; Start Menu shortcut for uninstall only - a service has no UI to launch.
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\{#MyAppExeName}"" start= auto DisplayName= ""{#MyServiceDisplayName}"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""Collects device inventory and heartbeat data for the Endpoint Management System."""; Flags: runhidden
; Restart automatically if the service ever crashes (3 restarts, 1 min apart, reset counter daily).
Filename: "{sys}\sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden

[Code]
procedure StopAndDeleteService();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // sc stop is asynchronous; give the service a moment to release its files.
  Sleep(3000);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyServiceName}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
end;

// Upgrade path: remove the running service before files are replaced;
// the [Run] section re-creates and restarts it afterwards.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopAndDeleteService();
end;

// Uninstall path: stop and remove the service before files are deleted.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopAndDeleteService();
end;
