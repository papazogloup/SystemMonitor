using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management; // Προσθήκη για System.Management
using LibreHardwareMonitor.Hardware;

namespace SystemMonitor
{
    public partial class SystemTrayApp : Form
    {
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private System.Windows.Forms.Timer? timer;
        private PerformanceCounter? cpuCounter;
        private NetworkInterface[]? networkInterfaces;
        private long lastBytesReceived = 0;
        private long lastBytesSent = 0;
        private DateTime lastNetworkCheck = DateTime.Now;
        
        private float currentCpuUsage = 0;
        private float currentRamUsage = 0;
        private float currentNetworkUsage = 0;
        private Computer computer;
        private float currentCpuTemperature = 0; // Προσθήκη μεταβλητής για θερμοκρασία CPU
        private float currentCpuMaxTemperature = 0; // Προσθήκη στα private fields της κλάσης

        // Memory info structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public SystemTrayApp()
        {
            InitializeComponent();
            
            try 
            {
                Debug.WriteLine("Initializing monitoring...");
                
                // Initialize LibreHardwareMonitor
                computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = false,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = false
                };
                computer.Open();
                Debug.WriteLine("LibreHardwareMonitor initialized");
                
                // Initialize CPU counter
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First reading (will be 0)
                Thread.Sleep(1000);     // Wait for first real measurement
                currentCpuUsage = cpuCounter.NextValue();
                Debug.WriteLine($"CPU counter initialized: {currentCpuUsage}%");
                
                // Initialize network interfaces
                networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                Debug.WriteLine($"Found {networkInterfaces.Length} network interfaces");
                
                // Start timer
                timer = new System.Windows.Forms.Timer
                {
                    Interval = 1000
                };
                timer.Tick += Timer_Tick;
                timer.Start();
                Debug.WriteLine("Timer started");
                
                // Initial update
                UpdateSystemInfo();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialization error: {ex.Message}");
                MessageBox.Show($"Error initializing monitoring: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                               "Initialization Error", 
                               MessageBoxButtons.OK, 
                               MessageBoxIcon.Error);
            }
        }
        
        private void SystemTrayApp_Load(object? sender, EventArgs e)
        {
            Debug.WriteLine("SystemTrayApp_Load called!");
            MessageBox.Show("Form Load Event Triggered!");  // Προσωρινό για testing
            InitializeMonitoring();
        }

        private void InitializeComponent()
        {
            // Hide the form
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            // Create tray menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("CPU: 0%", null, null);
            trayMenu.Items.Add("RAM: 0%", null, null);
            trayMenu.Items.Add("NET: 0 KB/s", null, null);
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, OnExit);

            // Create tray icon
            trayIcon = new NotifyIcon()
            {
                Text = "System Monitor",
                Icon = CreateTrayIcon(0, 0, 0),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += TrayIcon_DoubleClick;
        }

        private void InitializeMonitoring()
        {
            try
            {
                Debug.WriteLine("Initializing monitoring...");
                
                // Windows 11 τρόπος δημιουργίας του counter
                cpuCounter = new PerformanceCounter()
                {
                    CategoryName = "Processor Information",
                    CounterName = "% Processor Time",
                    InstanceName = "_Total",
                    ReadOnly = false
                };
                
                Debug.WriteLine("CPU Counter created");
                
                // Αρχική μέτρηση (θα επιστρέψει 0)
                float firstReading = cpuCounter.NextValue();
                Debug.WriteLine($"First CPU reading: {firstReading}");
                
                // Περιμένουμε 1 δευτερόλεπτο για την πρώτη πραγματική μέτρηση
                Thread.Sleep(1000);
                float secondReading = cpuCounter.NextValue();
                Debug.WriteLine($"Second CPU reading: {secondReading}");
                
                networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                Debug.WriteLine($"Found {networkInterfaces.Length} network interfaces");
                
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 1000;
                timer.Tick += Timer_Tick;
                timer.Start();
                Debug.WriteLine("Timer started");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in InitializeMonitoring: {ex.Message}");
                MessageBox.Show($"Error initializing monitoring: {ex.Message}\n\n" +
                               $"Please run as administrator and try again.", 
                               "Initialization Error", 
                               MessageBoxButtons.OK, 
                               MessageBoxIcon.Error);
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Αντί για Task.Run, το κάνουμε απευθείας για να αποφύγουμε race conditions
            UpdateSystemInfo();
        }

        private void UpdateSystemInfo()
        {
            try
            {
                // Update CPU
                if (cpuCounter != null)
                {
                    currentCpuUsage = cpuCounter.NextValue();
                    Debug.WriteLine($"CPU: {currentCpuUsage:F1}%");
                }
                
                // Update RAM
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(memStatus))
                {
                    currentRamUsage = memStatus.dwMemoryLoad;
                    Debug.WriteLine($"RAM: {currentRamUsage:F1}%");
                }
                
                // Update Network
                currentNetworkUsage = GetNetworkUsage();
                Debug.WriteLine($"Network: {currentNetworkUsage:F1} KB/s");

                // Update CPU Temperature
                currentCpuTemperature = GetCpuTemperature();
                Debug.WriteLine($"CPU Temp: {currentCpuTemperature:F1}°C");

                // Ενημέρωση UI
                UpdateTrayIcon();
                UpdateTrayMenu();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateSystemInfo: {ex.Message}");
            }
        }

