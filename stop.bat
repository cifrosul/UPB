@echo off
setlocal
echo Stopping Universal Peripheral Battery Monitor...
taskkill /f /im UniversalPeripheralBatteryMonitor.exe >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Universal Peripheral Battery Monitor stopped.
) else (
    echo [INFO] Process was not running.
)
endlocal
