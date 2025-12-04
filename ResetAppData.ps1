# MindVault App Data Reset Script
# This script deletes all app data (similar to "Clear Data" on Android)

Write-Host "================================" -ForegroundColor Cyan
Write-Host " MindVault Data Reset Utility" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# 1. Delete LocalApplicationData
$localAppData = "$env:LOCALAPPDATA\MindVault"
if (Test-Path $localAppData) {
    Write-Host "? Deleting LocalApplicationData..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $localAppData -ErrorAction SilentlyContinue
    Write-Host "  ? Deleted: $localAppData" -ForegroundColor Green
} else {
    Write-Host "  ? LocalApplicationData not found" -ForegroundColor Gray
}

# 2. Find and delete all package directories (MSIX/AppX)
Write-Host ""
Write-Host "? Searching for MSIX package directories..." -ForegroundColor Yellow
$packagesPath = "$env:LOCALAPPDATA\Packages"
$mindvaultPackages = Get-ChildItem $packagesPath -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*mindvault*" }

if ($mindvaultPackages) {
    foreach ($pkg in $mindvaultPackages) {
        $pkgPath = $pkg.FullName
        Write-Host "  ? Found: $($pkg.Name)" -ForegroundColor Cyan
        
        # Delete LocalState (database)
        $localState = Join-Path $pkgPath "LocalState"
        if (Test-Path $localState) {
            Remove-Item -Recurse -Force $localState -ErrorAction SilentlyContinue
            Write-Host "    ? Deleted LocalState" -ForegroundColor Green
        }
        
        # Delete LocalCache
        $localCache = Join-Path $pkgPath "LocalCache"
        if (Test-Path $localCache) {
            Remove-Item -Recurse -Force $localCache -ErrorAction SilentlyContinue
            Write-Host "    ? Deleted LocalCache" -ForegroundColor Green
        }
        
        # Delete Settings
        $settings = Join-Path $pkgPath "Settings"
        if (Test-Path $settings) {
            Remove-Item -Recurse -Force $settings -ErrorAction SilentlyContinue
            Write-Host "    ? Deleted Settings" -ForegroundColor Green
        }
        
        # Delete TempState
        $tempState = Join-Path $pkgPath "TempState"
        if (Test-Path $tempState) {
            Remove-Item -Recurse -Force $tempState -ErrorAction SilentlyContinue
            Write-Host "    ? Deleted TempState" -ForegroundColor Green
        }
    }
} else {
    Write-Host "  ? No MSIX packages found" -ForegroundColor Gray
}

# 3. Clear Visual Studio debug AppData (for Debug builds)
Write-Host ""
Write-Host "? Clearing Visual Studio debug AppData..." -ForegroundColor Yellow
$vsDebugPaths = @(
    "$env:USERPROFILE\AppData\Local\Packages\com.companyname.mindvault*",
    "$env:USERPROFILE\AppData\Local\Microsoft\VisualStudio\*\AppData\Local\Packages\*mindvault*"
)

foreach ($pattern in $vsDebugPaths) {
    $matches = Get-Item $pattern -ErrorAction SilentlyContinue
    if ($matches) {
        foreach ($match in $matches) {
            Write-Host "  ? Deleting: $($match.FullName)" -ForegroundColor Cyan
            Remove-Item -Recurse -Force $match.FullName -ErrorAction SilentlyContinue
            Write-Host "    ? Deleted" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host " ? Reset Complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "All app data has been deleted." -ForegroundColor White
Write-Host "Next time you run the app, it will be like a fresh install." -ForegroundColor White
Write-Host ""
Write-Host "Press any key to close..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
