# MindVault App Data Reset Script
# This script deletes all app data (similar to "Clear Data" on Android)

Write-Host "================================" -ForegroundColor Cyan
Write-Host " MindVault Data Reset Utility" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "WARNING: This will delete ALL app data!" -ForegroundColor Yellow
Write-Host "  - All flashcards and decks" -ForegroundColor Gray
Write-Host "  - All settings and preferences" -ForegroundColor Gray
Write-Host "  - Python environment" -ForegroundColor Gray
Write-Host "  - Onboarding/Profile state" -ForegroundColor Gray
Write-Host ""

$continue = Read-Host "Continue? (y/N)"
if ($continue -ne "y" -and $continue -ne "Y") {
    Write-Host "Cancelled." -ForegroundColor Gray
    exit
}

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
        
        # Delete entire package directory (includes LocalState, Settings, etc.)
        try {
            Remove-Item -Recurse -Force $pkgPath -ErrorAction Stop
            Write-Host "    ? Deleted entire package" -ForegroundColor Green
        }
        catch {
            Write-Host "    ? Could not delete package (app might be running)" -ForegroundColor Red
            Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Red
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

# 4. Clear Windows Registry Preferences (if any)
Write-Host ""
Write-Host "? Checking Windows Registry..." -ForegroundColor Yellow
$regPaths = @(
    "HKCU:\Software\mindvault",
    "HKCU:\Software\com.companyname.mindvault"
)

foreach ($regPath in $regPaths) {
    if (Test-Path $regPath) {
        Write-Host "  ? Found registry key: $regPath" -ForegroundColor Cyan
        try {
            Remove-Item -Path $regPath -Recurse -Force -ErrorAction Stop
            Write-Host "    ? Deleted registry key" -ForegroundColor Green
        }
        catch {
            Write-Host "    ? Could not delete registry key" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host " ? Reset Complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "All app data has been deleted." -ForegroundColor White
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host "  1. Close Visual Studio (if open)" -ForegroundColor White
Write-Host "  2. Rebuild the solution (Clean + Rebuild)" -ForegroundColor White
Write-Host "  3. Run the app - you'll see:" -ForegroundColor White
Write-Host "     - TaglinePage (splash screen)" -ForegroundColor Cyan
Write-Host "     - OnboardingPage (swipeable tutorial)" -ForegroundColor Cyan
Write-Host "     - SetProfilePage (avatar + username)" -ForegroundColor Cyan
Write-Host "     - HomePage (main menu)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to close..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
