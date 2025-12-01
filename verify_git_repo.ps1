# Git Repository Cleanup and Verification Script
# This script checks for large files and ensures the repository is lean

Write-Host "?? Git Repository Size Analysis" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if Git repository exists
if (-not (Test-Path ".git")) {
    Write-Host "? Error: Not a Git repository" -ForegroundColor Red
    exit 1
}

# 1. Show overall repository size
Write-Host "?? Repository Size:" -ForegroundColor Yellow
git count-objects -vH
Write-Host ""

# 2. Find all tracked files larger than 1MB
Write-Host "?? Large Files Currently Tracked (> 1MB):" -ForegroundColor Yellow
$largeFiles = git ls-files | Where-Object { Test-Path $_ } | ForEach-Object {
    $size = (Get-Item $_).Length
    if ($size -gt 1MB) {
        [PSCustomObject]@{
            Path = $_
            SizeMB = [math]::Round($size / 1MB, 2)
        }
    }
} | Sort-Object SizeMB -Descending

if ($largeFiles) {
    $largeFiles | Format-Table -AutoSize
    $totalSize = ($largeFiles | Measure-Object -Property SizeMB -Sum).Sum
    Write-Host "Total size of files > 1MB: $([math]::Round($totalSize, 2)) MB" -ForegroundColor Cyan
} else {
    Write-Host "? No files larger than 1MB found!" -ForegroundColor Green
}
Write-Host ""

# 3. Check for Python/AI files that shouldn't be tracked
Write-Host "?? Checking for Python/AI files (should NOT be tracked):" -ForegroundColor Yellow
$pythonPatterns = @(
    "*.whl", "*.pyc", "*.pyd", "*.gguf", "*.bin", 
    "Python/", "Python311/", "Wheels/", "site-packages/",
    "Models/*.gguf", "*.safetensors", "*.onnx"
)

$foundPythonFiles = @()
foreach ($pattern in $pythonPatterns) {
    $files = git ls-files | Select-String -Pattern $pattern -SimpleMatch
    if ($files) {
        $foundPythonFiles += $files
    }
}

if ($foundPythonFiles.Count -gt 0) {
    Write-Host "??  WARNING: Found Python/AI files tracked in Git:" -ForegroundColor Red
    $foundPythonFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Recommendation: Remove these files from Git:" -ForegroundColor Yellow
    Write-Host "  git rm --cached <file-path>" -ForegroundColor Cyan
} else {
    Write-Host "? No Python/AI files found in Git tracking!" -ForegroundColor Green
}
Write-Host ""

# 4. Check for build artifacts
Write-Host "?? Checking for build artifacts (should NOT be tracked):" -ForegroundColor Yellow
$buildPatterns = @("bin/", "obj/", "publish/", "Publish/")
$foundBuildFiles = @()

foreach ($pattern in $buildPatterns) {
    $files = git ls-files | Where-Object { $_ -like "*$pattern*" }
    if ($files) {
        $foundBuildFiles += $files
    }
}

if ($foundBuildFiles.Count -gt 0) {
    Write-Host "??  WARNING: Found build artifacts tracked in Git:" -ForegroundColor Red
    $foundBuildFiles | Select-Object -First 10 | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if ($foundBuildFiles.Count -gt 10) {
        Write-Host "  ... and $($foundBuildFiles.Count - 10) more files" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Recommendation: Remove these folders from Git:" -ForegroundColor Yellow
    Write-Host "  git rm -r --cached bin/ obj/ publish/" -ForegroundColor Cyan
} else {
    Write-Host "? No build artifacts found in Git tracking!" -ForegroundColor Green
}
Write-Host ""

# 5. Check for database files
Write-Host "?? Checking for database files (should NOT be tracked):" -ForegroundColor Yellow
$dbFiles = git ls-files | Where-Object { $_ -match '\.(db|db3|sqlite|sqlite3)$' }

if ($dbFiles) {
    Write-Host "??  WARNING: Found database files tracked in Git:" -ForegroundColor Red
    $dbFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Recommendation: Remove these files from Git:" -ForegroundColor Yellow
    Write-Host "  git rm --cached <file-path>" -ForegroundColor Cyan
} else {
    Write-Host "? No database files found in Git tracking!" -ForegroundColor Green
}
Write-Host ""

# 6. Check untracked large files
Write-Host "?? Large Untracked Files (> 5MB) in working directory:" -ForegroundColor Yellow
$untrackedLarge = Get-ChildItem -Recurse -File -ErrorAction SilentlyContinue | 
    Where-Object { $_.Length -gt 5MB -and -not ($_.FullName -match '\\\.git\\') } |
    Select-Object @{Name='Path';Expression={$_.FullName.Replace((Get-Location).Path + '\', '')}}, @{Name='SizeMB';Expression={[math]::Round($_.Length/1MB, 2)}} |
    Sort-Object SizeMB -Descending |
    Select-Object -First 20

if ($untrackedLarge) {
    $untrackedLarge | Format-Table -AutoSize
    Write-Host "? These files are not tracked (good!). Verify they're in .gitignore" -ForegroundColor Green
} else {
    Write-Host "? No large untracked files found!" -ForegroundColor Green
}
Write-Host ""

# 7. Summary and recommendations
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "?? SUMMARY & RECOMMENDATIONS" -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host ""

$totalTrackedFiles = (git ls-files | Measure-Object).Count
Write-Host "Total tracked files: $totalTrackedFiles" -ForegroundColor Cyan

# Calculate ideal vs actual
$idealSize = 20  # Target: < 20 MB
$actualSize = [math]::Round((git count-objects -v | Select-String "size-pack: (\d+)" | ForEach-Object { $_.Matches.Groups[1].Value }) / 1024, 2)

if ($actualSize -eq 0) {
    # Fallback: calculate from working directory
    $actualSize = [math]::Round(((git ls-files | Where-Object { Test-Path $_ } | ForEach-Object { (Get-Item $_).Length } | Measure-Object -Sum).Sum) / 1MB, 2)
}

Write-Host "Estimated repository size: ~$actualSize MB" -ForegroundColor Cyan
Write-Host "Target repository size: < $idealSize MB" -ForegroundColor Cyan
Write-Host ""

if ($actualSize -le $idealSize) {
    Write-Host "? EXCELLENT! Your repository is lean and optimal!" -ForegroundColor Green
} elseif ($actualSize -le 50) {
    Write-Host "??  Repository is acceptable but could be optimized." -ForegroundColor Yellow
} else {
    Write-Host "? Repository is too large. Review and remove unnecessary files." -ForegroundColor Red
}

Write-Host ""
Write-Host "Quick Actions:" -ForegroundColor Yellow
Write-Host "  • To remove published builds: git rm -r --cached publish/" -ForegroundColor Cyan
Write-Host "  • To remove Python files: git rm --cached Python/ Wheels/" -ForegroundColor Cyan
Write-Host "  • To remove AI models: git rm --cached Models/*.gguf" -ForegroundColor Cyan
Write-Host "  • After changes: git commit -m 'Clean up repository size'" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? See GIT_IGNORE_GUIDE.md for detailed information" -ForegroundColor Green
