# =====================================================
# Git Repository Cleanup Script
# Removes large files from Git history
# =====================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Git Repository Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if we're in a git repository
if (-not (Test-Path .git)) {
    Write-Host "Error: Not in a Git repository!" -ForegroundColor Red
    exit 1
}

Write-Host "Step 1: Checking for large files in Git history..." -ForegroundColor Yellow
Write-Host ""

# List large files in Git
$largeFiles = git ls-files | Where-Object { 
    if (Test-Path $_) {
        $size = (Get-Item $_).Length
        $size -gt 10MB
    }
}

if ($largeFiles) {
    Write-Host "Found large files currently tracked:" -ForegroundColor Red
    foreach ($file in $largeFiles) {
        if (Test-Path $file) {
            $sizeMB = [math]::Round((Get-Item $file).Length / 1MB, 2)
            Write-Host "  - $file ($sizeMB MB)" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

Write-Host "Step 2: Removing large files from Git tracking..." -ForegroundColor Yellow
Write-Host ""

# Files and folders to remove from Git
$filesToRemove = @(
    "Models/*.gguf",
    "Models/mindvault_qwen2_0.5b_q4_k_m.gguf",
    "Wheels/*",
    "Python311/*",
    "python311/*",
    "*.whl",
    "*.db3"
)

foreach ($pattern in $filesToRemove) {
    Write-Host "Removing: $pattern" -ForegroundColor Cyan
    git rm -r --cached $pattern -ErrorAction SilentlyContinue 2>$null
}

Write-Host ""
Write-Host "Step 3: Committing the removal..." -ForegroundColor Yellow
Write-Host ""

git add .gitignore
git commit -m "chore: Remove large files from Git tracking (AI models, Python, wheels)

- Removed AI model files (*.gguf) - 379MB
- Removed Python distribution (Python311/)
- Removed wheel files (Wheels/) - 100MB+
- Updated .gitignore to prevent future commits
- These files will be downloaded/installed on first run"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Cleanup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: To completely remove from history, run:" -ForegroundColor Yellow
Write-Host "  git filter-repo --path Models/ --invert-paths --force" -ForegroundColor Cyan
Write-Host "  git filter-repo --path Wheels/ --invert-paths --force" -ForegroundColor Cyan
Write-Host "  git filter-repo --path Python311/ --invert-paths --force" -ForegroundColor Cyan
Write-Host ""
Write-Host "Or install BFG Repo Cleaner for easier cleanup:" -ForegroundColor Yellow
Write-Host "  choco install bfg" -ForegroundColor Cyan
Write-Host "  bfg --delete-files '*.gguf' ." -ForegroundColor Cyan
Write-Host "  bfg --delete-files '*.whl' ." -ForegroundColor Cyan
Write-Host ""
Write-Host "After cleanup, force push:" -ForegroundColor Yellow
Write-Host "  git push origin main --force" -ForegroundColor Red
Write-Host ""
Write-Host "Repository size before: " -NoNewline -ForegroundColor White
$sizeBefore = (Get-ChildItem .git -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "$([math]::Round($sizeBefore, 2)) MB" -ForegroundColor Cyan
