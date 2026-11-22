@echo off
setlocal
echo Stopping Universal Peripheral Battery Monitor...
taskkill /f /im UniversalPeripheralBatteryMonitor.exe >nul 2>&1
taskkill /f /im UniversalPeripheralBatteryMonitor.old.exe >nul 2>&1

if exist "%~dp0UniversalPeripheralBatteryMonitor.old.exe" (
    del /f /q "%~dp0UniversalPeripheralBatteryMonitor.old.exe" >nul 2>&1
)

echo [SUCCESS] Universal Peripheral Battery Monitor stopped.
endlocal
