# Git Repository Cleanup Guide

## Problem
Your Git repository contains large files (379MB AI model, 100MB+ Python wheels) that should NOT be in version control. These files make cloning slow and waste GitHub storage.

## Files to Exclude from Git

### ?? MUST EXCLUDE (Very Large)
- `Models/mindvault_qwen2_0.5b_q4_k_m.gguf` (379 MB)
- `Wheels/*.whl` (100 MB+ each)
- `Python311/` folder (50-150 MB)
- `*.db3` database files (user data)

### ? SHOULD KEEP (Small - Source Code)
- All `.cs`, `.xaml`, `.csproj` files
- `README.md`, documentation
- `.gitignore`
- Small resource files (icons, fonts)

## Quick Fix

### Step 1: Run Cleanup Script

```powershell
.\cleanup_git_large_files.ps1
```

This will:
1. Remove large files from Git tracking
2. Update `.gitignore` to prevent future commits
3. Create a commit documenting the removal

### Step 2: Remove from Git History (Optional but Recommended)

To completely remove these files from Git history and reduce repo size:

#### Option A: Using BFG Repo-Cleaner (Easiest)

```powershell
# Install BFG (one-time)
choco install bfg

# Remove large files from history
bfg --delete-files '*.gguf' .
bfg --delete-files '*.whl' .
bfg --delete-folders 'Python311' .
bfg --delete-folders 'Wheels' .

# Clean up
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

#### Option B: Using git filter-repo

```powershell
# Install git-filter-repo
pip install git-filter-repo

# Remove paths from history
git filter-repo --path Models/mindvault_qwen2_0.5b_q4_k_m.gguf --invert-paths --force
git filter-repo --path Wheels/ --invert-paths --force
git filter-repo --path Python311/ --invert-paths --force
```

### Step 3: Force Push to GitHub

?? **WARNING**: This rewrites history. Coordinate with your team!

```powershell
git push origin main --force
```

## Result

### Before Cleanup
```
Repository size: ~500 MB
Clone time: 5-10 minutes
GitHub storage: Wasted on binary files
```

### After Cleanup
```
Repository size: ~5-10 MB
Clone time: 10-20 seconds
GitHub storage: Only source code
```

## How Files Will Be Handled

### For Developers Cloning the Repo:

1. **AI Model File**: Downloaded on first app run (Windows only)
   - App will detect missing model
   - Auto-download from Hugging Face or similar
   - Cached locally in `Models/` folder

2. **Python Distribution**: Installed automatically (Windows only)
   - `PythonBootstrapper.cs` handles installation
   - Downloads from python.org
   - Installs to `Python311/` folder

3. **Wheel Files**: Installed via pip (Windows only)
   - `llama-cpp-python` built/installed on first run
   - App handles dependencies automatically

4. **Database Files**: Created per user
   - Each user has their own `mindvault.db3`
   - Not shared between users
   - In `.gitignore` to prevent commits

### What Stays in Git:

```
? Source code (.cs, .xaml)
? Project files (.csproj, .sln)
? Documentation (.md files)
? Small resources (icons, fonts < 1MB)
? Configuration files
? Scripts (PowerShell, batch)
```

### What's Excluded from Git:

```
? AI models (*.gguf) - downloaded on demand
? Python distribution - auto-installed
? Wheel files (*.whl) - built on demand
? Database files (*.db3) - user-specific
? Build outputs (bin/, obj/)
? User preferences
```

## Updated .gitignore

The `.gitignore` file has been updated to prevent these files from being committed again. Key additions:

```gitignore
# AI Model Files
Models/
*.gguf
*.bin

# Python Distribution
Python311/
python311/

# Python Wheels
Wheels/
*.whl

# Database files
*.db3
*.sqlite3

# AI Dependencies
site-packages/
llama-cpp-python/
```

## Verification

After cleanup, verify the repository size:

```powershell
# Check current repo size
git count-objects -vH

# List files in Git
git ls-files

# Check for large files (should be none > 1MB)
git ls-files | ForEach-Object { 
    if (Test-Path $_) { 
        $size = (Get-Item $_).Length / 1MB
        if ($size -gt 1) {
            Write-Host "$_ : $([math]::Round($size, 2)) MB"
        }
    }
}
```

## Best Practices Going Forward

1. **Before Committing**: Check file sizes
   ```powershell
   git status | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue | Select Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}} }
   ```

2. **Use Git LFS** for any binary files that MUST be versioned:
   ```powershell
   git lfs track "*.png"
   git lfs track "*.jpg"
   ```

3. **Document external dependencies** in README.md:
   - Where models are downloaded from
   - How Python is installed
   - First-run setup process

4. **Never commit**:
   - Binary executables
   - Large data files
   - User-specific configurations
   - Build outputs
   - Temporary files

## Team Coordination

If others have cloned the repo before cleanup:

```powershell
# They should re-clone after you force push
git clone https://github.com/Berlinxx/MindVault.git

# Or reset their local copy (loses local changes!)
git fetch origin
git reset --hard origin/main
git clean -fdx
```

## Summary

? Updated `.gitignore` to exclude large files
? Removed large files from Git tracking  
? Optional: Remove from Git history for smaller repo
? Force push to apply changes to GitHub
? Document how files are obtained on first run

Your repository will now be ~50-100x smaller and much faster to clone!
