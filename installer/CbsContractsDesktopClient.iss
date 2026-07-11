#define AppName "CBS Contracts Desktop Client"
#define AppPublisher "ООО ЦБС"
#define AppVersion GetEnv("CBS_INSTALLER_VERSION")
#if AppVersion == ""
  #define AppVersion "1.0.1-beta"
#endif
#define AppExeName "CbsContractsDesktopClient.exe"
#define PublishDir GetEnv("CBS_INSTALLER_PUBLISH_DIR")
#if PublishDir == ""
  #define PublishDir "..\artifacts\publish\CbsContractsDesktopClient\win-x64"
#endif

[Setup]
AppId={{C9A438E4-7650-4E76-B334-EB8D88905A44}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\CBS\ContractsDesktopClient
DefaultGroupName=CBS Contracts
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=CbsContractsDesktopClient-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\Assets\favicon.ico
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные ярлыки:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\База контрактов"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\favicon.ico"
Name: "{userdesktop}\База контрактов"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\favicon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить Базу контрактов"; Flags: nowait postinstall skipifsilent
