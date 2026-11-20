; ============================================================================
;  Student Management System - Windows installer
;  Build:  "E:\SMS-Build\InnoSetup\ISCC.exe" installer.iss
;  Output: <repo>\dist\StudentManagementSystem-Setup.exe
;
;  What it does
;    1. checks .NET Framework 4.8 (cannot run without it)
;    2. installs IIS Express and SQL Server LocalDB when missing (bundled MSIs)
;    3. copies the compiled site (no source files) + launcher + SQL scripts
;    4. creates the StudentSMS database, or keeps an existing one
;    5. writes the connection string / base URL into Web.config
;    6. Start Menu + optional Desktop shortcut, uninstaller, browser launch
; ============================================================================

#define AppName        "Student Management System"
#define AppVersion     "2.0.0"
#define AppPublisher   "Student Information & Academic Management System"
#define AppExeName     "Start-SMS.cmd"

[Setup]
AppId={{3F6B2C11-9A4E-4B7D-8E21-5C4D0A6F1B93}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} installer
DefaultDirName={code:GetDefaultDir}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=StudentManagementSystem-Setup
SetupIconFile=..\build\stage\sms.ico
UninstallDisplayIcon={app}\sms.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Compiled application only - the publish step strips *.cs / *.csproj.
; Compiled application only - the publish step strips *.cs / *.csproj.
Source: "..\build\stage\site\*";        DestDir: "{app}\site";     Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\build\stage\site\bin\*";         DestDir: "{app}\bin";      Flags: ignoreversion recursesubdirs createallsubdirs
; Offline AI: model + runtime live beside the site so AiService resolves
; {app}\AI from either the site folder or the app folder.
Source: "..\build\stage\AI\*";           DestDir: "{app}\AI";       Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\build\stage\tools\SmsSetup.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "..\build\stage\sms.ico";       DestDir: "{app}";          Flags: ignoreversion
Source: "Start-SMS.cmd";                DestDir: "{app}";          Flags: ignoreversion
Source: "Stop-SMS.cmd";                 DestDir: "{app}";          Flags: ignoreversion
Source: "..\StudentManagementSystem\Database\*.sql"; DestDir: "{app}\Database"; Flags: ignoreversion
Source: "..\dist\USER-GUIDE.md"; DestDir: "{app}\docs"; DestName: "USER-GUIDE.md"; Flags: ignoreversion
Source: "..\dist\AI-MODEL-INFO.md"; DestDir: "{app}\docs"; DestName: "AI-MODEL-INFO.md"; Flags: ignoreversion
Source: "..\dist\RELEASE-NOTES.md"; DestDir: "{app}\docs"; DestName: "RELEASE-NOTES.md"; Flags: ignoreversion
Source: "..\dist\BUILD.md"; DestDir: "{app}\docs"; DestName: "BUILD.md"; Flags: ignoreversion
Source: "..\Documentation\07_Deployment_Guide.md"; DestDir: "{app}\docs"; DestName: "DEPLOYMENT.md"; Flags: ignoreversion

; Prerequisites, only extracted when actually needed.
; Source: "..\build\stage\prereq\iisexpress_amd64_en-US.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall onlyifdoesntexist; Check: NeedsIisExpress
; Source: "..\build\stage\prereq\SqlLocalDB.msi";              DestDir: "{tmp}"; Flags: deleteafterinstall onlyifdoesntexist; Check: NeedsLocalDb

[Icons]
Name: "{group}\{#AppName}";                 Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\sms.ico"; Comment: "Start the application"
Name: "{group}\Stop the local web server";  Filename: "{app}\Stop-SMS.cmd"; IconFilename: "{app}\sms.ico"
Name: "{group}\User guide";                 Filename: "{app}\docs\USER-GUIDE.md"
Name: "{group}\AI model information";       Filename: "{app}\docs\AI-MODEL-INFO.md"
Name: "{group}\Uninstall {#AppName}";       Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";           Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\sms.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start {#AppName} now"; Flags: postinstall nowait skipifsilent; Check: NotFailed

[UninstallRun]
Filename: "{app}\Stop-SMS.cmd"; Flags: runhidden waituntilterminated; RunOnceId: "StopSmsServer"

; The database is never dropped by the uninstaller. Only files the setup
; itself generated are removed so the install folder disappears cleanly.
[UninstallDelete]
Type: files; Name: "{app}\port.txt"
Type: dirifempty; Name: "{app}\site"
Type: dirifempty; Name: "{app}"

[InstallDelete]
Type: files; Name: "{app}\site\*.*"
Type: files; Name: "{app}\site\Content\fonts\dm-mono-latin-400-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\dm-mono-latin-500-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\figtree-latin-wght-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\red-hat-display-latin-wght-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\instrument-sans-latin-wght-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\geist-mono-latin-wght-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\archivo-latin-wght-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\schibsted-grotesk-latin-wght-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\space-mono-latin-400-normal.woff2"
Type: files; Name: "{app}\site\Content\fonts\space-mono-latin-700-normal.woff2"
Type: files; Name: "{app}\tools\*.*"
Type: files; Name: "{app}\*.cmd"
Type: files; Name: "{app}\*.ico"
; Delete old Gelato CSS files that must not remain
Type: files; Name: "{app}\site\Content\css\site.css"
Type: files; Name: "{app}\site\Content\css\theme.css"
Type: files; Name: "{app}\site\Content\css\ui.css"
Type: files; Name: "{app}\site\Content\css\motion.css"
Type: files; Name: "{app}\site\Content\css\auth.css"
Type: files; Name: "{app}\site\Content\css\typography.css"
Type: files; Name: "{app}\site\Content\css\validation.css"
Type: files; Name: "{app}\site\Content\css\site.css"
Type: dirifempty; Name: "{app}\site"
Type: dirifempty; Name: "{app}\tools"
Type: dirifempty; Name: "{app}\Database"
Type: dirifempty; Name: "{app}\docs"
Type: dirifempty; Name: "{app}"

