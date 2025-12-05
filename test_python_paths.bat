@echo off
echo.
echo ========================================
echo   Python Path Test
echo ========================================
echo.

echo Checking LocalAppData path:
echo %LOCALAPPDATA%
echo.

echo Expected MindVault folder:
echo %LOCALAPPDATA%\MindVault
echo.

echo Expected Python folder:
echo %LOCALAPPDATA%\MindVault\Python311
echo.

echo Checking if folders exist:
if exist "%LOCALAPPDATA%\MindVault" (
    echo [OK] MindVault folder exists
    
    if exist "%LOCALAPPDATA%\MindVault\Python311" (
        echo [OK] Python311 folder exists
        
        echo.
        echo Searching for python.exe:
        dir "%LOCALAPPDATA%\MindVault\Python311\python.exe" 2>nul
        if %errorLevel% equ 0 (
            echo [OK] Found at root level
        ) else (
            echo [NOT FOUND] Looking in subdirectories...
            dir "%LOCALAPPDATA%\MindVault\Python311\*python.exe" /s /b
        )
        
        echo.
        echo Python311 folder structure:
        dir "%LOCALAPPDATA%\MindVault\Python311" /b
    ) else (
        echo [NOT FOUND] Python311 folder does not exist
    )
) else (
    echo [NOT FOUND] MindVault folder does not exist
)

echo.
echo Checking log file:
if exist "%LOCALAPPDATA%\MindVault\run_log.txt" (
    echo [OK] Log file exists
    echo.
    echo Last 20 lines of log:
    powershell -Command "Get-Content '%LOCALAPPDATA%\MindVault\run_log.txt' -Tail 20"
) else (
    echo [NOT FOUND] Log file does not exist yet
    echo This will be created when you first run AI Summarize
)

echo.
pause
