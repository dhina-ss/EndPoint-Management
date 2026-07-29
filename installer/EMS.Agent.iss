; ------------------------------------------------------------------
; EMS Agent - Windows Service installer
;
; Build (from the repository root):
;   1. dotnet publish EMS.Agent -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
;   2. "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\EMS.Agent.iss
;   -> installer\output\EMSAgentSetup-<version>.exe
; ------------------------------------------------------------------

#define MyAppName "EMS Agent"
#define MyAppVersion "1.0.6"
#define MyAppPublisher "Dhinakaran Sekar"
#define MyAppExeName "EMS.Agent.exe"
#define MyServiceName "EMSAgent"
#define MyServiceDisplayName "EMS Endpoint Agent"
#define MyUsageTaskName "EMS App Usage Tracker"
; NOTE: the agent targets net8.0-windows (WinForms + GUI subsystem, no console),
; so the publish output lives under net8.0-windows - NOT net8.0. Pointing this at
; the old net8.0 folder silently ships a stale console-subsystem build.
#define PublishDir "..\EMS.Agent\bin\Release\net8.0-windows\win-x64\publish"

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
; Helper used at install time only (see RegisterUsageTrackerTask below);
; not part of the installed application.
Source: "ReencodeUtf16.ps1"; DestDir: "{tmp}"; Flags: dontcopy
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Keep an existing (possibly customized) config on upgrade.
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
; Everything else from the publish folder, minus dev/debug artifacts.
; With a single-file publish this matches nothing, hence skipifsourcedoesntexist.
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
    Excludes: "{#MyAppExeName},appsettings.json,appsettings.Development.json,*.pdb"; \
    Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Dirs]
; Shared state (activation.json, store-unlock.json) lives here. The activation
; and Store-unlock windows run in the user's NON-admin session and must write
; it, while the SYSTEM service reads it - so grant the Users group Modify.
; Without this, a file an elevated run created is read-only to standard users
; and activation fails with "Access to the path ... is denied".
Name: "{commonappdata}\EMS.Agent"; Permissions: users-modify

[Icons]
; The device is dormant until an EMS user signs in, so give them a way to
; re-open the activation window if they close it before signing in.
Name: "{group}\Activate {#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--login"
; When Store gating is enabled for the device, a user runs this to enter the
; EMS admin password and temporarily unlock the Microsoft Store for installs.
Name: "{group}\Unlock Microsoft Store"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--unlock-store"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Reset the data folder's ACL so the Users group can modify EXISTING files too
; (the [Dirs] grant only covers the folder and new children). Fixes an
; activation.json a prior elevated run left owned by Administrators and
; read-only for the signed-in user. S-1-5-32-545 = the built-in Users group.
Filename: "{sys}\icacls.exe"; Parameters: """{commonappdata}\EMS.Agent"" /grant *S-1-5-32-545:(OI)(CI)M /T /C /Q"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\{#MyAppExeName}"" start= auto DisplayName= ""{#MyServiceDisplayName}"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""Collects device inventory and heartbeat data for the Endpoint Management System."""; Flags: runhidden
; Restart automatically if the service ever crashes (3 restarts, 1 min apart, reset counter daily).
Filename: "{sys}\sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden
; Open the activation login window immediately after install. postinstall +
; nowait so setup finishes; skipifsilent so unattended installs are not
; blocked waiting on a person. The service stays dormant until this sign-in.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--login"; Description: "Activate EMS Agent (sign in)"; Flags: postinstall nowait skipifsilent

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
  CurrentUser, CurrentDomain, FullUserId, TaskCommand, XmlContent: string;
  XmlPathAnsi, XmlPath, ReencodeScript: string;
