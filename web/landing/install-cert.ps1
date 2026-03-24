# install-cert.ps1
# Extracts and installs the signing certificate from the MSIX package
# Must be run as Administrator

param(
    [string]$MsixPath = "NabXliffApp-win-x64.msix"
)

if (-not (Test-Path $MsixPath)) {
    Write-Host "File not found: $MsixPath" -ForegroundColor Red
    Write-Host "Usage: .\install-cert.ps1 -MsixPath path\to\NabXliffApp-win-x64.msix"
    exit 1
}

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "This script must be run as Administrator." -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as administrator', then try again."
    exit 1
}

Write-Host "Extracting certificate from $MsixPath..." -ForegroundColor Cyan

# Extract cert from MSIX signature
$cert = (Get-AuthenticodeSignature $MsixPath).SignerCertificate

if (-not $cert) {
    Write-Host "No certificate found in the MSIX package." -ForegroundColor Red
    exit 1
}

Write-Host "Certificate: $($cert.Subject)" -ForegroundColor Gray
Write-Host "Thumbprint:  $($cert.Thumbprint)" -ForegroundColor Gray
Write-Host ""

# Install to Trusted People store (Local Machine)
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "LocalMachine")
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()

Write-Host "Certificate installed to Trusted People store." -ForegroundColor Green
Write-Host "You can now double-click the .msix file to install the app." -ForegroundColor Green
