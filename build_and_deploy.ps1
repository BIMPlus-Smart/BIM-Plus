# Auto Pile Cap for Revit 2026 - Build and Deployment Script
$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   Auto Pile Cap Plugin - Revit 2026 Deployer    " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# 0. Locate dotnet SDK executable
$DotnetCmd = "dotnet"
if (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet\sdk") {
    $DotnetCmd = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
    $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
} elseif (Test-Path "C:\Program Files\dotnet\sdk") {
    $DotnetCmd = "C:\Program Files\dotnet\dotnet.exe"
}

# 1. Build Project using .NET 8 SDK
Write-Host "`n[1/3] Building AutoPileCap (.NET 8.0 Release)..." -ForegroundColor Yellow
try {
    & $DotnetCmd build "AutoPileCap.csproj" -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE."
    }
    Write-Host "Build Succeeded!" -ForegroundColor Green
} catch {
    Write-Host "Error during build: $_" -ForegroundColor Red
    exit 1
}

# 1.5 Obfuscate DLL using Obfuscar (Protects code from dnSpy & ILSpy)
$ObfuscarExe = "$env:USERPROFILE\.nuget\packages\obfuscar\2.2.50\tools\Obfuscar.Console.exe"
if (Test-Path $ObfuscarExe) {
    Write-Host "`n[Obfuscar] Protecting and obfuscating AutoPileCap.dll..." -ForegroundColor Yellow
    & $ObfuscarExe "$ScriptDir\obfuscar.xml"
    $ObfuscatedDll = "$ScriptDir\bin\Release\net8.0-windows\Obfuscated\AutoPileCap.dll"
    if (Test-Path $ObfuscatedDll) {
        Copy-Item $ObfuscatedDll -Destination "$ScriptDir\bin\Release\net8.0-windows\AutoPileCap.dll" -Force
        Write-Host "[Obfuscar] Protected DLL created and ready for Revit!" -ForegroundColor Green
    }
}

# 2. Prepare Revit 2026 Addins Folder
$RevitAddinDir = "$env:APPDATA\Autodesk\Revit\Addins\2026"
$PluginTargetDir = "$RevitAddinDir\AutoPileCap"

Write-Host "`n[2/3] Setting up Revit 2026 Addins directory..." -ForegroundColor Yellow
if (!(Test-Path $RevitAddinDir)) {
    New-Item -ItemType Directory -Path $RevitAddinDir -Force | Out-Null
    Write-Host "Created: $RevitAddinDir" -ForegroundColor DarkGray
}

if (!(Test-Path $PluginTargetDir)) {
    New-Item -ItemType Directory -Path $PluginTargetDir -Force | Out-Null
    Write-Host "Created: $PluginTargetDir" -ForegroundColor DarkGray
}

# 3. Copy Assemblies and Manifest
Write-Host "`n[3/3] Deploying files into Revit 2026 Addins folder..." -ForegroundColor Yellow

$BinDir = "$ScriptDir\bin\Release\net8.0-windows"
if (!(Test-Path $BinDir)) {
    Write-Error "Could not find build output at $BinDir"
    exit 1
}

# Copy DLLs & dependencies (with safe fallback for running Revit instances)
Get-ChildItem "$BinDir\*" | ForEach-Object {
    $file = $_
    $dest = Join-Path $PluginTargetDir $file.Name
    try {
        Copy-Item $file.FullName -Destination $dest -Force -ErrorAction Stop
    } catch {
        # File is locked by running Revit: rename old file and copy new
        try {
            $uniqueOld = "$($file.BaseName)_temp.old"
            $oldPath = Join-Path $PluginTargetDir $uniqueOld
            if (Test-Path $oldPath) { Remove-Item $oldPath -Force -ErrorAction SilentlyContinue }
            Rename-Item -Path $dest -NewName $uniqueOld -Force -ErrorAction SilentlyContinue
            Copy-Item -Path $file.FullName -Destination $dest -Force
        } catch {
            Write-Host "Note: Could not overwrite $($file.Name) directly. Please restart Revit to reload." -ForegroundColor Yellow
        }
    }
}
Write-Host "Deployed updated assemblies to $PluginTargetDir" -ForegroundColor Green

# Update and copy .addin manifest
$ManifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM Plus⁺</Name>
    <Assembly>$PluginTargetDir\AutoPileCap.dll</Assembly>
    <FullClassName>AutoPileCap.App</FullClassName>
    <ClientId>B8466657-6A57-4E28-8B4F-3498965E9A92</ClientId>
    <VendorId>AUTOPCAP</VendorId>
    <VendorDescription>BIM Plus⁺ Plugin for Autodesk Revit 2026</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

$ManifestPath = "$RevitAddinDir\AutoPileCap.addin"
Set-Content -Path $ManifestPath -Value $ManifestContent -Encoding UTF8
Write-Host "Installed manifest: $ManifestPath" -ForegroundColor Green

# 4. Automatically push to GitHub for clients
Write-Host "`n[4/4] Pushing update to GitHub for automatic client updates..." -ForegroundColor Yellow
$gitCmd = "$env:LOCALAPPDATA\GitHubDesktop\app-3.6.7\resources\app\git\cmd\git.exe"
if (-not (Test-Path $gitCmd)) {
    $gitCmd = (Get-ChildItem "$env:LOCALAPPDATA\GitHubDesktop" -Filter "git.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}

if ($gitCmd -and (Test-Path $gitCmd)) {
    try {
        & $gitCmd add -f "bin/Release/net8.0-windows/AutoPileCap.dll" "update_info.json"
        & $gitCmd add .
        & $gitCmd commit -m "Auto update $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -q
        $pushProc = Start-Process $gitCmd -ArgumentList "push origin main" -NoNewWindow -PassThru
        if ($pushProc.WaitForExit(15000)) {
            if ($pushProc.ExitCode -eq 0) {
                Write-Host "Pushed update to GitHub! Clients will auto-update when opening Revit." -ForegroundColor Green
            } else {
                Write-Host "Note: git push completed with code $($pushProc.ExitCode)." -ForegroundColor Yellow
            }
        } else {
            $pushProc.Kill()
            Write-Host "Note: git push timed out waiting for credentials. Open GitHub Desktop and click 'Push origin'." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Note: Could not push to GitHub: $_" -ForegroundColor Yellow
    }
}

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "   DEPLOYMENT SUCCESSFUL!                         " -ForegroundColor Green
Write-Host "   Start Revit 2026 and look for the tab:        " -ForegroundColor White
Write-Host "   [BIM Plus] -> [Pile Cap Rebar]                " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
