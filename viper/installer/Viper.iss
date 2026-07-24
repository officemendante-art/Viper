[Setup]
AppName=Viper
AppVersion=2.0
DefaultDirName={autopf}\Viper
DefaultGroupName=Viper
UninstallDisplayIcon={app}\Viper.exe
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=ViperSetup
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Files]
Source: "..\src\Viper\bin\Debug\net10.0-windows\Viper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\Viper\bin\Debug\net10.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\src\Viper\bin\Debug\net10.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{commonappdata}\Viper"; Permissions: everyone-full

[Icons]
Name: "{group}\Viper"; Filename: "{app}\Viper.exe"
Name: "{group}\Uninstall Viper"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Viper.exe"; Description: "Launch Viper Setup"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\Viper"