[Code]
var
  ChosenPort: Integer;
  SetupFailed: Boolean;

function NotFailed: Boolean;
begin
  Result := not SetupFailed;
end;

{ ---------------------------------------------------------------- defaults }

function GetDefaultDir(Param: string): string;
begin
  { The user asked for everything on E: - fall back to the standard
    program-files location on machines without an E: drive. }
  if DirExists('E:\') then
    Result := 'E:\StudentManagementSystem'
  else
    Result := ExpandConstant('{autopf}\StudentManagementSystem');
end;

function NetFx48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
    Result := Release >= 528040;
  if (not Result) and RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
    Result := Release >= 528040;
end;

function NeedsIisExpress: Boolean;
begin
  Result := not (FileExists(ExpandConstant('{pf}\IIS Express\iisexpress.exe')) or
                 FileExists(ExpandConstant('{pf32}\IIS Express\iisexpress.exe')));
end;

function NeedsLocalDb: Boolean;
begin
  Result := not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions');
end;

{ ------------------------------------------------------------------- port }

function PortInUse(Port: Integer): Boolean;
var
  Code: Integer;
begin
  { findstr exits 0 only when a LISTENING line carries this exact port. }
  Exec(ExpandConstant('{cmd}'),
       '/C netstat -an | findstr LISTENING | findstr /C:":' + IntToStr(Port) + ' "',
       '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := (Code = 0);
end;

function PickPort: Integer;
var
  P: Integer;
  Found: Boolean;
begin
  Result := 8080;
  Found := False;
  P := 8080;
  while (P <= 8099) and (not Found) do
  begin
    if not PortInUse(P) then
    begin
      Result := P;
      Found := True;
    end;
    P := P + 1;
  end;
end;

{ ------------------------------------------------------------- preflight }

function InitializeSetup: Boolean;
var
  Code: Integer;
begin
  Result := True;
  { Stop any running IIS Express for this app before install }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM iisexpress.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);

  if not NetFx48Installed then
  begin
    MsgBox('This application needs Microsoft .NET Framework 4.8.' + #13#10#13#10 +
           'Windows 10 (May 2019 update) and Windows 11 include it. On older builds,' + #13#10 +
           'install it from https://dotnet.microsoft.com/download/dotnet-framework/net48' + #13#10 +
           'and run this setup again.', mbCriticalError, MB_OK);
    Result := False;
  end;
end;

{ --------------------------------------------------------------- install }

function InstallMsi(FileName, Params, What: string): Boolean;
var
  Code: Integer;
begin
  WizardForm.StatusLabel.Caption := 'Installing ' + What + '...';
  Result := Exec(ExpandConstant('{sys}\msiexec.exe'),
                 '/i "' + FileName + '" ' + Params, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
  if not Result then
  begin
    SetupFailed := True;
    MsgBox('Could not install ' + What + ' (exit code ' + IntToStr(Code) + ').' + #13#10#13#10 +
           'Install it manually, then run this setup again.', mbCriticalError, MB_OK);
  end;
end;

function RunTool(Params, What: string): Boolean;
var
  Code: Integer;
begin
  WizardForm.StatusLabel.Caption := What + '...';
  Result := Exec(ExpandConstant('{app}\tools\SmsSetup.exe'), Params, ExpandConstant('{app}'),
                 SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
  if not Result then
  begin
    SetupFailed := True;
    MsgBox(What + ' failed (exit code ' + IntToStr(Code) + ').' + #13#10#13#10 +
           'The installer log shows the exact SQL error. Most often SQL Server LocalDB' + #13#10 +
           'is still starting; run the setup again after a minute.', mbCriticalError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Tmp: string;
begin
  if CurStep <> ssPostInstall then Exit;

  { 1. prerequisites }
  if NeedsIisExpress then
    if not InstallMsi(ExpandConstant('{tmp}\iisexpress_amd64_en-US.msi'), '/qn /norestart', 'IIS Express') then
      RaiseException('IIS Express installation failed.');

  if NeedsLocalDb then
    if not InstallMsi(ExpandConstant('{tmp}\SqlLocalDB.msi'),
                      '/qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES', 'SQL Server LocalDB') then
      RaiseException('SQL Server LocalDB installation failed.');

  { 2. remember the port the launcher should use }
  ChosenPort := PickPort;
  Tmp := ExpandConstant('{app}\port.txt');
  SaveStringToFile(Tmp, IntToStr(ChosenPort), False);

  { 3. database + configuration }
  if not RunTool('createdb -scripts "' + ExpandConstant('{app}\Database') + '"',
                 'Creating the StudentSMS database') then
    RaiseException('Database creation failed.');

  if not RunTool('configure -webconfig "' + ExpandConstant('{app}\site\Web.config') + '" -port ' + IntToStr(ChosenPort),
                 'Configuring the application') then
    RaiseException('Configuration failed.');

  if not RunTool('check', 'Verifying the database') then
    RaiseException('Database verification failed.');
end;




