@echo off
REM Check if all required AI files are present

echo.
echo ===================================
echo   MindVault Required Files Check
echo ===================================
echo.

set ERROR_FOUND=0

REM Check 1: Python311
echo [1/4] Checking Python311...
if exist "Python311\python.exe" (
    echo   [OK] Python311\python.exe exists
) else (
    echo   [ERROR] Python311\python.exe is MISSING
    set ERROR_FOUND=1
)

REM Check 2: flashcard_ai.py script
echo.
echo [2/4] Checking flashcard_ai.py script...
if exist "Scripts\flashcard_ai.py" (
    echo   [OK] Scripts\flashcard_ai.py exists
) else (
    echo   [ERROR] Scripts\flashcard_ai.py is MISSING
    echo   This file is required for AI generation
    set ERROR_FOUND=1
)

REM Check 3: AI Model file
echo.
echo [3/4] Checking AI model file...
if exist "Models\mindvault_qwen2_0.5b_q4_k_m.gguf" (
    for %%A in ("Models\mindvault_qwen2_0.5b_q4_k_m.gguf") do (
        set SIZE=%%~zA
        set /A SIZE_MB=!SIZE! / 1048576
        echo   [OK] Model file exists (%%~zA bytes = ~!SIZE_MB! MB)
    )
) else (
    echo   [ERROR] AI model file is MISSING
    echo   Expected: Models\mindvault_qwen2_0.5b_q4_k_m.gguf
    echo   This is a large file (~200-300 MB)
    set ERROR_FOUND=1
)

REM Check 4: Wheels folder (optional)
echo.
echo [4/4] Checking prebuilt wheels (optional)...
if exist "Wheels\llama_cpp_python-*.whl" (
    echo   [OK] Wheels folder exists with llama wheels
) else (
    echo   [INFO] No prebuilt wheels found (optional if Python311 has llama installed)
)

echo.
echo ===================================
if %ERROR_FOUND%==0 (
    echo   All required files present!
    echo   You can build and run the project.
) else (
    echo   MISSING REQUIRED FILES!
    echo.
    echo   Missing files must be obtained from:
    echo   1. Re-download the complete project archive
    echo   2. Or get missing files from project owner
    echo.
    echo   Missing files:
    if not exist "Scripts\flashcard_ai.py" echo     - Scripts\flashcard_ai.py
    if not exist "Models\mindvault_qwen2_0.5b_q4_k_m.gguf" echo     - Models\mindvault_qwen2_0.5b_q4_k_m.gguf (~200-300 MB)
    if not exist "Python311\python.exe" echo     - Python311\python.exe
)
echo ===================================
echo.
pause
