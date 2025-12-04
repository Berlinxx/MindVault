@echo off
REM Quick Start - Build MSIX Package
REM Run this from the PROJECT ROOT directory (where mindvault.csproj is located)

echo.
echo ==========================================
echo    MindVault MSIX Quick Build
echo ==========================================
echo.

REM Check if we're in the right directory
if not exist "mindvault.csproj" (
    echo ERROR: Please run this script from the project root directory!
    echo Current directory: %CD%
    echo.
    echo Expected: The folder containing mindvault.csproj
    pause
    exit /b 1
)

echo Starting MSIX package build...
echo.

pushd "%~dp0Packaging"
call build_msix.bat
popd

echo.
echo ==========================================
echo    Build Complete!
echo ==========================================
echo.
echo Next steps:
echo   1. Find MindVault.msix in the project root
echo   2. Copy these 3 files to share:
echo      - MindVault.msix
echo      - MindVault_Certificate.cer  
echo      - Packaging\run_me_first.bat
echo.
echo See Packaging\README.md for full documentation
echo.

pause
