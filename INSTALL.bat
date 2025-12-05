@echo off
REM ========================================
REM MindVault MSIX Installer
REM ========================================
REM This script installs the MindVault app and its certificate

setlocal EnableDelayedExpansion

echo.
echo ========================================
echo   MindVault MSIX Installer
echo ========================================
echo.

REM Check if running as Administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires Administrator privileges.
    echo.
    echo Please right-click this file and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

echo [1/4] Checking for required files...
echo.

REM Check for MSIX package
set "MSIX_FILE=MindVault.msix"
if not exist "%MSIX_FILE%" (
    echo ERROR: MindVault.msix not found in current directory
    echo.
    echo Please ensure MindVault.msix is in the same folder as this script.
    echo Current directory: %CD%
    echo.
    pause
    exit /b 1
)
echo    [OK] Found MindVault.msix

REM Check for certificate
set "CERT_FILE=MindVault_Certificate.cer"
if not exist "%CERT_FILE%" (
    echo ERROR: MindVault_Certificate.cer not found in current directory
    echo.
    echo Please ensure MindVault_Certificate.cer is in the same folder as this script.
    echo.
    pause
    exit /b 1
)
echo    [OK] Found MindVault_Certificate.cer
echo.

REM Check if certificate is already installed
echo [2/4] Checking certificate status...
certutil -verifystore TrustedPeople "MindVault" >nul 2>&1
if %errorLevel% equ 0 (
    echo    [OK] Certificate already installed
) else (
    echo    Installing certificate to Trusted People store...
    certutil -addstore TrustedPeople "%CERT_FILE%" >nul 2>&1
    if %errorLevel% neq 0 (
        echo    [ERROR] Failed to install certificate
        echo    Please install MindVault_Certificate.cer manually:
        echo    1. Double-click MindVault_Certificate.cer
        echo    2. Click "Install Certificate"
        echo    3. Select "Local Machine"
        echo    4. Choose "Place all certificates in the following store"
        echo    5. Click "Browse" and select "Trusted People"
        echo    6. Click OK and Finish
        echo.
        pause
        exit /b 1
    )
    echo    [OK] Certificate installed successfully
)
echo.

REM Remove existing installation
echo [3/4] Checking for existing installation...
powershell -Command "Get-AppxPackage -Name '*mindvault*' | Remove-AppxPackage" >nul 2>&1
if %errorLevel% equ 0 (
    echo    [OK] Removed previous installation
) else (
    echo    [OK] No previous installation found
)
echo.

REM Install MSIX package
echo [4/4] Installing MindVault...
powershell -Command "Add-AppxPackage -Path '%MSIX_FILE%'" >nul 2>&1
if %errorLevel% neq 0 (
    echo    [ERROR] Installation failed
    echo.
    echo    Troubleshooting:
    echo    1. Ensure Windows version is 10.0.19041.0 or higher
    echo    2. Verify certificate is trusted
    echo    3. Check if Developer Mode is enabled (Settings ^> Update ^& Security ^> For developers)
    echo.
    pause
    exit /b 1
)

echo    [OK] MindVault installed successfully!
echo.
echo ========================================
echo   Installation Complete!
echo ========================================
echo.
echo MindVault has been installed to your system.
echo.
echo To launch the app:
echo   1. Press the Windows key
echo   2. Type "MindVault"
echo   3. Click the MindVault icon
echo.
echo First-time setup:
echo   - Python will be extracted automatically (may take 1-2 minutes)
echo   - AI features will be available after extraction completes
echo   - Check %%LOCALAPPDATA%%\MindVault\run_log.txt for details
echo.
pause
