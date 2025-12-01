# MindVault - Git Repository Best Practices

## ?? Quick Reference

### ? DO Commit to Git:
- Source code files (`.cs`, `.xaml`)
- Project files (`.csproj`, `.sln`)
- Resources (images, fonts, sounds in `Resources/`)
- Documentation (`.md` files)
- Setup scripts (`.ps1`, `.bat`, `.py`)
- Configuration files

### ? DO NOT Commit to Git:
- **Build outputs** (`bin/`, `obj/`, `publish/`)
- **Python runtime** (`Python/`, `Python311/`)
- **Python wheels** (`Wheels/`, `*.whl`)
- **AI models** (`Models/*.gguf`, `*.bin`)
- **User databases** (`*.db`, `*.sqlite`)
- **IDE files** (`.vs/`, `*.user`)
- **Log files** (`*.log`, `logcat*.txt`)
- **Screenshots** (`device_screenshot*.png`)

## ?? Before Committing - Checklist

```powershell
# 1. Check what you're about to commit
git status

# 2. Look for large files
git ls-files --stage | Where-Object { $_ -match '\d+\s+([^\s]+)' } | ForEach-Object { if ((Get-Item $Matches[1] -ErrorAction SilentlyContinue).Length -gt 5MB) { $Matches[1] } }

# 3. Run verification script
.\verify_git_repo.ps1

# 4. Stage only what you need
git add <specific-files>

# 5. Commit with meaningful message
git commit -m "Add feature: <description>"
```

## ?? Common Mistakes to Avoid

### Mistake 1: Committing Build Outputs
```powershell
# ? DON'T DO THIS
git add bin/
git add obj/
git add publish/

# ? If accidentally staged, remove:
git reset HEAD bin/ obj/ publish/
```

### Mistake 2: Committing Python/AI Files
```powershell
# ? DON'T DO THIS
git add Python/
git add Wheels/
git add Models/*.gguf

# ? If accidentally committed, remove from tracking:
git rm --cached -r Python/ Wheels/ Models/
```

### Mistake 3: Committing User Data
```powershell
# ? DON'T DO THIS
git add *.db
git add *.sqlite

# ? These should never be in Git
git rm --cached *.db *.sqlite
```

## ?? Repository Size Targets

| Component | Target Size | Notes |
|-----------|-------------|-------|
| Source code | < 10 MB | C# and XAML files |
| Resources | < 5 MB | Images, fonts, sounds |
| Documentation | < 1 MB | Markdown files |
| **Total** | **< 20 MB** | **Ideal repository size** |

## ??? Useful Commands

### Check Repository Size
```powershell
git count-objects -vH
```

### Find Large Files
```powershell
git ls-files | ForEach-Object { 
    if (Test-Path $_) { 
        [PSCustomObject]@{
            Path=$_; 
            SizeMB=[math]::Round((Get-Item $_).Length/1MB, 2)
        } 
    } 
} | Where-Object SizeMB -gt 1 | Sort-Object SizeMB -Descending
```

### Clean Working Directory
```powershell
# Remove build artifacts
dotnet clean

# Remove untracked files (be careful!)
git clean -fd -e Python/ -e Wheels/ -e Models/
```

### Remove File from Git History
```powershell
# Remove single file
git rm --cached path/to/file

# Remove folder recursively
git rm -r --cached folder/

# Commit the change
git commit -m "Remove large files from tracking"
```

## ?? Clone & Setup Workflow

When someone clones the repository:

```bash
# 1. Clone (fast, only ~2-20 MB)
git clone https://github.com/Berlinxx/MindVault.git
cd MindVault

# 2. Restore NuGet packages
dotnet restore

# 3. Build
dotnet build

# 4. Run (Windows)
# Python & AI dependencies auto-install on first launch
dotnet run
```

**No manual setup needed!** ??

## ?? Verify Your Setup

Run the verification script regularly:

```powershell
.\verify_git_repo.ps1
```

This will:
- ? Check repository size
- ? Find large tracked files
- ? Detect Python/AI files in Git
- ? Find build artifacts
- ? List untracked large files
- ? Provide recommendations

## ?? Troubleshooting

### "I accidentally committed a large file!"

```powershell
# If not pushed yet
git reset HEAD~1
git rm --cached large-file.bin
git commit -m "Remove large file"

# If already pushed (careful, rewrites history!)
git rm --cached large-file.bin
git commit --amend --no-edit
git push --force-with-lease
```

### "My repository is too large!"

```powershell
# 1. Find large files
.\verify_git_repo.ps1

# 2. Remove from Git
git rm --cached <large-file-path>

# 3. Clean Git history (advanced)
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch <path>" \
  --prune-empty --tag-name-filter cat -- --all
```

### "What should I do with Python/AI files?"

**Answer**: Nothing! They're automatically managed:
- Excluded via `.gitignore`
- Downloaded by `PythonBootstrapper.cs` on first run
- Stored locally in `%LOCALAPPDATA%\MindVault\`
- Never committed to Git

## ?? Related Files

- **`.gitignore`** - Defines what files to exclude
- **`GIT_IGNORE_GUIDE.md`** - Detailed exclusion explanations
- **`verify_git_repo.ps1`** - Repository verification script
- **`SETUP_GUIDE.md`** - Development environment setup
- **`TROUBLESHOOTING.md`** - Common issues and solutions

## ?? Key Principles

1. **Source Code Only**: Only commit code you wrote
2. **No Build Artifacts**: Build outputs are generated, not stored
3. **No Dependencies**: External dependencies are downloaded/restored
4. **No User Data**: Never commit personal/user-specific files
5. **Keep It Small**: Target repository size < 20 MB
6. **Document Everything**: Update docs when adding exclusions

## ? Summary

**Golden Rule**: If it can be **generated**, **downloaded**, or is **user-specific**, it should **NOT** be in Git!

Your repository should contain:
- ?? Source code you write
- ?? Documentation you create  
- ?? Essential resources (images, fonts)

Everything else is auto-generated or auto-downloaded! ??

---

**Need Help?** Check:
- `.\verify_git_repo.ps1` - Automated checks
- `GIT_IGNORE_GUIDE.md` - Detailed guide
- `TROUBLESHOOTING.md` - Common issues
