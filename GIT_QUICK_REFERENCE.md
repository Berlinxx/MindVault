# ?? Git Quick Reference - MindVault

## Repository Status: ? OPTIMIZED (2.47 MB)

---

## ?? Essential Commands

### Daily Workflow
```powershell
# Before committing - check for issues
.\pre_commit_check.ps1

# Stage your changes
git add <specific-files>

# Commit with message
git commit -m "Description of changes"

# Push to remote
git push
```

### Weekly Maintenance
```powershell
# Verify repository health
.\verify_git_repo.ps1

# Clean local build artifacts
dotnet clean

# Check repository size
git count-objects -vH
```

---

## ? Commit Rules

### ? DO Commit
- `.cs` - C# source files
- `.xaml` - XAML UI files
- `.csproj`, `.sln` - Project files
- `Resources/*` - Images, fonts, sounds
- `*.md` - Documentation
- `.ps1`, `.bat` - Setup scripts

### ? DON'T Commit
- `bin/`, `obj/`, `publish/` - Build outputs
- `Python/`, `Wheels/` - Python dependencies
- `Models/*.gguf` - AI models (500MB!)
- `*.db`, `*.sqlite` - User databases
- `.vs/`, `*.user` - IDE files
- `*.log`, `logcat*.txt` - Log files

---

## ??? Common Scenarios

### "I accidentally staged a large file"
```powershell
# Remove from staging
git reset HEAD <file-path>
```

### "I committed something by mistake"
```powershell
# Undo last commit (keeps changes)
git reset HEAD~1

# Remove file from Git but keep locally
git rm --cached <file-path>
```

### "Check what I'm about to commit"
```powershell
# Run pre-commit check
.\pre_commit_check.ps1

# See staged changes
git diff --cached
```

### "Find large files in my repo"
```powershell
# Run full analysis
.\verify_git_repo.ps1
```

---

## ?? Size Targets

| Item | Target | Current |
|------|--------|---------|
| Repository | < 20 MB | **2.47 MB** ? |
| Single File | < 5 MB | **0.48 MB max** ? |

---

## ?? One-Command Checks

```powershell
# Full pre-commit verification
.\pre_commit_check.ps1 && git status

# Full repository health check
.\verify_git_repo.ps1
```

---

## ?? Documentation

- **GIT_BEST_PRACTICES.md** - Developer guide
- **GIT_IGNORE_GUIDE.md** - What's excluded & why
- **GIT_REPOSITORY_OPTIMIZATION_SUMMARY.md** - Full summary
- **SETUP_GUIDE.md** - Development setup
- **TROUBLESHOOTING.md** - Common issues

---

## ?? Quick Help

**Problem**: Large file warning  
**Solution**: `git reset HEAD <file>` then add to `.gitignore`

**Problem**: Too many files staged  
**Solution**: `git reset HEAD .` then stage selectively

**Problem**: Commit blocked  
**Solution**: Read error messages, fix issues, try again

---

## ? Remember

1. ?? **Check before commit**: `.\pre_commit_check.ps1`
2. ?? **Keep it small**: Only source code & resources
3. ?? **No builds**: They're auto-generated
4. ?? **No Python**: Auto-downloads on first run
5. ?? **No AI models**: Auto-downloads when needed

---

**Status**: Repository optimized | **Size**: 2.47 MB | **Target**: < 20 MB ?
