# Testing Guide: MSIX Python Extraction Fix

## Overview
This guide provides step-by-step instructions to test the Python extraction fix for MSIX-installed apps.

---

## ? Step 1: Build the Project

### Option A: Visual Studio
1. Open the solution in Visual Studio
2. Set configuration to **Release**
3. Set platform to **Windows**
4. Right-click solution ? **Rebuild Solution**
5. Wait for build to complete
6. ? Verify: Build succeeded with no errors

### Option B: Command Line
```powershell
cd "C:\Users\micha\Downloads\AI DONE (3)\dwa\AI DONE"
dotnet build -c Release -f net9.0-windows10.0.19041.0
```
? Verify: `Build succeeded.`

---

## ? Step 2: Test Locally (Release Mode)

### 2.1. Clean LocalAppData Folder
```powershell
# Remove existing Python installation
Remove-Item "$env:LOCALAPPDATA\MindVault" -Recurse -Force -ErrorAction SilentlyContinue
```

### 2.2. Run the App
1. Navigate to build output:
   ```
   bin\Release\net9.0-windows10.0.19041.0\win10-x64\
   ```
2. Double-click `mindvault.exe`
3. App should launch successfully

### 2.3. Test Python Extraction
1. In the app, navigate to flashcard creation
2. Click **"AI Summarize"** button
3. Wait for extraction to complete

### 2.4. Verify Success
```powershell
# Check if Python was extracted
Test-Path "$env:LOCALAPPDATA\MindVault\Python311\python.exe"
# Should return: True

# Check for python311.dll
Test-Path "$env:LOCALAPPDATA\MindVault\Python311\python311.dll"
# Should return: True

# View the log
notepad "$env:LOCALAPPDATA\MindVault\run_log.txt"
```

**? Expected in Log**:
```
[timestamp] Found python311.zip: 125.3 MB
[timestamp] Extracted DLL: python311.dll to ...
[timestamp] ? Python successfully extracted
[timestamp] ? python311.dll found
[timestamp] ? python3.dll found
```

**? If You See Errors**:
```
ERROR: ZIP file not found
ERROR: Missing critical DLLs: python311.dll
```
? Go to Step 6: Troubleshooting

---

## ? Step 3: Create MSIX Package

### 3.1. Using Visual Studio
1. Right-click the project in Solution Explorer
2. Select **Publish...**
3. Choose **MSIX** package
4. Follow the packaging wizard
5. Set package version (e.g., 1.0.0.0)
6. Choose output directory
7. Click **Finish**

### 3.2. Verify Package Contents
1. Find the generated `.msix` file
2. Extract it to a temporary folder:
   ```powershell
   # Rename .msix to .zip
   $msixPath = "C:\Path\To\mindvault.msix"
   $extractPath = "C:\Temp\msix_contents"
   Expand-Archive -Path $msixPath -DestinationPath $extractPath -Force
   ```
3. **? Verify files are present**:
   ```powershell
   Test-Path "$extractPath\python311.zip"
   Test-Path "$extractPath\Models\mindvault_qwen2_0.5b_q4_k_m.gguf"
   Test-Path "$extractPath\Scripts\flashcard_ai.py"
   ```
   All should return **True**

---

## ? Step 4: Install MSIX Package

### 4.1. Clean Installation
```powershell
# Remove any existing installation
Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage

# Delete LocalAppData folder
Remove-Item "$env:LOCALAPPDATA\MindVault" -Recurse -Force -ErrorAction SilentlyContinue
```

### 4.2. Install Package
```powershell
# Install the MSIX package
Add-AppxPackage -Path "C:\Path\To\mindvault.msix"
```

**? Verify Installation**:
```powershell
Get-AppxPackage -Name "*mindvault*"
# Should show your app package
```

---

## ? Step 5: Test MSIX-Installed App

### 5.1. Launch the App
1. Press **Windows Key**
2. Type **"mindvault"**
3. Click the app icon
4. App should launch

### 5.2. Test Python Extraction
1. Navigate to flashcard creation
2. Click **"AI Summarize"** button
3. Watch the extraction progress
4. Wait for completion (may take 30-60 seconds)

### 5.3. Test AI Features
1. After extraction completes, upload a document (.pdf, .docx, .pptx, or .txt)
2. Click **"Generate Flashcards"**
3. Wait for AI to process the document
4. ? Verify: Flashcards are generated successfully
5. ? Verify: NO "python311.dll not found" error

### 5.4. Check Logs
```powershell
# View the extraction log
notepad "$env:LOCALAPPDATA\MindVault\run_log.txt"
```

**? Success Indicators**:
```
[timestamp] AppContext.BaseDirectory: C:\Program Files\WindowsApps\...
[timestamp] Found python311.zip: 125.3 MB
[timestamp] Starting extraction to: C:\Users\...\AppData\Local\MindVault\Python311
[timestamp] ZIP archive opened, 3542 entries to extract
[timestamp] Extracted DLL: python311.dll to ...
[timestamp] Extracted DLL: python3.dll to ...
[timestamp] Extraction complete: 3542 entries processed
[timestamp] ? Python successfully extracted to: ...
[timestamp] ? python311.dll found at ...
[timestamp] ? python3.dll found at ...
```

### 5.5. Verify File Structure
```powershell
# Check Python installation
Get-ChildItem "$env:LOCALAPPDATA\MindVault\Python311" -Recurse -File | 
    Where-Object { $_.Name -like "*.dll" } | 
    Select-Object Name, FullName

# Should show python311.dll, python3.dll, and other DLLs
```

