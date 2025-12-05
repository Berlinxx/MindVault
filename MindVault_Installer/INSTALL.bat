@echo off
REM ========================================
REM MindVault MSIX Installer (Safe Mode + Debug)
REM ========================================

setlocal EnableDelayedExpansion

REM Use pushd instead of cd /d for better network/UNC path support
pushd "%~dp0"

echo.
echo ========================================
echo    MindVault MSIX Installer
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

REM ---------------------------------------------------------
REM DYNAMICALLY FIND THE MSIX FILE
REM ---------------------------------------------------------
set "MSIX_FILE="
for %%F in (*.msix) do (
    set "MSIX_FILE=%%F"
    goto :FoundMSIX
)

:FoundMSIX
if not defined MSIX_FILE (
    echo ERROR: No .msix file found in current directory.
    echo.
    echo Please ensure the MindVault MSIX package is in this folder:
    REM Quotes added below to prevent crash in 'Program Files (x86)'
    echo "%CD%"
    echo.
    pause
    exit /b 1
)
echo    [OK] Found package: %MSIX_FILE%

REM ---------------------------------------------------------
REM DYNAMICALLY FIND THE CERTIFICATE FILE
REM ---------------------------------------------------------
set "CERT_FILE="
for %%F in (*.cer) do (
    set "CERT_FILE=%%F"
    goto :FoundCert
)

:FoundCert
if not defined CERT_FILE (
    echo ERROR: No .cer certificate file found in current directory.
    echo.
    echo Please ensure the certificate .cer file is in this folder.
    echo.
    pause
    exit /b 1
)
echo    [OK] Found certificate: %CERT_FILE%
echo.

REM Check if certificate is already installed
echo [2/4] Installing certificate...
echo.

REM Install to Trusted Root Certification Authorities (required for MSIX trust)
echo    Installing to Trusted Root Certification Authorities...
certutil -addstore Root "%CERT_FILE%" >nul 2>&1
if %errorLevel% equ 0 (
    echo    [OK] Certificate installed to Root store
) else (
    certutil -verifystore Root "MindVault" >nul 2>&1
    if %errorLevel% equ 0 (
        echo    [OK] Certificate already in Root store
    ) else (
        echo    [WARNING] Failed to install to Root store
    )
)

REM Also install to Trusted People store (additional security layer)
echo    Installing to Trusted People store...
certutil -addstore TrustedPeople "%CERT_FILE%" >nul 2>&1
if %errorLevel% equ 0 (
    echo    [OK] Certificate installed to TrustedPeople store
) else (
    certutil -verifystore TrustedPeople "MindVault" >nul 2>&1
    if %errorLevel% equ 0 (
        echo    [OK] Certificate already in TrustedPeople store
    ) else (
        echo    [WARNING] Failed to install to TrustedPeople store
    )
)

echo.
echo    [OK] Certificate installation complete
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
echo [4/4] Installing %MSIX_FILE%...
REM Try to install silently first
powershell -Command "Add-AppxPackage -Path '%MSIX_FILE%'" >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo    [ERROR] Silent installation failed. Retrying with details...
    echo    ============================================================
    REM Run again WITHOUT silencing output so the user sees the real error
    powershell -Command "Add-AppxPackage -Path '%MSIX_FILE%'"
    echo    ============================================================
    echo.
    echo    Troubleshooting:
    echo    1. Read the red error message above carefully.
    echo    2. If it says "Certificate chain", the cert is not trusted (try installing manually).
    echo    3. If it says "Dependencies", you might need to install VCLibs.
    echo.
    pause
    exit /b 1
)

echo    [OK] MindVault installed successfully!
echo.
echo ========================================
echo    Installation Complete!
echo ========================================
echo.
echo MindVault has been installed to your system.
echo.
echo To launch the app:
echo    1. Press the Windows key
echo    2. Type "MindVault"
echo    3. Click the MindVault icon
echo.
pause