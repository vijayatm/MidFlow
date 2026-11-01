#ifndef AppVersion
  #error AppVersion must be supplied by scripts/package-release.ps1.
#endif

#define AppName "MidFlow"
#define AppPublisher "Vijay ATM"
#define AppUrl "https://github.com/vijayatm/MidFlow"
#define AppExeName "MidFlow.App.exe"
#define AppId "{{71D661A7-03CB-4EA4-8DCB-FB4CE6710F32}}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases/latest
DefaultDirName={localappdata}\Programs\MidFlow
DefaultGroupName=MidFlow
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=MidFlow
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no
SetupLogging=yes
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
OutputBaseFilename=MidFlow-Setup-x64
SetupIconFile=..\src\MidFlow.App\MidFlow.ico
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=MidFlow
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MidFlow"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall MidFlow"; Filename: "{uninstallexe}"
Name: "{autodesktop}\MidFlow"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch MidFlow"; Flags: nowait postinstall skipifsilent

[Code]
var
  RemoveUserData: Boolean;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  RemoveUserData := False;
  if DirExists(ExpandConstant('{userappdata}\MouseFlow')) then
    RemoveUserData := MsgBox(
      'Keep your MidFlow settings, profiles, logs, and recovery backups? Choose No to remove them.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDNO;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and RemoveUserData then
    DelTree(ExpandConstant('{userappdata}\MouseFlow'), True, True, True);
end;
