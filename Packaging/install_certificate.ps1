# MindVault Certificate Installer (PowerShell)
# Run as Administrator

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   MindVault Certificate Installer" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please:" -ForegroundColor Yellow
    Write-Host "1. Right-click on this script" -ForegroundColor Yellow
    Write-Host "2. Select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Check if certificate file exists
$certFile = "MindVault_Certificate.cer"
if (-not (Test-Path $certFile)) {
    Write-Host "ERROR: $certFile not found!" -ForegroundColor Red
    Write-Host "Please make sure you have extracted all files." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Installing certificate to Trusted Root Certification Authorities..." -ForegroundColor Yellow
Write-Host ""

try {
    # Import certificate to Trusted Root store
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFile)
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
    $store.Open("ReadWrite")
    $store.Add($cert)
    $store.Close()
    
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host "  Certificate installed successfully!" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Certificate Details:" -ForegroundColor Cyan
    Write-Host "  Subject: $($cert.Subject)" -ForegroundColor White
    Write-Host "  Issuer: $($cert.Issuer)" -ForegroundColor White
    Write-Host "  Valid Until: $($cert.NotAfter)" -ForegroundColor White
    Write-Host ""
    Write-Host "You can now double-click MindVault.msix" -ForegroundColor Green
    Write-Host "to install the application." -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host "  ERROR: Certificate installation failed!" -ForegroundColor Red
    Write-Host "==========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please try:" -ForegroundColor Yellow
    Write-Host "1. Running this script as Administrator" -ForegroundColor White
    Write-Host "2. Using run_me_first.bat instead" -ForegroundColor White
    Write-Host "3. Installing certificate manually (see INSTALLATION_GUIDE.md)" -ForegroundColor White
    Write-Host ""
}

Read-Host "Press Enter to exit"
