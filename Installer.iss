#define MyAppName "AutoPileCap"
#define MyAppVersion "1.1.3"
#define MyAppPublisher "Phearak"
#define RevitVersion "2026"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{B8466657-6A57-4E28-8B4F-3498965E9A92}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Install to the ProgramData Autodesk Addins folder for all users
DefaultDirName={commonappdata}\Autodesk\Revit\Addins\{#RevitVersion}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Output installer executable name
OutputBaseFilename=BIMPlus_Setup
SetupIconFile=BIM Plus.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Files]
; Copy the .addin manifest file directly to the Addins\2026 folder
Source: "AutoPileCap.addin"; DestDir: "{app}"; Flags: ignoreversion

; Copy all compiled DLLs to a subfolder named AutoPileCap inside the Addins\2026 folder
Source: "bin\Release\net8.0-windows\*"; DestDir: "{app}\{#MyAppName}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb, Mapping.txt, Obfuscated\*"

[Run]
; Patch the .addin file to use the absolute path to the DLL to prevent Revit path resolution failures
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command ""$xml = [xml](Get-Content '{app}\AutoPileCap.addin'); $xml.RevitAddIns.AddIn.Assembly = '{app}\{#MyAppName}\AutoPileCap.dll'; $xml.Save('{app}\AutoPileCap.addin')"""; Flags: runhidden

[Messages]
SetupAppTitle=Setup - {#MyAppName}
SetupWindowTitle=Setup - {#MyAppName}

[Code]
// Check if Revit is running and warn the user
function InitializeSetup(): Boolean;
begin
  Result := True;
  // It's a good practice to ensure Revit is closed before installing plugins
  // so the DLLs aren't locked and the plugin loads fresh when Revit starts.
end;
