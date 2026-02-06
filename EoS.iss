; Setup script for MMA-EoS

#define MyAppId "{1EA841E3-AA98-43A8-80E9-3BB8E4375B2E}"
#define MyAppName "MMA-EoS"
#define MyAppDir "EoS"
#define MyAppVersion GetVersionNumbersString(AddBackslash(SourcePath) + "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\eos.dll")
#define MyAppPublisher "Thomas Chust"
#define MyAppURL "https://chust.org/repos/eos"
#define MyAppSupportURL "https://mma-eos.slack.com/"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppURL}
ArchitecturesInstallIn64BitMode=x64
DefaultDirName={autopf}\{#MyAppDir}
DefaultGroupName={#MyAppName}
LicenseFile=LICENSE.txt
; NOTE: Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ChangesEnvironment=yes
OutputDir=bin
OutputBaseFilename={#MyAppDir}-{#MyAppVersion}
SetupIconFile=ICON.ico
UninstallDisplayIcon={app}\eos.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: addpath; Description: "Add MMA-EoS to the &Path"
Name: default; Description: "Set &Default Database:"
Name: default\SLB21; Description: "Stixrude et al. 2021"; GroupDescription: "Set Default Database:"; Flags: exclusive unchecked
Name: default\HHP13; Description: "Holland et al. 2013"; GroupDescription: "Set Default Database:"; Flags: exclusive unchecked
Name: default\SLB11; Description: "Stixrude et al. 2011"; GroupDescription: "Set Default Database:"; Flags: exclusive
Name: default\SLB08; Description: "Xu et al. 2008"; GroupDescription: "Set Default Database:"; Flags: exclusive unchecked
Name: default\PSN07; Description: "Piazzoni et al. 2007"; GroupDescription: "Set Default Database:"; Flags: exclusive unchecked
Name: default\FSR04; Description: "Fabrichnaya et al. 2004"; GroupDescription: "Set Default Database:"; Flags: exclusive unchecked

[Files]
; NOTE: Don't use "Flags: ignoreversion" on any shared system files
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "ICON.svg"; DestDir: "{app}"; Flags: ignoreversion
Source: "ICON.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "doc\Release\*"; DestDir: "{app}\doc"; Flags: recursesubdirs ignoreversion
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\*"; DestDir: "{app}\bin"; Flags: recursesubdirs ignoreversion; Check: IsX86
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}\bin"; Flags: recursesubdirs ignoreversion; Check: IsX64
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\SLB21.xml"; DestDir: "{app}\bin"; DestName: "default.xml"; Flags: ignoreversion; Tasks: default\SLB21
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\HHP13.xml"; DestDir: "{app}\bin"; DestName: "default.xml"; Flags: ignoreversion; Tasks: default\HHP13
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\SLB11.xml"; DestDir: "{app}\bin"; DestName: "default.xml"; Flags: ignoreversion; Tasks: default\SLB11
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\SLB08.xml"; DestDir: "{app}\bin"; DestName: "default.xml"; Flags: ignoreversion; Tasks: default\SLB08
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\PSN07.xml"; DestDir: "{app}\bin"; DestName: "default.xml"; Flags: ignoreversion; Tasks: default\PSN07
Source: "EoS.CommandLineTool\bin\Release\net8.0-windows\win-x86\publish\FSR04.xml"; DestDir: "{app}\bin"; DestName: "default.xml"; Flags: ignoreversion; Tasks: default\FSR04

[Icons]
IconFilename: "{app}\ICON.ico"; Name: "{group}\Command Prompt for {#MyAppName}"; Filename: "cmd.exe"; Parameters: "/K PATH {app}\bin;%PATH%"; WorkingDir: "%USERPROFILE%"
Name: "{group}\Documentation for {#MyAppName}"; Filename: "{app}\doc\README.html"
Name: "{group}\Discussion Forum for {#MyAppName}"; Filename: "{#MyAppSupportURL}"
Name: "{group}\Source Repository for {#MyAppName}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\doc\README.html"; Description: "Show the Documentation"; Flags: postinstall shellexec skipifsilent

[Code]
const
  SystemEnvironment = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  UserEnvironment = 'Environment';
  SoftwareUninstall = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';

var
  Tasks: String;

// Retrieve system or user Path.
function GetPath(var Path: String): Boolean;
begin
  if IsAdminInstallMode() then
    result := RegQueryStringValue(HKEY_LOCAL_MACHINE, SystemEnvironment, 'Path', Path)
  else
    result := RegQueryStringValue(HKEY_CURRENT_USER, UserEnvironment, 'Path', Path);
end;

// Set system or user Path.
function SetPath(const Path: String): Boolean;
begin
  if IsAdminInstallMode() then
    result := RegWriteExpandStringValue(HKEY_LOCAL_MACHINE, SystemEnvironment, 'Path', Path)
  else
    result := RegWriteExpandStringValue(HKEY_CURRENT_USER, UserEnvironment, 'Path', Path);
end;

// Check whether a directory is contained in the Path.
// Additionally return start position and count of characters.
function IsDirInPath(const DirName: String; const Path: String; var Start, Count: Integer): Boolean;
var
  Stop: Integer;
begin
  result := false;
  Start := Pos(Uppercase(DirName), Uppercase(Path));
  if Start > 0 then
  begin
    Stop := Start + Length(DirName);
    result := (
      ((Start = 1) or (Path[Start - 1] = ';')) and
      ((Stop - 1 = Length(Path)) or (Path[Stop] = ';'))
    );
    if result and (Stop <= Length(Path)) then
      Stop := Stop + 1;
    Count := Stop - Start;
  end;
end;

// Prepend a directory to the user Path, or append it to the system Path, if necessary.
procedure AddDirToPath(const DirName: String);
var
  Path: String;
  Start, Count: Integer;
begin
  if GetPath(Path) then
  begin
    if not IsDirInPath(DirName, Path, Start, Count) then
    begin
      if IsAdminInstallMode() then
      begin
        if Length(Path) > 0 then Path := Path + ';';
        Path := Path + DirName;
      end
      else
      begin
        if Length(Path) > 0 then Insert(';', Path, 0);
        Insert(DirName, Path, 0);
      end;
      if not SetPath(Path) then Log('Path modification failed.');
    end;
  end;
end;

// Remove a directory from the system or user Path, if necessary.
procedure RemoveDirFromPath(const DirName: String);
var
  Path: String;
  Start, Count: Integer;
begin
  if GetPath(Path) then
  begin
    if IsDirInPath(DirName, Path, Start, Count) then
    begin
      Delete(Path, Start, Count);
      if not SetPath(Path) then Log('Path modification failed.');
    end;
  end;
end;

// Check whether a task was selected during installation.
function UninstallIsTaskSelected(const Task: String): Boolean;
var
  Found: Integer;
begin
  Found := Pos(Task, Tasks);
  result := (Found > 0) and ((Found = 1) or (Tasks[Found - 1] = ',')) and ((Found + Length(Task) > Length(Tasks)) or (Tasks[Found + Length(Task)] = ','));
end;

function InitializeUninstall: Boolean;
var
  Success: Boolean;
begin
  result := True;
  if IsAdminInstallMode then
    Success := RegQueryStringValue(HKEY_LOCAL_MACHINE, SoftwareUninstall, 'Inno Setup: Selected Tasks', Tasks)
  else
    Success := RegQueryStringValue(HKEY_CURRENT_USER, SoftwareUninstall, 'Inno Setup: Selected Tasks', Tasks);
  if not Success then
    Log('Failed to determine selected tasks.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('addpath') then
      // Add app directory to Path in post-install step
      AddDirToPath(ExpandConstant('{app}\bin'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if UninstallIsTaskSelected('addpath') then
      // Remove app directory from path in uninstall step
      RemoveDirFromPath(ExpandConstant('{app}\bin'));
  end;
end;
