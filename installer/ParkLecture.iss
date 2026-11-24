[Setup]
AppId={{A73DF5AF-57E8-4FF1-B041-E1CE17DB236F}
AppName=박주임 강의도구 (ParkLecture)
AppVersion={#AppVersion}
AppPublisher=박주임
AppPublisherURL=https://github.com/kijung4290/park-lecture
AppUpdatesURL=https://github.com/kijung4290/park-lecture/releases
DefaultDirName={localappdata}\Programs\ParkLecture
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
AppMutex=Local\ParkLectureInstalled
CloseApplications=no
SourceDir=..
OutputDir=dist
OutputBaseFilename=ParkLecture-Setup
SetupIconFile=assets\parklecture.ico
UninstallDisplayIcon={app}\ParkLecture.exe
VersionInfoVersion={#AppVersion}.0
WizardStyle=modern

[Languages]
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ko.FrameworkRequired=Microsoft .NET Framework 4.8 이상을 설치한 뒤 다시 실행해 주세요.
en.FrameworkRequired=Install Microsoft .NET Framework 4.8 or later and run Setup again.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "bin\ParkLecture.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\ParkLecture.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\ParkLecture.Updater.exe"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\ParkLecture\Install"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletevalue uninsdeletekeyifempty

[Icons]
Name: "{userprograms}\박주임 강의도구"; Filename: "{app}\ParkLecture.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\박주임 강의도구"; Filename: "{app}\ParkLecture.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ParkLecture.exe"; Description: "{cm:LaunchProgram,ParkLecture}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'ParkLecture';

function InitializeSetup(): Boolean;
var
  FrameworkRelease: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', FrameworkRelease)
    and (FrameworkRelease >= 528040);
  if not Result then
    SuppressibleMsgBox(CustomMessage('FrameworkRequired'), mbError, MB_OK, IDOK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Command: String;
begin
  if (CurStep = ssPostInstall) and RegQueryStringValue(HKCU, RunKey, RunValue, Command) then
    RegWriteStringValue(HKCU, RunKey, RunValue, '"' + ExpandConstant('{app}\ParkLecture.exe') + '"');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if (CurUninstallStep = usPostUninstall) and RegQueryStringValue(HKCU, RunKey, RunValue, Command) then
    if CompareText(Command, '"' + ExpandConstant('{app}\ParkLecture.exe') + '"') = 0 then
      RegDeleteValue(HKCU, RunKey, RunValue);
end;
