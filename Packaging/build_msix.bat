@echo off
REM ========================================
REM MindVault MSIX Package Builder
REM ========================================

setlocal enabledelayedexpansion

echo.
echo ==========================================
echo    MindVault MSIX Package Builder
echo ==========================================
echo.

REM Configuration
set "PROJECT_NAME=mindvault"
set "PACKAGE_NAME=MindVault"
set "VERSION=1.0.0.0"
set "PUBLISHER=CN=MindVault"
set "OUTPUT_DIR=MindVaultPackage"
set "CERT_NAME=MindVault_Certificate"

REM Go to project root (one level up from Packaging folder)
cd ..

REM Check if running in project directory
if not exist "%PROJECT_NAME%.csproj" (
    echo ERROR: %PROJECT_NAME%.csproj not found!
    echo Please run this script from the Packaging directory within the project.
    pause
    exit /b 1
)

REM Step 1: Build the project in Release mode
echo [1/6] Building project in Release mode...
echo.
dotnet publish -c Release -f net9.0-windows10.0.19041.0 -p:RuntimeIdentifierOverride=win10-x64

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo.

REM Step 2: Create/verify certificate
echo [2/6] Creating self-signed certificate...
echo.

if not exist "%CERT_NAME%.pfx" (
    echo Creating new certificate...
    
    REM Create certificate using PowerShell
    powershell -Command "& { $cert = New-SelfSignedCertificate -Type Custom -Subject '%PUBLISHER%' -KeyUsage DigitalSignature -FriendlyName 'MindVault Certificate' -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}'); $password = ConvertTo-SecureString -String 'mindvault123' -Force -AsPlainText; Export-PfxCertificate -Cert $cert -FilePath '%CERT_NAME%.pfx' -Password $password; Export-Certificate -Cert $cert -FilePath '%CERT_NAME%.cer' -Type CERT; }"
    
    if !ERRORLEVEL! NEQ 0 (
        echo ERROR: Certificate creation failed!
        pause
        exit /b 1
    )
    
    echo Certificate created successfully!
) else (
    echo Certificate already exists: %CERT_NAME%.pfx
)

echo.

REM Step 3: Create package manifest
echo [3/6] Creating package manifest...
echo.

set "MANIFEST_FILE=%OUTPUT_DIR%\AppxManifest.xml"
mkdir "%OUTPUT_DIR%" 2>nul

(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<Package
echo   xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
echo   xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
echo   xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
echo   IgnorableNamespaces="uap rescap"^>
echo.
echo   ^<Identity Name="%PACKAGE_NAME%"
echo             Publisher="%PUBLISHER%"
echo             Version="%VERSION%" /^>
echo.
echo   ^<Properties^>
echo     ^<DisplayName^>MindVault^</DisplayName^>
echo     ^<PublisherDisplayName^>MindVault^</PublisherDisplayName^>
echo     ^<Logo^>app_icon.png^</Logo^>
echo   ^</Properties^>
echo.
echo   ^<Dependencies^>
echo     ^<TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" /^>
echo   ^</Dependencies^>
echo.
echo   ^<Resources^>
echo     ^<Resource Language="en-us" /^>
echo   ^</Resources^>
echo.
echo   ^<Applications^>
echo     ^<Application Id="MindVault"
echo       Executable="%PROJECT_NAME%.exe"
echo       EntryPoint="Windows.FullTrustApplication"^>
echo       ^<uap:VisualElements
echo         DisplayName="MindVault"
echo         Description="AI-Powered Flashcard Study App"
echo         BackgroundColor="transparent"
echo         Square150x150Logo="app_icon.png"
echo         Square44x44Logo="app_icon.png"^>
echo         ^<uap:DefaultTile Wide310x150Logo="app_icon.png" /^>
echo       ^</uap:VisualElements^>
echo     ^</Application^>
echo   ^</Applications^>
echo.
echo   ^<Capabilities^>
echo     ^<Capability Name="internetClient" /^>
echo     ^<rescap:Capability Name="runFullTrust" /^>
echo   ^</Capabilities^>
echo ^</Package^>
) > "%MANIFEST_FILE%"

