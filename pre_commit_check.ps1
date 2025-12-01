# Pre-Commit Verification Script
# Run this before committing to ensure no large or unwanted files are staged

Write-Host "?? Pre-Commit Verification Check" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if there are staged files
$stagedFiles = git diff --cached --name-only
if (-not $stagedFiles) {
    Write-Host "??  No files are staged for commit." -ForegroundColor Yellow
    Write-Host "Use 'git add <file>' to stage files first." -ForegroundColor Yellow
    exit 0
}

$issuesFound = $false

# 1. Check for large files (> 5MB)
Write-Host "?? Checking for large files (> 5MB)..." -ForegroundColor Yellow
$largeFiles = $stagedFiles | Where-Object { 
    Test-Path $_ -and (Get-Item $_).Length -gt 5MB 
} | ForEach-Object {
    [PSCustomObject]@{
        File = $_
        SizeMB = [math]::Round((Get-Item $_).Length / 1MB, 2)
    }
}

if ($largeFiles) {
    Write-Host "? ERROR: Large files detected!" -ForegroundColor Red
    $largeFiles | Format-Table -AutoSize
    Write-Host "Action required: Remove these files from staging:" -ForegroundColor Yellow
    $largeFiles | ForEach-Object { Write-Host "  git reset HEAD $($_.File)" -ForegroundColor Cyan }
    $issuesFound = $true
} else {
    Write-Host "? No large files found" -ForegroundColor Green
}
Write-Host ""

# 2. Check for Python/AI files
Write-Host "?? Checking for Python/AI files..." -ForegroundColor Yellow
$pythonPatterns = @('\.whl$', '\.pyc$', '\.pyd$', '\.gguf$', '\.bin$', 'Python/', 'Wheels/', 'site-packages/', 'Models/.*\.gguf')
$pythonFiles = $stagedFiles | Where-Object {
    $file = $_
    $pythonPatterns | Where-Object { $file -match $_ }
}

if ($pythonFiles) {
    Write-Host "? ERROR: Python/AI files detected!" -ForegroundColor Red
    $pythonFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "These files should NOT be committed. Remove them:" -ForegroundColor Yellow
    Write-Host "  git reset HEAD Python/ Wheels/ Models/" -ForegroundColor Cyan
    $issuesFound = $true
} else {
    Write-Host "? No Python/AI files found" -ForegroundColor Green
}
Write-Host ""

# 3. Check for build artifacts
Write-Host "?? Checking for build artifacts..." -ForegroundColor Yellow
$buildPatterns = @('bin/', 'obj/', 'publish/', 'Publish/')
$buildFiles = $stagedFiles | Where-Object {
    $file = $_
    $buildPatterns | Where-Object { $file -match $_ }
}

if ($buildFiles) {
    Write-Host "? ERROR: Build artifacts detected!" -ForegroundColor Red
    $buildFiles | Select-Object -First 10 | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if ($buildFiles.Count -gt 10) {
        Write-Host "  ... and $($buildFiles.Count - 10) more" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "These should NOT be committed. Remove them:" -ForegroundColor Yellow
    Write-Host "  git reset HEAD bin/ obj/ publish/" -ForegroundColor Cyan
    $issuesFound = $true
} else {
    Write-Host "? No build artifacts found" -ForegroundColor Green
}
Write-Host ""

# 4. Check for database files
Write-Host "?? Checking for database files..." -ForegroundColor Yellow
$dbFiles = $stagedFiles | Where-Object { $_ -match '\.(db|db3|sqlite|sqlite3)$' }

if ($dbFiles) {
    Write-Host "? ERROR: Database files detected!" -ForegroundColor Red
    $dbFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "User databases should NOT be committed. Remove them:" -ForegroundColor Yellow
    $dbFiles | ForEach-Object { Write-Host "  git reset HEAD $_" -ForegroundColor Cyan }
    $issuesFound = $true
} else {
    Write-Host "? No database files found" -ForegroundColor Green
}
Write-Host ""

# 5. Check for IDE and OS files
Write-Host "?? Checking for IDE/OS files..." -ForegroundColor Yellow
$idePatterns = @('\.vs/', '\.vscode/', '\.idea/', '\.user$', '\.suo$', '\.DS_Store$', 'Thumbs.db$')
$ideFiles = $stagedFiles | Where-Object {
    $file = $_
    $idePatterns | Where-Object { $file -match $_ }
}

if ($ideFiles) {
    Write-Host "??  WARNING: IDE/OS files detected:" -ForegroundColor Yellow
    $ideFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Consider removing these (usually not needed):" -ForegroundColor Yellow
    $ideFiles | ForEach-Object { Write-Host "  git reset HEAD $_" -ForegroundColor Cyan }
    Write-Host ""
} else {
    Write-Host "? No IDE/OS files found" -ForegroundColor Green
}
Write-Host ""

# 6. Show staged files summary
Write-Host "?? Files to be committed:" -ForegroundColor Cyan
$stagedCount = ($stagedFiles | Measure-Object).Count
Write-Host "Total: $stagedCount files" -ForegroundColor Cyan
Write-Host ""

# Show breakdown by type
$codeFiles = $stagedFiles | Where-Object { $_ -match '\.(cs|xaml|csproj|sln)$' }
$docFiles = $stagedFiles | Where-Object { $_ -match '\.(md|txt)$' }
$scriptFiles = $stagedFiles | Where-Object { $_ -match '\.(ps1|bat|sh|py)$' }
$resourceFiles = $stagedFiles | Where-Object { $_ -match '^Resources/' }
$otherFiles = $stagedFiles | Where-Object { 
    $_ -notmatch '\.(cs|xaml|csproj|sln|md|txt|ps1|bat|sh|py)$' -and $_ -notmatch '^Resources/' 
}

if ($codeFiles) { Write-Host "  Code files: $(($codeFiles | Measure-Object).Count)" -ForegroundColor Cyan }
if ($docFiles) { Write-Host "  Documentation: $(($docFiles | Measure-Object).Count)" -ForegroundColor Cyan }
if ($scriptFiles) { Write-Host "  Scripts: $(($scriptFiles | Measure-Object).Count)" -ForegroundColor Cyan }
if ($resourceFiles) { Write-Host "  Resources: $(($resourceFiles | Measure-Object).Count)" -ForegroundColor Cyan }
if ($otherFiles) { Write-Host "  Other: $(($otherFiles | Measure-Object).Count)" -ForegroundColor Cyan }
Write-Host ""

# Final verdict
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "VERDICT" -ForegroundColor Cyan
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host ""

if ($issuesFound) {
    Write-Host "? COMMIT BLOCKED - Issues found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please fix the issues above before committing." -ForegroundColor Yellow
    Write-Host "Run 'git status' to review staged files." -ForegroundColor Yellow
    Write-Host ""
    exit 1
} else {
    Write-Host "? All checks passed! Safe to commit." -ForegroundColor Green
    Write-Host ""
    Write-Host "Proceed with commit:" -ForegroundColor Cyan
    Write-Host "  git commit -m 'Your commit message'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Or review files first:" -ForegroundColor Cyan
    Write-Host "  git diff --cached" -ForegroundColor Cyan
    Write-Host ""
    exit 0
}