        private void UpdateTrayIcon()
        {
            if (trayIcon != null)
            {
                // Create new icon with current usage levels
                var newIcon = CreateTrayIcon(currentCpuUsage, currentRamUsage, currentNetworkUsage);
                trayIcon.Icon?.Dispose();
                trayIcon.Icon = newIcon;
                
                // Update tooltip
                string networkText = currentNetworkUsage >= 1024 ? 
                    $"{currentNetworkUsage / 1024:F1} MB/s" : 
                    $"{currentNetworkUsage:F0} KB/s";
                    
                trayIcon.Text = $"CPU: {currentCpuUsage:F1}%\nRAM: {currentRamUsage:F1}%\nNET: {networkText}";
            }
        }

        private void UpdateTrayMenu()
        {
            if (trayMenu != null)
            {
                string networkText = currentNetworkUsage >= 1024 ? 
                    $"{currentNetworkUsage / 1024:F1} MB/s" : 
                    $"{currentNetworkUsage:F0} KB/s";

                trayMenu.Items[0].Text = $"CPU: {currentCpuUsage:F1}%";
                trayMenu.Items[1].Text = $"RAM: {currentRamUsage:F1}%";
                trayMenu.Items[2].Text = $"NET: {networkText}";
                trayMenu.Items[3].Text = $"CPU Avg: {currentCpuTemperature:F1}°C";
                
                // Πρόσθεσε το Core Max στο μενού
                if (trayMenu.Items.Count == 6) // Αν δεν έχει προστεθεί ήδη
                {
                    trayMenu.Items.Insert(4, new ToolStripMenuItem($"CPU Max: {currentCpuMaxTemperature:F1}°C"));
                }
                else
                {
                    trayMenu.Items[4].Text = $"CPU Max: {currentCpuMaxTemperature:F1}°C";
                }
            }
        }

