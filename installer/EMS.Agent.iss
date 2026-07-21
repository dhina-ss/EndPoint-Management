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
#define MyUsageTaskName "EMS App Usage Tracker"
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
// Application usage (foreground-window) tracking cannot run inside the
// Windows Service: services execute in Session 0, which has no desktop and
// cannot observe the interactive user's foreground window. It runs instead
// as a per-user Scheduled Task with a logon trigger, so it executes inside
// the same session as whoever is signed in - the same pattern used by
// legitimate endpoint-management and screen-time tools generally.
//
// The task targets the specific user account the installer is running under
// (the common case: a user or admin runs the installer under their own
// signed-in session). It does not automatically cover a different user later
// signing into the same shared machine - re-run the installer under that
// user's session to add them too.
procedure RegisterUsageTrackerTask();
var
  ResultCode: Integer;
  CurrentUser, TaskAction, CreateParams: string;
begin
  CurrentUser := GetEnv('USERNAME');
  if CurrentUser = '' then
  begin
    Log('Skipping usage-tracker task: could not determine the current username.');
    Exit;
  end;

  TaskAction := '"' + ExpandConstant('{app}\{#MyAppExeName}') + '" --usage-tracker';
  CreateParams :=
    '/Create /TN "{#MyUsageTaskName}" /TR "' + TaskAction + '" ' +
    '/SC ONLOGON /RU "' + CurrentUser + '" /IT /RL LIMITED /F';

  if Exec(ExpandConstant('{sys}\schtasks.exe'), CreateParams, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      // Start it once immediately so the already-active session picks up
      // usage tracking right away, instead of waiting for the next sign-in.
      Exec(ExpandConstant('{sys}\schtasks.exe'), '/Run /TN "{#MyUsageTaskName}"', '',
        SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end
    else
      Log(Format('schtasks /Create for the usage-tracker task exited with code %d.', [ResultCode]));
  end;
end;

procedure RemoveUsageTrackerTask();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\schtasks.exe'), '/End /TN "{#MyUsageTaskName}"', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "{#MyUsageTaskName}" /F', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

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

// Kills any running usage-tracker (or lingering service) process so the exe
// is not locked when files are replaced. The Scheduled Task registration
// itself is left in place; RegisterUsageTrackerTask (below) re-registers
// and restarts it once the new exe is in place.
procedure StopUsageTrackerProcess();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Upgrade path: remove the running service and any tracker process before
// files are replaced; the [Run] section re-creates the service afterwards,
// and RegisterUsageTrackerTask (below) re-registers and restarts the task.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    StopAndDeleteService();
    StopUsageTrackerProcess();
  end;

  if CurStep = ssPostInstall then
    RegisterUsageTrackerTask();
end;

// Uninstall path: stop and remove the service and the scheduled task before
// files are deleted.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopAndDeleteService();
    RemoveUsageTrackerTask();
  end;
end;
