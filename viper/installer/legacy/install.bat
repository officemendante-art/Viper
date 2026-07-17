@echo off
REM ============================================================================
REM Viper Installer — Service Registration and Hardening
REM Run as Administrator after building the solution.
REM ============================================================================

set SERVICE_NAME=ViperService
set WATCHDOG_NAME=ViperWatchdog
set SERVICE_EXE=%~dp0src\Viper.Service\bin\Debug\net10.0\Viper.Service.exe
set WATCHDOG_EXE=%~dp0src\Viper.Watchdog\bin\Debug\net10.0\Viper.Watchdog.exe
set CONFIG_DIR=%ProgramData%\Viper

echo [1/5] Creating config directory with hardened ACLs...
if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"

REM SPEC.md §5.1: Write access restricted to SYSTEM/Administrators.
REM Explicit deny for Users group to prevent config tampering.
icacls "%CONFIG_DIR%" /inheritance:r
icacls "%CONFIG_DIR%" /grant:r "NT AUTHORITY\SYSTEM:(OI)(CI)F"
icacls "%CONFIG_DIR%" /grant:r "BUILTIN\Administrators:(OI)(CI)F"
icacls "%CONFIG_DIR%" /deny "BUILTIN\Users:(OI)(CI)(W,D)"
icacls "%CONFIG_DIR%" /grant:r "BUILTIN\Users:(OI)(CI)R"
echo    Done.

echo [2/5] Installing Viper.Service...
sc create %SERVICE_NAME% binPath= "%SERVICE_EXE%" start= auto obj= "LocalSystem" DisplayName= "Viper Application Lock Service"
sc description %SERVICE_NAME% "Viper offline application locker - intercepts and locks protected applications."
sc failure %SERVICE_NAME% reset= 86400 actions= restart/5000/restart/10000/restart/30000
echo    Done.

echo [3/5] Installing Viper.Watchdog...
sc create %WATCHDOG_NAME% binPath= "%WATCHDOG_EXE%" start= auto obj= "LocalSystem" DisplayName= "Viper Watchdog Service"
sc description %WATCHDOG_NAME% "Viper watchdog - monitors and restarts the Viper Application Lock Service."
sc failure %WATCHDOG_NAME% reset= 86400 actions= restart/5000/restart/10000/restart/30000
echo    Done.

echo [4/5] Applying SDDL hardening to services...
REM SPEC.md §5.2: Deny SERVICE_STOP, SERVICE_PAUSE_CONTINUE, SERVICE_CHANGE_CONFIG
REM to Interactive/Authenticated Users. Only Administrators and SYSTEM can control.
REM
REM SDDL breakdown:
REM   D: = DACL
REM   (A;;CCLCSWRPWPDTLOCRRC;;;SY)  = SYSTEM: full service control
REM   (A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA) = Administrators: full control
REM   (A;;CCLCSWLOCRRC;;;IU)        = Interactive Users: query status + enumerate only
REM   (A;;CCLCSWLOCRRC;;;SU)        = Service logon users: query status only
REM
REM Note: Interactive Users (IU) get ONLY query/enumerate rights.
REM SERVICE_STOP (RP), SERVICE_PAUSE_CONTINUE (RPWP), SERVICE_CHANGE_CONFIG (DC)
REM are deliberately absent from the IU entry.
sc sdset %SERVICE_NAME% "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)"
sc sdset %WATCHDOG_NAME% "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)"
echo    Done.

echo [5/5] Starting services...
sc start %SERVICE_NAME%
sc start %WATCHDOG_NAME%
echo    Done.

echo.
echo ============================================================================
echo Viper installation complete.
echo   - ViperService and ViperWatchdog are running.
echo   - Config directory: %CONFIG_DIR%
echo   - Standard users cannot stop or modify these services.
echo   - To uninstall, run uninstall.bat as Administrator.
echo ============================================================================
