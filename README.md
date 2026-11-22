# Universal Peripheral Battery Monitor

A lightweight, zero-dependency Windows system tray utility that displays live battery percentages for **any wireless keyboard and mouse**, regardless of connection method:
- **Bluetooth / Bluetooth LE (HOGP)** (e.g., Lenovo, Dell, HP, Apple Magic, Microsoft Surface, Keychron, Logitech, etc.)
- **2.4 GHz Proprietary USB Dongles** (Keychron, Razer HyperSpeed, Corsair, SteelSeries, etc.)
- **Multi-device Unified Receivers** (Logitech Unifying, Lightspeed, Logi Bolt)
- **Standard USB HID Battery Peripherals** (devices implementing standard HID Battery Status Page `0x85`)
- **Custom Configurable Receivers** via `config.json`


---

## Key Features

1. **Native & Zero-Install**:
   - Compiles and runs using only standard Microsoft Windows components (`csc.exe` and .NET Framework 4.x).
   - No Python, Node.js, Rust, or external libraries required.
   - Memory footprint is under 15 MB.

2. **Universal Multi-Protocol Detection Engine**:
   - **Windows PnP Bluetooth LE / HOGP**: Queries device property `DEVPKEY_Device_BatteryPercentage` (`{104EA319-6EE2-4701-BD47-8DDBF425BBE5}, 2`). Captures virtually all modern Bluetooth mice and keyboards.
   - **Keychron 2.4G RF Protocol**: Communicates with Keychron dongles on Vendor Usage Page `0xFFC1` via raw overlapped `WriteFile` interrupt packets (`0xB3 0x06`) and reads back battery responses on report ID `0xB4`.
   - **Logitech HID++ 1.0 & 2.0 Engine**: Auto-discovers devices paired with Logitech Unifying, Lightspeed, and Bolt receivers (`VID 0x046D`), reading feature `0x1000` (Unified Battery) or `0x1004` (Battery status).
   - **Razer HyperSpeed Wireless**: Queries Razer 2.4GHz dongles (`VID 0x1532`) via report ID `0x00`, command `0x02 0x07`.
   - **Standard HID Battery Page**: Traverses standard HID collections for Usage Page `0x85` (Battery System).

3. **Intelligent Device Categorization & Selection**:
   - Automatically inspects device names, HID collections, and interface descriptors to classify devices as **Keyboard**, **Mouse**, **Headset**, **Controller**, or **Other**.
   - By default, automatically selects the highest-confidence active wireless keyboard and mouse.
   - A visual **Setup Dialog** lets you explicitly pin a specific keyboard or mouse if you have multiple peripherals connected.

4. **Customizable Taskbar Display**:
   - **Dual Tray Icons**: Separate icons for Keyboard (blue header) and Mouse (green header) with live percentage numbers and color-coded level bars.
   - **Single Combined Icon**: Displays both Keyboard and Mouse percentages side-by-side in one compact tray icon (`K:44 M:69`).
   - **Single Icon Modes**: Show only Mouse or only Keyboard.
   - **Multiple Visual Styles**:
     - *Badge + Bar*: Large digits with color bar underneath (green > 35%, amber 16-35%, red <= 15%).
     - *Large Number*: Maximum readability for single devices.
     - *Mini Gauge / Dial*: Circular battery dial.

5. **Live Dashboard**:
   - View all discovered wireless peripherals at a glance, along with connection type (Bluetooth, 2.4G Dongle, USB), battery percentage, status, and device ID.

---
<img width="315" height="280" alt="image" src="https://github.com/user-attachments/assets/b1a15d87-6bad-4785-a3ad-ce932416ce07" />
<img width="388" height="176" alt="image" src="https://github.com/user-attachments/assets/50cdbed7-7ac3-40e6-b34b-557b7f0fcf4c" />
<img width="317" height="302" alt="image" src="https://github.com/user-attachments/assets/677bc8d0-d0ad-44d6-9b7d-61dca1180618" />
<img width="100" height="62" alt="image" src="https://github.com/user-attachments/assets/dcfd9527-9bef-4dc7-bf63-5a9517f9f32e" />






## Quick Start

### Running the Monitor
Double-click `run.bat` or run:
```cmd
run.bat
```
The monitor will start in the background and place its icon(s) in your system tray (notification area near the clock).

### Rebuilding from Source
Double-click `build.bat` or run:
```cmd
build.bat
```
This invokes Windows built-in `csc.exe` to compile `UniversalPeripheralBatteryMonitor.cs` into `UniversalPeripheralBatteryMonitor.exe`.

### Stopping the Monitor
Double-click `stop.bat` or run:
```cmd
stop.bat
```

---

## Tray Context Menu Options

Right-click any battery tray icon to access:
- **📊 Show Battery Dashboard**: Opens a modern dark-mode window showing all detected devices and their battery levels.
- **🔄 Refresh Now**: Immediately queries all peripherals for fresh battery readings.
- **⚙ Setup & Device Selection**: Choose which detected peripheral to assign to the primary keyboard and mouse slots, change poll interval, or adjust display modes.
- **Display Mode**: Switch between Dual Icons, Combined Icon, Mouse Only, or Keyboard Only.
- **Icon Style**: Switch between Badge + Bar, Large Number, or Gauge.
- **Start with Windows**: Toggle automatic startup on Windows boot (persisted in registry).
- **Exit**: Closes the application.

---

## Configuration (`config.json`)

Settings are automatically saved in `config.json` next to the executable:
```json
{
  "PreferredKeyboardId": "auto",
  "PreferredMouseId": "auto",
  "PollIntervalSeconds": 120,
  "StartWithWindows": false,
  "DisplayMode": 0,
  "IconStyle": 0,
  "CustomDevices": []
}
```

### Adding Custom Proprietary Dongles
If you have an exotic 2.4GHz wireless dongle that uses custom HID report queries, you can add it directly to `CustomDevices` in `config.json`:
```json
{
  "CustomDevices": [
    {
      "Name": "Custom Gaming Mouse",
      "Vid": 43690,
      "Pid": 53286,
      "UsagePage": 65473,
      "QueryBytesHex": "B3060000",
      "ResponseByteIndex": 20,
      "IsOverlappedWrite": true,
      "Category": "Mouse"
    }
  ]
}
```

---

## Directory Structure
- `UniversalPeripheralBatteryMonitor.cs`: Complete C# source code (Win32 P/Invoke, HID/PnP scanner, dynamic icon renderer, setup dialog, dashboard).
- `UniversalPeripheralBatteryMonitor.exe`: Compiled native executable.
- `build.bat`: One-click recompiler.
- `run.bat`: Launcher.
- `stop.bat`: One-click terminator.
- `uninstall-autostart.bat`: Registry cleanup utility.
- `README.md`: This documentation.
