#define MyAppName "BIM Plus⁺"
#define MyAppVersion "1.1.5"
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
UninstallDisplayIcon={app}\AutoPileCap\BIM Plus.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
WizardImageFile=Resources\wizard_banner.bmp
WizardSmallImageFile=Resources\wizard_small.bmp
PrivilegesRequired=admin

[Dirs]
; Ensure standard users have full modify access to AutoPileCap folder so silent background auto-updates work without admin prompt
Name: "{app}\AutoPileCap"; Permissions: users-full

[Files]
; Copy the .addin manifest file directly to the Addins\2026 folder
Source: "AutoPileCap.addin"; DestDir: "{app}"; Flags: ignoreversion

; Copy the application logo icon for Control Panel Add/Remove Programs
Source: "BIM Plus.ico"; DestDir: "{app}\AutoPileCap"; Flags: ignoreversion

; Copy all compiled DLLs to a subfolder named AutoPileCap inside the Addins\2026 folder
Source: "bin\Release\net8.0-windows\*"; DestDir: "{app}\AutoPileCap"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb, Mapping.txt, Obfuscated\*"

[Run]
; Patch the .addin file to use the absolute path to the DLL to prevent Revit path resolution failures
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command ""$xml = [xml](Get-Content '{app}\AutoPileCap.addin'); $xml.RevitAddIns.AddIn.Assembly = '{app}\AutoPileCap\AutoPileCap.dll'; $xml.Save('{app}\AutoPileCap.addin')"""; Flags: runhidden

[Messages]
SetupAppTitle=Setup - {#MyAppName}
SetupWindowTitle=Setup - {#MyAppName}
WelcomeLabel1=Welcome to {#MyAppName} Setup
WelcomeLabel2=This will install {#MyAppName} for Autodesk Revit {#RevitVersion} on your computer.%n%nIt is recommended that you close Autodesk Revit before continuing.
FinishedHeadingLabel=Completing the {#MyAppName} Setup Wizard
FinishedLabelNoIcons=Setup has finished installing {#MyAppName} on your computer.%n%nYou can now open Autodesk Revit {#RevitVersion} to access the "%1" tab.
FinishedLabel=Setup has finished installing {#MyAppName} on your computer.%n%nYou can now open Autodesk Revit {#RevitVersion} to access the "%1" tab.
ClickFinish=Click Finish to exit Setup.

[Code]
procedure InitializeWizard;
begin
  WizardForm.Font.Name := 'Segoe UI';
  WizardForm.WelcomeLabel1.Font.Size := 13;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.FinishedHeadingLabel.Font.Size := 13;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
