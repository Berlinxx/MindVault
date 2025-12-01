# Fix: Automatic Export of Scripts and AI Model to LocalAppData

## Problem

When you deleted `%LocalAppData%\MindVault\`, you experienced the same issue as your groupmate:
- The app showed "INSTALL PYTHON + LLAMA" button
- Log showed: `script=False, model=False`

**Root Cause**: The files weren't being copied to `%LocalAppData%\MindVault\Runtime\` automatically when the environment was checked.

---

## Solution Implemented

### What Was Changed:

#### 1. **Enhanced `IsEnvironmentHealthyAsync()`**
Now **automatically copies** files to LocalAppData before checking:

```csharp
// IMPORTANT: Ensure files are copied to LocalAppData before checking
var scriptsDir = Path.Combine(PythonWorkDir, "Scripts"); 
var modelsDir = Path.Combine(PythonWorkDir, "Models");
Directory.CreateDirectory(scriptsDir);
Directory.CreateDirectory(modelsDir);

// Copy script if not already there
var scriptPath = Path.Combine(scriptsDir, "flashcard_ai.py");
if (!File.Exists(scriptPath))
{
    var bundledScript = Path.Combine(AppContext.BaseDirectory, "Scripts", "flashcard_ai.py");
    if (File.Exists(bundledScript))
    {
        File.Copy(bundledScript, scriptPath, true);
        Log($"Copied flashcard_ai.py to {scriptPath}");
    }
}

// Copy model if not already there
var modelPath = Path.Combine(modelsDir, "mindvault_qwen2_0.5b_q4_k_m.gguf");
if (!File.Exists(modelPath))
{
    modelPath = CopyModelToLocalAppData(modelsDir);
}
```

**Result**: Files are **automatically exported** the first time `IsEnvironmentHealthyAsync()` is called!

#### 2. **New Method: `CopyModelToLocalAppData()`**
Intelligently finds and copies the model file:

```csharp
string CopyModelToLocalAppData(string targetDir)
{
    // 1. If already exists in LocalAppData, return it
    // 2. Try build output: bin\Debug\...\Models\*.gguf
    // 3. Try source directory: .\Models\*.gguf
    // 4. Log error if not found
}
```

**Searches in order**:
1. LocalAppData (if already copied)
2. Build output directory
3. Source directory (for development)

#### 3. **Updated `EnsureResourceFilesAsync()`**
Now uses the new copy method and provides better logging.

#### 4. **Updated `PrepareScriptToLocal()`**
Now checks if file exists before copying.

---

## How It Works Now

### First Time Running (After Deleting LocalAppData):

1. User opens app and navigates to AI Summarize page
2. `QuickCheckEnvironmentAsync()` is called
3. `IsEnvironmentHealthyAsync()` is called
4. **Files are automatically copied**:
   - `flashcard_ai.py` ? `%LocalAppData%\MindVault\Runtime\Scripts\`
   - `mindvault_qwen2_0.5b_q4_k_m.gguf` ? `%LocalAppData%\MindVault\Runtime\Models\`
5. Environment check passes
6. UI shows "Ready to generate flashcards" ?

### Subsequent Runs:

1. Files already exist in LocalAppData
2. `IsEnvironmentHealthyAsync()` detects them immediately
3. No copying needed
4. UI shows correct button instantly ?

---

## Testing the Fix

### Test 1: Delete LocalAppData and Restart
```powershell
# Delete the folder
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\MindVault"

# Run the app
# Navigate to AI Summarize page
# Should automatically copy files and show "Ready to generate flashcards"
```

### Test 2: Check Logs
```powershell
# View the run_log.txt
Get-Content "$env:LOCALAPPDATA\MindVault\run_log.txt" -Tail 20
```

**Expected log entries**:
```
[timestamp] Copied flashcard_ai.py to C:\Users\...\LocalAppData\MindVault\Runtime\Scripts\flashcard_ai.py
[timestamp] Copying model from build output: ...\Models\mindvault_qwen2_0.5b_q4_k_m.gguf -> ...
[timestamp] IsEnvironmentHealthyAsync: py=True, script=True, model=True, llama=True
```

---

## Summary

**Before Fix**:
- ? Files only copied during `EnsurePythonReadyAsync()` (AI Summarize button click)
- ? `IsEnvironmentHealthyAsync()` checked LocalAppData **before** files were copied
- ? Result: UI showed wrong button

**After Fix**:
- ? `IsEnvironmentHealthyAsync()` **copies files automatically** before checking
- ? Files appear in LocalAppData on first environment check
- ? UI shows correct button immediately
- ? Works even after deleting LocalAppData folder

**Key Improvement**: **Proactive file copying** instead of reactive checking!
