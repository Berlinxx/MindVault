# MindVault MSIX Packaging Guide

## Overview

This guide explains how to create an MSIX package for MindVault that can be distributed to users with a simple certificate installer.

## Files Included

- **`build_msix.bat`** - Automated script to build and package MindVault as MSIX
- **`run_me_first.bat`** - Certificate installer for end users
- **`README.md`** - This file

## Prerequisites

### For Building (Developer)

1. **Windows 10/11 SDK**
   - Download from: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
   - Required tools: `makeappx.exe` and `signtool.exe`

2. **.NET 9 SDK**
   - Already installed if you can build the project

### For Installing (End User)

- Windows 10 version 19041 (May 2020 Update) or later
- Administrator access (for certificate installation only)

## How to Build MSIX Package

### Step 1: Run the Build Script

1. Open Command Prompt or PowerShell
2. Navigate to the project root directory (where `mindvault.csproj` is located)
3. Run:
   ```cmd
   cd Packaging
   build_msix.bat
   ```

The script will:
- ? Build the project in Release mode
- ? Create a self-signed certificate (`MindVault_Certificate.pfx` and `.cer`)
- ? Generate the MSIX package manifest
- ? Copy all necessary files
- ? Create `MindVault.msix`
- ? Sign the package with the certificate

### Step 2: Locate Output Files

After successful build, you'll have:

```
?? Project Root
 ??? MindVault.msix                    (The installer - ~150-200 MB)
 ??? MindVault_Certificate.cer         (Public certificate for users)
 ??? MindVault_Certificate.pfx         (Private key - DO NOT SHARE!)
 ??? Packaging/
     ??? run_me_first.bat               (Certificate installer script)
```

## How to Distribute

### What to Share with Users

Create a folder containing:
1. **`MindVault.msix`** - The application installer
2. **`MindVault_Certificate.cer`** - The security certificate
3. **`run_me_first.bat`** - Certificate installation script

Compress these 3 files into a ZIP and share (e.g., via Google Drive, OneDrive, etc.)

### DO NOT SHARE

- ? **`MindVault_Certificate.pfx`** - This is your private signing key!
- ? Keep this file secure for signing future updates

## Installation Instructions for Users

### Method 1: Using the Batch File (Recommended)

1. **Extract all files** from the ZIP to a folder
2. **Right-click `run_me_first.bat`**
3. **Select "Run as administrator"**
   - This installs the security certificate
   - You'll see a success message
4. **Double-click `MindVault.msix`**
   - Click "Install"
   - App installs in seconds
5. **Done!** MindVault appears in Start Menu

### Method 2: Manual Certificate Installation

If the batch file doesn't work:

1. **Right-click `MindVault_Certificate.cer`**
2. Select **"Install Certificate"**
3. Choose **"Local Machine"** (requires admin)
4. Click **"Next"**
5. Select **"Place all certificates in the following store"**
6. Click **"Browse"** ? Choose **"Trusted Root Certification Authorities"**
7. Click **"Next"** ? **"Finish"**
8. Accept the security warning
9. **Double-click `MindVault.msix`** to install

## Troubleshooting

### Build Errors

**"makeappx.exe not found"**
- Install Windows 10/11 SDK
- Add SDK bin directory to PATH, or script will auto-detect common locations

**"dotnet publish failed"**
- Make sure you're in the project root directory
- Ensure .NET 9 SDK is installed: `dotnet --version`

### Installation Errors

**"This app package's publisher certificate could not be verified"**
- The certificate wasn't installed
- Run `run_me_first.bat` as administrator first

**"This app can't run on your PC"**
- Requires Windows 10 version 19041 or later
- Check Windows version: `winver`

**"Installation failed"**
- Previous version might be installed
- Uninstall from Settings ? Apps ? MindVault ? Uninstall
- Try installing again

## Updating the App

### For Developers

1. Increment version in `build_msix.bat`:
   ```batch
   set "VERSION=1.1.0.0"
   ```

2. Run `build_msix.bat` again

3. Use the **same certificate** (existing `.pfx`) to sign the update
   - Users won't need to reinstall the certificate
   - Update will install over the old version

### For Users

1. Download the new MSIX file
2. **No need to run `run_me_first.bat` again** (certificate already installed)
3. Double-click the new MSIX
4. Update installs automatically

## Advantages of MSIX

? **Clean Install/Uninstall** - No registry mess, no leftover files  
? **Automatic Updates** - Can be updated without uninstalling  
? **Sandboxed** - Runs in a secure container  
? **One-Click Install** - Just double-click after certificate is installed  
? **Windows Store Ready** - Can be published to Microsoft Store later  
? **All Files Included** - Python, models, everything bundled  

## Publishing to Microsoft Store (Optional)

To avoid certificate installation entirely:

1. Sign up for Microsoft Partner Center
2. Reserve app name "MindVault"
3. Upload the MSIX (no certificate needed for Store)
4. Users can install directly from Store with no warnings

## File Size Optimization

Current package size: ~150-200 MB (includes Python + AI models)

To reduce size:
- Python311: ~100 MB (required for AI features)
- AI Model (.gguf): ~200-300 MB (required for AI features)
- App binaries: ~10-20 MB

**Note:** All AI files are necessary for offline flashcard generation.

## Security Notes

### Certificate Safety

- The `.pfx` file contains your private key
- **Never share the `.pfx` file publicly**
- Only share the `.cer` file with users
- Store `.pfx` securely for signing future updates

### For Enterprise/Commercial Use

Consider:
1. Using a real code signing certificate from a CA (e.g., DigiCert, Sectigo)
   - Costs ~$200-400/year
   - No certificate installation needed by users
   - More trusted by Windows SmartScreen

2. Publishing to Microsoft Store
   - One-time $19 registration fee
   - No certificate warnings
   - Automatic updates

## Support

If users encounter issues:

1. **Check Windows version**: Must be 19041 or later
2. **Verify certificate**: Run `run_me_first.bat` as admin
3. **Check antivirus**: May block MSIX installation
4. **Clear package cache**: `wsreset.exe` in PowerShell

## Summary

### For Developers:
```cmd
cd Packaging
build_msix.bat
```
? Creates: `MindVault.msix`, `MindVault_Certificate.cer`, `MindVault_Certificate.pfx`

### For Distribution:
Share: `MindVault.msix` + `MindVault_Certificate.cer` + `run_me_first.bat`

### For Users:
1. Run `run_me_first.bat` (as admin, one time only)
2. Double-click `MindVault.msix`
3. Enjoy! ??

---

**Note:** The first build may take a few minutes as it compiles the entire project in Release mode and bundles all dependencies including Python and AI models (~150-200 MB total).
