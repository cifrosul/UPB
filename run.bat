@echo off
setlocal
cd /d "%~dp0"

echo Stopping any running instance...
taskkill /f /im UniversalPeripheralBatteryMonitor.exe >nul 2>&1

if not exist "%~dp0UniversalPeripheralBatteryMonitor.exe" (
    echo Binary not found. Building first...
    call "%~dp0build.bat"
    if errorlevel 1 exit /b 1
)

echo Starting Universal Peripheral Battery Monitor in taskbar...
start "" "%~dp0UniversalPeripheralBatteryMonitor.exe"
echo [SUCCESS] Monitor launched in background. Check your notification area / system tray near the clock.
endlocal
exit /b 0
