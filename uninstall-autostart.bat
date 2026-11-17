@echo off
setlocal
echo Removing autostart entry from Windows Registry...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "UniversalPeripheralBatteryMonitor" /f >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Autostart entry removed successfully.
) else (
    echo [INFO] No autostart entry was found.
)
endlocal
