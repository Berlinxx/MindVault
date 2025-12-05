@echo off
REM ========================================
REM Certificate Verification Script
REM ========================================
REM Use this on other PCs to check if certificate can be installed

setlocal EnableDelayedExpansion

echo.
echo ========================================
echo   MindVault Certificate Verifier
echo ========================================
echo.

REM Check for certificate file
set "CERT_FILE=MindVault_Certificate.cer"
if not exist "%CERT_FILE%" (
    echo [ERROR] Certificate file not found: %CERT_FILE%
    echo.
    echo Please ensure %CERT_FILE% is in the same folder.
    echo.
    pause
    exit /b 1
)

echo [OK] Found certificate file: %CERT_FILE%
echo.

REM Display certificate information
echo Certificate Information:
echo ========================
certutil -dump "%CERT_FILE%" | findstr /C:"Subject:" /C:"Issuer:" /C:"NotBefore:" /C:"NotAfter:" /C:"Serial Number:"
echo.

REM Check if certificate is installed
echo Checking if certificate is already installed...
certutil -verifystore TrustedPeople "MindVault" >nul 2>&1
if %errorLevel% equ 0 (
    echo [OK] Certificate is already installed in Trusted People store
    echo.
    certutil -verifystore TrustedPeople "MindVault" | findstr /C:"Subject:" /C:"Serial Number:"
    echo.
) else (
    echo [INFO] Certificate is not yet installed
    echo.
)

echo.
echo To install this certificate:
echo 1. Run INSTALL.bat as Administrator (recommended)
echo    OR
echo 2. Double-click %CERT_FILE% and follow the wizard
echo.
pause
