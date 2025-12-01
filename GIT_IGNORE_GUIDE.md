# Git Repository Size Management Guide

This guide explains what files are tracked in Git and what files are intentionally excluded to keep the repository lean and efficient.

## ? What IS Tracked in Git (Essential Files Only)

### Source Code
- All `.cs`, `.xaml`, `.csproj` files
- Configuration files: `App.config`, `appsettings.json`
- Resource files: Images, fonts, audio in `Resources/` folder
- Documentation: `*.md` files

### Configuration & Setup
- `.gitignore` - Git exclusion rules
- `MauiProgram.cs` - App configuration
- Project files: `*.csproj`, `*.sln`
- Setup scripts: `check_setup.ps1`, `check_setup.bat`, `*.py` setup scripts

### Platform-Specific Files
- Platform folders: `Platforms/Android/`, `Platforms/iOS/`, `Platforms/Windows/`
- Manifest files: `AndroidManifest.xml`, `Info.plist`

## ? What is NOT Tracked in Git (Excluded Files)

### 1. Build Artifacts (Auto-generated)
```
bin/
obj/
publish/
*.dll (in output folders)
*.exe (in output folders)
```
**Why excluded**: These are auto-generated during build and can be recreated anytime. They're large and change frequently.

### 2. Python & AI Dependencies (VERY LARGE - 500MB+)
```
Python/
Python311/
Wheels/*.whl
site-packages/
__pycache__/
*.pyc
```
**Why excluded**: These are downloaded/installed by `PythonBootstrapper.cs` at runtime. Can be 500MB-1GB+.

### 3. AI Model Files (VERY LARGE - 100MB-500MB each)
```
Models/*.gguf
Models/*.bin
*.onnx
*.safetensors
```
**Why excluded**: AI models are downloaded on-demand by the app. The `mindvault_qwen2_0.5b_q4_k_m.gguf` file alone is ~500MB.

### 4. User Data & Databases
```
*.db
*.sqlite
*.sqlite3
AppData/
LocalAppData/
```
**Why excluded**: Contains user's personal flashcards and preferences. Should never be in Git.

### 5. IDE & OS Files
```
.vs/
.vscode/
.idea/
*.user
*.suo
.DS_Store
Thumbs.db
```
**Why excluded**: IDE-specific settings that vary per developer.

### 6. NuGet Packages
```
packages/
*.nupkg
```
**Why excluded**: These are restored from `NuGet.config` during build.

### 7. Logs & Debug Files
```
*.log
logcat*.txt
device_log*.txt
crash-*.txt
```
**Why excluded**: Temporary debugging files not needed in source control.

### 8. Build Output & Published Apps
```
publish/
*.apk
*.aab
*.ipa
*.msix
```
**Why excluded**: Final build artifacts. Should be generated from source, not stored in Git.

## ?? Repository Size Guidelines

| Category | Typical Size | Status |
|----------|--------------|--------|
| Source code | 5-10 MB | ? Tracked |
| Resources (images, fonts, audio) | 2-5 MB | ? Tracked |
| Documentation | < 1 MB | ? Tracked |
| **Python runtime** | 50-150 MB | ? **Excluded** |
| **Python wheels** | 100-300 MB | ? **Excluded** |
| **AI models** | 300-500 MB | ? **Excluded** |
| **Build artifacts** | 50-200 MB | ? **Excluded** |

**Total Git repo size goal**: **< 20 MB** (source + resources only)

## ?? How Dependencies Are Installed

Since Python, AI models, and wheels are not in Git, they're installed automatically:

### First-Time Setup (Windows only)
1. User launches the app
2. `PythonBootstrapper.cs` detects missing Python
3. Downloads embedded Python (~50 MB)
4. Downloads required wheels (~200 MB)
5. Installs dependencies in `%LOCALAPPDATA%\MindVault\`

### AI Model Download (On-Demand)
1. User clicks "Summarize from Content"
2. `PythonFlashcardService.cs` checks for model
3. Downloads `mindvault_qwen2_0.5b_q4_k_m.gguf` (~500 MB) if missing
4. Stores in `Models/` folder locally

All downloads happen **once per machine** and are reused.

## ??? Verifying Your Git Repository

### Check Repository Size
```powershell
# Show repository size
git count-objects -vH

# List largest tracked files
git ls-files | ForEach-Object { [PSCustomObject]@{Path=$_; SizeMB=[math]::Round((Get-Item $_).Length/1MB, 2)} } | Sort-Object SizeMB -Descending | Select-Object -First 20
```

### Find Accidentally Tracked Large Files
```powershell
# Find files > 5MB in Git
git ls-files | ForEach-Object { if(Test-Path $_) { $size = (Get-Item $_).Length; if($size -gt 5MB) { Write-Host "$_ : $([math]::Round($size/1MB, 2)) MB" } } }
```

### Remove Accidentally Tracked Files
```powershell
# Remove from Git but keep locally
git rm --cached path/to/large/file

# Remove entire folder from Git
git rm -r --cached publish/

# Commit the removal
git commit -m "Remove large files from Git tracking"
```

## ?? Best Practices

1. **Before committing**: Run `git status` and verify no large files are staged
2. **Check file sizes**: Use `git ls-files` commands above periodically
3. **Update .gitignore**: If you add new dependencies, update `.gitignore` immediately
4. **Clean local build artifacts**: Run `dotnet clean` regularly
5. **Don't commit**:
   - Build outputs (`bin/`, `obj/`, `publish/`)
   - Python files (`Python/`, `Wheels/`, `*.pyc`)
   - AI models (`Models/*.gguf`)
   - User data (`*.db`, `*.sqlite`)
   - IDE files (`.vs/`, `*.user`)

## ?? Cloning & Setting Up From Git

When someone clones your repository:

```bash
# 1. Clone the repository (should be < 20 MB)
git clone https://github.com/Berlinxx/MindVault.git

# 2. Open in Visual Studio 2022
# 3. Restore NuGet packages (automatic)
# 4. Build the solution
# 5. Run on Windows - Python & AI will auto-install on first use
```

No manual Python or AI setup needed! ??

## ?? Related Documentation

- **SETUP_GUIDE.md** - Full development setup instructions
- **TROUBLESHOOTING.md** - Common issues and solutions
- **OFFLINE_AI_SETUP.md** - AI feature setup details
- **check_setup.ps1** - Verify Python & AI installation

## ? Summary

Your Git repository should contain **only source code and essential resources**. All large dependencies (Python, AI models, wheels, build outputs) are:
- ? Excluded from Git via `.gitignore`
- ? Downloaded automatically by the app
- ? Stored locally per machine
- ? Never committed to version control

This keeps your repository fast to clone, easy to sync, and efficient for collaboration! ??
