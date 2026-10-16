param (
    [string]$TargetDll,
    [string]$TargetDir
)

# Code signing removed as per user request to keep the standard unsigned add-in notification
# $cert = Get-ChildItem "Cert:\CurrentUser\My" | Where-Object { $_.Subject -eq "CN=BIMPlusDeveloper" } | Select-Object -First 1
# 
# if ($cert) {
#     Write-Host "Signing DLL with cert..."
#     Set-AuthenticodeSignature -FilePath $TargetDll -Certificate $cert
# } else {
#     Write-Host "Certificate not found, skipping signing."
# }

# Copy to Revit Addins folder
$AddinDir = "$env:APPDATA\Autodesk\Revit\Addins\2026\AutoPileCap"
$ManifestDir = "$env:APPDATA\Autodesk\Revit\Addins\2026"

if (-not (Test-Path $AddinDir)) {
    New-Item -ItemType Directory -Force -Path $AddinDir | Out-Null
}

Write-Host "Copying DLL to $AddinDir"
Copy-Item -Path $TargetDll -Destination $AddinDir -Force

$AddinFile = Join-Path $TargetDir "AutoPileCap.addin"
if (Test-Path $AddinFile) {
    Write-Host "Copying Addin to $ManifestDir"
    Copy-Item -Path $AddinFile -Destination $ManifestDir -Force
}
