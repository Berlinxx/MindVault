# ?? Quick Start Guide - After Restoration

## ? What's Already Done

All your Python/AI integration code has been restored and verified:
- ? PythonBootstrapper.cs - Complete
- ? PythonFlashcardService.cs - Complete
- ? AddFlashcardsPage.xaml.cs - Complete
- ? SummarizeContentPage.xaml.cs - Complete
- ? .gitignore - Updated
- ? Build - Successful

## ?? What You Need to Do Now

### Step 1: Get Required Files (One-Time Setup)

You need these files that are **too large for git**:

#### A. Python311.zip (~50-150MB)
**Option 1 - Python.org (Recommended)**:
```powershell
# Download Python 3.11.9 Embeddable Package (64-bit)
# URL: https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip
# Save as: python311.zip
```

**Option 2 - If you had it before**:
Check your backup, old git commits, or team members' machines.

#### B. get-pip.py (~2MB)
```powershell
# Download from official source
Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile "get-pip.py"
```

#### C. Llama-CPP-Python Wheels (~30-600MB)
```powershell
# CPU Version (REQUIRED - ~30MB)
# Download from: https://github.com/abetlen/llama-cpp-python/releases
# Look for: llama_cpp_python-0.3.16-cp311-cp311-win_amd64.whl

# GPU Version (OPTIONAL - ~600MB)
# Same URL, look for: llama_cpp_python-0.3.16-cp311-cp311-win_amd64-cuda122.whl
```

### Step 2: Place Files in Solution Directory

```
YourSolution/
??? mindvault.csproj
??? python311.zip          ? Place here
??? get-pip.py             ? Place here
??? Scripts/
?   ??? flashcard_ai.py   (? already present)
??? Models/
?   ??? mindvault_qwen2_0.5b_q4_k_m.gguf  (? already present)
??? Wheels/
    ??? cpu/
    ?   ??? llama_cpp_python-0.3.16-cp311-cp311-win_amd64.whl  ? Place here
    ??? gpu/
        ??? llama_cpp_python-0.3.16-cp311-cp311-win_amd64-cuda122.whl  ? Place here (optional)
```

### Step 3: Build and Test

```powershell
# Open Visual Studio
# Set to Windows target
# Build the project
dotnet build -c Debug -f net9.0-windows10.0.19041.0

# Run the app
# Click "AI Summarize" button
# Follow installation prompts
# Test flashcard generation
```

## ?? How to Verify Everything Works

### Test 1: File Presence
Before building, verify files exist:
```powershell
Test-Path "python311.zip"          # Should be True
Test-Path "get-pip.py"             # Should be True
Test-Path "Wheels/cpu/*.whl"       # Should find 1 file
```

### Test 2: First Run (Installation)
1. Run the app (Windows only)
2. Navigate to AddFlashcardsPage
3. Click "AI Summarize" button
4. Should see: "Python runtime not detected" prompt
5. Click OK to start installation
6. Watch progress overlay (extraction, installation)
7. Should navigate to SummarizeContentPage

### Test 3: Verify Installation
Check these folders were created:
```powershell
# Installation should create:
$env:LOCALAPPDATA\MindVault\Python311\python311\python.exe  # Should exist
$env:LOCALAPPDATA\MindVault\Python311\python311\Lib\site-packages\llama_cpp\  # Should exist
$env:LOCALAPPDATA\MindVault\setup_complete.txt  # Should exist
```

### Test 4: Generate Flashcards
1. On SummarizeContentPage, paste some content
2. Click "Generate Flashcards"
3. Watch progress bar with ETA
4. Should see flashcards generated
5. Check they were added to your deck

## ?? Troubleshooting

### Problem: "python311.zip not found"
**Solution**: Make sure python311.zip is in the **solution root directory** (same folder as mindvault.csproj)

### Problem: "llama-cpp-python wheel not found"
**Solution**: 
- Check wheels are in `Wheels/cpu/` folder
- Verify filename matches: `llama_cpp_python-*-cp311-*-win_amd64.whl`
- Don't rename the wheel files

### Problem: "get-pip.py not found"
**Solution**: Download from https://bootstrap.pypa.io/get-pip.py and place in solution root

### Problem: "Python311 folder incomplete"
**Solution**: 
- Delete `%LOCALAPPDATA%\MindVault` folder
- Restart app
- Re-run installation

### Check Logs
If something fails, check:
```
%LOCALAPPDATA%\MindVault\run_log.txt
```

## ?? Quick Reference - File Sizes

| File | Size | Required |
|------|------|----------|
| python311.zip | ~50MB | ? Yes |
| get-pip.py | 2MB | ? Yes |
| CPU wheel | ~30MB | ? Yes |
| GPU wheel | ~600MB | ?? Optional |
| AI Model (.gguf) | 300MB | ? Already present |
| Python script (.py) | <1MB | ? Already present |

## ?? Success Criteria

You'll know everything works when:
1. ? Build succeeds with no errors
2. ? "AI Summarize" button appears on AddFlashcardsPage
3. ? Installation completes without errors
4. ? SummarizeContentPage shows "Generate Flashcards" button
5. ? Flashcard generation produces cards
6. ? Generated cards appear in deck editor

## ?? Need Help?

### Common Issues:
1. **Can't find python311.zip**: Check old commits, ask team members, or download from Python.org
2. **Wheels not downloading**: Make sure you're downloading **cp311** (Python 3.11) and **win_amd64** (Windows 64-bit) versions
3. **Installation fails**: Check `run_log.txt` for detailed error messages

### Debug Mode:
Enable detailed logging by checking:
```
%LOCALAPPDATA%\MindVault\run_log.txt
```

## ? Final Checklist

Before committing/sharing:
- [ ] python311.zip placed in solution directory
- [ ] get-pip.py placed in solution directory  
- [ ] CPU wheel placed in Wheels/cpu/ folder
- [ ] (Optional) GPU wheel placed in Wheels/gpu/ folder
- [ ] Build succeeds
- [ ] Test installation on clean machine
- [ ] Test flashcard generation
- [ ] Verify .gitignore excludes large files

---

**You're all set!** ??

Once you have the required files in place, your Python/AI integration will work exactly as it did before the deletion.

---
**Last Updated**: 2024
**Status**: Ready for testing after obtaining required files
