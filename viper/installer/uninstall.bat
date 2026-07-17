@echo off
REM ============================================================================
REM Viper Uninstaller — Requires Administrator
REM ============================================================================

set SERVICE_NAME=ViperService
set WATCHDOG_NAME=ViperWatchdog
set CONFIG_DIR=%ProgramData%\Viper

echo Viper Uninstaller
echo ============================================================================
echo.

REM Check for admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This uninstaller requires Administrator privileges.
    echo Right-click and select "Run as administrator".
    pause
    exit /b 1
)

echo [1/3] Stopping services...
sc stop %SERVICE_NAME% >nul 2>&1
sc stop %WATCHDOG_NAME% >nul 2>&1
timeout /t 3 /nobreak >nul
echo    Done.

echo [2/3] Removing services from SCM...
sc delete %SERVICE_NAME%
sc delete %WATCHDOG_NAME%
echo    Done.

echo.
set /p DELETECONFIG="Delete configuration data at %CONFIG_DIR%? (Y/N): "
if /i "%DELETECONFIG%"=="Y" (
    echo [3/3] Deleting configuration...
    REM Reset ACLs first so we can delete
    icacls "%CONFIG_DIR%" /reset /T /Q >nul 2>&1
    rmdir /s /q "%CONFIG_DIR%"
    echo    Configuration deleted.
) else (
    echo [3/3] Configuration preserved at %CONFIG_DIR%.
)

echo.
echo ============================================================================
echo Viper has been uninstalled.
echo ============================================================================
pause
