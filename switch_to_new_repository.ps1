# Script to Switch to New GitHub Repository
# This script helps you switch your local repository to a new GitHub remote

Write-Host "`n" -NoNewline
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "  ?? Switch to New GitHub Repository" -ForegroundColor Green
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# Step 1: Show current remote
Write-Host "?? Current Remote Configuration:" -ForegroundColor Yellow
git remote -v
Write-Host ""

# Step 2: Verify local repository is clean
Write-Host "?? Checking Local Repository Status..." -ForegroundColor Yellow
$status = git status --porcelain
if ($status) {
    Write-Host "??  Warning: You have uncommitted changes!" -ForegroundColor Red
    Write-Host "Please commit or stash your changes before switching remotes." -ForegroundColor Yellow
    Write-Host ""
    git status
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-Host "Cancelled. Please commit your changes first." -ForegroundColor Yellow
        exit 0
    }
} else {
    Write-Host "? Working tree is clean" -ForegroundColor Green
}
Write-Host ""

# Step 3: Check repository size
Write-Host "?? Checking Repository Size..." -ForegroundColor Yellow
$repoInfo = git count-objects -v
$packSize = ($repoInfo | Select-String "size-pack: (\d+)" | ForEach-Object { $_.Matches.Groups[1].Value })
$sizeMB = [math]::Round([int64]$packSize / 1024, 2)
Write-Host "Current repository size: $sizeMB MB" -ForegroundColor Cyan

if ($sizeMB -gt 100) {
    Write-Host "??  Warning: Repository is larger than expected!" -ForegroundColor Red
    Write-Host "Run verify_git_repo.ps1 to check for large files." -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Instructions for creating new repository
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "?? INSTRUCTIONS" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "To create a new GitHub repository:" -ForegroundColor Yellow
Write-Host "  1. Go to https://github.com/new" -ForegroundColor Cyan
Write-Host "  2. Repository name: Choose a name (e.g., 'MindVault-Clean' or 'MindVault-v2')" -ForegroundColor Cyan
Write-Host "  3. Description: 'MindVault - AI-powered flashcard app (Clean repository)'" -ForegroundColor Cyan
Write-Host "  4. Visibility: Choose Public or Private" -ForegroundColor Cyan
Write-Host "  5. ??  DO NOT check 'Add README', 'Add .gitignore', or 'Add license'" -ForegroundColor Red
Write-Host "  6. Click 'Create repository'" -ForegroundColor Cyan
Write-Host "  7. Copy the repository URL" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Enter when you've created the repository..." -ForegroundColor Yellow
Read-Host

# Step 5: Get new repository URL
Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "?? NEW REPOSITORY URL" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "Enter your new repository URL (e.g., https://github.com/Berlinxx/MindVault-Clean.git):" -ForegroundColor Yellow
$newRemote = Read-Host "URL"

# Validate URL
if ([string]::IsNullOrWhiteSpace($newRemote)) {
    Write-Host "? Error: URL cannot be empty" -ForegroundColor Red
    exit 1
}

if ($newRemote -notmatch "^https://github\.com/[^/]+/[^/]+\.git$" -and $newRemote -notmatch "^git@github\.com:[^/]+/[^/]+\.git$") {
    Write-Host "??  Warning: URL format looks incorrect" -ForegroundColor Yellow
    Write-Host "Expected format: https://github.com/username/repo.git" -ForegroundColor Yellow
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "??  UPDATING REMOTE" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# Step 6: Backup old remote (optional)
Write-Host "?? Backing up old remote as 'origin-old'..." -ForegroundColor Yellow
try {
    git remote rename origin origin-old 2>$null
    Write-Host "? Old remote backed up as 'origin-old'" -ForegroundColor Green
} catch {
    Write-Host "??  Could not backup old remote (might not exist)" -ForegroundColor Yellow
}
Write-Host ""

# Step 7: Add new remote
Write-Host "? Adding new remote as 'origin'..." -ForegroundColor Yellow
try {
    git remote add origin $newRemote
    Write-Host "? New remote added successfully" -ForegroundColor Green
} catch {
    Write-Host "? Error adding new remote: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 8: Verify new remote
Write-Host "?? Verifying new remote configuration..." -ForegroundColor Yellow
git remote -v
Write-Host ""

# Step 9: Ask to push
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "?? PUSH TO NEW REPOSITORY" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "Ready to push to new repository?" -ForegroundColor Yellow
Write-Host "This will push all commits and branches to: $newRemote" -ForegroundColor Cyan
Write-Host ""
$push = Read-Host "Push now? (Y/n)"

if ($push -eq "" -or $push -eq "y" -or $push -eq "Y") {
    Write-Host ""
    Write-Host "?? Pushing to new repository..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        # Push main branch
        Write-Host "Pushing 'main' branch..." -ForegroundColor Cyan
        git push -u origin main
        Write-Host "? Main branch pushed successfully" -ForegroundColor Green
        Write-Host ""
        
        # Ask to push other branches
        $branches = git branch -a | Where-Object { $_ -notmatch "remotes/origin-old" -and $_ -notmatch "HEAD" -and $_ -notmatch "^\*?\s*main$" }
        if ($branches) {
            Write-Host "Other branches detected:" -ForegroundColor Yellow
            $branches | ForEach-Object { Write-Host "  $_" -ForegroundColor Cyan }
            $pushAll = Read-Host "Push all branches? (y/N)"
            if ($pushAll -eq "y" -or $pushAll -eq "Y") {
                git push origin --all
                Write-Host "? All branches pushed" -ForegroundColor Green
            }
        }
        
        # Ask to push tags
        $tags = git tag
        if ($tags) {
            Write-Host ""
            Write-Host "Tags detected:" -ForegroundColor Yellow
            $tags | ForEach-Object { Write-Host "  $_" -ForegroundColor Cyan }
            $pushTags = Read-Host "Push all tags? (y/N)"
            if ($pushTags -eq "y" -or $pushTags -eq "Y") {
                git push origin --tags
                Write-Host "? All tags pushed" -ForegroundColor Green
            }
        }
        
    } catch {
        Write-Host "? Error during push: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "You can try pushing manually:" -ForegroundColor Yellow
        Write-Host "  git push -u origin main" -ForegroundColor Cyan
        exit 1
    }
} else {
    Write-Host "??  Skipped push. You can push manually later:" -ForegroundColor Yellow
    Write-Host "  git push -u origin main" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "? SUCCESS!" -ForegroundColor Green
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""
Write-Host "Your repository is now connected to:" -ForegroundColor Green
Write-Host "  $newRemote" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Verify on GitHub: Go to your new repository and check all files are there" -ForegroundColor Cyan
Write-Host "  2. Test clone: Clone the repository in a new directory and verify it builds" -ForegroundColor Cyan
Write-Host "  3. Update documentation: Update README.md and other docs with new repository URL" -ForegroundColor Cyan
Write-Host "  4. Archive old repository: (Optional) Archive the old repository on GitHub" -ForegroundColor Cyan
Write-Host ""
Write-Host "Old remote is still available as 'origin-old' if needed." -ForegroundColor Cyan
Write-Host ""
Write-Host "Run './verify_git_repo.ps1' to verify repository health." -ForegroundColor Green
Write-Host ""