begin
  CurrentUser := GetEnv('USERNAME');
  if CurrentUser = '' then
  begin
    Log('Skipping usage-tracker task: could not determine the current username.');
    Exit;
  end;

  CurrentDomain := GetEnv('USERDOMAIN');
  if CurrentDomain <> '' then
    FullUserId := CurrentDomain + '\' + CurrentUser
  else
    FullUserId := CurrentUser;

  // Built as a Scheduled Task XML definition rather than the flat
  // "schtasks /TR <string>" form: /TR requires the caller to correctly
  // nest quotes around a path that itself contains spaces, which is
  // fragile and shell-dependent. XML's <Command>/<Arguments> are separate
  // elements, so the path never needs quoting at all.
  TaskCommand := ExpandConstant('{app}\{#MyAppExeName}');
  XmlContent :=
    '<?xml version="1.0" encoding="UTF-16"?>' + #13#10 +
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' + #13#10 +
    '  <Triggers>' + #13#10 +
    '    <LogonTrigger>' + #13#10 +
    '      <Enabled>true</Enabled>' + #13#10 +
    '      <UserId>' + FullUserId + '</UserId>' + #13#10 +
    '    </LogonTrigger>' + #13#10 +
    '  </Triggers>' + #13#10 +
    '  <Principals>' + #13#10 +
    '    <Principal id="Author">' + #13#10 +
    '      <UserId>' + FullUserId + '</UserId>' + #13#10 +
    '      <LogonType>InteractiveToken</LogonType>' + #13#10 +
    '      <RunLevel>LeastPrivilege</RunLevel>' + #13#10 +
    '    </Principal>' + #13#10 +
    '  </Principals>' + #13#10 +
    '  <Settings>' + #13#10 +
    '    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' + #13#10 +
    '    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>' + #13#10 +
    '    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' + #13#10 +
    '    <StartWhenAvailable>true</StartWhenAvailable>' + #13#10 +
    '    <AllowStartOnDemand>true</AllowStartOnDemand>' + #13#10 +
    '    <Enabled>true</Enabled>' + #13#10 +
    '    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>' + #13#10 +
    '  </Settings>' + #13#10 +
    '  <Actions Context="Author">' + #13#10 +
    '    <Exec>' + #13#10 +
    '      <Command>' + TaskCommand + '</Command>' + #13#10 +
    '      <Arguments>--usage-tracker</Arguments>' + #13#10 +
    '    </Exec>' + #13#10 +
    '  </Actions>' + #13#10 +
    '</Task>';

  // Pascal Script can only write ANSI/UTF-8 directly; schtasks needs real
  // UTF-16, so write the ANSI draft first, then re-encode it via the
  // bundled helper script before handing it to schtasks.
  XmlPathAnsi := ExpandConstant('{tmp}\EMSUsageTrackerTask.ansi.xml');
  XmlPath := ExpandConstant('{tmp}\EMSUsageTrackerTask.xml');
  SaveStringToFile(XmlPathAnsi, XmlContent, False);

  ExtractTemporaryFile('ReencodeUtf16.ps1');
  ReencodeScript := ExpandConstant('{tmp}\ReencodeUtf16.ps1');
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + ReencodeScript + '" "' + XmlPathAnsi + '" "' + XmlPath + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if Exec(ExpandConstant('{sys}\schtasks.exe'),
       '/Create /TN "{#MyUsageTaskName}" /XML "' + XmlPath + '" /F',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      // Start it once immediately so the already-active session picks up
      // usage tracking right away, instead of waiting for the next sign-in.
      Exec(ExpandConstant('{sys}\schtasks.exe'), '/Run /TN "{#MyUsageTaskName}"', '',
        SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end
    else
      Log(Format('schtasks /Create (XML) for the usage-tracker task exited with code %d.', [ResultCode]));
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

// Deletes leftover retired-exe copies from earlier upgrades (best-effort;
// silently skips any that are still locked by a running old process).
procedure CleanupRetiredExes();
var
  findRec: TFindRec;
begin
  if FindFirst(ExpandConstant('{app}\EMS.Agent.old-*.exe'), findRec) then
  begin
    try
      repeat
        DeleteFile(ExpandConstant('{app}\') + findRec.Name);
      until not FindNext(findRec);
    finally
      FindClose(findRec);
    end;
  end;
end;

// Renames the existing exe aside so the fresh copy can be written even when
// the old exe is still locked by a running service/task. Windows permits
// renaming a running executable (the open handle tracks the file object, not
// its name), so this guarantees the new build actually replaces the old one -
// which a plain overwrite cannot when the file is in use.
procedure RetireOldExe();
var
  target, retired: string;
begin
  target := ExpandConstant('{app}\{#MyAppExeName}');
  if not FileExists(target) then
    Exit;

  retired := ExpandConstant('{app}\EMS.Agent.old-')
    + GetDateTimeString('yyyymmddhhnnss', #0, #0) + '.exe';

  if not RenameFile(target, retired) then
    Log('Could not retire the old exe; the file copy may fail if it is locked.');
end;

procedure StopAndDeleteService();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // sc stop is asynchronous; wait for it to fully release the exe before
  // deleting. A generous wait avoids the copy failing on a locked file
  // (which would leave the OLD build installed).
  Sleep(6000);
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
  // Killing a process is asynchronous too; give Windows time to release the
  // file handle so the file copy that follows can overwrite the exe.
  Sleep(2500);
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
    // Even after stopping, a lingering handle can keep the exe locked; rename
    // it aside so the new build is guaranteed to be written.
    CleanupRetiredExes();
    RetireOldExe();
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