echo Manifest created: %MANIFEST_FILE%
echo.

REM Step 4: Copy build output to package directory
echo [4/6] Copying build output...
echo.

set "BUILD_DIR=bin\Release\net9.0-windows10.0.19041.0\win10-x64"

if not exist "%BUILD_DIR%" (
    echo ERROR: Build output not found at %BUILD_DIR%
    pause
    exit /b 1
)

REM Copy all files
xcopy /E /I /Y "%BUILD_DIR%\*" "%OUTPUT_DIR%\" >nul

REM Copy icon
if exist "Resources\AppIcon\app_icon.png" (
    copy /Y "Resources\AppIcon\app_icon.png" "%OUTPUT_DIR%\" >nul
)

echo Files copied successfully!
echo.

REM Step 5: Create MSIX package
echo [5/6] Creating MSIX package...
echo.

REM Find makeappx.exe
set "MAKEAPPX="
for /f "delims=" %%i in ('where makeappx.exe 2^>nul') do set "MAKEAPPX=%%i"

if "%MAKEAPPX%"=="" (
    REM Try Windows SDK paths
    for %%V in (10.0.22621.0 10.0.22000.0 10.0.19041.0) do (
        if exist "C:\Program Files (x86)\Windows Kits\10\bin\%%V\x64\makeappx.exe" (
            set "MAKEAPPX=C:\Program Files (x86)\Windows Kits\10\bin\%%V\x64\makeappx.exe"
            goto :found_makeappx
        )
    )
)

:found_makeappx
if "%MAKEAPPX%"=="" (
    echo ERROR: makeappx.exe not found!
    echo.
    echo Please install Windows SDK from:
    echo https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
    pause
    exit /b 1
)

echo Using: %MAKEAPPX%
echo.

REM Create MSIX
"%MAKEAPPX%" pack /d "%OUTPUT_DIR%" /p "%PACKAGE_NAME%.msix" /nv

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: MSIX package creation failed!
    pause
    exit /b 1
)

echo.
echo MSIX package created: %PACKAGE_NAME%.msix
echo.

REM Step 6: Sign the package
echo [6/6] Signing MSIX package...
echo.

REM Find signtool.exe
set "SIGNTOOL="
for /f "delims=" %%i in ('where signtool.exe 2^>nul') do set "SIGNTOOL=%%i"

if "%SIGNTOOL%"=="" (
    REM Try Windows SDK paths
    for %%V in (10.0.22621.0 10.0.22000.0 10.0.19041.0) do (
        if exist "C:\Program Files (x86)\Windows Kits\10\bin\%%V\x64\signtool.exe" (
            set "SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\%%V\x64\signtool.exe"
            goto :found_signtool
        )
    )
)

:found_signtool
if "%SIGNTOOL%"=="" (
    echo WARNING: signtool.exe not found! Package will not be signed.
    echo Install Windows SDK to enable signing.
) else (
    echo Using: %SIGNTOOL%
    echo.
    
    "%SIGNTOOL%" sign /fd SHA256 /a /f "%CERT_NAME%.pfx" /p mindvault123 "%PACKAGE_NAME%.msix"
    
    if !ERRORLEVEL! EQU 0 (
        echo Package signed successfully!
    ) else (
        echo WARNING: Package signing failed!
    )
)

echo.
echo ==========================================
echo   MSIX Package Created Successfully!
echo ==========================================
echo.
echo Output files:
echo   - %PACKAGE_NAME%.msix (installer)
echo   - %CERT_NAME%.cer (certificate for end users)
echo   - %CERT_NAME%.pfx (certificate for signing - keep private!)
echo.
echo To distribute:
echo   1. Copy %PACKAGE_NAME%.msix
echo   2. Copy %CERT_NAME%.cer
echo   3. Copy run_me_first.bat
echo.
echo Users should:
echo   1. Run run_me_first.bat (installs certificate)
echo   2. Double-click %PACKAGE_NAME%.msix (installs app)
echo.

pause
