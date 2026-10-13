#ifndef MyVersion
  #define MyVersion "0.0.1"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\..\publish\win-x64"
#endif

[Setup]
AppId={{2DCBF442-E0EF-4E52-99F3-815DEBCE4BE1}
AppName=License Log View
AppVersion={#MyVersion}
AppPublisher=MZ-Gen-Labs
AppPublisherURL=https://github.com/MZ-Gen-Labs/license-log-view
DefaultDirName={localappdata}\Programs\License Log View
DefaultGroupName=License Log View
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=LicenseLogView-{#MyVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\LicenseLogView.exe
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\LicenseLogView.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#PayloadDir}\Start-Windows.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PayloadDir}\data\*"; DestDir: "{app}\data"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\License Log View"; Filename: "{app}\LicenseLogView.exe"; Parameters: "serve"; WorkingDir: "{app}"
Name: "{group}\Uninstall License Log View"; Filename: "{uninstallexe}"
Name: "{userdesktop}\License Log View"; Filename: "{app}\LicenseLogView.exe"; Parameters: "serve"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LicenseLogView.exe"; Parameters: "serve"; WorkingDir: "{app}"; Description: "Start License Log View"; Flags: nowait postinstall skipifsilent
