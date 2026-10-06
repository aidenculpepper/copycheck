[Setup]
AppId={{B26608E9-B9A1-40C6-974F-C356D0B16CE9}
AppName=CopyCheck
AppVersion=1.0.20
AppPublisher=CopyCheck
DefaultDirName={autopf}\CopyCheck
DisableDirPage=yes
DefaultGroupName=CopyCheck
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
OutputDir=.
OutputBaseFilename=CopyCheckSetup
SetupIconFile=CopyCheck.ico
UninstallDisplayIcon={app}\CopyCheck.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dark
CloseApplications=yes
RestartApplications=no
AppMutex=Local\CopyCheck.Foundation
UninstallDisplayName=CopyCheck
VersionInfoVersion=1.0.20.0

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "CopyCheck.exe"; Flags: dontcopy
Source: "CopyCheck.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "CopyCheck.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "installed.flag"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\CopyCheck"; Filename: "{app}\CopyCheck.exe"; IconFilename: "{app}\CopyCheck.ico"
Name: "{commondesktop}\CopyCheck"; Filename: "{app}\CopyCheck.exe"; IconFilename: "{app}\CopyCheck.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\CopyCheck.exe"; Description: "Launch CopyCheck"; Flags: nowait postinstall skipifsilent runasoriginaluser

Filename: "{app}\CopyCheck.exe"; Flags: nowait runasoriginaluser; Check: AutoUpdate

[Code]
function AutoUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:COPYCHECKAUTO|0}') = '1';
end;

function BootstrapArguments(): String;
begin
  Result := '--install-latest';
  if AutoUpdate() then Result := Result + ' --silent-update';
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile('CopyCheck.exe');
  if not Exec(ExpandConstant('{tmp}\CopyCheck.exe'), BootstrapArguments(), ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
    MsgBox('Could not check GitHub for the latest installer.', mbError, MB_OK);
    Result := False;
    exit;
  end;
  if (ResultCode = 1) and AutoUpdate() then begin
    Result := False;
    exit;
  end;
  if ResultCode = 1 then
    Result := MsgBox('Install the bundled CopyCheck 1.0.20 version instead? It may not be the latest version.', mbConfirmation, MB_YESNO) = IDYES
  else Result := ResultCode = 0;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not Exec(ExpandConstant('{app}\CopyCheck.exe'), '--configure-install', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Could not configure elevated startup. Run Setup again.');
    if ResultCode <> 0 then
      RaiseException('CopyCheck startup configuration failed. See the preceding CopyCheck message for the cause.');
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if not Exec(ExpandConstant('{app}\CopyCheck.exe'), '--remove-install', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
    MsgBox('Could not remove CopyCheck startup tasks. Close CopyCheck and try again.', mbError, MB_OK);
    exit;
  end;
  if ResultCode <> 0 then begin
    MsgBox('Could not remove CopyCheck startup tasks. Try uninstalling again.', mbError, MB_OK);
    exit;
  end;
  Result := True;
end;
