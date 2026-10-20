# BIM Plus for Revit 2026 & 2027 - Build and Deployment Script
$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   BIM Plus Plugin - Revit 2026 & 2027 Deployer   " -ForegroundColor Cyan
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

# 1. Build Project (.NET 8.0 for Revit 2026 and .NET 10.0 for Revit 2027)
Write-Host "`n[1/4] Building BIM Plus (Multi-targeting .NET 8.0 & .NET 10.0 Release)..." -ForegroundColor Yellow
try {
    & $DotnetCmd build "AutoPileCap.csproj" -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE."
    }
    Write-Host "Build Succeeded for both Revit 2026 & 2027!" -ForegroundColor Green
} catch {
    Write-Host "Error during build: $_" -ForegroundColor Red
    exit 1
}

# 2. Obfuscate DLLs using Obfuscar (Protects code from dnSpy & ILSpy)
$ObfuscarExe = "$env:USERPROFILE\.nuget\packages\obfuscar\2.2.50\tools\Obfuscar.Console.exe"
if (Test-Path $ObfuscarExe) {
    Write-Host "`n[2/4] Protecting and obfuscating DLLs with Obfuscar..." -ForegroundColor Yellow
    
    # Revit 2026 (.NET 8)
    if (Test-Path "$ScriptDir\obfuscar.xml") {
        Write-Host "Obfuscating Revit 2026 assembly..." -ForegroundColor DarkGray
        & $ObfuscarExe "$ScriptDir\obfuscar.xml"
        $Obf2026 = "$ScriptDir\bin\Release\net8.0-windows\Obfuscated\AutoPileCap.dll"
        if (Test-Path $Obf2026) {
            Copy-Item $Obf2026 -Destination "$ScriptDir\bin\Release\net8.0-windows\AutoPileCap.dll" -Force
            Write-Host "[Obfuscar] Revit 2026 DLL protected successfully!" -ForegroundColor Green
        }
    }

    # Revit 2027 (.NET 10)
    if (Test-Path "$ScriptDir\obfuscar_2027.xml") {
        Write-Host "Obfuscating Revit 2027 assembly..." -ForegroundColor DarkGray
        & $ObfuscarExe "$ScriptDir\obfuscar_2027.xml"
        $Obf2027 = "$ScriptDir\bin\Release\net10.0-windows\Obfuscated\AutoPileCap.dll"
        if (Test-Path $Obf2027) {
            Copy-Item $Obf2027 -Destination "$ScriptDir\bin\Release\net10.0-windows\AutoPileCap.dll" -Force
            Write-Host "[Obfuscar] Revit 2027 DLL protected successfully!" -ForegroundColor Green
        }
    }
}

# 3. Deploy to Revit 2026 and Revit 2027 Addins folders
Write-Host "`n[3/4] Deploying files into Revit 2026 and 2027 Addins folders..." -ForegroundColor Yellow

function Deploy-RevitVersion([string]$revitVer, [string]$targetFramework) {
    $RevitAddinDir = "$env:APPDATA\Autodesk\Revit\Addins\$revitVer"
    $PluginTargetDir = "$RevitAddinDir\AutoPileCap"
    $BinDir = "$ScriptDir\bin\Release\$targetFramework"

    if (!(Test-Path $BinDir)) {
        Write-Host "Skipping Revit $revitVer (no build output at $BinDir)" -ForegroundColor DarkGray
        return
    }

    if (!(Test-Path $RevitAddinDir)) {
        New-Item -ItemType Directory -Path $RevitAddinDir -Force | Out-Null
    }
    if (!(Test-Path $PluginTargetDir)) {
        New-Item -ItemType Directory -Path $PluginTargetDir -Force | Out-Null
    }

    # Copy DLLs & dependencies
    Get-ChildItem "$BinDir\*" -Exclude "Obfuscated" | ForEach-Object {
        $file = $_
        $dest = Join-Path $PluginTargetDir $file.Name
        try {
            Copy-Item $file.FullName -Destination $dest -Force -ErrorAction Stop
        } catch {
            try {
                $uniqueOld = "$($file.BaseName)_temp.old"
                $oldPath = Join-Path $PluginTargetDir $uniqueOld
                if (Test-Path $oldPath) { Remove-Item $oldPath -Force -ErrorAction SilentlyContinue }
                Rename-Item -Path $dest -NewName $uniqueOld -Force -ErrorAction SilentlyContinue
                Copy-Item -Path $file.FullName -Destination $dest -Force
            } catch {
                Write-Host "Note: Could not overwrite $($file.Name) in Revit $revitVer directly. Please restart Revit to reload." -ForegroundColor Yellow
            }
        }
    }

    # Copy and configure .addin manifest
    $ManifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM Plus⁺</Name>
    <Assembly>$PluginTargetDir\AutoPileCap.dll</Assembly>
    <FullClassName>AutoPileCap.App</FullClassName>
    <ClientId>B8466657-6A57-4E28-8B4F-3498965E9A92</ClientId>
    <VendorId>AUTOPCAP</VendorId>
    <VendorDescription>BIM Plus⁺ Plugin for Autodesk Revit $revitVer</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    $ManifestPath = "$RevitAddinDir\AutoPileCap.addin"
    Set-Content -Path $ManifestPath -Value $ManifestContent -Encoding UTF8
    Write-Host "Installed into Revit $revitVer at: $PluginTargetDir" -ForegroundColor Green
}

Deploy-RevitVersion -revitVer "2026" -targetFramework "net8.0-windows"
Deploy-RevitVersion -revitVer "2027" -targetFramework "net10.0-windows"

# 4. Automatically push to GitHub for clients
Write-Host "`n[4/4] Pushing update to GitHub for automatic client updates..." -ForegroundColor Yellow
$gitCmd = "$env:LOCALAPPDATA\GitHubDesktop\app-3.6.7\resources\app\git\cmd\git.exe"
if (-not (Test-Path $gitCmd)) {
    $gitCmd = (Get-ChildItem "$env:LOCALAPPDATA\GitHubDesktop" -Filter "git.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}

if ($gitCmd -and (Test-Path $gitCmd)) {
    try {
        & $gitCmd add -f "bin/Release/net8.0-windows/AutoPileCap.dll" "bin/Release/net10.0-windows/AutoPileCap.dll" "update_info.json"
        & $gitCmd add .
        & $gitCmd commit -m "Auto update for Revit 2026 & 2027 $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -q
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
Write-Host "   Supported versions: Revit 2026 & Revit 2027    " -ForegroundColor White
Write-Host "   Ribbon Tab: [BIM Plus⁺]                        " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
