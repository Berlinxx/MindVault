# MindVault Required Files Checker
# Verifies all files needed for AI flashcard generation

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  MindVault Required Files Check" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$allGood = $true
$missingFiles = @()

# Check 1: Python311
Write-Host "[1/4] Checking Python311 folder..." -ForegroundColor Yellow
if (Test-Path "Python311\python.exe") {
    Write-Host "  ? Python311\python.exe exists" -ForegroundColor Green
} else {
    Write-Host "  ? Python311\python.exe is MISSING" -ForegroundColor Red
    $missingFiles += "Python311\python.exe"
    $allGood = $false
}

# Check 2: flashcard_ai.py script
Write-Host ""
Write-Host "[2/4] Checking flashcard_ai.py script..." -ForegroundColor Yellow
if (Test-Path "Scripts\flashcard_ai.py") {
    $scriptSize = (Get-Item "Scripts\flashcard_ai.py").Length
    Write-Host "  ? Scripts\flashcard_ai.py exists ($scriptSize bytes)" -ForegroundColor Green
} else {
    Write-Host "  ? Scripts\flashcard_ai.py is MISSING" -ForegroundColor Red
    Write-Host "    This file is REQUIRED for AI generation to work" -ForegroundColor Yellow
    $missingFiles += "Scripts\flashcard_ai.py"
    $allGood = $false
}

# Check 3: AI Model file
Write-Host ""
Write-Host "[3/4] Checking AI model file..." -ForegroundColor Yellow
$modelFile = "Models\mindvault_qwen2_0.5b_q4_k_m.gguf"
if (Test-Path $modelFile) {
    $modelSize = (Get-Item $modelFile).Length / 1MB
    $modelSizeMB = [math]::Round($modelSize, 2)
    Write-Host "  ? AI model file exists (${modelSizeMB} MB)" -ForegroundColor Green
    
    if ($modelSize -lt 100) {
        Write-Host "  ? WARNING: Model file seems too small (expected ~200-300 MB)" -ForegroundColor Yellow
        Write-Host "    File may be corrupt or incomplete" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? AI model file is MISSING" -ForegroundColor Red
    Write-Host "    Expected: Models\mindvault_qwen2_0.5b_q4_k_m.gguf (~200-300 MB)" -ForegroundColor Yellow
    $missingFiles += "Models\mindvault_qwen2_0.5b_q4_k_m.gguf"
    $allGood = $false
}

# Check 4: Wheels folder (optional but helpful)
Write-Host ""
Write-Host "[4/4] Checking prebuilt wheels (optional)..." -ForegroundColor Yellow
if (Test-Path "Wheels") {
    $wheels = Get-ChildItem "Wheels\llama_cpp_python-*.whl" -ErrorAction SilentlyContinue
    if ($wheels.Count -gt 0) {
        Write-Host "  ? Found $($wheels.Count) llama wheel(s)" -ForegroundColor Green
    } else {
        Write-Host "  ? Wheels folder exists but no llama wheels found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? Wheels folder not found (optional)" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan

if ($allGood) {
    Write-Host "  ? ALL REQUIRED FILES PRESENT!" -ForegroundColor Green
    Write-Host "  You can build and run the project." -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Open mindvault.sln in Visual Studio" -ForegroundColor Gray
    Write-Host "  2. Set Configuration to Debug" -ForegroundColor Gray
    Write-Host "  3. Set Platform to Windows" -ForegroundColor Gray
    Write-Host "  4. Build ? Rebuild Solution" -ForegroundColor Gray
    Write-Host "  5. Press F5 to run" -ForegroundColor Gray
} else {
    Write-Host "  ? MISSING REQUIRED FILES!" -ForegroundColor Red
    Write-Host ""
    Write-Host "The following files are missing:" -ForegroundColor Yellow
    foreach ($file in $missingFiles) {
        Write-Host "  - $file" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "These files MUST be obtained from:" -ForegroundColor Yellow
    Write-Host "  Option 1: Re-download the complete project archive" -ForegroundColor Gray
    Write-Host "  Option 2: Get missing files from project owner" -ForegroundColor Gray
    Write-Host "  Option 3: Check if files are in .gitignore (for Git repos)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "IMPORTANT NOTES:" -ForegroundColor Yellow
    Write-Host "  • The AI model file is ~200-300 MB (may not be in Git)" -ForegroundColor Gray
    Write-Host "  • flashcard_ai.py is the Python AI script" -ForegroundColor Gray
    Write-Host "  • These files are REQUIRED for AI features to work" -ForegroundColor Gray
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "Press Enter to close"
