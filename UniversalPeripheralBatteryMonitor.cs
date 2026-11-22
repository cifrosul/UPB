using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace UniversalPeripheralBatteryMonitor
{
    static class Program
    {
        private const string AppGuid = "Global\\UniversalPeripheralBatteryMonitor_AppMutex_v2";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unhandled.log"),
                        "UnhandledException:\n" + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null"));
                }
                catch { }
            };
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unhandled.log"),
                        "ThreadException:\n" + (e.Exception != null ? e.Exception.ToString() : "null"));
                }
                catch { }
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                bool createdNew;
                using (Mutex mutex = new Mutex(false, AppGuid, out createdNew))
                {
                    if (!createdNew)
                    {
                        MessageBox.Show(
                            "Universal Peripheral Battery Monitor is already running in your taskbar system tray.\n\n" +
                            "Look for the battery icons in the notification area near your clock.\n" +
                            "(Click the '^' overflow arrow next to the clock if hidden).",
                            "Already Running",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new UniversalTrayAppContext());
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unhandled.log"),
                        "Main catch:\n" + ex.ToString());
                }
                catch { }
            }
        }
    }

    public enum DeviceCategory
    {
        Keyboard,
        Mouse,
        Headset,
        Controller,
        Other
    }

    public class PeripheralDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DeviceCategory Category { get; set; }
        public string ConnectionType { get; set; }
        public int BatteryPercentage { get; set; }
        public bool IsConnected { get; set; }
        public bool IsSleeping { get; set; }
        public bool IsCharging { get; set; }
        public string HardwarePath { get; set; }
        public string Protocol { get; set; }
        public DateTime LastUpdated { get; set; }
        public string StatusMessage { get; set; }

        public PeripheralDevice()
        {
            BatteryPercentage = -1;
            LastUpdated = DateTime.Now;
            Category = DeviceCategory.Other;
            ConnectionType = "Wireless";
            StatusMessage = "Detecting...";
        }

        public string GetDisplayPercentage()
        {
            return BatteryPercentage >= 0 ? (BatteryPercentage + "%") : "--%";
        }

        public string GetCategoryLabel()
        {
            switch (Category)
            {
                case DeviceCategory.Keyboard: return "KB";
                case DeviceCategory.Mouse: return "M";
                case DeviceCategory.Headset: return "H";
                case DeviceCategory.Controller: return "PAD";
                default: return "DEV";
            }
        }
    }

    public class CustomDeviceConfig
    {
        public string Name { get; set; }
        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }
        public ushort UsagePage { get; set; }
        public byte QueryReportId { get; set; }
        public byte[] QueryPayload { get; set; }
        public byte ExpectedResponseReportId { get; set; }
        public int BatteryByteOffset { get; set; }
        public DeviceCategory Category { get; set; }

        public CustomDeviceConfig()
        {
            Category = DeviceCategory.Mouse;
            QueryReportId = 0xB3;
            QueryPayload = new byte[] { 0x06 };
            ExpectedResponseReportId = 0xB4;
            BatteryByteOffset = 20;
        }
    }

    public class AppSettings
    {
        public string PreferredKeyboardId { get; set; }
        public string PreferredMouseId { get; set; }
        public int PollIntervalSeconds { get; set; }
        public int DisplayMode { get; set; } // 0 = Dual, 1 = KbOnly, 2 = MouseOnly, 3 = Combined, 4 = AllDevices
        public int IconStyle { get; set; } // 0 = Badge+Bar, 1 = LargeNumber, 2 = GaugeOnly
        public List<CustomDeviceConfig> CustomDevices { get; set; }

        public AppSettings()
        {
            PreferredKeyboardId = "auto";
            PreferredMouseId = "auto";
            PollIntervalSeconds = 120;
            DisplayMode = 0;
            IconStyle = 0;
            CustomDevices = new List<CustomDeviceConfig>();
        }

        public static string GetConfigPath()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            return Path.Combine(dir, "config.json");
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            string path = GetConfigPath();
            if (File.Exists(path))
            {
                try
                {
                    string content = File.ReadAllText(path, Encoding.UTF8);
                    // Simple, zero-dependency key-value parser for basic settings
                    settings.PreferredKeyboardId = ExtractJsonString(content, "PreferredKeyboardId", "auto");
                    settings.PreferredMouseId = ExtractJsonString(content, "PreferredMouseId", "auto");
                    settings.PollIntervalSeconds = ExtractJsonInt(content, "PollIntervalSeconds", 120);
                    settings.DisplayMode = ExtractJsonInt(content, "DisplayMode", 0);
                    settings.IconStyle = ExtractJsonInt(content, "IconStyle", 0);
                }
                catch { }
            }
            else
            {
                settings.Save();
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"PreferredKeyboardId\": \"" + EscapeJson(PreferredKeyboardId) + "\",");
                sb.AppendLine("  \"PreferredMouseId\": \"" + EscapeJson(PreferredMouseId) + "\",");
                sb.AppendLine("  \"PollIntervalSeconds\": " + PollIntervalSeconds + ",");
                sb.AppendLine("  \"DisplayMode\": " + DisplayMode + ",");
                sb.AppendLine("  \"IconStyle\": " + IconStyle);
                sb.AppendLine("}");
                File.WriteAllText(GetConfigPath(), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ExtractJsonString(string json, string key, string def)
        {
            int idx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return def;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return def;
            int q1 = json.IndexOf('"', colon);
            if (q1 < 0) return def;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return def;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static int ExtractJsonInt(string json, string key, int def)
        {
            int idx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return def;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return def;
            int end = json.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end < 0) end = json.Length;
            string numStr = json.Substring(colon + 1, end - colon - 1).Trim();
            int res;
            if (int.TryParse(numStr, out res)) return res;
            return def;
        }
    }

    public class UniversalTrayAppContext : ApplicationContext
    {
        private NotifyIcon _kbIcon;
        private NotifyIcon _mouseIcon;
        private List<NotifyIcon> _extraIcons = new List<NotifyIcon>();

        private ContextMenuStrip _contextMenu;
        private Control _invoker;

        private ToolStripMenuItem _menuHeader;
        private ToolStripMenuItem _menuDevicesGroup;
        private ToolStripMenuItem _menuRefresh;
        private ToolStripMenuItem _menuInterval;
        private ToolStripMenuItem _menuDisplayMode;
        private ToolStripMenuItem _menuIconStyle;
        private ToolStripMenuItem _menuAutoStart;
        private ToolStripMenuItem _menuSetup;
        private ToolStripMenuItem _menuDashboard;
        private ToolStripMenuItem _menuExit;

        private System.Windows.Forms.Timer _pollTimer;
        private AppSettings _settings;

        private List<PeripheralDevice> _detectedDevices = new List<PeripheralDevice>();
        private PeripheralDevice _activeKeyboard = null;
        private PeripheralDevice _activeMouse = null;

        private Icon _currentKbIcon = null;
        private Icon _currentMouseIcon = null;
        private bool _firstPollDone = false;

        private DashboardForm _dashboardForm = null;
        private SetupDialog _setupDialog = null;

        public UniversalTrayAppContext()
        {
            _settings = AppSettings.Load();
            InitializeComponents();
            PerformPollAsync();
        }

        private void InitializeComponents()
        {
            // Create hidden control for thread-safe cross-thread UI dispatching
            _invoker = new Control();
            IntPtr forceHandle = _invoker.Handle;

            _contextMenu = new ContextMenuStrip();
            _contextMenu.Font = new Font("Segoe UI", 9f);

            _menuHeader = new ToolStripMenuItem("Universal Peripheral Battery Monitor");
            _menuHeader.Enabled = false;
            _menuHeader.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _contextMenu.Items.Add(_menuHeader);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Devices list section
            _menuDevicesGroup = new ToolStripMenuItem("Devices: Scanning...");
            _menuDevicesGroup.Enabled = false;
            _contextMenu.Items.Add(_menuDevicesGroup);
            _contextMenu.Items.Add(new ToolStripSeparator());

            _menuRefresh = new ToolStripMenuItem("Refresh Now", null, delegate { PerformPollAsync(); });
            _contextMenu.Items.Add(_menuRefresh);

            // Interval menu
            _menuInterval = new ToolStripMenuItem("Polling Interval");
            AddIntervalOption(_menuInterval, "30 seconds", 30);
            AddIntervalOption(_menuInterval, "1 minute", 60);
            AddIntervalOption(_menuInterval, "2 minutes", 120, _settings.PollIntervalSeconds == 120);
            AddIntervalOption(_menuInterval, "5 minutes", 300, _settings.PollIntervalSeconds == 300);
            AddIntervalOption(_menuInterval, "10 minutes", 600, _settings.PollIntervalSeconds == 600);
            _contextMenu.Items.Add(_menuInterval);

            // Display Mode menu
            _menuDisplayMode = new ToolStripMenuItem("Tray Display Mode");
            AddDisplayModeOption(_menuDisplayMode, "Dual Icons (Keyboard & Mouse)", 0, _settings.DisplayMode == 0);
            AddDisplayModeOption(_menuDisplayMode, "Keyboard Icon Only", 1, _settings.DisplayMode == 1);
            AddDisplayModeOption(_menuDisplayMode, "Mouse Icon Only", 2, _settings.DisplayMode == 2);
            AddDisplayModeOption(_menuDisplayMode, "Combined Single Icon", 3, _settings.DisplayMode == 3);
            AddDisplayModeOption(_menuDisplayMode, "All Detected Wireless Devices", 4, _settings.DisplayMode == 4);
            _contextMenu.Items.Add(_menuDisplayMode);

            // Icon Style menu
            _menuIconStyle = new ToolStripMenuItem("Icon Visual Style");
            AddStyleOption(_menuIconStyle, "Badge Number + Battery Bar", 0, _settings.IconStyle == 0);
            AddStyleOption(_menuIconStyle, "Large Bold Number", 1, _settings.IconStyle == 1);
            AddStyleOption(_menuIconStyle, "Battery Gauge Only", 2, _settings.IconStyle == 2);
            _contextMenu.Items.Add(_menuIconStyle);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Setup / Configuration
            _menuSetup = new ToolStripMenuItem("Device Setup & Configuration...", null, delegate { ShowSetupDialog(); });
            _contextMenu.Items.Add(_menuSetup);

            _menuDashboard = new ToolStripMenuItem("Battery Dashboard...", null, delegate { ShowDashboard(); });
            _contextMenu.Items.Add(_menuDashboard);

            // Autostart
            _menuAutoStart = new ToolStripMenuItem("Start with Windows", null, ToggleAutoStart);
            _menuAutoStart.Checked = AutoStartManager.IsAutoStartEnabled();
            _contextMenu.Items.Add(_menuAutoStart);

            _contextMenu.Items.Add(new ToolStripSeparator());

            _menuExit = new ToolStripMenuItem("Exit", null, delegate { ExitApp(); });
            _contextMenu.Items.Add(_menuExit);

            // Immediate initial icons
            _currentKbIcon = IconRenderer.CreateDeviceIcon("KB", -1, false, _settings.IconStyle);
            _currentMouseIcon = IconRenderer.CreateDeviceIcon("M", -1, false, _settings.IconStyle);

            _kbIcon = new NotifyIcon();
            _kbIcon.Text = "Keyboard: Detecting...";
            _kbIcon.Icon = _currentKbIcon;
            _kbIcon.ContextMenuStrip = _contextMenu;
            _kbIcon.Visible = true;
            _kbIcon.DoubleClick += delegate { ShowDashboard(); };
            _kbIcon.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowDashboard();
            };

            _mouseIcon = new NotifyIcon();
            _mouseIcon.Text = "Mouse: Detecting...";
            _mouseIcon.Icon = _currentMouseIcon;
            _mouseIcon.ContextMenuStrip = _contextMenu;
            _mouseIcon.Visible = true;
            _mouseIcon.DoubleClick += delegate { ShowDashboard(); };
            _mouseIcon.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowDashboard();
            };

            // Setup Timer
            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = Math.Max(10000, _settings.PollIntervalSeconds * 1000);
            _pollTimer.Tick += delegate { PerformPollAsync(); };
            _pollTimer.Start();

            UpdateTrayVisibility();
        }

        private void AddIntervalOption(ToolStripMenuItem parent, string text, int seconds, bool isDefault = false)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = isDefault;
            item.Click += delegate
            {
                foreach (ToolStripMenuItem sib in parent.DropDownItems) sib.Checked = false;
                item.Checked = true;
                _settings.PollIntervalSeconds = seconds;
                _settings.Save();
                _pollTimer.Interval = _settings.PollIntervalSeconds * 1000;
            };
            parent.DropDownItems.Add(item);
        }

        private void AddDisplayModeOption(ToolStripMenuItem parent, string text, int mode, bool isDefault = false)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = isDefault;
            item.Click += delegate
            {
                foreach (ToolStripMenuItem sib in parent.DropDownItems) sib.Checked = false;
                item.Checked = true;
                _settings.DisplayMode = mode;
                _settings.Save();
                UpdateTrayVisibility();
                UpdateTrayIcons();
            };
            parent.DropDownItems.Add(item);
        }

        private void AddStyleOption(ToolStripMenuItem parent, string text, int styleIndex, bool isDefault = false)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = isDefault;
            item.Click += delegate
            {
                foreach (ToolStripMenuItem sib in parent.DropDownItems) sib.Checked = false;
                item.Checked = true;
                _settings.IconStyle = styleIndex;
                _settings.Save();
                UpdateTrayIcons();
            };
            parent.DropDownItems.Add(item);
        }

        private void UpdateTrayVisibility()
        {
            switch (_settings.DisplayMode)
            {
                case 0: // Dual (Keyboard & Mouse)
                    _kbIcon.Visible = true;
                    _mouseIcon.Visible = true;
                    ClearExtraIcons();
                    break;
                case 1: // Keyboard Only
                    _kbIcon.Visible = true;
                    _mouseIcon.Visible = false;
                    ClearExtraIcons();
                    break;
                case 2: // Mouse Only
                    _kbIcon.Visible = false;
                    _mouseIcon.Visible = true;
                    ClearExtraIcons();
                    break;
                case 3: // Combined Single Icon
                    _kbIcon.Visible = true;
                    _mouseIcon.Visible = false;
                    ClearExtraIcons();
                    break;
                case 4: // All Devices Mode
                    _kbIcon.Visible = true;
                    _mouseIcon.Visible = true;
                    SyncExtraIcons();
                    break;
            }
        }

        private void ClearExtraIcons()
        {
            foreach (var ico in _extraIcons)
            {
                ico.Visible = false;
                ico.Dispose();
            }
            _extraIcons.Clear();
        }

        private void SyncExtraIcons()
        {
            // For any additional wireless devices beyond primary keyboard and mouse
            ClearExtraIcons();
            foreach (var dev in _detectedDevices)
            {
                if (dev == _activeKeyboard || dev == _activeMouse) continue;
                if (!dev.IsConnected && dev.BatteryPercentage < 0) continue;

                NotifyIcon nIcon = new NotifyIcon();
                nIcon.Icon = IconRenderer.CreateDeviceIcon(dev.GetCategoryLabel(), dev.BatteryPercentage, dev.IsConnected, _settings.IconStyle);
                nIcon.Text = string.Format("{0}: {1}%\nConnection: {2}", dev.Name, dev.GetDisplayPercentage(), dev.ConnectionType);
                nIcon.ContextMenuStrip = _contextMenu;
                nIcon.Visible = true;
                nIcon.DoubleClick += delegate { ShowDashboard(); };
                _extraIcons.Add(nIcon);
            }
        }

        private void ToggleAutoStart(object sender, EventArgs e)
        {
            bool newState = !AutoStartManager.IsAutoStartEnabled();
            AutoStartManager.SetAutoStart(newState);
            _menuAutoStart.Checked = newState;
        }

        private void PerformPollAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    List<PeripheralDevice> freshList = UniversalHardwareScanner.ScanAllPeripherals(_settings);

                    // Preserve last known battery readings if device is asleep
                    foreach (var fresh in freshList)
                    {
                        PeripheralDevice old = _detectedDevices.Find(d => d.Id == fresh.Id);
                        if (old != null && !fresh.IsConnected && old.BatteryPercentage >= 0)
                        {
                            fresh.BatteryPercentage = old.BatteryPercentage;
                            fresh.IsSleeping = true;
                            fresh.StatusMessage = "Standby (Last: " + old.BatteryPercentage + "%)";
                        }
                    }

                    _detectedDevices = freshList;

                    // Determine active keyboard
                    _activeKeyboard = null;
                    if (!string.IsNullOrEmpty(_settings.PreferredKeyboardId) && _settings.PreferredKeyboardId != "auto")
                    {
                        _activeKeyboard = _detectedDevices.Find(d => d.Id == _settings.PreferredKeyboardId);
                    }
                    if (_activeKeyboard == null)
                    {
                        _activeKeyboard = _detectedDevices.Find(d => d.Category == DeviceCategory.Keyboard && d.IsConnected) ??
                                          _detectedDevices.Find(d => d.Category == DeviceCategory.Keyboard);
                    }

                    // Determine active mouse
                    _activeMouse = null;
                    if (!string.IsNullOrEmpty(_settings.PreferredMouseId) && _settings.PreferredMouseId != "auto")
                    {
                        _activeMouse = _detectedDevices.Find(d => d.Id == _settings.PreferredMouseId);
                    }
                    if (_activeMouse == null)
                    {
                        _activeMouse = _detectedDevices.Find(d => d.Category == DeviceCategory.Mouse && d.IsConnected) ??
                                       _detectedDevices.Find(d => d.Category == DeviceCategory.Mouse);
                    }

                    // Dispatch UI updates via _invoker
                    if (_invoker != null && _invoker.IsHandleCreated)
                    {
                        try
                        {
                            _invoker.BeginInvoke((MethodInvoker)delegate
                            {
                                try
                                {
                                    UpdateTrayIcons();
                                    UpdateContextMenuItems();

                                    if (_dashboardForm != null && !_dashboardForm.IsDisposed)
                                    {
                                        _dashboardForm.UpdateDashboard(_detectedDevices, _activeKeyboard, _activeMouse);
                                    }
                                    if (_setupDialog != null && !_setupDialog.IsDisposed)
                                    {
                                        _setupDialog.UpdateDeviceList(_detectedDevices);
                                    }

                                    if (!_firstPollDone)
                                    {
                                        _firstPollDone = true;
                                        string kbMsg = _activeKeyboard != null ? (_activeKeyboard.Name + ": " + _activeKeyboard.GetDisplayPercentage()) : "No Keyboard";
                                        string mMsg = _activeMouse != null ? (_activeMouse.Name + ": " + _activeMouse.GetDisplayPercentage()) : "No Mouse";

                                        _kbIcon.ShowBalloonTip(4000, "Universal Battery Monitor",
                                            string.Format("{0}\n{1}\nActive in taskbar system tray.", kbMsg, mMsg),
                                            ToolTipIcon.Info);
                                    }
                                }
                                catch (Exception uiEx)
                                {
                                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), "UI update error: " + uiEx.ToString()); } catch { }
                                }
                            });
                        }
                        catch { }
                    }
                }
                catch (Exception bgEx)
                {
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), "Background poll error: " + bgEx.ToString()); } catch { }
                }
            });
        }

        private void UpdateContextMenuItems()
        {
            // Update devices listing in context menu
            _contextMenu.SuspendLayout();

            // Clear old dynamic items
            int headerIdx = _contextMenu.Items.IndexOf(_menuHeader);
            int refreshIdx = _contextMenu.Items.IndexOf(_menuRefresh);

            for (int i = refreshIdx - 1; i > headerIdx; i--)
            {
                _contextMenu.Items.RemoveAt(i);
            }

            int insertIdx = headerIdx + 1;

            if (_detectedDevices.Count == 0)
            {
                ToolStripMenuItem noneItem = new ToolStripMenuItem("No wireless devices detected");
                noneItem.Enabled = false;
                _contextMenu.Items.Insert(insertIdx++, noneItem);
            }
            else
            {
                foreach (var dev in _detectedDevices)
                {
                    string iconPrefix = dev.Category == DeviceCategory.Keyboard ? "[KB] " :
                                        dev.Category == DeviceCategory.Mouse ? "[Mouse] " :
                                        dev.Category == DeviceCategory.Headset ? "[Headset] " : "[Device] ";

                    string isPrimary = (dev == _activeKeyboard || dev == _activeMouse) ? " [Primary]" : "";
                    string text = string.Format("{0}{1}: {2} ({3}){4}",
                        iconPrefix, dev.Name, dev.GetDisplayPercentage(), dev.ConnectionType, isPrimary);

                    ToolStripMenuItem item = new ToolStripMenuItem(text, null, delegate { ShowDashboard(); });
                    _contextMenu.Items.Insert(insertIdx++, item);
                }
            }

            _contextMenu.Items.Insert(insertIdx, new ToolStripSeparator());
            _contextMenu.ResumeLayout();
        }

        private void UpdateTrayIcons()
        {
            try
            {
                if (_settings.DisplayMode == 3) // Combined
                {
                    Icon oldIcon = _currentKbIcon;
                    _currentKbIcon = IconRenderer.CreateCombinedIcon(_activeKeyboard, _activeMouse);
                    _kbIcon.Icon = _currentKbIcon;
                    _kbIcon.Text = string.Format("KB: {0} | Mouse: {1}",
                        _activeKeyboard != null ? _activeKeyboard.GetDisplayPercentage() : "N/A",
                        _activeMouse != null ? _activeMouse.GetDisplayPercentage() : "N/A");
                    if (oldIcon != null) { DestroyIconHandle(oldIcon); }
                }
                else
                {
                    // Keyboard Icon
                    Icon oldKb = _currentKbIcon;
                    int kbPct = _activeKeyboard != null ? _activeKeyboard.BatteryPercentage : -1;
                    bool kbConn = _activeKeyboard != null && _activeKeyboard.IsConnected;
                    string kbName = _activeKeyboard != null ? _activeKeyboard.Name : "Keyboard";

                    _currentKbIcon = IconRenderer.CreateDeviceIcon("KB", kbPct, kbConn, _settings.IconStyle);
                    _kbIcon.Icon = _currentKbIcon;
                    string kbTip = string.Format("{0}: {1}%\nStatus: {2}\nConnection: {3}",
                        kbName,
                        kbPct >= 0 ? kbPct.ToString() : "N/A",
                        kbConn ? (_activeKeyboard.IsSleeping ? "Standby" : "Active") : "Offline",
                        _activeKeyboard != null ? _activeKeyboard.ConnectionType : "None");
                    if (kbTip.Length >= 127) kbTip = kbTip.Substring(0, 126);
                    _kbIcon.Text = kbTip;
                    if (oldKb != null) { DestroyIconHandle(oldKb); }

                    // Mouse Icon
                    Icon oldMouse = _currentMouseIcon;
                    int mPct = _activeMouse != null ? _activeMouse.BatteryPercentage : -1;
                    bool mConn = _activeMouse != null && _activeMouse.IsConnected;
                    string mName = _activeMouse != null ? _activeMouse.Name : "Mouse";

                    _currentMouseIcon = IconRenderer.CreateDeviceIcon("M", mPct, mConn, _settings.IconStyle);
                    _mouseIcon.Icon = _currentMouseIcon;
                    string mTip = string.Format("{0}: {1}%\nStatus: {2}\nConnection: {3}",
                        mName,
                        mPct >= 0 ? mPct.ToString() : "N/A",
                        mConn ? (_activeMouse.IsSleeping ? "Standby" : "Active") : "Offline",
                        _activeMouse != null ? _activeMouse.ConnectionType : "None");
                    if (mTip.Length >= 127) mTip = mTip.Substring(0, 126);
                    _mouseIcon.Text = mTip;
                    if (oldMouse != null) { DestroyIconHandle(oldMouse); }
                }

                if (_settings.DisplayMode == 4)
                {
                    SyncExtraIcons();
                }
            }
            catch { }
        }

        private void DestroyIconHandle(Icon ico)
        {
            try
            {
                if (ico != null)
                {
                    IconRenderer.DestroyNativeIcon(ico.Handle);
                    ico.Dispose();
                }
            }
            catch { }
        }

        private void ShowDashboard()
        {
            if (_dashboardForm == null || _dashboardForm.IsDisposed)
            {
                _dashboardForm = new DashboardForm(_detectedDevices, _activeKeyboard, _activeMouse,
                    delegate { PerformPollAsync(); },
                    delegate { ShowSetupDialog(); });
                _dashboardForm.Show();
            }
            else
            {
                _dashboardForm.BringToFront();
                _dashboardForm.WindowState = FormWindowState.Normal;
            }
        }

        private void ShowSetupDialog()
        {
            if (_setupDialog == null || _setupDialog.IsDisposed)
            {
                _setupDialog = new SetupDialog(_settings, _detectedDevices, delegate
                {
                    _settings.Save();
                    _pollTimer.Interval = Math.Max(10000, _settings.PollIntervalSeconds * 1000);
                    UpdateTrayVisibility();
                    PerformPollAsync();
                });
                _setupDialog.Show();
            }
            else
            {
                _setupDialog.BringToFront();
                _setupDialog.WindowState = FormWindowState.Normal;
            }
        }

        private void ExitApp()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
            }

            if (_kbIcon != null)
            {
                _kbIcon.Visible = false;
                _kbIcon.Dispose();
            }
            if (_mouseIcon != null)
            {
                _mouseIcon.Visible = false;
                _mouseIcon.Dispose();
            }
            ClearExtraIcons();

            DestroyIconHandle(_currentKbIcon);
            DestroyIconHandle(_currentMouseIcon);

            if (_invoker != null)
            {
                _invoker.Dispose();
                _invoker = null;
            }

            Application.Exit();
        }
    }

    public static class IconRenderer
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public static void DestroyNativeIcon(IntPtr hIcon)
        {
            try { DestroyIcon(hIcon); } catch { }
        }

        public static Icon CreateDeviceIcon(string label, int pct, bool connected, int style)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);

                    Color statusColor = Color.FromArgb(34, 197, 94); // Green
                    if (!connected) statusColor = Color.FromArgb(140, 140, 140);
                    else if (pct < 20) statusColor = Color.FromArgb(239, 68, 68); // Red
                    else if (pct < 50) statusColor = Color.FromArgb(245, 158, 11); // Amber

                    if (style == 1) // Large Number Mode
                    {
                        using (Font fL = new Font("Segoe UI", 8f, FontStyle.Bold))
                        using (Brush bL = new SolidBrush(Color.White))
                        {
                            g.DrawString(label, fL, bL, 1, 0);
                        }

                        string num = (connected && pct >= 0) ? pct.ToString() : "--";
                        using (Font fN = new Font("Arial Black", 11f, FontStyle.Bold))
                        using (Brush bN = new SolidBrush(statusColor))
                        using (Brush bShadow = new SolidBrush(Color.Black))
                        {
                            g.DrawString(num, fN, bShadow, 1, 12);
                            g.DrawString(num, fN, bN, 0, 11);
                        }
                    }
                    else if (style == 2) // Battery Gauge Only
                    {
                        using (Font fL = new Font("Segoe UI", 9f, FontStyle.Bold))
                        using (Brush bL = new SolidBrush(Color.White))
                        {
                            g.DrawString(label, fL, bL, 2, 0);
                        }

                        Rectangle bRect = new Rectangle(2, 16, 25, 14);
                        Rectangle nRect = new Rectangle(27, 20, 3, 6);

                        using (Brush bBack = new SolidBrush(Color.FromArgb(50, 50, 50)))
                        {
                            g.FillRectangle(bBack, bRect);
                        }
                        if (connected && pct > 0)
                        {
                            int fill = Math.Max(2, (int)((bRect.Width - 2) * (pct / 100.0)));
                            using (Brush bFill = new SolidBrush(statusColor))
                            {
                                g.FillRectangle(bFill, bRect.X + 1, bRect.Y + 1, fill, bRect.Height - 2);
                            }
                        }
                        using (Pen p = new Pen(Color.White, 1.5f))
                        {
                            g.DrawRectangle(p, bRect);
                            g.DrawRectangle(p, nRect);
                        }
                    }
                    else // Default: Badge Number + Battery Bar
                    {
                        using (Font fLabel = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                        using (Brush bL = new SolidBrush(Color.FromArgb(230, 230, 230)))
                        using (Brush bShadow = new SolidBrush(Color.Black))
                        {
                            g.DrawString(label, fLabel, bShadow, 2, 1);
                            g.DrawString(label, fLabel, bL, 1, 0);
                        }

                        string num = (connected && pct >= 0) ? pct.ToString() : "--";
                        using (Font fNum = new Font("Arial", 8f, FontStyle.Bold))
                        using (Brush bNum = new SolidBrush(Color.White))
                        using (Brush bShadow = new SolidBrush(Color.Black))
                        {
                            SizeF sz = g.MeasureString(num, fNum);
                            float nx = 31 - sz.Width;
                            g.DrawString(num, fNum, bShadow, nx + 1, 1);
                            g.DrawString(num, fNum, bNum, nx, 0);
                        }

                        Rectangle battRect = new Rectangle(1, 17, 26, 13);
                        Rectangle nubRect = new Rectangle(27, 21, 3, 5);

                        using (Brush bBack = new SolidBrush(Color.FromArgb(35, 35, 35)))
                        {
                            g.FillRectangle(bBack, battRect);
                        }

                        if (connected && pct > 0)
                        {
                            int fillW = Math.Max(2, (int)((battRect.Width - 2) * (pct / 100.0)));
                            using (Brush bFill = new SolidBrush(statusColor))
                            {
                                g.FillRectangle(bFill, battRect.X + 1, battRect.Y + 1, fillW, battRect.Height - 2);
                            }
                        }

                        using (Pen pBorder = new Pen(Color.FromArgb(220, 220, 220), 1.2f))
                        {
                            g.DrawRectangle(pBorder, battRect);
                            g.DrawRectangle(pBorder, nubRect);
                        }
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon ico = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyNativeIcon(hIcon);
                return ico;
            }
        }

        public static Icon CreateCombinedIcon(PeripheralDevice kb, PeripheralDevice mouse)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    int kbPct = kb != null ? kb.BatteryPercentage : -1;
                    bool kbConn = kb != null && kb.IsConnected;
                    DrawMiniBar(g, "K", kbPct, kbConn, 0, 0);

                    int mPct = mouse != null ? mouse.BatteryPercentage : -1;
                    bool mConn = mouse != null && mouse.IsConnected;
                    DrawMiniBar(g, "M", mPct, mConn, 0, 16);
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon ico = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyNativeIcon(hIcon);
                return ico;
            }
        }

        private static void DrawMiniBar(Graphics g, string label, int pct, bool connected, int x, int y)
        {
            Color c = Color.FromArgb(34, 197, 94);
            if (!connected) c = Color.FromArgb(140, 140, 140);
            else if (pct < 20) c = Color.FromArgb(239, 68, 68);
            else if (pct < 50) c = Color.FromArgb(245, 158, 11);

            using (Font f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (Brush bText = new SolidBrush(Color.White))
            {
                g.DrawString(label, f, bText, x, y);
            }

            Rectangle bRect = new Rectangle(x + 12, y + 2, 16, 9);
            using (Brush bBack = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                g.FillRectangle(bBack, bRect);
            }
            if (connected && pct > 0)
            {
                int w = Math.Max(1, (int)((bRect.Width - 2) * (pct / 100.0)));
                using (Brush bFill = new SolidBrush(c))
                {
                    g.FillRectangle(bFill, bRect.X + 1, bRect.Y + 1, w, bRect.Height - 2);
                }
            }
            using (Pen p = new Pen(Color.White, 1f))
            {
                g.DrawRectangle(p, bRect);
            }
        }
    }

    public static class UniversalHardwareScanner
    {
        private static readonly Guid GUID_DEVINTERFACE_HID = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        private static readonly DEVPROPKEY DEVPKEY_Device_BatteryPercentage = new DEVPROPKEY
        {
            fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
            pid = 2
        };

        [StructLayout(LayoutKind.Sequential)]
        struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid interfaceClassGuid;
            public int flags;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct OVERLAPPED
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr hEvent;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, ref uint RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDevicePropertyW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref DEVPROPKEY propertyKey, out uint propertyType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize, uint flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, StringBuilder PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_SetOutputReport(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetFeature(IntPtr HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        // ====================================================================
        //  HARDENED HID I/O LAYER
        //
        //  Goals of this layer:
        //   1. Memory safety: every overlapped ReadFile/WriteFile/DeviceIoControl
        //      uses unmanaged, address-stable buffers + OVERLAPPED structs, and
        //      that memory is never freed until the kernel has confirmed it is
        //      finished with it (completion or confirmed cancellation).
        //   2. Least privilege: every HID interface is first opened with ZERO
        //      access just to read VID/PID/usage/report lengths. Only interfaces
        //      that match a known protocol are re-opened, and only with the access
        //      they actually need (generic battery scan = read-only, never write).
        //   3. No unbounded blocking: HidD_GetFeature (no timeout) is replaced by
        //      an overlapped IOCTL with a timeout and confirmed cancellation.
        // ====================================================================

        const uint ACCESS_NONE = 0;
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ_WRITE = 3;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        const uint IOCTL_HID_GET_FEATURE = 0xB0192;
        const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES
        {
            public uint Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        // Overlapped I/O always goes through unmanaged pointers (see HidOp).
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "ReadFile")]
        static extern bool ReadFileOv(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, IntPtr lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "WriteFile")]
        static extern bool WriteFileOv(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, IntPtr lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetOverlappedResult")]
        static extern bool GetOverlappedResultOv(IntPtr hFile, IntPtr lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, IntPtr lpBytesReturned, IntPtr lpOverlapped);

        private static bool IsValidHandle(IntPtr h)
        {
            return h != IntPtr.Zero && h.ToInt64() != -1;
        }

        private static IntPtr OpenHid(string path, uint access)
        {
            return CreateFile(path, access, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        }

        /// <summary>Immutable description of one HID interface, gathered once per scan with a zero-access handle.</summary>
        private sealed class HidEntry
        {
            public string DevicePath;
            public ushort Vid, Pid;
            public ushort UsagePage, Usage;
            public ushort InLen, OutLen, FeatLen;
        }

        /// <summary>
        /// One in-flight overlapped operation. Owns an unmanaged OVERLAPPED, buffer and event.
        /// The memory is only released once the kernel is provably done with it; if a cancel
        /// cannot be confirmed, the memory is intentionally leaked (a few bytes) rather than
        /// freed under a pending I/O request, which would corrupt the heap.
        /// </summary>
        private sealed class HidOp : IDisposable
        {
            private const int ERROR_IO_PENDING = 997;
            private readonly IntPtr _dev;
            private readonly int _len;
            private IntPtr _ov, _buf, _evt;
            private bool _inFlight;

            public HidOp(IntPtr device, int length)
            {
                if (length <= 0 || length > 65535) throw new ArgumentOutOfRangeException("length");
                _dev = device;
                _len = length;
                _evt = CreateEvent(IntPtr.Zero, true, false, null);
                if (_evt == IntPtr.Zero) throw new InvalidOperationException("CreateEvent failed");
                _ov = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
                _buf = Marshal.AllocHGlobal(length);
                ResetState();
            }

            private void ResetState()
            {
                OVERLAPPED o = new OVERLAPPED();
                o.hEvent = _evt;
                Marshal.StructureToPtr(o, _ov, false);
                Marshal.Copy(new byte[_len], 0, _buf, _len);
            }

            private bool Begin(bool completedSynchronously)
            {
                if (completedSynchronously) { _inFlight = true; return true; }   // event is still signaled; harvested in Wait()
                _inFlight = Marshal.GetLastWin32Error() == ERROR_IO_PENDING;
                return _inFlight;
            }

            public bool StartRead()
            {
                if (_inFlight) return false;
                ResetState();
                return Begin(ReadFileOv(_dev, _buf, (uint)_len, IntPtr.Zero, _ov));
            }

            public bool StartWrite(byte[] data)
            {
                if (_inFlight || data == null || data.Length == 0) return false;
                ResetState();
                int n = Math.Min(data.Length, _len);
                Marshal.Copy(data, 0, _buf, n);
                return Begin(WriteFileOv(_dev, _buf, (uint)n, IntPtr.Zero, _ov));
            }

            /// <summary>Feature report read via overlapped IOCTL (what HidD_GetFeature does, but cancellable).</summary>
            public bool StartGetFeature(byte reportId)
            {
                if (_inFlight) return false;
                ResetState();
                Marshal.WriteByte(_buf, reportId);
                return Begin(DeviceIoControl(_dev, IOCTL_HID_GET_FEATURE, _buf, (uint)_len, _buf, (uint)_len, IntPtr.Zero, _ov));
            }

            /// <summary>Returns bytes transferred, or -1 on timeout/failure (the operation is cancelled and confirmed dead first).</summary>
            public int Wait(uint timeoutMs)
            {
                if (!_inFlight) return -1;
                if (WaitForSingleObject(_evt, timeoutMs) == 0)
                {
                    uint n;
                    bool ok = GetOverlappedResultOv(_dev, _ov, out n, false);
                    _inFlight = false;
                    return ok ? (int)n : -1;
                }
                Cancel();
                return -1;
            }

            private void Cancel()
            {
                if (!_inFlight) return;
                CancelIoEx(_dev, _ov);
                // A cancelled request still completes and signals the event: wait for that
                // so we know the kernel will not touch our memory again.
                if (WaitForSingleObject(_evt, 2000) == 0)
                {
                    uint n;
                    GetOverlappedResultOv(_dev, _ov, out n, false);
                    _inFlight = false;
                }
            }

            public byte[] Data(int count)
            {
                count = Math.Max(0, Math.Min(count, _len));
                byte[] r = new byte[count];
                if (count > 0) Marshal.Copy(_buf, r, 0, count);
                return r;
            }

            public void Dispose()
            {
                Cancel();
                if (_inFlight) return;   // cancel not confirmed: leak instead of risking heap corruption
                if (_buf != IntPtr.Zero) { Marshal.FreeHGlobal(_buf); _buf = IntPtr.Zero; }
                if (_ov != IntPtr.Zero) { Marshal.FreeHGlobal(_ov); _ov = IntPtr.Zero; }
                if (_evt != IntPtr.Zero) { CloseHandle(_evt); _evt = IntPtr.Zero; }
            }
        }

        /// <summary>
        /// Sends one output report and returns the first input report that satisfies 'accept',
        /// or null on timeout. Unrelated input reports (e.g. async notifications) are skipped
        /// instead of being mistaken for the reply.
        /// </summary>
        private static byte[] HidTransact(IntPtr dev, byte[] outReport, int inLen, uint writeTimeoutMs, uint replyTimeoutMs, bool alsoSetOutputReport, Func<byte[], bool> accept)
        {
            using (HidOp rd = new HidOp(dev, inLen))
            using (HidOp wr = new HidOp(dev, outReport.Length))
            {
                if (!rd.StartRead()) return null;
                if (!wr.StartWrite(outReport)) return null;
                wr.Wait(writeTimeoutMs);   // outcome ignored; Wait() guarantees the write is finished or cancelled
                if (alsoSetOutputReport) HidD_SetOutputReport(dev, outReport, (uint)outReport.Length);

                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    long remaining = (long)replyTimeoutMs - sw.ElapsedMilliseconds;
                    if (remaining <= 0) return null;
                    int n = rd.Wait((uint)remaining);
                    if (n < 0) return null;
                    byte[] data = rd.Data(n);
                    if (accept(data)) return data;
                    if (!rd.StartRead()) return null;
                }
            }
        }

        private static List<string> EnumerateHidPaths()
        {
            List<string> paths = new List<string>();
            Guid hidGuid = GUID_DEVINTERFACE_HID;
            IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (!IsValidHandle(hDevInfo)) return paths;

            try
            {
                SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(ifData);
                uint index = 0;

                while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, index++, ref ifData))
                {
                    uint reqSize = 0;
                    SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, IntPtr.Zero, 0, ref reqSize, IntPtr.Zero);
                    if (reqSize == 0) continue;

                    IntPtr pDetail = Marshal.AllocHGlobal((int)reqSize);
                    try   // FreeHGlobal is now in a finally: no leak if anything below throws
                    {
                        Marshal.WriteInt32(pDetail, IntPtr.Size == 8 ? 8 : 5);
                        if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, pDetail, reqSize, ref reqSize, IntPtr.Zero))
                        {
                            string path = Marshal.PtrToStringAuto(new IntPtr(pDetail.ToInt64() + 4));
                            if (!string.IsNullOrEmpty(path)) paths.Add(path);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pDetail);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }
            return paths;
        }

        /// <summary>Enumerates every HID interface ONCE and probes it with a zero-access handle (no read/write rights requested).</summary>
        private static List<HidEntry> BuildHidInventory()
        {
            List<HidEntry> inventory = new List<HidEntry>();
            foreach (string path in EnumerateHidPaths())
            {
                IntPtr h = CreateFile(path, ACCESS_NONE, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (!IsValidHandle(h)) continue;
                try
                {
                    HIDD_ATTRIBUTES attr = new HIDD_ATTRIBUTES();
                    attr.Size = (uint)Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                    if (!HidD_GetAttributes(h, ref attr)) continue;

                    IntPtr pre;
                    if (!HidD_GetPreparsedData(h, out pre)) continue;
                    try
                    {
                        HIDP_CAPS caps;
                        if (HidP_GetCaps(pre, out caps) != HIDP_STATUS_SUCCESS) continue;

                        HidEntry e = new HidEntry();
                        e.DevicePath = path;
                        e.Vid = attr.VendorID;
                        e.Pid = attr.ProductID;
                        e.UsagePage = caps.UsagePage;
                        e.Usage = caps.Usage;
                        e.InLen = caps.InputReportByteLength;
                        e.OutLen = caps.OutputReportByteLength;
                        e.FeatLen = caps.FeatureReportByteLength;
                        inventory.Add(e);
                    }
                    finally
                    {
                        HidD_FreePreparsedData(pre);
                    }
                }
                finally
                {
                    CloseHandle(h);
                }
            }
            return inventory;
        }

        public static List<PeripheralDevice> ScanAllPeripherals(AppSettings settings)
        {
            List<PeripheralDevice> list = new List<PeripheralDevice>();
            Dictionary<string, bool> seen = new Dictionary<string, bool>();

            // 1. Scan PnP Devices (Bluetooth LE, Windows native battery properties)
            ScanPnpDevices(list, seen);

            // One enumeration + zero-access probe shared by all HID scanners
            List<HidEntry> hid = BuildHidInventory();

            // 2. Scan Keychron proprietary dongles (VID 0x3434)
            ScanKeychronDevices(list, seen, hid);

            // 3. Scan Logitech HID++ dongles (VID 0x046D)
            ScanLogitechDevices(list, seen, hid);

            // 4. Scan Razer wireless dongles (VID 0x1532)
            ScanRazerDevices(list, seen, hid);

            // 5. Scan Generic HID standard battery page (UsagePage 0x85)
            ScanGenericHidBatteryDevices(list, seen, hid);

            // 6. Scan custom configured devices
            if (settings != null && settings.CustomDevices != null)
            {
                ScanCustomDevices(list, seen, settings.CustomDevices, hid);
            }

            return list;
        }

        private static void ScanPnpDevices(List<PeripheralDevice> list, Dictionary<string, bool> seen)
        {
            IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 0x14); // DIGCF_ALLCLASSES | DIGCF_PRESENT
            if (hDevInfo == IntPtr.Zero || hDevInfo.ToInt64() == -1) return;

            try
            {
                SP_DEVINFO_DATA devData = new SP_DEVINFO_DATA();
                devData.cbSize = Marshal.SizeOf(devData);
                uint index = 0;

                while (SetupDiEnumDeviceInfo(hDevInfo, index++, ref devData))
                {
                    StringBuilder sbName = new StringBuilder(512);
                    uint regType, reqSize;
                    if (!SetupDiGetDeviceRegistryPropertyW(hDevInfo, ref devData, 12, out regType, sbName, (uint)sbName.Capacity, out reqSize)) // SPDRP_FRIENDLYNAME
                    {
                        SetupDiGetDeviceRegistryPropertyW(hDevInfo, ref devData, 0, out regType, sbName, (uint)sbName.Capacity, out reqSize); // SPDRP_DEVICEDESC
                    }
                    string name = sbName.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    StringBuilder sbClass = new StringBuilder(256);
                    SetupDiGetDeviceRegistryPropertyW(hDevInfo, ref devData, 7, out regType, sbClass, (uint)sbClass.Capacity, out reqSize); // SPDRP_CLASS
                    string className = sbClass.ToString();

                    uint propType, propReqSize;
                    byte[] propBuf = new byte[32];
                    DEVPROPKEY battKey = DEVPKEY_Device_BatteryPercentage;

                    if (SetupDiGetDevicePropertyW(hDevInfo, ref devData, ref battKey, out propType, propBuf, (uint)propBuf.Length, out propReqSize, 0))
                    {
                        int pct = propBuf[0];
                        if (pct >= 0 && pct <= 100)
                        {
                            string id = "pnp:" + name.ToLower().Trim();
                            if (!seen.ContainsKey(id))
                            {
                                seen[id] = true;
                                PeripheralDevice dev = new PeripheralDevice();
                                dev.Id = id;
                                dev.Name = name;
                                dev.BatteryPercentage = pct;
                                dev.IsConnected = true;
                                dev.ConnectionType = "Bluetooth LE / PnP";
                                dev.Protocol = "Windows DEVPKEY";
                                dev.StatusMessage = "Active - " + pct + "%";
                                dev.Category = CategorizeDevice(name, "", className, 0, 0);
                                list.Add(dev);
                            }
                        }
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }
        }

        private static void ScanKeychronDevices(List<PeripheralDevice> list, Dictionary<string, bool> seen, List<HidEntry> hid)
        {
            // Keychron 2.4G dongle: vendor usage page 0xFFC1, 64-byte reports (VID 0x3434).
            // Interfaces are pre-filtered from the zero-access inventory, so nothing else is ever opened read/write.
            const string devId = "keychron:dongle";

            foreach (HidEntry e in hid)
            {
                if (seen.ContainsKey(devId)) return;
                if (e.Vid != 0x3434 || e.UsagePage != 0xFFC1 || e.OutLen != 64 || e.InLen != 64) continue;

                IntPtr hDev = OpenHid(e.DevicePath, GENERIC_READ | GENERIC_WRITE);
                if (!IsValidHandle(hDev)) continue;

                try
                {
                    byte[] outReport = new byte[64];
                    outReport[0] = 0xB3;
                    outReport[1] = 0x06;

                    // Original behaviour preserved: interrupt write + HidD_SetOutputReport, then wait up to 2.5 s for a 0xB4 reply.
                    byte[] resp = HidTransact(hDev, outReport, 64, 500, 2500, true,
                        d => d.Length >= 21 && d[0] == 0xB4 && d[20] <= 100);

                    PeripheralDevice dev = new PeripheralDevice();
                    dev.Id = devId;
                    dev.Name = "Keychron M6 Wireless Mouse";
                    dev.Category = DeviceCategory.Mouse;
                    dev.ConnectionType = "2.4GHz Dongle (Keychron Link)";
                    dev.HardwarePath = e.DevicePath;
                    dev.Protocol = "Keychron 0xFFC1 (0xB3/0xB4)";

                    if (resp != null)
                    {
                        int pct = resp[20];
                        dev.BatteryPercentage = pct;
                        dev.IsConnected = true;
                        dev.StatusMessage = "Active - " + pct + "%";
                    }
                    else
                    {
                        dev.BatteryPercentage = -1;
                        dev.IsConnected = false;
                        dev.IsSleeping = true;
                        dev.StatusMessage = "Standby (Move mouse to wake)";
                    }
                    seen[devId] = true;
                    list.Add(dev);
                }
                finally
                {
                    CloseHandle(hDev);
                }
            }
        }

        private static void ScanLogitechDevices(List<PeripheralDevice> list, Dictionary<string, bool> seen, List<HidEntry> hid)
        {
            // Logitech HID++ over Unifying / Lightspeed / Bolt receivers (VID 0x046D).
            // Only vendor-defined collections (usage page >= 0xFF00) are opened for writing;
            // standard keyboard/mouse/consumer collections are never touched.
            foreach (HidEntry e in hid)
            {
                if (e.Vid != 0x046D || e.UsagePage < 0xFF00 || e.OutLen < 7 || e.InLen < 7) continue;

                IntPtr hDev = OpenHid(e.DevicePath, GENERIC_READ | GENERIC_WRITE);
                if (!IsValidHandle(hDev)) continue;

                try
                {
                    for (byte devIndex = 1; devIndex <= 4; devIndex++)
                    {
                        string devId = "logitech:" + devIndex;
                        if (seen.ContainsKey(devId)) continue;

                        // HID++ 1.0 register read (0x81) of register 0x07 (battery): 0x10 <idx> 0x81 0x07 0 0 0
                        byte[] outReport = new byte[e.OutLen];
                        outReport[0] = 0x10;
                        outReport[1] = devIndex;
                        outReport[2] = 0x81;
                        outReport[3] = 0x07;

                        byte idx = devIndex;
                        // Accept only the matching register reply; HID++ error replies (sub-id 0x8F) and unrelated
                        // notifications are skipped instead of being decoded as a battery level.
                        byte[] resp = HidTransact(hDev, outReport, e.InLen, 200, 300, false,
                            d => d.Length >= 7 && d[0] == 0x10 && d[1] == idx && d[2] == 0x81 && d[3] == 0x07);
                        if (resp == null) continue;

                        int rawLevel = resp[4];
                        int pct = -1;
                        if (rawLevel <= 100) pct = rawLevel;
                        else if (rawLevel == 0x10) pct = 100;
                        else if (rawLevel == 0x20) pct = 50;
                        else if (rawLevel == 0x40) pct = 20;
                        else if (rawLevel == 0x80) pct = 5;
                        if (pct < 0) continue;

                        seen[devId] = true;
                        PeripheralDevice dev = new PeripheralDevice();
                        dev.Id = devId;
                        dev.Name = string.Format("Logitech Device #{0}", devIndex);
                        dev.BatteryPercentage = pct;
                        dev.IsConnected = true;
                        dev.ConnectionType = "Logitech Unifying/Lightspeed";
                        dev.Protocol = "Logitech HID++";
                        dev.HardwarePath = e.DevicePath;
                        dev.Category = devIndex == 1 ? DeviceCategory.Mouse : DeviceCategory.Keyboard;
                        dev.StatusMessage = "Active - " + pct + "%";
                        list.Add(dev);
                    }
                }
                finally
                {
                    CloseHandle(hDev);
                }
            }
        }

        private static void ScanRazerDevices(List<PeripheralDevice> list, Dictionary<string, bool> seen, List<HidEntry> hid)
        {
            // Razer HyperSpeed 2.4GHz dongles (VID 0x1532), 90-byte status reports.
            const string devId = "razer:wireless";

            foreach (HidEntry e in hid)
            {
                if (seen.ContainsKey(devId)) return;
                if (e.Vid != 0x1532 || e.OutLen < 90 || e.InLen < 90) continue;

                IntPtr hDev = OpenHid(e.DevicePath, GENERIC_READ | GENERIC_WRITE);
                if (!IsValidHandle(hDev)) continue;

                try
                {
                    byte[] outReport = new byte[e.OutLen];
                    outReport[0] = 0x00; // Report ID
                    outReport[1] = 0x00; // Status
                    outReport[2] = 0x00; // Transaction ID
                    outReport[5] = 0x02; // Command Class: Device
                    outReport[6] = 0x07; // Command ID: Battery

                    byte[] resp = HidTransact(hDev, outReport, e.InLen, 200, 500, false, d => d.Length >= 10);
                    if (resp == null) continue;

                    int pct = (int)((resp[9] / 255.0) * 100);
                    if (pct < 0 || pct > 100) continue;

                    seen[devId] = true;
                    PeripheralDevice dev = new PeripheralDevice();
                    dev.Id = devId;
                    dev.Name = "Razer Wireless Mouse/Keyboard";
                    dev.BatteryPercentage = pct;
                    dev.IsConnected = true;
                    dev.ConnectionType = "Razer HyperSpeed 2.4G";
                    dev.Protocol = "Razer HID";
                    dev.HardwarePath = e.DevicePath;
                    dev.Category = DeviceCategory.Mouse;
                    dev.StatusMessage = "Active - " + pct + "%";
                    list.Add(dev);
                }
                finally
                {
                    CloseHandle(hDev);
                }
            }
        }

        private static void ScanGenericHidBatteryDevices(List<PeripheralDevice> list, Dictionary<string, bool> seen, List<HidEntry> hid)
        {
            // USB-IF Battery System (0x85) / Power Device (0x84) pages.
            // The inventory pass already identified these by usage page, so only matching interfaces are opened,
            // and only READ-ONLY (a feature-report read needs no write access).
            foreach (HidEntry e in hid)
            {
                if (e.UsagePage != 0x85 && e.UsagePage != 0x84) continue;
                if (e.FeatLen == 0) continue;

                string devId = "hid_bat:" + e.DevicePath.ToLower();
                if (seen.ContainsKey(devId)) continue;

                IntPtr hDev = OpenHid(e.DevicePath, GENERIC_READ);
                if (!IsValidHandle(hDev)) continue;

                try
                {
                    using (HidOp op = new HidOp(hDev, e.FeatLen))
                    {
                        // Same layout as HidD_GetFeature (report ID 0 in byte 0), but with a 1 s timeout
                        // and confirmed cancellation instead of a call that can block forever.
                        if (!op.StartGetFeature(0)) continue;
                        if (op.Wait(1000) < 1) continue;

                        byte[] buf = op.Data(e.FeatLen);
                        int pct = buf.Length > 1 ? buf[1] : -1;
                        if (pct < 0 || pct > 100) continue;

                        seen[devId] = true;
                        PeripheralDevice dev = new PeripheralDevice();
                        dev.Id = devId;
                        dev.Name = "Standard HID Wireless Device";
                        dev.BatteryPercentage = pct;
                        dev.IsConnected = true;
                        dev.ConnectionType = "USB HID Battery System (2.4G)";
                        dev.Protocol = "UsagePage 0x85";
                        dev.HardwarePath = e.DevicePath;
                        dev.Category = CategorizeDevice("", e.DevicePath, "", e.UsagePage, e.Usage);
                        dev.StatusMessage = "Active - " + pct + "%";
                        list.Add(dev);
                    }
                }
                finally
                {
                    CloseHandle(hDev);
                }
            }
        }

        private static void ScanCustomDevices(List<PeripheralDevice> list, Dictionary<string, bool> seen, List<CustomDeviceConfig> customConfigs, List<HidEntry> hid)
        {
            if (customConfigs == null || customConfigs.Count == 0) return;

            foreach (CustomDeviceConfig cfg in customConfigs)
            {
                // Validate config-supplied values before they drive any I/O.
                if (cfg == null || string.IsNullOrEmpty(cfg.Name)) continue;
                if (cfg.BatteryByteOffset < 0 || cfg.BatteryByteOffset >= 4096) continue;

                string devId = "custom:" + cfg.Name.ToLower();
                if (seen.ContainsKey(devId)) continue;

                foreach (HidEntry e in hid)
                {
                    if (e.Vid != cfg.VendorId || e.Pid != cfg.ProductId) continue;
                    if (cfg.UsagePage != 0 && e.UsagePage != cfg.UsagePage) continue;
                    if (e.InLen == 0 || e.OutLen == 0) continue;
                    if (cfg.BatteryByteOffset >= e.InLen) continue;

                    IntPtr hDev = OpenHid(e.DevicePath, GENERIC_READ | GENERIC_WRITE);
                    if (!IsValidHandle(hDev)) continue;

                    try
                    {
                        // Exact report lengths from the device's own descriptor (never larger than it declares).
                        byte[] outReport = new byte[e.OutLen];
                        outReport[0] = cfg.QueryReportId;
                        if (cfg.QueryPayload != null)
                        {
                            for (int i = 0; i < cfg.QueryPayload.Length && i + 1 < outReport.Length; i++)
                                outReport[i + 1] = cfg.QueryPayload[i];
                        }

                        CustomDeviceConfig c = cfg;
                        byte[] resp = HidTransact(hDev, outReport, e.InLen, 500, 2000, false,
                            d => d.Length > c.BatteryByteOffset && d[0] == c.ExpectedResponseReportId);
                        if (resp == null) continue;

                        int pct = resp[cfg.BatteryByteOffset];
                        if (pct < 0 || pct > 100) continue;

                        seen[devId] = true;
                        PeripheralDevice dev = new PeripheralDevice();
                        dev.Id = devId;
                        dev.Name = cfg.Name;
                        dev.BatteryPercentage = pct;
                        dev.IsConnected = true;
                        dev.ConnectionType = "Custom 2.4G Receiver";
                        dev.Protocol = "Custom Query";
                        dev.HardwarePath = e.DevicePath;
                        dev.Category = cfg.Category;
                        dev.StatusMessage = "Active - " + pct + "%";
                        list.Add(dev);
                    }
                    finally
                    {
                        CloseHandle(hDev);
                    }

                    if (seen.ContainsKey(devId)) break;
                }
            }
        }

        public static DeviceCategory CategorizeDevice(string name, string hardwareId, string className, ushort usagePage, ushort usage)
        {
            if (usagePage == 1)
            {
                if (usage == 6) return DeviceCategory.Keyboard;
                if (usage == 2) return DeviceCategory.Mouse;
            }

            string s = ((name ?? "") + " " + (hardwareId ?? "") + " " + (className ?? "")).ToLower();

            if (s.Contains("keyboard") || s.Contains("keybrd") || s.Contains(" kb") || s.Contains("trackpoint") || s.Contains("keypad"))
            {
                return DeviceCategory.Keyboard;
            }

            if (s.Contains("mouse") || s.Contains("pointer") || s.Contains("trackball") || s.Contains("touchpad") ||
                s.Contains(" m6") || s.Contains(" m3") || s.Contains(" m5") || s.Contains("master") || s.Contains("superlight"))
            {
                return DeviceCategory.Mouse;
            }

            if (s.Contains("headset") || s.Contains("headphone") || s.Contains("buds") || s.Contains("airpods") || s.Contains("audio"))
            {
                return DeviceCategory.Headset;
            }

            if (s.Contains("controller") || s.Contains("gamepad") || s.Contains("joystick"))
            {
                return DeviceCategory.Controller;
            }

            return DeviceCategory.Other;
        }
    }

    public static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "UniversalPeripheralBatteryMonitor";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(AppName);
                        return val != null;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(AppName, "\"" + exePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update Windows startup setting:\n" + ex.Message, "Auto-Start Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    public class ModernSectionPanel : Panel
    {
        private string _title;
        public string Title
        {
            get { return _title; }
            set { _title = value; Invalidate(); }
        }

        public ModernSectionPanel(string title)
        {
            _title = title;
            this.BackColor = Color.FromArgb(28, 29, 33);
            this.Padding = new Padding(15, 30, 15, 15);
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Border
            using (Pen p = new Pen(Color.FromArgb(50, 52, 60), 1f))
            {
                Rectangle r = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                g.DrawRectangle(p, r);
            }

            // Left accent bar
            using (Brush b = new SolidBrush(Color.FromArgb(59, 130, 246))) // Blue accent
            {
                g.FillRectangle(b, 0, 0, 4, this.Height);
            }

            // Title
            if (!string.IsNullOrEmpty(_title))
            {
                using (Font f = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Brush bText = new SolidBrush(Color.FromArgb(240, 240, 245)))
                {
                    g.DrawString(_title, f, bText, 14, 8);
                }
            }
        }
    }

    public class ModernBatteryBar : Panel
    {
        private int _percentage;
        private bool _isConnected;

        public int Percentage
        {
            get { return _percentage; }
            set { _percentage = value; Invalidate(); }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            set { _isConnected = value; Invalidate(); }
        }

        public ModernBatteryBar(int pct, bool connected)
        {
            _percentage = pct;
            _isConnected = connected;
            this.Height = 14;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(22, 23, 26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;

            // Track background
            using (Brush bTrack = new SolidBrush(Color.FromArgb(24, 25, 28)))
            using (Pen pBorder = new Pen(Color.FromArgb(55, 57, 65), 1f))
            {
                g.FillRectangle(bTrack, 0, 0, w - 1, h - 1);
                g.DrawRectangle(pBorder, 0, 0, w - 1, h - 1);
            }

            // Fill
            if (_isConnected && _percentage > 0)
            {
                int fillWidth = Math.Max(4, (int)((w - 4) * (Math.Min(100, _percentage) / 100.0)));
                Color barColor = Color.FromArgb(34, 197, 94); // Green
                if (_percentage < 20) barColor = Color.FromArgb(239, 68, 68); // Red
                else if (_percentage < 50) barColor = Color.FromArgb(245, 158, 11); // Amber

                using (Brush bFill = new SolidBrush(barColor))
                {
                    g.FillRectangle(bFill, 2, 2, fillWidth, h - 4);
                }
            }
        }
    }

    public class SetupDialog : Form
    {
        private ComboBox _cbKeyboard;
        private ComboBox _cbMouse;
        private ComboBox _cbInterval;
        private ComboBox _cbDisplayMode;
        private ComboBox _cbStyle;
        private Button _btnSave;
        private Button _btnCancel;

        private AppSettings _settings;
        private List<PeripheralDevice> _devices;
        private Action _onSave;

        public SetupDialog(AppSettings settings, List<PeripheralDevice> devices, Action onSave)
        {
            _settings = settings;
            _devices = devices;
            _onSave = onSave;
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "Universal Peripheral Battery Monitor - Setup";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 20);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.DoubleBuffered = true;

            Label lblHeader = new Label
            {
                Text = "Device Setup & Configuration",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Location = new Point(22, 14),
                AutoSize = true,
                ForeColor = Color.White
            };
            this.Controls.Add(lblHeader);

            Label lblSub = new Label
            {
                Text = "Configure which wireless peripherals are pinned to your taskbar tray icons.",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(22, lblHeader.Bottom + 6),
                AutoSize = true,
                ForeColor = Color.FromArgb(156, 163, 175)
            };
            this.Controls.Add(lblSub);

            // Section 1: Device Mapping
            int pnl1Y = lblSub.Bottom + 12;
            ModernSectionPanel pnlMapping = new ModernSectionPanel("Primary Peripherals Assignment")
            {
                Location = new Point(20, pnl1Y),
                Size = new Size(595, 170)
            };

            Label lblKb = new Label
            {
                Text = "Primary Keyboard:",
                Location = new Point(18, 42),
                Size = new Size(150, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 230, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cbKeyboard = new ComboBox
            {
                Location = new Point(175, 40),
                Size = new Size(395, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 44, 50),
                ForeColor = Color.White
            };

            Label lblM = new Label
            {
                Text = "Primary Mouse:",
                Location = new Point(18, 87),
                Size = new Size(150, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 230, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cbMouse = new ComboBox
            {
                Location = new Point(175, 85),
                Size = new Size(395, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 44, 50),
                ForeColor = Color.White
            };

            Label lblMappingTip = new Label
            {
                Text = "Tip: When set to Auto-Detect, the highest-confidence active devices are selected automatically.",
                Location = new Point(18, 130),
                Size = new Size(550, 20),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(150, 150, 160)
            };

            pnlMapping.Controls.Add(lblKb);
            pnlMapping.Controls.Add(_cbKeyboard);
            pnlMapping.Controls.Add(lblM);
            pnlMapping.Controls.Add(_cbMouse);
            pnlMapping.Controls.Add(lblMappingTip);
            this.Controls.Add(pnlMapping);

            // Section 2: Preferences
            int pnl2Y = pnlMapping.Bottom + 14;
            ModernSectionPanel pnlPref = new ModernSectionPanel("Tray Display & Polling Preferences")
            {
                Location = new Point(20, pnl2Y),
                Size = new Size(595, 195)
            };

            Label lblMode = new Label
            {
                Text = "Tray Display Mode:",
                Location = new Point(18, 42),
                Size = new Size(150, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 230, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cbDisplayMode = new ComboBox
            {
                Location = new Point(175, 40),
                Size = new Size(395, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 44, 50),
                ForeColor = Color.White
            };
            _cbDisplayMode.Items.Add("Dual Icons (Separate Keyboard & Mouse Icons)");
            _cbDisplayMode.Items.Add("Keyboard Icon Only");
            _cbDisplayMode.Items.Add("Mouse Icon Only");
            _cbDisplayMode.Items.Add("Combined Single Icon (K:44 M:69)");
            _cbDisplayMode.Items.Add("All Detected Wireless Devices");

            Label lblStyle = new Label
            {
                Text = "Icon Visual Style:",
                Location = new Point(18, 87),
                Size = new Size(150, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 230, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cbStyle = new ComboBox
            {
                Location = new Point(175, 85),
                Size = new Size(395, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 44, 50),
                ForeColor = Color.White
            };
            _cbStyle.Items.Add("Badge Number + Battery Bar (Default)");
            _cbStyle.Items.Add("Large Bold Number (High Visibility)");
            _cbStyle.Items.Add("Battery Gauge / Dial Only");

            Label lblInt = new Label
            {
                Text = "Polling Interval:",
                Location = new Point(18, 132),
                Size = new Size(150, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 230, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _cbInterval = new ComboBox
            {
                Location = new Point(175, 130),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 44, 50),
                ForeColor = Color.White
            };
            _cbInterval.Items.Add("30 seconds");
            _cbInterval.Items.Add("1 minute");
            _cbInterval.Items.Add("2 minutes (Default)");
            _cbInterval.Items.Add("5 minutes");
            _cbInterval.Items.Add("10 minutes");

            pnlPref.Controls.Add(lblMode);
            pnlPref.Controls.Add(_cbDisplayMode);
            pnlPref.Controls.Add(lblStyle);
            pnlPref.Controls.Add(_cbStyle);
            pnlPref.Controls.Add(lblInt);
            pnlPref.Controls.Add(_cbInterval);
            this.Controls.Add(pnlPref);

            // Action Buttons
            int btnY = pnlPref.Bottom + 16;
            _btnSave = new Button
            {
                Text = "Save & Apply",
                UseMnemonic = false,
                Location = new Point(345, btnY),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(34, 197, 94),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += delegate
            {
                SaveConfig();
                this.Close();
            };
            this.Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(495, btnY),
                Size = new Size(120, 36),
                BackColor = Color.FromArgb(45, 47, 54),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(65, 67, 75);
            _btnCancel.Click += delegate { this.Close(); };
            this.Controls.Add(_btnCancel);

            // Set responsive client size
            this.ClientSize = new Size(635, btnY + 52);

            // Adaptive height check for small screens / high scaling
            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            int maxAllowedH = (int)(workArea.Height * 0.92);
            if (this.Height > maxAllowedH)
            {
                this.Height = maxAllowedH;
                this.AutoScroll = true;
            }

            PopulateUI();
        }

        private void PopulateUI()
        {
            UpdateDeviceList(_devices);

            if (_settings.PollIntervalSeconds == 30) _cbInterval.SelectedIndex = 0;
            else if (_settings.PollIntervalSeconds == 60) _cbInterval.SelectedIndex = 1;
            else if (_settings.PollIntervalSeconds == 300) _cbInterval.SelectedIndex = 3;
            else if (_settings.PollIntervalSeconds == 600) _cbInterval.SelectedIndex = 4;
            else _cbInterval.SelectedIndex = 2; // 2 min

            _cbDisplayMode.SelectedIndex = Math.Max(0, Math.Min(4, _settings.DisplayMode));
            _cbStyle.SelectedIndex = Math.Max(0, Math.Min(2, _settings.IconStyle));
        }

        public void UpdateDeviceList(List<PeripheralDevice> devices)
        {
            _devices = devices ?? new List<PeripheralDevice>();

            _cbKeyboard.Items.Clear();
            _cbKeyboard.Items.Add("Auto-Detect (Recommended)");

            _cbMouse.Items.Clear();
            _cbMouse.Items.Add("Auto-Detect (Recommended)");

            int selectedKbIdx = 0;
            int selectedMouseIdx = 0;

            for (int i = 0; i < _devices.Count; i++)
            {
                var d = _devices[i];
                string label = string.Format("{0} [{1}] - {2}% ({3})", d.Name, d.Category, d.BatteryPercentage >= 0 ? d.BatteryPercentage.ToString() : "--", d.ConnectionType);

                int itemKb = _cbKeyboard.Items.Add(label);
                if (d.Id == _settings.PreferredKeyboardId) selectedKbIdx = itemKb;

                int itemM = _cbMouse.Items.Add(label);
                if (d.Id == _settings.PreferredMouseId) selectedMouseIdx = itemM;
            }

            _cbKeyboard.SelectedIndex = selectedKbIdx;
            _cbMouse.SelectedIndex = selectedMouseIdx;
        }

        private void SaveConfig()
        {
            if (_cbKeyboard.SelectedIndex > 0 && _cbKeyboard.SelectedIndex - 1 < _devices.Count)
            {
                _settings.PreferredKeyboardId = _devices[_cbKeyboard.SelectedIndex - 1].Id;
            }
            else
            {
                _settings.PreferredKeyboardId = "auto";
            }

            if (_cbMouse.SelectedIndex > 0 && _cbMouse.SelectedIndex - 1 < _devices.Count)
            {
                _settings.PreferredMouseId = _devices[_cbMouse.SelectedIndex - 1].Id;
            }
            else
            {
                _settings.PreferredMouseId = "auto";
            }

            if (_cbInterval.SelectedIndex == 0) _settings.PollIntervalSeconds = 30;
            else if (_cbInterval.SelectedIndex == 1) _settings.PollIntervalSeconds = 60;
            else if (_cbInterval.SelectedIndex == 3) _settings.PollIntervalSeconds = 300;
            else if (_cbInterval.SelectedIndex == 4) _settings.PollIntervalSeconds = 600;
            else _settings.PollIntervalSeconds = 120;

            _settings.DisplayMode = _cbDisplayMode.SelectedIndex;
            _settings.IconStyle = _cbStyle.SelectedIndex;

            if (_onSave != null)
            {
                _onSave();
            }
        }
    }

    public class DashboardForm : Form
    {
        private FlowLayoutPanel _flpCards;
        private Label _lblStatus;
        private Button _btnRefresh;
        private Button _btnSetup;
        private Button _btnClose;

        private Action _refreshAction;
        private Action _setupAction;

        public DashboardForm(List<PeripheralDevice> devices, PeripheralDevice kb, PeripheralDevice mouse, Action refreshAction, Action setupAction)
        {
            _refreshAction = refreshAction;
            _setupAction = setupAction;
            InitializeUI();
            UpdateDashboard(devices, kb, mouse);
        }

        private void InitializeUI()
        {
            this.Text = "Universal Peripheral Battery Dashboard";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 20);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.DoubleBuffered = true;

            Label lblHeader = new Label
            {
                Text = "Wireless Peripherals Battery Monitor",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Location = new Point(22, 14),
                AutoSize = true,
                ForeColor = Color.White,
                UseMnemonic = false
            };
            this.Controls.Add(lblHeader);

            Label lblSub = new Label
            {
                Text = "Live battery status and hardware metrics across all wireless peripherals",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(22, lblHeader.Bottom + 6),
                AutoSize = true,
                ForeColor = Color.FromArgb(156, 163, 175)
            };
            this.Controls.Add(lblSub);

            int listY = lblSub.Bottom + 12;
            _flpCards = new FlowLayoutPanel
            {
                Location = new Point(20, listY),
                Size = new Size(595, 430),
                AutoScroll = true,
                BackColor = Color.Transparent,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown
            };
            this.Controls.Add(_flpCards);

            int footerY = _flpCards.Bottom + 12;
            _lblStatus = new Label
            {
                Text = "Last updated: Just now",
                Location = new Point(22, footerY + 8),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 140, 148),
                AutoSize = true
            };
            this.Controls.Add(_lblStatus);

            _btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new Point(310, footerY),
                Size = new Size(95, 34),
                BackColor = Color.FromArgb(45, 47, 54),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };
            _btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(65, 67, 75);
            _btnRefresh.Click += delegate
            {
                _btnRefresh.Enabled = false;
                _btnRefresh.Text = "Polling...";
                if (_refreshAction != null)
                {
                    _refreshAction();
                }
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 2500 };
                t.Tick += delegate
                {
                    _btnRefresh.Enabled = true;
                    _btnRefresh.Text = "Refresh";
                    t.Stop();
                    t.Dispose();
                };
                t.Start();
            };
            this.Controls.Add(_btnRefresh);

            _btnSetup = new Button
            {
                Text = "Setup...",
                Location = new Point(415, footerY),
                Size = new Size(95, 34),
                BackColor = Color.FromArgb(45, 47, 54),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };
            _btnSetup.FlatAppearance.BorderColor = Color.FromArgb(65, 67, 75);
            _btnSetup.Click += delegate
            {
                if (_setupAction != null)
                {
                    _setupAction();
                }
            };
            this.Controls.Add(_btnSetup);

            _btnClose = new Button
            {
                Text = "Close",
                Location = new Point(520, footerY),
                Size = new Size(95, 34),
                BackColor = Color.FromArgb(45, 47, 54),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };
            _btnClose.FlatAppearance.BorderColor = Color.FromArgb(65, 67, 75);
            _btnClose.Click += delegate { this.Close(); };
            this.Controls.Add(_btnClose);

            // Responsive sizing based on working area
            this.ClientSize = new Size(635, footerY + 48);

            Rectangle workArea = Screen.FromControl(this).WorkingArea;
            int maxAllowedH = (int)(workArea.Height * 0.90);
            if (this.Height > maxAllowedH)
            {
                this.Height = maxAllowedH;
                _flpCards.Height = this.ClientSize.Height - _flpCards.Top - 56;
                _btnRefresh.Top = this.ClientSize.Height - 44;
                _btnSetup.Top = this.ClientSize.Height - 44;
                _btnClose.Top = this.ClientSize.Height - 44;
                _lblStatus.Top = this.ClientSize.Height - 36;
            }
        }

        public void UpdateDashboard(List<PeripheralDevice> devices, PeripheralDevice kb, PeripheralDevice mouse)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { UpdateDashboard(devices, kb, mouse); });
                return;
            }

            _flpCards.SuspendLayout();
            _flpCards.Controls.Clear();

            if (devices == null || devices.Count == 0)
            {
                Panel emptyPanel = new Panel
                {
                    Size = new Size(575, 120),
                    BackColor = Color.FromArgb(28, 29, 33),
                    Margin = new Padding(0, 0, 0, 10)
                };
                Label emptyLbl = new Label
                {
                    Text = "No wireless peripherals detected.\n\nEnsure your Bluetooth keyboard/mouse or 2.4GHz USB dongle is connected,\nthen click 'Refresh' below.",
                    Size = new Size(550, 90),
                    Location = new Point(12, 16),
                    ForeColor = Color.FromArgb(160, 160, 165),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9.5f)
                };
                emptyPanel.Controls.Add(emptyLbl);
                _flpCards.Controls.Add(emptyPanel);
            }
            else
            {
                int cardWidth = _flpCards.ClientSize.Width > 520 ? _flpCards.ClientSize.Width - 10 : 575;
                foreach (var dev in devices)
                {
                    Panel card = new Panel
                    {
                        Size = new Size(cardWidth, 130),
                        BackColor = Color.FromArgb(28, 29, 33),
                        Margin = new Padding(0, 0, 0, 12),
                        Padding = new Padding(14, 10, 14, 10)
                    };
                    card.Paint += delegate(object s, PaintEventArgs e)
                    {
                        using (Pen p = new Pen(Color.FromArgb(50, 52, 60), 1f))
                        {
                            e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                        }
                    };

                    // ZONE A: Right Column Dedicated Battery Box
                    Panel pnlPct = new Panel
                    {
                        Location = new Point(card.Width - 120, 10),
                        Size = new Size(106, 44),
                        BackColor = Color.FromArgb(22, 23, 27)
                    };
                    pnlPct.Paint += delegate(object s, PaintEventArgs e)
                    {
                        using (Pen p = new Pen(Color.FromArgb(45, 47, 55), 1f))
                        {
                            e.Graphics.DrawRectangle(p, 0, 0, pnlPct.Width - 1, pnlPct.Height - 1);
                        }
                    };

                    int pct = dev.BatteryPercentage;
                    bool connected = dev.IsConnected;
                    string pctText = dev.GetDisplayPercentage();
                    if (dev.IsSleeping) pctText = pct >= 0 ? (pct + "%") : "Standby";
                    else if (!connected) pctText = pct >= 0 ? (pct + "%") : "Offline";

                    Color pctColor = GetColorForPercentage(pct, connected);

                    Label lblPct = new Label
                    {
                        Text = pctText,
                        Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                        ForeColor = pctColor,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    pnlPct.Controls.Add(lblPct);
                    card.Controls.Add(pnlPct);

                    // ZONE B: Left Header Area (Guaranteed not to touch Zone A)
                    Label lblName = new Label
                    {
                        Text = dev.Name,
                        Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(245, 245, 247),
                        Location = new Point(14, 12),
                        MaximumSize = new Size(card.Width - 145, 22),
                        AutoEllipsis = true,
                        AutoSize = true
                    };
                    card.Controls.Add(lblName);

                    // Primary Tag (Row 2, Below device name to prevent horizontal collision)
                    if (dev == kb || dev == mouse)
                    {
                        Label lblTrayTag = new Label
                        {
                            Text = dev == kb ? "★ Primary Keyboard" : "★ Primary Mouse",
                            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                            ForeColor = Color.FromArgb(96, 165, 250),
                            BackColor = Color.FromArgb(30, 45, 75),
                            Location = new Point(14, lblName.Bottom + 4),
                            Padding = new Padding(5, 2, 5, 2),
                            AutoSize = true
                        };
                        card.Controls.Add(lblTrayTag);
                    }

                    // ZONE C: Battery Bar
                    ModernBatteryBar bar = new ModernBatteryBar(pct, connected)
                    {
                        Location = new Point(14, 58),
                        Width = card.Width - 28
                    };
                    card.Controls.Add(bar);

                    // ZONE D: Metadata Lines
                    string statusStr = !string.IsNullOrEmpty(dev.StatusMessage) ? dev.StatusMessage : (connected ? "Active" : "Offline");
                    Label lblMeta1 = new Label
                    {
                        Text = string.Format("Connection: {0}   •   Status: {1}", dev.ConnectionType, statusStr),
                        Font = new Font("Segoe UI", 8.5f),
                        ForeColor = Color.FromArgb(200, 200, 205),
                        Location = new Point(14, 78),
                        AutoSize = true
                    };
                    card.Controls.Add(lblMeta1);

                    string protoStr = !string.IsNullOrEmpty(dev.Protocol) ? dev.Protocol : "Standard HID";
                    Label lblMeta2 = new Label
                    {
                        Text = string.Format("Protocol: {0}", protoStr),
                        Font = new Font("Segoe UI", 8f),
                        ForeColor = Color.FromArgb(140, 140, 148),
                        Location = new Point(14, 98),
                        AutoSize = true
                    };
                    card.Controls.Add(lblMeta2);

                    _flpCards.Controls.Add(card);
                }
            }

            _flpCards.ResumeLayout();
            _lblStatus.Text = "Last updated: " + DateTime.Now.ToString("HH:mm:ss");
        }

        private Color GetColorForPercentage(int pct, bool connected)
        {
            if (!connected && pct < 0) return Color.FromArgb(156, 163, 175); // Gray
            if (pct >= 50) return Color.FromArgb(34, 197, 94); // Green
            if (pct >= 20) return Color.FromArgb(245, 158, 11); // Amber
            if (pct >= 0) return Color.FromArgb(239, 68, 68); // Red
            return Color.FromArgb(156, 163, 175); // Gray
        }
    }
}
