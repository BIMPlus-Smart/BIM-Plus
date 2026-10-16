Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "   BIM Plus - License Key Generator       " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$Salt = [byte[]]@(0x50, 0x68, 0x65, 0x61, 0x72, 0x61, 0x6B, 0x42, 0x49, 0x4D, 0x50, 0x6C, 0x75, 0x73, 0x32, 0x30, 0x32, 0x36, 0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 0x4B, 0x65, 0x79, 0x21, 0x23, 0x24, 0x25, 0x26)

$MachineId = Read-Host "Enter the user's Machine ID (e.g. ABCD-1234-EF56)"
$cleanId = $MachineId.Trim().ToUpper().Replace("-", "").Replace(" ", "")

if ([string]::IsNullOrEmpty($cleanId)) {
    Write-Host "Machine ID cannot be empty." -ForegroundColor Red
    Pause
    exit
}

try {
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = $Salt
    
    $data = [System.Text.Encoding]::UTF8.GetBytes($cleanId)
    $hashBytes = $hmac.ComputeHash($data)
    
    $hex = ($hashBytes[0..5] | ForEach-Object { $_.ToString("X2") }) -join ""
    # Format: XXXX-XXXX-XXXX (exact same length as Machine ID)
    $ActivationCode = "$($hex.Substring(0,4))-$($hex.Substring(4,4))-$($hex.Substring(8,4))"
    
    # Try copying to clipboard automatically
    try {
        Set-Clipboard -Value $ActivationCode
        $copiedNotice = " (Copied to clipboard!)"
    } catch {
        $copiedNotice = ""
    }

    Write-Host ""
    Write-Host ("Generated Activation Code" + $copiedNotice + ":") -ForegroundColor Green
    Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host $ActivationCode -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Send this code back to the user." -ForegroundColor White
} catch {
    Write-Host "[ERROR] Failed to generate code: $_" -ForegroundColor Red
}

Write-Host "=========================================" -ForegroundColor Cyan
Pause
