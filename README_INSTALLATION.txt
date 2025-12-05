# MindVault MSIX Installation Guide

## ?? Package Contents

This package contains:
- **MindVault.msix** - The application package
- **MindVault_Certificate.cer** - Security certificate for installation
- **INSTALL.bat** - Automated installation script
- **README_INSTALLATION.txt** - This file

---

## ?? Quick Installation (Recommended)

### For the PC that created the MSIX:

1. **Right-click** `INSTALL.bat`
2. Select **"Run as Administrator"**
3. Wait for installation to complete
4. Press Windows key and type "MindVault" to launch

### For other PCs:

1. **Copy all files** to the target PC:
   - MindVault.msix
   - MindVault_Certificate.cer
   - INSTALL.bat

2. **Right-click** `INSTALL.bat`
3. Select **"Run as Administrator"**
4. Wait for installation to complete
5. Launch MindVault from Start Menu

---

## ?? System Requirements

- **OS**: Windows 10 version 19041.0 or higher
- **Architecture**: x64 (64-bit)
- **Storage**: ~500 MB free space
- **RAM**: 4 GB minimum, 8 GB recommended
- **Privileges**: Administrator access for installation

---

## ?? Certificate Installation (Manual Method)

If automatic installation fails, install the certificate manually:

### Step 1: Install Certificate
1. **Double-click** `MindVault_Certificate.cer`
2. Click **"Install Certificate"**
3. Select **"Local Machine"**
4. Click **"Next"**

### Step 2: Choose Certificate Store
1. Select **"Place all certificates in the following store"**
2. Click **"Browse"**
3. Select **"Trusted People"**
4. Click **"OK"**
5. Click **"Next"** then **"Finish"**
6. Click **"Yes"** when prompted

### Step 3: Install MSIX
1. **Right-click** `MindVault.msix`
2. Select **"Install"**
3. Wait for installation to complete

---

## ?? Manual Installation (PowerShell)

If you prefer PowerShell, use these commands:

```powershell
# Run PowerShell as Administrator

# 1. Install certificate
certutil -addstore TrustedPeople "MindVault_Certificate.cer"

# 2. Remove old version (if exists)
Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage

# 3. Install MSIX package
Add-AppxPackage -Path ".\MindVault.msix"

# 4. Verify installation
Get-AppxPackage -Name "*mindvault*"
```

---

## ? Verify Installation

After installation, verify everything works:

```powershell
# Check if app is installed
Get-AppxPackage -Name "*mindvault*"

# Check certificate
certutil -verifystore TrustedPeople "MindVault"

# Check LocalAppData folder (after first launch)
Test-Path "$env:LOCALAPPDATA\MindVault"
```

---

## ?? First Launch

### What to Expect:

1. **First Launch** (~30-60 seconds):
   - Python runtime will be extracted to LocalAppData
   - AI model will be copied
   - Progress may appear in the app

2. **Subsequent Launches** (instant):
   - App starts immediately
   - All features ready to use

### Verify AI Features:

1. Launch MindVault
2. Navigate to flashcard creation
3. Click **"AI Summarize"**
4. Upload a document (.pdf, .docx, .pptx, or .txt)
5. Click **"Generate Flashcards"**
6. ? Flashcards should be generated successfully

---

## ?? Checking Logs

If you encounter issues, check the log file:

**Location**: `%LOCALAPPDATA%\MindVault\run_log.txt`

**Quick access**:
```cmd
notepad "%LOCALAPPDATA%\MindVault\run_log.txt"
```

**What to look for**:
- ? `"? Python successfully extracted"`
- ? `"? python311.dll found"`
- ? `"ERROR: Missing critical DLLs"`
- ? `"ERROR: ZIP file not found"`

---

## ?? Reinstallation

To reinstall MindVault:

```powershell
# 1. Uninstall current version
Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage

# 2. Delete local data (optional - removes flashcards!)
Remove-Item "$env:LOCALAPPDATA\MindVault" -Recurse -Force

# 3. Reinstall
Add-AppxPackage -Path ".\MindVault.msix"
```

---

## ?? Troubleshooting

### Issue 1: "This app package cannot be installed"

**Cause**: Certificate not trusted

**Solution**:
1. Install certificate manually (see Certificate Installation section)
2. Ensure you selected "Trusted People" store
3. Restart installation

### Issue 2: "Windows cannot install package"

**Cause**: Old version still installed or corrupted

**Solution**:
```powershell
# Force remove old version
Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage
Get-AppxPackage -AllUsers -Name "*mindvault*" | Remove-AppxPackage -AllUsers

# Clean up LocalAppData
Remove-Item "$env:LOCALAPPDATA\MindVault" -Recurse -Force -ErrorAction SilentlyContinue

# Retry installation
```

### Issue 3: "python311.dll not found" error

**Cause**: Python extraction incomplete

**Solution**:
```powershell
# Delete Python folder
Remove-Item "$env:LOCALAPPDATA\MindVault\Python311" -Recurse -Force

# Relaunch app to trigger re-extraction
```

### Issue 4: App won't launch from Start Menu

