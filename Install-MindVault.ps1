# MindVault MSIX Installation Script (PowerShell)
# Requires: Administrator privileges

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MindVault MSIX Installer (PowerShell)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$msixFile = "MindVault.msix"
$certFile = "MindVault_Certificate.cer"
$packageName = "*mindvault*"

# Check for required files
Write-Host "[1/5] Checking for required files..." -ForegroundColor Yellow

if (-not (Test-Path $msixFile)) {
    Write-Host "  [ERROR] $msixFile not found" -ForegroundColor Red
    Write-Host "  Please ensure $msixFile is in the current directory." -ForegroundColor Yellow
    Write-Host "  Current directory: $(Get-Location)" -ForegroundColor Gray
    exit 1
}
Write-Host "  [OK] Found $msixFile" -ForegroundColor Green

if (-not (Test-Path $certFile)) {
    Write-Host "  [ERROR] $certFile not found" -ForegroundColor Red
    Write-Host "  Please ensure $certFile is in the current directory." -ForegroundColor Yellow
    exit 1
}
Write-Host "  [OK] Found $certFile" -ForegroundColor Green

# Get file sizes
$msixSize = [math]::Round((Get-Item $msixFile).Length / 1MB, 2)
Write-Host "  MSIX package size: $msixSize MB" -ForegroundColor Gray
Write-Host ""

# Check and install certificate
Write-Host "[2/5] Checking certificate..." -ForegroundColor Yellow

try {
    $certExists = certutil -verifystore TrustedPeople "MindVault" 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Certificate already installed" -ForegroundColor Green
    } else {
        throw "Certificate not installed"
    }
} catch {
    Write-Host "  Installing certificate..." -ForegroundColor Yellow
    try {
        certutil -addstore TrustedPeople $certFile | Out-Null
        Write-Host "  [OK] Certificate installed successfully" -ForegroundColor Green
    } catch {
        Write-Host "  [ERROR] Failed to install certificate" -ForegroundColor Red
        Write-Host "  Please install $certFile manually:" -ForegroundColor Yellow
        Write-Host "    1. Double-click $certFile" -ForegroundColor Gray
        Write-Host "    2. Click 'Install Certificate'" -ForegroundColor Gray
        Write-Host "    3. Select 'Local Machine'" -ForegroundColor Gray
        Write-Host "    4. Choose 'Trusted People' store" -ForegroundColor Gray
        exit 1
    }
}
Write-Host ""

# Check for existing installation
Write-Host "[3/5] Checking for existing installation..." -ForegroundColor Yellow

$existingApp = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($existingApp) {
    Write-Host "  Found existing version: $($existingApp.Version)" -ForegroundColor Yellow
    Write-Host "  Removing previous installation..." -ForegroundColor Yellow
    try {
        Remove-AppxPackage -Package $existingApp.PackageFullName
        Write-Host "  [OK] Previous version removed" -ForegroundColor Green
    } catch {
        Write-Host "  [WARNING] Failed to remove previous version" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
} else {
    Write-Host "  [OK] No previous installation found" -ForegroundColor Green
}
Write-Host ""

# Check system requirements
Write-Host "[4/5] Verifying system requirements..." -ForegroundColor Yellow

$osVersion = [System.Environment]::OSVersion.Version
$requiredBuild = 19041
if ($osVersion.Build -ge $requiredBuild) {
    Write-Host "  [OK] Windows version: $($osVersion.Major).$($osVersion.Minor) (Build $($osVersion.Build))" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] Windows build $($osVersion.Build) is below minimum ($requiredBuild)" -ForegroundColor Yellow
    Write-Host "  Installation may fail. Please update Windows." -ForegroundColor Yellow
}

$arch = [System.Environment]::GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
if ($arch -eq "AMD64" -or $arch -eq "x64") {
    Write-Host "  [OK] Architecture: x64" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] Architecture: $arch (expected x64)" -ForegroundColor Yellow
}
Write-Host ""

# Install MSIX package
Write-Host "[5/5] Installing MindVault..." -ForegroundColor Yellow

try {
    Add-AppxPackage -Path $msixFile -ErrorAction Stop
    Write-Host "  [OK] Installation successful!" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Installation failed" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Troubleshooting:" -ForegroundColor Yellow
    Write-Host "    - Ensure Developer Mode is enabled (Settings > Update & Security > For developers)" -ForegroundColor Gray
    Write-Host "    - Verify certificate is in Trusted People store" -ForegroundColor Gray
    Write-Host "    - Check if .NET 9.0 runtime is installed" -ForegroundColor Gray
    Write-Host "    - Try running: Get-AppxLog -ActivityID <ID from error>" -ForegroundColor Gray
    exit 1
}
Write-Host ""

# Verify installation
Write-Host "Verifying installation..." -ForegroundColor Yellow
$installedApp = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($installedApp) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Package Details:" -ForegroundColor White
    Write-Host "  Name:           $($installedApp.Name)" -ForegroundColor Gray
    Write-Host "  Version:        $($installedApp.Version)" -ForegroundColor Gray
    Write-Host "  Architecture:   $($installedApp.Architecture)" -ForegroundColor Gray
    Write-Host "  Install Location: $($installedApp.InstallLocation)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To launch MindVault:" -ForegroundColor White
    Write-Host "  1. Press the Windows key" -ForegroundColor Gray
    Write-Host "  2. Type 'MindVault'" -ForegroundColor Gray
    Write-Host "  3. Click the MindVault icon" -ForegroundColor Gray
    Write-Host ""
    Write-Host "First-time setup:" -ForegroundColor White
    Write-Host "  - Python will extract automatically (1-2 minutes)" -ForegroundColor Gray
    Write-Host "  - AI features available after extraction" -ForegroundColor Gray
    Write-Host "  - Check logs: $env:LOCALAPPDATA\MindVault\run_log.txt" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "  [WARNING] Installation verification failed" -ForegroundColor Yellow
    Write-Host "  The app may still be installed. Check Start Menu." -ForegroundColor Gray
}

# Optional: Launch app
Write-Host "Would you like to launch MindVault now? (Y/N): " -NoNewline -ForegroundColor Cyan
$response = Read-Host
if ($response -eq "Y" -or $response -eq "y") {
    Write-Host "Launching MindVault..." -ForegroundColor Yellow
    try {
        # Get the app's executable
        $appId = $installedApp.PackageFamilyName
        Start-Process "shell:AppsFolder\$appId!App"
        Write-Host "[OK] MindVault launched" -ForegroundColor Green
    } catch {
        Write-Host "[INFO] Please launch MindVault manually from Start Menu" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
