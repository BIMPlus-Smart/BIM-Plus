param (
    [string]$TargetDll,
    [string]$TargetDir,
    [string]$RevitVersion = "2026"
)

# Copy to Revit Addins folder
$AddinDir = "$env:APPDATA\Autodesk\Revit\Addins\$RevitVersion\AutoPileCap"
$ManifestDir = "$env:APPDATA\Autodesk\Revit\Addins\$RevitVersion"

if (-not (Test-Path $AddinDir)) {
    New-Item -ItemType Directory -Force -Path $AddinDir | Out-Null
}

Write-Host "Copying DLL to $AddinDir (Revit $RevitVersion)"
Copy-Item -Path $TargetDll -Destination $AddinDir -Force

$AddinFile = Join-Path $TargetDir "AutoPileCap.addin"
if (Test-Path $AddinFile) {
    Write-Host "Copying Addin to $ManifestDir"
    Copy-Item -Path $AddinFile -Destination $ManifestDir -Force
}
