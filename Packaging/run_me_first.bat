@echo off
REM ========================================
REM MindVault - Certificate Installer
REM Run this FIRST before installing MindVault.msix
REM ========================================

echo.
echo ==========================================
echo    MindVault Certificate Installer
echo ==========================================
echo.
echo This will install the security certificate
echo required to run MindVault.
echo.
echo Administrator privileges required!
echo.
pause

REM Check if certificate file exists
if not exist "MindVault_Certificate.cer" (
    echo ERROR: MindVault_Certificate.cer not found!
    echo Please make sure you have extracted all files.
    pause
    exit /b 1
)

echo.
echo Installing certificate to Trusted Root...
echo.

REM Install certificate to Trusted Root Certification Authorities
certutil -addstore "Root" "MindVault_Certificate.cer"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ==========================================
    echo   Certificate installed successfully!
    echo ==========================================
    echo.
    echo You can now double-click MindVault.msix
    echo to install the application.
    echo.
) else (
    echo.
    echo ==========================================
    echo   ERROR: Certificate installation failed!
    echo ==========================================
    echo.
    echo Please run this script as Administrator:
    echo 1. Right-click on run_me_first.bat
    echo 2. Select "Run as administrator"
    echo.
)

pause
