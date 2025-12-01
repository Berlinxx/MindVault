# Understanding the Offline vs Online Behavior

## The Issue

Your groupmate's PC has Python installed, and the app is detecting **their system Python** instead of using the bundled Python311 folder. This causes it to try installing packages from the internet.

## Why This Happens

The app checks for Python in this order:

1. **Bundled Python311** (in build output folder) - OFFLINE ?
2. **Bundled Python311** (in source folder) - OFFLINE ?  
3. **System Python** (installed on PC) - May need internet ??
4. **Download Python** (if nothing found) - Needs internet ?

If your groupmate has Python installed (step 3), it skips the bundled Python and tries to install `llama-cpp-python` on their system Python, which requires:
- Internet connection
- Visual Studio Build Tools (C++ compiler)
- Or prebuilt wheels

## The Fix

### What I Changed in the Code

I modified `PythonBootstrapper.cs` to:
1. **Check source directory first** - Now detects Python311 even before building
2. **Add warnings** - Shows clear messages when using system vs bundled Python
3. **Prioritize bundled over system** - Your bundled Python311 is now preferred

### What Your Groupmate Should Do

**Option 1: Use Built Application (EASIEST)**

Instead of sharing source code, share the **built app**:

```
1. On YOUR PC, build in Release mode:
   - Open Visual Studio
   - Set Configuration to "Release"
   - Set Platform to "Windows"
   - Build ? Rebuild Solution

2. Go to: bin\Release\net9.0-windows10.0.19041.0\win10-x64\

3. This folder contains EVERYTHING needed (offline):
   - Your .exe
   - Python311\ folder (copied from source)
   - Models\ folder with AI model
   - Wheels\ folder
   - All DLLs

4. Compress THIS FOLDER and share via Google Drive

5. Groupmate just:
   - Extract
   - Double-click check_setup.bat
   - Run the .exe
   
   NO Visual Studio needed! NO building needed!
```

**Option 2: Share Source + Verify Setup (For Development)**

If they need to build from source:

```
1. Verify Python311 folder is in the compressed archive
2. Tell them to run check_setup.bat BEFORE opening Visual Studio
3. If check passes ? open .sln and build
4. If check fails ? files didn't extract properly
```

## How to Test

### On Your PC
Run: `.\check_setup.bat`
- Should pass all checks ?

### On Groupmate's PC
1. Extract your shared archive
2. Run: `.\check_setup.bat`
3. Should show which files are missing

## Common Problems & Solutions

### Problem: "Python311 not found"
**Cause**: Incomplete extraction or wrong extraction path
**Fix**: Re-extract using 7-Zip to a simple path like `C:\MindVault`

### Problem: "llama_cpp import failed"
**Cause**: Python311 folder is incomplete or corrupted
**Fix**: 
1. On your PC, verify `Python311\Lib\site-packages\llama_cpp\` exists
2. Re-compress and re-share
3. Make sure compression tool doesn't skip files

### Problem: "Python failed to run"
**Cause**: Missing Visual C++ Redistributables
**Fix**: Install from https://aka.ms/vs/17/release/vc_redist.x64.exe

### Problem: Still using system Python
**Cause**: Build output doesn't have Python311 copied yet
**Fix**: 
1. Build the project first (creates bin\ folder)
2. .csproj automatically copies Python311 to bin\
3. Run from bin\ output folder

## What the New Code Does

### Before (Old Behavior)
```
1. Check build output for Python311 ? (doesn't exist before build)
2. Check system Python ? (finds groupmate's Python)
3. Try to install llama-cpp-python ?? (needs internet)
```

### After (New Behavior)
```
1. Check build output for Python311 ? (doesn't exist before build)
2. Check SOURCE folder for Python311 ? (finds your bundled Python!)
3. Use bundled Python with llama already installed ? (fully offline)
4. Only check system Python if bundled not found
```

## Summary

**The Problem**: Groupmate's system Python being detected instead of bundled Python311

**The Solution**: 
- Code now checks source directory before system Python
- Added diagnostic tools (check_setup.bat, check_setup.ps1)
- Added clear setup guide (SETUP_GUIDE.md)

**Best Practice**: Share the **built application** (bin\Release folder), not the source code. This guarantees everything is bundled and ready to run offline.

---

## Files Created for Your Team

1. **check_setup.bat** - Quick Windows checker (double-click to run)
2. **check_setup.ps1** - Detailed PowerShell checker (more info)
3. **SETUP_GUIDE.md** - Complete setup instructions
4. **THIS_FILE.md** - Technical explanation of the issue

**Next Steps:**
1. Commit these new files to your repo
2. Share with your team
3. Tell them to run `check_setup.bat` first
4. Or better: share the built app from bin\Release\
