# Delete Corrupted MindVault Database
# Run this script BEFORE starting the app

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "   MindVault Database Cleanup Utility" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Find the database file
$appDataPath = "$env:LOCALAPPDATA\Packages"
$mindvaultFolder = Get-ChildItem -Path $appDataPath -Filter "*mindvault*" -Directory -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -eq $mindvaultFolder) {
    Write-Host "Could not find MindVault app data folder." -ForegroundColor Yellow
    Write-Host "Trying alternative location..." -ForegroundColor Yellow
    
    # Try user-specific path
    $altPath = "$env:LOCALAPPDATA\User Name\com.companyname.mindvault\Data"
    if (Test-Path $altPath) {
        $dbPath = Join-Path $altPath "mindvault.db3"
    } else {
        Write-Host "ERROR: Cannot find MindVault database location" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please manually navigate to:" -ForegroundColor Yellow
        Write-Host "%LOCALAPPDATA%\Packages\[MindVault folder]\LocalState\" -ForegroundColor Yellow
        Write-Host "and delete the mindvault.db3 file" -ForegroundColor Yellow
        pause
        exit 1
    }
} else {
    $dbPath = Join-Path $mindvaultFolder.FullName "LocalState\mindvault.db3"
}

Write-Host "Database location: $dbPath" -ForegroundColor White
Write-Host ""

if (-not (Test-Path $dbPath)) {
    Write-Host "Database file does not exist (this is OK)" -ForegroundColor Green
    Write-Host "The app will create a fresh database on next launch" -ForegroundColor Green
    Write-Host ""
    pause
    exit 0
}

# Check if file is locked
try {
    $fileStream = [System.IO.File]::Open($dbPath, 'Open', 'Read', 'None')
    $fileStream.Close()
    $fileStream.Dispose()
    $isLocked = $false
} catch {
    $isLocked = $true
}

if ($isLocked) {
    Write-Host "WARNING: Database file is currently in use!" -ForegroundColor Red
    Write-Host "Please close the MindVault app completely and try again." -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

# Create backup
$backupPath = $dbPath + ".backup_" + (Get-Date -Format "yyyyMMdd_HHmmss")
Write-Host "Creating backup: $backupPath" -ForegroundColor Yellow
try {
    Copy-Item $dbPath $backupPath -Force
    Write-Host "Backup created successfully" -ForegroundColor Green
} catch {
    Write-Host "WARNING: Could not create backup: $_" -ForegroundColor Yellow
}
Write-Host ""

# Delete corrupted database
Write-Host "Deleting corrupted database..." -ForegroundColor Yellow
try {
    Remove-Item $dbPath -Force
    Write-Host "Database deleted successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now start the MindVault app." -ForegroundColor Green
    Write-Host "It will create a fresh database automatically." -ForegroundColor Green
} catch {
    Write-Host "ERROR: Could not delete database: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please try:" -ForegroundColor Yellow
    Write-Host "1. Close ALL instances of MindVault" -ForegroundColor Yellow
    Write-Host "2. Close Visual Studio debugger" -ForegroundColor Yellow
    Write-Host "3. Run this script again" -ForegroundColor Yellow
}

Write-Host ""
pause
