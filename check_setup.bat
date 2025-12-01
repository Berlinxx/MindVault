@echo off
REM MindVault Quick Setup Checker
REM Double-click this file to verify your setup

echo.
echo ===================================
echo   MindVault Setup Checker
echo ===================================
echo.

REM Check Python311
if exist "Python311\python.exe" (
    echo [OK] Python311 found
) else (
    echo [ERROR] Python311 folder is MISSING
    echo.
    echo Please re-extract the project completely.
    goto :error
)

REM Check Model
if exist "Models\mindvault_qwen2_0.5b_q4_k_m.gguf" (
    echo [OK] AI model found
) else (
    echo [ERROR] AI model file is MISSING
    echo.
    echo Expected: Models\mindvault_qwen2_0.5b_q4_k_m.gguf
    goto :error
)

REM Test Python
echo.
echo Testing Python execution...
Python311\python.exe --version
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Python failed to run
    echo.
    echo Install Visual C++ Redistributables:
    echo https://aka.ms/vs/17/release/vc_redist.x64.exe
    goto :error
)

REM Test llama_cpp import
echo.
echo Testing llama_cpp package...
Python311\python.exe -c "import llama_cpp; print('[OK] llama_cpp ready')"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] llama_cpp import failed
    echo.
    echo Python311 folder may be incomplete or corrupted.
    goto :error
)

echo.
echo ===================================
echo   ALL CHECKS PASSED!
echo   Your setup is ready.
echo ===================================
echo.
echo You can now open mindvault.sln and build the project.
echo.
pause
exit /b 0

:error
echo.
echo ===================================
echo   SETUP INCOMPLETE
echo ===================================
echo.
echo Please:
echo 1. Re-extract the project archive
echo 2. Verify all files were extracted
echo 3. Run this checker again
echo.
pause
exit /b 1