        private Icon CreateTrayIcon(float cpuUsage, float ramUsage, float networkUsage)
        {
            using (var bitmap = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bitmap))
            {
                // Μαύρο background
                g.Clear(Color.Black);

                // CPU μπάρα (Έντονο μπλε) - 3 pixels
                int cpuHeight = Math.Max(1, (int)(16 * (cpuUsage / 100.0f)));
                using (var brush = new SolidBrush(Color.FromArgb(0, 120, 255)))
                {
                    g.FillRectangle(brush, 0, 16 - cpuHeight, 3, cpuHeight);
                }

                // RAM μπάρα (Έντονο πράσινο) - 3 pixels
                int ramHeight = Math.Max(1, (int)(16 * (ramUsage / 100.0f)));
                using (var brush = new SolidBrush(Color.FromArgb(0, 255, 0)))
                {
                    g.FillRectangle(brush, 3, 16 - ramHeight, 3, ramHeight);
                }

                // Network μπάρα (Μπορντό) - 3 pixels
                float networkScale = Math.Min(networkUsage / (10 * 1024), 1.0f);
                int networkHeight = Math.Max(1, (int)(16 * networkScale));
                using (var brush = new SolidBrush(Color.FromArgb(220, 20, 60)))
                {
                    g.FillRectangle(brush, 6, 16 - networkHeight, 3, networkHeight);
                }

                // CPU Average Temperature μπάρα (Κίτρινο) - 3 pixels
                float tempScale = Math.Min(currentCpuTemperature / 100.0f, 1.0f);
                int tempHeight = Math.Max(1, (int)(16 * tempScale));
                using (var brush = new SolidBrush(Color.FromArgb(255, 255, 0)))
                {
                    g.FillRectangle(brush, 9, 16 - tempHeight, 3, tempHeight);
                }

                // CPU Max Temperature μπάρα (Πορτοκαλί) - 4 pixels
                float maxTempScale = Math.Min(currentCpuMaxTemperature / 100.0f, 1.0f);
                int maxTempHeight = Math.Max(1, (int)(16 * maxTempScale));
                using (var brush = new SolidBrush(Color.FromArgb(255, 140, 0))) // Πορτοκαλί
                {
                    g.FillRectangle(brush, 12, 16 - maxTempHeight, 4, maxTempHeight);
                }

                IntPtr hIcon = bitmap.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private float GetNetworkUsage()
        {
            try
            {
                if (networkInterfaces == null) return 0;
                
                long totalBytesReceived = 0;
                long totalBytesSent = 0;
                
                foreach (NetworkInterface ni in networkInterfaces.Where(x => 
                    x.OperationalStatus == OperationalStatus.Up && 
                    (x.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                     x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)))
                {
                    IPv4InterfaceStatistics stats = ni.GetIPv4Statistics();
                    totalBytesReceived += stats.BytesReceived;
                    totalBytesSent += stats.BytesSent;
                }
                
                DateTime now = DateTime.Now;
                double timeDiff = (now - lastNetworkCheck).TotalSeconds;
                
                if (timeDiff > 0 && lastBytesReceived > 0)
                {
                    long totalBytes = (totalBytesReceived + totalBytesSent);
                    long lastTotalBytes = (lastBytesReceived + lastBytesSent);
                    
                    float bytesPerSecond = (float)((totalBytes - lastTotalBytes) / timeDiff);
                    float kilobytesPerSecond = bytesPerSecond / 1024;
                    
                    lastBytesReceived = totalBytesReceived;
                    lastBytesSent = totalBytesSent;
                    lastNetworkCheck = now;
                    
                    return Math.Max(0, kilobytesPerSecond);
                }
                
                lastBytesReceived = totalBytesReceived;
                lastBytesSent = totalBytesSent;
                lastNetworkCheck = now;
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void TrayIcon_DoubleClick(object? sender, EventArgs e)
        {
            // Optional: You could show a detailed window on double-click
            MessageBox.Show(
                $"System Monitor\n\n" +
                $"CPU: {currentCpuUsage:F1}%\n" +
                $"RAM: {currentRamUsage:F1}%\n" +
                $"Network: {(currentNetworkUsage >= 1024 ? $"{currentNetworkUsage / 1024:F1} MB/s" : $"{currentNetworkUsage:F0} KB/s")}",
                "System Monitor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void OnExit(object? sender, EventArgs e)
        {
            Application.Exit();
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                computer?.Close();
                timer?.Stop();
                timer?.Dispose();
                trayIcon?.Dispose();
                cpuCounter?.Dispose();
            }
            base.Dispose(disposing);
        }

        private float GetCpuTemperature()
        {
            try
            {
                if (computer?.Hardware == null) return 0;

                foreach (var hardware in computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        hardware.Update();
                        float? coreAvg = null;
                        float? coreMax = null;

                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (sensor.Name.Equals("Core Average", StringComparison.OrdinalIgnoreCase))
                                {
                                    coreAvg = sensor.Value;
                                }
                                else if (sensor.Name.Equals("Core Max", StringComparison.OrdinalIgnoreCase))
                                {
                                    coreMax = sensor.Value;
                                    if (coreMax.HasValue)
                                    {
                                        currentCpuMaxTemperature = coreMax.Value;
                                        Debug.WriteLine($"CPU Core Max: {coreMax.Value}°C");
                                    }
                                }
                            }
                        }

                        if (coreAvg.HasValue)
                        {
                            Debug.WriteLine($"CPU Core Average: {coreAvg.Value}°C");
                            return coreAvg.Value;
                        }
                    }
                }

                Debug.WriteLine("Core sensors not found, falling back to manual calculation");
                return GetManualCoreAverage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetCpuTemperature: {ex.Message}");
                return 0;
            }
        }

        // Fallback μέθοδος σε περίπτωση που δεν βρούμε το Core Average
        private float GetManualCoreAverage()
        {
            float sum = 0;
            int count = 0;

            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature &&
                            sensor.Name.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase) &&
                            !sensor.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase))
                        {
                            if (sensor.Value.HasValue)
                            {
                                Debug.WriteLine($"{sensor.Name}: {sensor.Value.Value}°C");
                                sum += sensor.Value.Value;
                                count++;
                            }
                        }
                    }
                }
            }

            float avgTemp = count > 0 ? sum / count : 0;
            Debug.WriteLine($"Manually calculated average: {avgTemp:F1}°C");
            return avgTemp;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Debug.WriteLine("Application starting...");
            Application.SetHighDpiMode(HighDpiMode.SystemAware); // Για Windows 11
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var app = new SystemTrayApp();
            Debug.WriteLine("Running application...");
            Application.Run(app); // Περνάμε το app ως παράμετρο
        }
    }
}
