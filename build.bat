@echo off
setlocal
echo ========================================================
echo Compiling Universal Peripheral Battery Monitor
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERROR] .NET Framework C# compiler csc.exe not found.
    pause
    exit /b 1
)

taskkill /f /im UniversalPeripheralBatteryMonitor.exe >nul 2>&1

set ICON_FLAG=
if exist "%~dp0app.ico" (
    set ICON_FLAG=/win32icon:"%~dp0app.ico"
)

"%CSC%" /nologo /target:winexe /optimize+ %ICON_FLAG% /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:"%~dp0UniversalPeripheralBatteryMonitor.exe" "%~dp0UniversalPeripheralBatteryMonitor.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] UniversalPeripheralBatteryMonitor.exe compiled successfully!
) else (
    echo [ERROR] Compilation failed with error code %ERRORLEVEL%.
)

endlocal
