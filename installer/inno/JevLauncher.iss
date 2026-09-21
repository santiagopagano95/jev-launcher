; Jev Launcher - instalador (Inno Setup 6)
; Compilar con: installer\build-inno.ps1

#define MyAppName "Jev Launcher"
#define MyAppPublisher "Jev Launcher"
#define MyAppExeName "JevLauncher.App.exe"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{8E2E4C1A-5B7D-4C2E-9F31-2A6D4E7B9C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\JevLauncher
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=JevLauncher-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\..\src\JevLauncher.App\Assets\jev.ico
MinVersion=10.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\inno-payload\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Jev Launcher"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Jev Launcher"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function HasDotNet10Desktop(): Boolean;
var
  Base: String;
  FindRec: TFindRec;
begin
  Result := False;
  Base := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(Base) then Exit;
  if FindFirst(AddBackslash(Base) + '*', FindRec) then
  try
    repeat
      if Pos('10.', FindRec.Name) = 1 then
      begin
        Result := True;
        Break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not HasDotNet10Desktop() then
  begin
    if MsgBox('Jev Launcher necesita el .NET 10 Desktop Runtime y no lo encontre.' + #13#10 + #13#10 +
              'Queres abrir la pagina de descarga? (podes continuar igual, pero la app no arranca sin el.)',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '',
                SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'JevLauncher');
end;