**Cause**: Installation incomplete

**Solution**:
1. Press `Windows + R`
2. Type: `shell:AppsFolder`
3. Find MindVault in the list
4. Right-click ? Pin to Start / Pin to Taskbar

### Issue 5: Developer Mode Required

**Error**: "You need to enable Developer Mode"

**Solution**:
1. Open **Settings**
2. Go to **Update & Security** > **For developers**
3. Enable **Developer mode**
4. Retry installation

---

## ?? Security Notes

### About the Certificate

The included certificate (`MindVault_Certificate.cer`) is a self-signed certificate used to sign the MSIX package. This is safe when:

- ? You created the certificate yourself
- ? You're distributing to trusted users only
- ? The certificate is installed in the "Trusted People" store

### For Production Distribution:

For wider distribution, consider:
1. **Code Signing Certificate** from a trusted CA (e.g., DigiCert, Sectigo)
2. **Microsoft Store** distribution (automatic trust)
3. **Enterprise Distribution** with company PKI

### Certificate Validity:

- Check certificate expiration: Double-click `.cer` file
- The MSIX will not install after certificate expires
- Re-sign MSIX with new certificate before expiration

---

## ?? File Structure After Installation

```
%LOCALAPPDATA%\MindVault\
??? Python311\              (Extracted Python runtime)
?   ??? python.exe
?   ??? python311.dll
?   ??? ... (other Python files)
??? Runtime\
?   ??? Scripts\
?   ?   ??? flashcard_ai.py
?   ??? Models\
?   ?   ??? mindvault_qwen2_0.5b_q4_k_m.gguf
?   ??? flashcards.json
??? run_log.txt             (Extraction and runtime log)
??? setup_complete.txt      (Setup flag)

C:\Program Files\WindowsApps\
??? [PackageFullName]\      (MSIX app files - READ ONLY)
    ??? mindvault.exe
    ??? python311.zip       (Source for extraction)
    ??? ... (app files)
```

---

## ?? Distribution Guide

### For sharing with other users:

1. **Create a distribution folder**:
   ```
   MindVault_v1.0\
   ??? MindVault.msix
   ??? MindVault_Certificate.cer
   ??? INSTALL.bat
   ??? README_INSTALLATION.txt
   ```

2. **Compress to ZIP**:
   - Select all files
   - Right-click ? Send to ? Compressed (zipped) folder
   - Name: `MindVault_v1.0_Installer.zip`

3. **Share via**:
   - Google Drive
   - OneDrive
   - Dropbox
   - USB drive
   - Network share

4. **Provide instructions**:
   - Extract ZIP to a folder
   - Run INSTALL.bat as Administrator
   - Launch from Start Menu

---

## ?? Update Process

To update to a newer version:

1. **Uninstall old version** (optional - keeps user data):
   ```powershell
   Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage
   ```

2. **Install new version**:
   ```cmd
   REM Run as Administrator
   INSTALL.bat
   ```

3. **Your data is preserved**:
   - Flashcards remain in LocalAppData
   - Python installation is reused
   - Settings are maintained

---

## ?? Support

If you encounter issues:

1. **Check the log file**:
   ```cmd
   notepad "%LOCALAPPDATA%\MindVault\run_log.txt"
   ```

2. **Verify installation**:
   ```powershell
   Get-AppxPackage -Name "*mindvault*"
   ```

3. **Check certificate**:
   ```cmd
   certutil -verifystore TrustedPeople "MindVault"
   ```

4. **Clean reinstall**:
   - Uninstall app
   - Delete `%LOCALAPPDATA%\MindVault`
   - Reinstall using INSTALL.bat

---

## ?? Technical Details

- **Package Name**: mindvault
- **Publisher**: CN=MindVault
- **Architecture**: x64
- **Min OS Version**: 10.0.19041.0 (Windows 10 2004)
- **Target Framework**: .NET 9.0
- **Certificate Type**: Self-signed (for testing/internal use)

---

## ? Features

After installation, MindVault provides:

- ?? **Flashcard Management** - Create, edit, and organize flashcards
- ?? **AI-Powered Generation** - Generate flashcards from documents (PDF, DOCX, PPTX, TXT)
- ?? **Spaced Repetition** - Smart learning algorithm for better retention
- ?? **Multiplayer Mode** - Quiz sessions over local network
- ?? **Import/Export** - Backup and share your flashcards
- ?? **Offline First** - Works without internet (AI included)

---

## ?? Uninstallation

To completely remove MindVault:

```powershell
# 1. Uninstall app
Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage

# 2. Remove user data (optional)
Remove-Item "$env:LOCALAPPDATA\MindVault" -Recurse -Force

# 3. Remove certificate (optional)
certutil -delstore TrustedPeople "MindVault"
```

Or use Windows Settings:
1. Open **Settings** > **Apps** > **Installed apps**
2. Find **MindVault**
3. Click three dots ? **Uninstall**

---

**Installation Guide Version**: 1.0  
**Last Updated**: December 2024  
**Created for**: MindVault MSIX Distribution

?? **Enjoy using MindVault!**
