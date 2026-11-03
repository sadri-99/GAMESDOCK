#define AppVersion "1.9.5"
[Setup]
AppId={{D2DE7436-86BE-4C32-A06E-D1BFDAC14635}
AppName=GamesDock
AppVersion={#AppVersion}
AppVerName=GamesDock v{#AppVersion}
AppPublisher=sadri-99
AppPublisherURL=https://github.com/sadri-99/GAMESDOCK
DefaultDirName={localappdata}\Programs\GamesDock
DisableDirPage=yes
DefaultGroupName=GamesDock
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=GamesDock-Setup-{#AppVersion}
SetupIconFile=..\app\icon.ico
UninstallDisplayIcon={app}\GamesDock.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
CloseApplications=yes
RestartApplications=no
AppMutex=GamesDock_Single_Instance
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
spanish.Startup=Iniciar GamesDock al entrar en Windows
english.Startup=Start GamesDock when signing in to Windows
spanish.Desktop=Crear acceso directo en escritorio
english.Desktop=Create a desktop shortcut
spanish.Launch=Abrir GamesDock
english.Launch=Launch GamesDock
spanish.StartupError=No se ha podido configurar el inicio con Windows. Consulta el archivo dock.log en la carpeta de ajustes de GamesDock.
english.StartupError=Could not configure Windows startup. Check dock.log in the GamesDock settings folder.

[Tasks]
Name: "startup"; Description: "{cm:Startup}"
Name: "desktopicon"; Description: "{cm:Desktop}"; Flags: unchecked

[Files]
Source: "..\app\GamesDock.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\GamesDock"; Filename: "{app}\GamesDock.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\GamesDock"; Filename: "{app}\GamesDock.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\GamesDock.exe"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard;
var
  Config: AnsiString;
  Text: String;
begin
  { Retain existing startup preference when upgrading from portable or installed versions. }
  if LoadStringFromFile(ExpandConstant('{userappdata}\GamesDock\config.json'), Config) then
  begin
    Text := String(Config);
    StringChangeEx(Text, ' ', '', True);
    StringChangeEx(Text, #9, '', True);
    StringChangeEx(Text, #10, '', True);
    StringChangeEx(Text, #13, '', True);
    if Pos('"autostart":false', Text) > 0 then
      WizardSelectTasks('!startup')
    else if Pos('"autostart":true', Text) > 0 then
      WizardSelectTasks('startup');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExitCode: Integer;
  Mode: String;
begin
  if CurStep = ssPostInstall then
  begin
    Mode := 'off';
    if WizardIsTaskSelected('startup') then Mode := 'on';
    if not Exec(ExpandConstant('{app}\GamesDock.exe'), '--configure-startup ' + Mode,
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      RaiseException(CustomMessage('StartupError'));
    if ExitCode <> 0 then RaiseException(CustomMessage('StartupError'));
  end;
end;

procedure StopInstalledDock;
var
  ExitCode, Attempt: Integer;
begin
  if FileExists(ExpandConstant('{app}\GamesDock.exe')) then
  begin
    Exec(ExpandConstant('{app}\GamesDock.exe'), '--shutdown', ExpandConstant('{app}'),
      SW_HIDE, ewWaitUntilTerminated, ExitCode);
    for Attempt := 1 to 100 do
    begin
      if not CheckForMutexes('GamesDock_Single_Instance') then Exit;
      Sleep(100);
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopInstalledDock;
  Result := '';
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
  StopInstalledDock;
  Result := Exec(ExpandConstant('{app}\GamesDock.exe'), '--configure-startup off',
    ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if Result then Result := ExitCode = 0;
  if not Result then
    MsgBox(CustomMessage('StartupError'), mbError, MB_OK);
end;
