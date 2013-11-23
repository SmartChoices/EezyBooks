; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

[Setup]
AppName=Prager Book Inventory Help File

;  don't forget to change version number -->
AppVerName=Prager Book Inventory Help File
AppPublisher=Prager, Software
AppPublisherURL=http://www.PragerSoftware.com
AppSupportURL=http://www.PragerSoftware.com
AppUpdatesURL=http://www.PragerSoftware.com
DefaultGroupName=Prager
AllowNoIcons=yes
;LicenseFile=D:\Upload\Prager\Prager License Agreement.rtf
Compression=lzma/max
SolidCompression=yes

;---don't allow user to change target directory
UsePreviousAppDir=no
DisableDirPage=yes
DefaultDirName={pf}\Prager
OutputDir=D:\Upload\Prager
;  program name
OutputBaseFilename=PragerInventoryHelp

[Tasks]
;Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
;Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\Program Files\helpMATIC Pro HTML\My Projects\Prager Inventory Program\BookInventoryPgm.chm"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
;Name: "{group}\Prager Book Inventory "; Filename: "{app}\Prager Book Inventory.exe"
;Name: "{group}\{cm:UninstallProgram,Prager Book Inventory }"; Filename: "{uninstallexe}"
;Name: "{userdesktop}\Prager Book Inventory "; Filename: "{app}\Prager Book Inventory.exe"; Tasks: desktopicon
;Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Prager Book Inventory "; Filename: "{app}\Prager Book Inventory.exe"; Tasks: quicklaunchicon

[Run]
;Filename: "{app}\Prager Book Inventory.exe"; Description: "{cm:LaunchProgram,Prager Book Inventory }"; Flags: nowait postinstall skipifsilent

