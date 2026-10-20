#define MyAppName "BIM Plus⁺"
#define MyAppVersion "1.1.6"
#define MyAppPublisher "Phearak"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{B8466657-6A57-4E28-8B4F-3498965E9A92}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Install to the ProgramData Autodesk Addins folder for all users
DefaultDirName={commonappdata}\Autodesk\Revit\Addins
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Output installer executable name
OutputBaseFilename=BIMPlus_Setup
SetupIconFile=BIM Plus.ico
UninstallDisplayIcon={commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap\BIM Plus.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
WizardImageFile=Resources\wizard_banner.bmp
WizardSmallImageFile=Resources\wizard_small.bmp
PrivilegesRequired=admin

[Types]
Name: "full"; Description: "Full installation (Revit 2026 & Revit 2027)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "revit2026"; Description: "Autodesk Revit 2026 (.NET 8.0)"; Types: full custom
Name: "revit2027"; Description: "Autodesk Revit 2027 (.NET 10.0)"; Types: full custom

[Dirs]
; Ensure standard users have full modify access to AutoPileCap folder so silent background auto-updates work without admin prompt
Name: "{commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap"; Permissions: users-full; Components: revit2026
Name: "{commonappdata}\Autodesk\Revit\Addins\2027\AutoPileCap"; Permissions: users-full; Components: revit2027

[Files]
; --- Revit 2026 (.NET 8.0) ---
Source: "AutoPileCap.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2026"; Components: revit2026; Flags: ignoreversion
Source: "BIM Plus.ico"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap"; Components: revit2026; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\*"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap"; Components: revit2026; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb, Mapping.txt, Obfuscated\*"

; --- Revit 2027 (.NET 10.0) ---
Source: "AutoPileCap.addin"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2027"; Components: revit2027; Flags: ignoreversion
Source: "BIM Plus.ico"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2027\AutoPileCap"; Components: revit2027; Flags: ignoreversion
Source: "bin\Release\net10.0-windows\*"; DestDir: "{commonappdata}\Autodesk\Revit\Addins\2027\AutoPileCap"; Components: revit2027; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb, Mapping.txt, Obfuscated\*"

[Run]
; Patch the .addin files to use the absolute path to the DLL to prevent Revit path resolution failures
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command ""$xml = [xml](Get-Content '{commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap.addin'); $xml.RevitAddIns.AddIn.Assembly = '{commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap\AutoPileCap.dll'; $xml.Save('{commonappdata}\Autodesk\Revit\Addins\2026\AutoPileCap.addin')"""; Components: revit2026; Flags: runhidden
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -Command ""$xml = [xml](Get-Content '{commonappdata}\Autodesk\Revit\Addins\2027\AutoPileCap.addin'); $xml.RevitAddIns.AddIn.Assembly = '{commonappdata}\Autodesk\Revit\Addins\2027\AutoPileCap\AutoPileCap.dll'; $xml.Save('{commonappdata}\Autodesk\Revit\Addins\2027\AutoPileCap.addin')"""; Components: revit2027; Flags: runhidden

[Messages]
SetupAppTitle=Setup - {#MyAppName}
SetupWindowTitle=Setup - {#MyAppName}
WelcomeLabel1=Welcome to {#MyAppName} Setup
WelcomeLabel2=This will install {#MyAppName} for Autodesk Revit 2026 and 2027 on your computer.%n%nIt is recommended that you close Autodesk Revit before continuing.
FinishedHeadingLabel=Completing the {#MyAppName} Setup Wizard
FinishedLabelNoIcons=Setup has finished installing {#MyAppName} on your computer.%n%nYou can now open Autodesk Revit 2026 or 2027 to access the "%1" tab.
FinishedLabel=Setup has finished installing {#MyAppName} on your computer.%n%nYou can now open Autodesk Revit 2026 or 2027 to access the "%1" tab.
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
