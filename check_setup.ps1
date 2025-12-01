# MindVault Setup Diagnostic Tool
# Run this script to verify all required files are present before running the app

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "  MindVault Setup Checker" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# Check 1: Python311 folder
Write-Host "[1/5] Checking Python311 folder..." -ForegroundColor Yellow
$pythonExe = "Python311\python.exe"
if (Test-Path $pythonExe) {
    Write-Host "  ? Python311\python.exe found" -ForegroundColor Green
    
    # Check Python version
    try {
        $version = & .\Python311\python.exe --version 2>&1
        Write-Host "  ? Python version: $version" -ForegroundColor Green
    } catch {
        Write-Host "  ? Python exists but cannot run!" -ForegroundColor Red
        Write-Host "    Error: $_" -ForegroundColor Red
        $allGood = $false
    }
} else {
    Write-Host "  ? Python311\python.exe NOT FOUND" -ForegroundColor Red
    Write-Host "    Expected at: $((Get-Location).Path)\Python311\python.exe" -ForegroundColor Yellow
    $allGood = $false
}

# Check 2: Python site-packages
Write-Host ""
Write-Host "[2/5] Checking Python packages..." -ForegroundColor Yellow
$sitePackages = "Python311\Lib\site-packages"
if (Test-Path $sitePackages) {
    Write-Host "  ? site-packages folder exists" -ForegroundColor Green
    
    # Check for llama_cpp
    if (Test-Path "Python311\Lib\site-packages\llama_cpp") {
        Write-Host "  ? llama_cpp package found" -ForegroundColor Green
    } else {
        Write-Host "  ? llama_cpp package MISSING" -ForegroundColor Red
        Write-Host "    Python311 may be incomplete" -ForegroundColor Yellow
        $allGood = $false
    }
} else {
    Write-Host "  ? site-packages folder MISSING" -ForegroundColor Red
    $allGood = $false
}

# Check 3: Model file
Write-Host ""
Write-Host "[3/5] Checking AI model file..." -ForegroundColor Yellow
$modelFile = "Models\mindvault_qwen2_0.5b_q4_k_m.gguf"
if (Test-Path $modelFile) {
    $size = [math]::Round((Get-Item $modelFile).Length / 1MB, 2)
    Write-Host "  ? Model file found (${size} MB)" -ForegroundColor Green
    if ($size -lt 100) {
        Write-Host "  ? Model file seems too small (expected ~200-300 MB)" -ForegroundColor Yellow
        Write-Host "    File may be corrupt or incomplete" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? Model file MISSING" -ForegroundColor Red
    Write-Host "    Expected at: $((Get-Location).Path)\$modelFile" -ForegroundColor Yellow
    $allGood = $false
}

# Check 4: Wheels folder
Write-Host ""
Write-Host "[4/5] Checking prebuilt wheels..." -ForegroundColor Yellow
if (Test-Path "Wheels") {
    $wheels = Get-ChildItem "Wheels\llama_cpp_python-*.whl" -ErrorAction SilentlyContinue
    if ($wheels.Count -gt 0) {
        Write-Host "  ? Found $($wheels.Count) llama wheel(s)" -ForegroundColor Green
        foreach ($wheel in $wheels) {
            $wheelSize = [math]::Round($wheel.Length / 1MB, 2)
            Write-Host "    - $($wheel.Name) (${wheelSize} MB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ? Wheels folder exists but no llama wheels found" -ForegroundColor Yellow
        Write-Host "    This is optional if Python311 has llama_cpp installed" -ForegroundColor Gray
    }
} else {
    Write-Host "  ? Wheels folder not found" -ForegroundColor Yellow
    Write-Host "    This is optional if Python311 has llama_cpp installed" -ForegroundColor Gray
}

# Check 5: Test llama_cpp import
Write-Host ""
Write-Host "[5/5] Testing llama_cpp import..." -ForegroundColor Yellow
if (Test-Path $pythonExe) {
    try {
        $output = & .\Python311\python.exe -c "import llama_cpp; print('IMPORT_OK')" 2>&1
        if ($output -match "IMPORT_OK") {
            Write-Host "  ? llama_cpp imports successfully" -ForegroundColor Green
        } else {
            Write-Host "  ? llama_cpp import FAILED" -ForegroundColor Red
            Write-Host "    Output: $output" -ForegroundColor Gray
            $allGood = $false
        }
    } catch {
        Write-Host "  ? Import test FAILED: $_" -ForegroundColor Red
        $allGood = $false
    }
}

# Check 6: Visual C++ Redistributables
Write-Host ""
Write-Host "[6/6] Checking Visual C++ Redistributables..." -ForegroundColor Yellow
$vcRedist = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" -ErrorAction SilentlyContinue
if ($vcRedist) {
    Write-Host "  ? Visual C++ Redistributables detected" -ForegroundColor Green
    Write-Host "    Version: $($vcRedist.Version)" -ForegroundColor Gray
} else {
    Write-Host "  ? Visual C++ Redistributables not detected" -ForegroundColor Yellow
    Write-Host "    Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Yellow
}

# Final summary
Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
if ($allGood) {
    Write-Host "  ? ALL CHECKS PASSED!" -ForegroundColor Green
    Write-Host "  Your setup is ready to use." -ForegroundColor Green
} else {
    Write-Host "  ? SETUP INCOMPLETE" -ForegroundColor Red
    Write-Host "  Fix the errors above before running the app." -ForegroundColor Red
    Write-Host ""
    Write-Host "Common Solutions:" -ForegroundColor Yellow
    Write-Host "  1. Re-extract the project archive completely" -ForegroundColor Gray
    Write-Host "  2. Install VC++ Redistributables from link above" -ForegroundColor Gray
    Write-Host "  3. Add project folder to antivirus exclusions" -ForegroundColor Gray
    Write-Host "  4. Extract to a simple path (e.g., C:\MindVault)" -ForegroundColor Gray
}
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Keep window open
Read-Host "Press Enter to close"
