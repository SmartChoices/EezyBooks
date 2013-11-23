
[Setup]
AppName=Prager Pricing Program
AppVerName=Prager Pricing Program Version 5.2.5 BETA
AppPublisher=Prager, Software
AppPublisherURL=http://www.PragerSoftware.com
AppSupportURL=http://www.PragerSoftware.com
AppUpdatesURL=http://www.PragerSoftware.com
DefaultDirName={pf}\Prager
DefaultGroupName=Prager
AlwaysShowComponentsList=yes
AllowNoIcons=yes
alwaysrestart=yes
LicenseFile=j:\Upload\Prager\Prager License Agreement.rtf
Compression=lzma/max
OutputDir=j:\Upload\Prager
SolidCompression=yes
OutputBaseFilename=PragerPricingSetup-BETA
;AlwaysRestart=yes

[_ISTool]
EnableISX=true

[Code]
 //-------------    Open URL in web browser after uninstall    -------------------
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
ErrorCode: Integer;
begin
if (CurUninstallStep = usPostUninstall) then
  ShellExec('open', 'http://www.pragersoftware.com/feedback.html', '', '', SW_SHOW, ewNoWait, ErrorCode)
end;

var
dotnetRedistPath: string;
downloadNeeded: boolean;
dotNetNeeded: boolean;
memoDependenciesNeeded: string;

procedure isxdl_AddFile(URL, Filename: PChar);
external 'isxdl_AddFile@files:isxdl.dll stdcall';
function isxdl_DownloadFiles(hWnd: Integer): Integer;
external 'isxdl_DownloadFiles@files:isxdl.dll stdcall';
function isxdl_SetOption(Option, Value: PChar): Integer;
external 'isxdl_SetOption@files:isxdl.dll stdcall';


const
dotnetRedistURL = 'http://download.microsoft.com/download/5/6/7/567758a3-759e-473e-bf8f-52154438565a/dotnetfx.exe';
// local system for testing...
// dotnetRedistURL = 'http://192.168.1.1/dotnetfx.exe';

function InitializeSetup(): Boolean;

begin
Result := true;
dotNetNeeded := false;

// Check for required netfx installation
if (not RegKeyExists(HKLM, 'Software\Microsoft\.NETFramework\policy\v2.0')) then begin
   dotNetNeeded := true;
  if (not IsAdminLoggedOn()) then begin
  MsgBox('This application needs the Microsoft .NET Framework to be installed by an Administrator', mbInformation, MB_OK);
  Result := false;
    end else begin
    memoDependenciesNeeded := memoDependenciesNeeded + ' .NET Framework' #13;
    dotnetRedistPath := ExpandConstant('{src}\dotnetfx.exe');
      if not FileExists(dotnetRedistPath) then begin
      dotnetRedistPath := ExpandConstant('{tmp}\dotnetfx.exe');
        if not FileExists(dotnetRedistPath) then begin
        isxdl_AddFile(dotnetRedistURL, dotnetRedistPath);
        downloadNeeded := true;
        end;
      end;
    SetIniString('install', 'dotnetRedist', dotnetRedistPath, ExpandConstant('{tmp}\dep.ini'));
    end;
  end;
end;

function NextButtonClick(CurPage: Integer): Boolean;
var
hWnd: Integer;
ResultCode: Integer;

begin
Result := true;

if CurPage = wpReady then begin

   hWnd := StrToInt(ExpandConstant('{wizardhwnd}'));
   // don't try to init isxdl if it's not needed because it will error on < ie 3
  if downloadNeeded then begin

  isxdl_SetOption('label', 'Downloading Microsoft .NET Framework');
  isxdl_SetOption('description', 'This application needs to install the Microsoft .NET Framework. Please wait while Setup is downloading extra files to your computer.');
    if isxdl_DownloadFiles(hWnd) = 0 then Result := false;
    end;
    if (Result = true) and (dotNetNeeded = true) then begin
      if Exec(ExpandConstant(dotnetRedistPath), '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then begin
      // handle success if necessary; ResultCode contains the exit code
        if not (ResultCode = 0) then begin
        Result := false;
        end;
      end else begin
      // handle failure if necessary; ResultCode contains the error code
      Result := false;
      end;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
s: string;

begin
if memoDependenciesNeeded <> '' then
  s := s + 'Dependencies to install:' + NewLine + memoDependenciesNeeded + NewLine;
  s := s + MemoDirInfo + NewLine + NewLine;

  Result := s
end;

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "j:\C# Applications\Prager Pricing Program\Prager Pricing Program\bin\Release\Prager Pricing Program.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: C:\Program Files\ISTool\isxdl.dll; Flags: dontcopy

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\Prager Pricing Program"; Filename: "{app}\Prager Pricing Program.exe"
Name: "{group}\{cm:UninstallProgram,Prager Pricing Program}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Prager Pricing Program"; Filename: "{app}\Prager Pricing Program.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Prager Pricing Program"; Filename: "{app}\Prager Pricing Program.exe"; Tasks: quicklaunchicon

[Run]
;Filename: "{tmp}\dotnetfx.exe"; Parameters: "/q:a /c:""install /q"""; Check: IsDotNET2Detected(); StatusMsg: "Installing .NET 2.0... this may take a few minutes.";
Filename: "{app}\Prager Pricing Program.exe"; Description: "{cm:LaunchProgram,Prager Pricing Program}"; Flags: nowait postinstall skipifsilent
