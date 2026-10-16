$cert = New-SelfSignedCertificate -Subject "CN=BIMPlusDeveloper" -Type CodeSigningCert -CertStoreLocation "Cert:\CurrentUser\My"

$store1 = New-Object System.Security.Cryptography.X509Certificates.X509Store "Root", "CurrentUser"
$store1.Open("ReadWrite")
$store1.Add($cert)
$store1.Close()

$store2 = New-Object System.Security.Cryptography.X509Certificates.X509Store "TrustedPublisher", "CurrentUser"
$store2.Open("ReadWrite")
$store2.Add($cert)
$store2.Close()

Write-Host "Certificate Thumbprint: $($cert.Thumbprint)"
