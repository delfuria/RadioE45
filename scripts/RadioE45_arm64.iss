[Setup]
AppName=RadioE45
AppVersion=0.20
AppPublisher=RadioE45
AppPublisherURL=https://www.radioe45.it
AppSupportURL=https://www.radioe45.it
AppUpdatesURL=https://www.radioe45.it
DefaultDirName={autopf}\RadioE45
DefaultGroupName=RadioE45
AllowNoIcons=yes
UninstallDisplayIcon={app}\RadioE45.exe
OutputDir=installer
OutputBaseFilename=RadioE45_Setup_arm64
SetupIconFile=publish\arm64\appicon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "Crea icona sul {commondesktop}"; GroupDescription: "Icone aggiuntive:"; Flags: unchecked

[Files]
Source: "publish\arm64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RadioE45"; Filename: "{app}\RadioE45.exe"
Name: "{group}\{cm:UninstallProgram,RadioE45}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\RadioE45"; Filename: "{app}\RadioE45.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RadioE45.exe"; Description: "{cm:LaunchProgram,RadioE45}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"