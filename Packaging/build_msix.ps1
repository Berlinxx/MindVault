# MindVault MSIX Builder (PowerShell)
# Handles paths with spaces and special characters better than batch files

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   MindVault MSIX Package Builder" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$ProjectName = "mindvault"
$PackageName = "MindVault"
$Version = "1.0.0.0"
$Publisher = "CN=MindVault"
$CertName = "MindVault_Certificate"

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

# Change to project root
Set-Location $ProjectRoot
Write-Host "Working directory: $ProjectRoot" -ForegroundColor Yellow
Write-Host ""

# Check if project file exists
if (-not (Test-Path "$ProjectName.csproj")) {
    Write-Host "ERROR: $ProjectName.csproj not found!" -ForegroundColor Red
    Write-Host "Current directory: $(Get-Location)" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 1: Build the project
Write-Host "[1/6] Building project in Release mode..." -ForegroundColor Green
Write-Host ""

$buildResult = dotnet publish -c Release -f net9.0-windows10.0.19041.0 -p:RuntimeIdentifierOverride=win10-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Step 2: Create certificate
Write-Host "[2/6] Creating self-signed certificate..." -ForegroundColor Green
Write-Host ""

$certPath = Join-Path $ProjectRoot "$CertName.pfx"
$cerPath = Join-Path $ProjectRoot "$CertName.cer"

if (-not (Test-Path $certPath)) {
    Write-Host "Creating new certificate..."
    
    try {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher `
            -KeyUsage DigitalSignature -FriendlyName 'MindVault Certificate' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
        
        $password = ConvertTo-SecureString -String 'mindvault123' -Force -AsPlainText
        
        Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $password | Out-Null
        Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
        
        Write-Host "Certificate created successfully!" -ForegroundColor Green
    }
    catch {
        Write-Host "ERROR: Certificate creation failed!" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Yellow
        Read-Host "Press Enter to exit"
        exit 1
    }
} else {
    Write-Host "Certificate already exists: $CertName.pfx" -ForegroundColor Yellow
}

Write-Host ""

# Step 3: Create package manifest
Write-Host "[3/6] Creating package manifest..." -ForegroundColor Green
Write-Host ""

$outputDir = Join-Path $ProjectRoot "MindVaultPackage"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$manifestFile = Join-Path $outputDir "AppxManifest.xml"

$manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <Identity Name="$PackageName"
            Publisher="$Publisher"
            Version="$Version" />

  <Properties>
    <DisplayName>MindVault</DisplayName>
    <PublisherDisplayName>MindVault</PublisherDisplayName>
    <Logo>app_icon.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Applications>
    <Application Id="MindVault"
      Executable="$ProjectName.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="MindVault"
        Description="AI-Powered Flashcard Study App"
        BackgroundColor="transparent"
        Square150x150Logo="app_icon.png"
        Square44x44Logo="app_icon.png">
        <uap:DefaultTile Wide310x150Logo="app_icon.png" />
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

Set-Content -Path $manifestFile -Value $manifestContent
Write-Host "Manifest created: $manifestFile" -ForegroundColor Green
Write-Host ""

# Step 4: Copy build output
Write-Host "[4/6] Copying build output..." -ForegroundColor Green
Write-Host ""

$buildDir = Join-Path $ProjectRoot "bin\Release\net9.0-windows10.0.19041.0\win10-x64"

if (-not (Test-Path $buildDir)) {
    Write-Host "ERROR: Build output not found at $buildDir" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Copy-Item -Path "$buildDir\*" -Destination $outputDir -Recurse -Force

# Copy icon
$iconPath = Join-Path $ProjectRoot "Resources\AppIcon\app_icon.png"
if (Test-Path $iconPath) {
    Copy-Item -Path $iconPath -Destination $outputDir -Force
}

Write-Host "Files copied successfully!" -ForegroundColor Green
Write-Host ""

# Step 5: Create MSIX package
Write-Host "[5/6] Creating MSIX package..." -ForegroundColor Green
Write-Host ""

# Find makeappx.exe
$makeappx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if (-not $makeappx) {
    $sdkVersions = @("10.0.22621.0", "10.0.22000.0", "10.0.19041.0")
    foreach ($ver in $sdkVersions) {
        $path = "C:\Program Files (x86)\Windows Kits\10\bin\$ver\x64\makeappx.exe"
        if (Test-Path $path) {
            $makeappx = $path
            break
        }
    }
}

if (-not $makeappx) {
    Write-Host "ERROR: makeappx.exe not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Windows SDK from:" -ForegroundColor Yellow
    Write-Host "https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/" -ForegroundColor Cyan
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Using: $makeappx" -ForegroundColor Yellow
Write-Host ""

$msixPath = Join-Path $ProjectRoot "$PackageName.msix"
& $makeappx pack /d $outputDir /p $msixPath /nv

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: MSIX package creation failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "MSIX package created: $PackageName.msix" -ForegroundColor Green
Write-Host ""

# Step 6: Sign the package
Write-Host "[6/6] Signing MSIX package..." -ForegroundColor Green
Write-Host ""

$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signtool) {
    $sdkVersions = @("10.0.22621.0", "10.0.22000.0", "10.0.19041.0")
    foreach ($ver in $sdkVersions) {
        $path = "C:\Program Files (x86)\Windows Kits\10\bin\$ver\x64\signtool.exe"
        if (Test-Path $path) {
            $signtool = $path
            break
        }
    }
}

if (-not $signtool) {
    Write-Host "WARNING: signtool.exe not found! Package will not be signed." -ForegroundColor Yellow
    Write-Host "Install Windows SDK to enable signing." -ForegroundColor Yellow
} else {
    Write-Host "Using: $signtool" -ForegroundColor Yellow
    Write-Host ""
    
    & $signtool sign /fd SHA256 /a /f $certPath /p mindvault123 $msixPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Package signed successfully!" -ForegroundColor Green
    } else {
        Write-Host "WARNING: Package signing failed!" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  MSIX Package Created Successfully!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output files:" -ForegroundColor Cyan
Write-Host "  - $PackageName.msix (installer)" -ForegroundColor White
Write-Host "  - $CertName.cer (certificate for end users)" -ForegroundColor White
Write-Host "  - $CertName.pfx (certificate for signing - keep private!)" -ForegroundColor Yellow
Write-Host ""
Write-Host "To distribute:" -ForegroundColor Cyan
Write-Host "  1. Copy $PackageName.msix" -ForegroundColor White
Write-Host "  2. Copy $CertName.cer" -ForegroundColor White
Write-Host "  3. Copy Packaging\run_me_first.bat" -ForegroundColor White
Write-Host ""
Write-Host "Users should:" -ForegroundColor Cyan
Write-Host "  1. Run run_me_first.bat (installs certificate)" -ForegroundColor White
Write-Host "  2. Double-click $PackageName.msix (installs app)" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to exit"
