; Inno Setup 7 script for ProcX. Compiled by build\publish.ps1, which passes these defines:
;   AppVersion, SourceDir (self-contained publish output), OutputDir, OutputBaseName, Architecture

#ifndef AppVersion
  #error AppVersion must be defined, e.g. ISCC /DAppVersion=0.1.0
#endif
#ifndef SourceDir
  #error SourceDir must be defined (the self-contained publish folder).
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif
#ifndef OutputBaseName
  #define OutputBaseName "ProcX-" + AppVersion + "-setup"
#endif
#ifndef Architecture
  #define Architecture "x64compatible"
#endif

#define AppName "ProcX"
#define AppExe "ProcX.exe"

[Setup]
; Never change AppId: it identifies the installation for upgrades and uninstall.
AppId={{5B8F6F0E-3C6A-4C0B-9D0B-7F4E1B8D2A41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=ProcX contributors
AppPublisherURL=https://github.com/
SetupIconFile=..\..\src\ProcX.App\ProcX.ico
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile={#SourceDir}\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
ArchitecturesAllowed={#Architecture}
ArchitecturesInstallIn64BitMode={#Architecture}
MinVersion=10.0.17763
; Per-machine by default (Program Files); the user may choose a per-user install in the wizard.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The whole self-contained publish folder: the exe, the .NET runtime files, LICENSE and README.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; If ProcX replaces the Windows Task Manager, removing the program would leave Ctrl+Shift+Esc pointing at a
; missing file. This restores the system Task Manager, and only when the replacement points to this copy.
Filename: "{app}\{#AppExe}"; Parameters: "--unregister-replacement-if-ours"; Flags: runhidden skipifdoesntexist; RunOnceId: "RestoreTaskManager"
