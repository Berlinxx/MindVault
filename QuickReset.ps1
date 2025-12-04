# Quick Reset - No Confirmation (for development)
Write-Host "? Quick Resetting MindVault..." -ForegroundColor Cyan

# Close the app if running
Get-Process -Name "mindvault" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Delete LocalAppData
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\MindVault" -ErrorAction SilentlyContinue

# Delete MSIX packages
$packagesPath = "$env:LOCALAPPDATA\Packages"
Get-ChildItem $packagesPath -Directory -ErrorAction SilentlyContinue | 
    Where-Object { $_.Name -like "*mindvault*" } | 
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Delete registry keys
Remove-Item -Path "HKCU:\Software\mindvault" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\com.companyname.mindvault" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "? Done! Rebuild and run the app." -ForegroundColor Green