---

## ? Step 6: Troubleshooting

### Issue 1: "python311.dll not found" Error

**Symptoms**:
- Error dialog appears after clicking "AI Summarize"
- App shows: "The code execution cannot proceed because python311.dll was not found"

**Solution**:
1. Check the log file:
   ```powershell
   notepad "$env:LOCALAPPDATA\MindVault\run_log.txt"
   ```

2. Look for these error patterns:
   ```
   ERROR: ZIP file not found at ...
   ```
   **Fix**: Re-create MSIX package ensuring `python311.zip` is included

   ```
   ERROR: Missing critical DLLs: python311.dll
   ```
   **Fix**: ZIP file is corrupted, replace with fresh `python311.zip`

3. Delete and re-extract:
   ```powershell
   Remove-Item "$env:LOCALAPPDATA\MindVault\Python311" -Recurse -Force
   ```
   Then restart the app

### Issue 2: Extraction Takes Too Long

**Symptoms**:
- Extraction progress stuck at a certain percentage
- App appears frozen

**Solution**:
1. Wait at least 2-3 minutes (extraction is large ~125 MB)
2. Check Task Manager for `mindvault.exe` CPU usage
3. If truly stuck, restart the app

### Issue 3: MSIX Package Won't Install

**Symptoms**:
- `Add-AppxPackage` fails with error

**Solution**:
1. Check if certificate is trusted:
   ```powershell
   # If using self-signed certificate
   # Install the certificate to Trusted Root
   ```

2. Try uninstalling first:
   ```powershell
   Get-AppxPackage -Name "*mindvault*" | Remove-AppxPackage
   ```

3. Reinstall:
   ```powershell
   Add-AppxPackage -Path "C:\Path\To\mindvault.msix"
   ```

### Issue 4: Log File Shows "ZIP File Not Found"

**Symptoms**:
```
[timestamp] ERROR: Bundled ZIP not found at ...
[timestamp] Files in AppContext.BaseDirectory:
[timestamp]   - mindvault.exe
[timestamp]   - mindvault.dll
```
(No python311.zip listed)

**Solution**:
1. The MSIX package is missing `python311.zip`
2. Check your `.csproj` file:
   ```xml
   <Content Include="python311.zip" Condition="$(TargetFramework.Contains('windows'))">
       <CopyToOutputDirectory>Always</CopyToOutputDirectory>
   </Content>
   ```
3. Rebuild and re-create MSIX package

---

## ? Step 7: Final Verification Checklist

Use this checklist to confirm everything works:

### Build & Package
- [ ] Project builds without errors
- [ ] `python311.zip` is in build output directory
- [ ] MSIX package created successfully
- [ ] MSIX package contains `python311.zip` (verified by extraction)

### Local Testing (Release Mode)
- [ ] App launches from build output directory
- [ ] Python extracts successfully
- [ ] No "python311.dll" error
- [ ] AI features generate flashcards
- [ ] Log file shows successful extraction

### MSIX Testing
- [ ] MSIX package installs successfully
- [ ] App launches from Start Menu
- [ ] Python extracts on first run
- [ ] `python311.dll` exists in LocalAppData
- [ ] AI features work correctly
- [ ] No error dialogs
- [ ] Log file shows successful extraction

### Final Test
- [ ] Uninstall app
- [ ] Delete LocalAppData folder
- [ ] Reinstall from MSIX
- [ ] Test AI features again
- [ ] Everything works ?

---

## ?? Test Report Template

Use this template to document your testing:

```markdown
## Test Report: MSIX Python Extraction Fix

**Date**: [Date]
**Tester**: [Your Name]
**Build Version**: [Version]

### Environment
- OS: Windows 11 [or Windows 10]
- OS Build: [e.g., 22H2]
- Test Type: [Local / MSIX Install]

### Test Results

#### Build Test
- [ ] ? Build succeeded
- [ ] ? No compilation errors
- [ ] ? python311.zip present in output

#### Local Test (Release Mode)
- [ ] ? App launches
- [ ] ? Python extracts correctly
- [ ] ? python311.dll verified
- [ ] ? AI features work
- [ ] ? [Describe any issues]

#### MSIX Test
- [ ] ? Package installs
- [ ] ? App launches
- [ ] ? Python extracts correctly
- [ ] ? python311.dll verified
- [ ] ? AI features work
- [ ] ? [Describe any issues]

### Log File Excerpt
```
[Paste relevant log entries here]
```

### Issues Found
[List any issues encountered]

### Conclusion
- [ ] ? All tests passed
- [ ] ? Issues found (see above)
```

---

## ?? Success Criteria

The fix is considered successful when:

1. ? MSIX-installed app launches without errors
2. ? Python extracts to `%LOCALAPPDATA%\MindVault\Python311\`
3. ? `python311.dll` is present and verified
4. ? AI summarize feature generates flashcards
5. ? No "python311.dll not found" error dialogs
6. ? Log file shows successful extraction with DLL verification
7. ? Works on clean install (deleted LocalAppData folder)

---

## ?? Additional Resources

- **Detailed Fix Documentation**: `MSIX_PYTHON_EXTRACTION_FIX.md`
- **Quick Summary**: `QUICK_FIX_SUMMARY.md`
- **Log File Location**: `%LOCALAPPDATA%\MindVault\run_log.txt`

---

**Status**: ? Ready for Testing  
**Last Updated**: December 2024
