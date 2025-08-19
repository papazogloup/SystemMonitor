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
using System.Media;  // Add at the
using System.Speech.Synthesis;

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
        private Computer? computer;
        private AlertSystem? alertSystem;
        private MonitorSettings settings = new();
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

        private Dictionary<BarType, System.Windows.Forms.Timer> blinkTimers = new();
        private Dictionary<BarType, bool> blinkStates = new();
        private static readonly Random random = new Random();
        private SpeechSynthesizer? speechSynthesizer;

        // Add these fields to the SystemTrayApp class:
        private DateTime snoozeUntil = DateTime.MinValue;
        private bool isSnoozeActive = false;

        public SystemTrayApp()
        {
            try 
            {
                // Initialize speech synthesizer
                speechSynthesizer = new SpeechSynthesizer();
                speechSynthesizer.Volume = 80;  // 0-100
                speechSynthesizer.Rate = 0;     // -10 to 10, 0 is normal speed

                // Load settings first
                settings = SettingsManager.LoadSettings();
                if (!settings.Bars.Any())
                {
                    settings = new MonitorSettings(); // Use default settings if empty
                    SettingsManager.SaveSettings(settings);
                }

                InitializeComponent();
                
                // Initialize monitoring components
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
                
                // Initialize AlertSystem
                alertSystem = new AlertSystem(settings);
                alertSystem.OnAlert += HandleAlert;
                
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
                UpdateTrayIcon();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
        
        private void InitializeComponent()
        {
            // Hide the form
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            // Create tray menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Options...", null, OnSettings);
            trayMenu.Items.Add("About", null, OnAbout);
            trayMenu.Items.Add("-"); // separator
            trayMenu.Items.Add("Exit", null, OnExit);

            // Create tray icon
            trayIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(0, 0, 0),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            // Remove any existing click handlers and add the correct ones
            trayIcon.MouseClick += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    // Single click = Snooze
                    ActivateSnooze();
                }
            };

            trayIcon.MouseDoubleClick += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    // Double click = Settings
                    ShowSettings();
                }
            };
        }

        private void ShowSettings()
        {
            // If settings form is already open, bring it to front
            if (Application.OpenForms.OfType<SettingsForm>().Any())
            {
                var form = Application.OpenForms.OfType<SettingsForm>().First();
                form.WindowState = FormWindowState.Normal;
                form.BringToFront();
                return;
            }

            // Otherwise create and show new settings form
            var settingsForm = new SettingsForm(settings)
            {
                Owner = this
            };
            settingsForm.Show();
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
                }
                
                // Update RAM
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    currentRamUsage = memStatus.dwMemoryLoad;
                }
                
                // Update Network
                currentNetworkUsage = GetNetworkUsage();

                // Update CPU Temperature
                currentCpuTemperature = GetCpuTemperature();
                
                // Check thresholds and trigger alerts - ONLY FOR VISIBLE BARS
                if (alertSystem != null)
                {
                    var metrics = new Dictionary<BarType, float>
                    {
                        { BarType.CPU, currentCpuUsage },
                        { BarType.RAM, currentRamUsage },
                        { BarType.Network, currentNetworkUsage },
                        { BarType.CPUTemp, currentCpuTemperature },
                        { BarType.CPUMaxTemp, currentCpuMaxTemperature }
                    };

                    // Filter to only visible bars
                    foreach (var bar in settings.Bars.Where(b => b.IsVisible))
                    {
                        if (metrics.ContainsKey(bar.Type))
                        {
                            float value = metrics[bar.Type];
                            if (value > bar.Threshold)
                            {
                                string message = $"{bar.Type} is above threshold: {value:F1}";
                                switch (bar.Type)
                                {
                                    case BarType.CPU:
                                        message = $"CPU Usage is above {bar.Threshold}%: {value:F1}%";
                                        break;
                                    case BarType.RAM:
                                        message = $"Memory Usage is above {bar.Threshold}%: {value:F1}%";
                                        break;
                                    case BarType.Network:
                                        message = $"Network Usage is above {bar.Threshold} KB/s: {value:F1} KB/s";
                                        break;
                                    case BarType.CPUTemp:
                                        message = $"CPU Temperature is above {bar.Threshold}°C: {value:F1}°C";
                                        break;
                                    case BarType.CPUMaxTemp:
                                        message = $"CPU Max Temperature is above {bar.Threshold}°C: {value:F1}°C";
                                        break;
                                }
                                Debug.WriteLine($"Triggering alert for {bar.Type}: value={value:F1}, threshold={bar.Threshold:F1}");
                                alertSystem.TriggerAlert(bar.Type, message);
                            }
                        }
                    }
                }

                // Update tray icon and tooltip - ONLY SHOW VISIBLE BARS
                if (trayIcon != null)
                {
                    var tooltipLines = new List<string>();

                    // Add snooze status if active
                    if (isSnoozeActive && DateTime.Now < snoozeUntil)
                    {
                        var remaining = snoozeUntil - DateTime.Now;
                        tooltipLines.Add($"Snoozed: {remaining.Minutes:D2}:{remaining.Seconds:D2}");
                    }

                    // Use the ordered bars from settings - ONLY VISIBLE ONES
                    foreach (var bar in settings.Bars.Where(b => b.IsVisible))
                    {
                        string value = "";
                        switch (bar.Type)
                        {
                            case BarType.CPU:
                                value = $"CPU: {currentCpuUsage:F1}%";
                                break;
                            case BarType.RAM:
                                value = $"RAM: {currentRamUsage:F1}%";
                                break;
                            case BarType.Network:
                                value = currentNetworkUsage >= 1024 
                                    ? $"NET: {currentNetworkUsage / 1024:F1} MB/s"
                                    : $"NET: {currentNetworkUsage:F0} KB/s";
                                break;
                            case BarType.CPUTemp:
                                value = $"TMP: {currentCpuTemperature:F1}°";
                                break;
                            case BarType.CPUMaxTemp:
                                value = $"MAX: {currentCpuMaxTemperature:F1}°";
                                break;
                        }
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            tooltipLines.Add(value);
                        }
                    }

                    // Create tooltip text with length limit (Windows tooltip limit is ~127 characters)
                    string tooltipText = string.Join("\n", tooltipLines);
                    if (tooltipText.Length > 120) // Keep some margin
                    {
                        // Truncate to safe length
                        tooltipText = tooltipText.Substring(0, 117) + "...";
                    }

                    try
                    {
                        trayIcon.Text = tooltipText;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Fallback to minimal tooltip
                        trayIcon.Text = "System Monitor";
                    }
                    
                    UpdateTrayIcon();
                }
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error in UpdateSystemInfo: {ex.Message}");
            }
        }

        private void UpdateTrayIcon()
        {
            if (trayIcon == null) return;

            try
            {
                using var newIcon = CreateTrayIcon(currentCpuUsage, currentRamUsage, currentNetworkUsage);
                var oldIcon = trayIcon.Icon;
                trayIcon.Icon = (Icon)newIcon.Clone();
                oldIcon?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating tray icon: {ex.Message}");
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

        private void OnSettings(object? sender, EventArgs e)
        {
            ShowSettings();
        }
        
        private void OnAbout(object? sender, EventArgs e)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(
                $"System Monitor\n\n" +
                $"Version {version?.ToString() ?? "unknown"}\n\n" +
                "Copyleft: papazogloup",
                "About System Monitor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private Dictionary<BarType, float> peakValues = new();

        private Icon CreateTrayIcon(float cpuUsage, float ramUsage, float networkUsage)
        {
            using var bitmap = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bitmap);
            
            g.Clear(Color.Black);
            
            // Only include visible bars in values
            var visibleBars = settings.Bars.Where(b => b.IsVisible).ToList();
            
            if (visibleBars.Count == 0)
            {
                // If no bars are visible, return a simple black icon
                return Icon.FromHandle(bitmap.GetHicon());
            }
            
            var values = new Dictionary<BarType, float>();
            
            foreach (var bar in visibleBars)
            {
                switch (bar.Type)
                {
                    case BarType.CPU:
                        values[bar.Type] = cpuUsage;
                        break;
                    case BarType.RAM:
                        values[bar.Type] = ramUsage;
                        break;
                    case BarType.Network:
                        values[bar.Type] = networkUsage;
                        break;
                    case BarType.CPUTemp:
                        values[bar.Type] = currentCpuTemperature;
                        break;
                    case BarType.CPUMaxTemp:
                        values[bar.Type] = currentCpuMaxTemperature;
                        break;
                }
            }

            // Update peak values only for visible bars
            foreach (var pair in values)
            {
                if (!peakValues.ContainsKey(pair.Key) || pair.Value > peakValues[pair.Key])
                {
                    peakValues[pair.Key] = pair.Value;
                }
            }

            int totalBars = visibleBars.Count;
            int baseWidth = 16 / totalBars;
            int extraPixels = 16 % totalBars;
            int currentPos = 0;

            using var grayPen = new Pen(Color.FromArgb(64, 64, 64), 1);
            using var peakPen = new Pen(Color.FromArgb(128, 128, 128), 1);
            using var thresholdPen = new Pen(Color.FromArgb(255, 0, 0), 1);

            for (int i = 0; i < totalBars; i++)
            {
                var bar = visibleBars[i];
                float value = values[bar.Type];
                float peak = peakValues.ContainsKey(bar.Type) ? peakValues[bar.Type] : 0;
                float threshold = bar.Threshold;

                float scale = bar.Type == BarType.Network ?
                    Math.Min(value / 10240, 1.0f) :
                    Math.Min(value / 100.0f, 1.0f);

                float peakScale = bar.Type == BarType.Network ?
                    Math.Min(peak / 10240, 1.0f) :
                    Math.Min(peak / 100.0f, 1.0f);

                float thresholdScale = bar.Type == BarType.Network ?
                    Math.Min(threshold / 10240, 1.0f) :
                    Math.Min(threshold / 100.0f, 1.0f);

                int barSize = baseWidth + (i < extraPixels ? 1 : 0);
                int barLength = Math.Max(1, (int)(16 * scale));
                int peakLine = Math.Max(1, (int)(16 * peakScale));
                int thresholdLine = Math.Max(1, (int)(16 * thresholdScale));

                if (settings.IsHorizontalLayout)
                {
                    if (settings.ShowGuideLines)
                    {
                        g.DrawLine(grayPen, 
                            0, currentPos + barSize/2,
                            16, currentPos + barSize/2);
                    }

                    g.FillRectangle(new SolidBrush(bar.Color),
                        0, currentPos,
                        barLength, barSize);

                    if (settings.ShowPeakLines)
                    {
                        g.DrawLine(peakPen,
                            peakLine, currentPos,
                            peakLine, currentPos + barSize);
                    }

                    if (settings.ShowThresholdLines)
                    {
                        g.DrawLine(thresholdPen,
                            thresholdLine, currentPos,
                            thresholdLine, currentPos + barSize);
                    }
                }
                else
                {
                    if (settings.ShowGuideLines)
                    {
                        g.DrawLine(grayPen, 
                            currentPos + barSize/2, 0,
                            currentPos + barSize/2, 16);
                    }

                    g.FillRectangle(new SolidBrush(bar.Color),
                        currentPos, 16 - barLength,
                        barSize, barLength);

                    if (settings.ShowPeakLines)
                    {
                        g.DrawLine(peakPen,
                            currentPos, 16 - peakLine,
                            currentPos + barSize, 16 - peakLine);
                    }

                    if (settings.ShowThresholdLines)
                    {
                        g.DrawLine(thresholdPen,
                            currentPos, 16 - thresholdLine,
                            currentPos + barSize, 16 - thresholdLine);
                    }
                }

                if (blinkStates.TryGetValue(bar.Type, out bool isBlinking) && isBlinking)
                {
                    using var whitePen = new Pen(Color.White, 1);
                    if (settings.IsHorizontalLayout)
                    {
                        g.DrawRectangle(whitePen, 0, currentPos, 15, barSize - 1);
                    }
                    else
                    {
                        g.DrawRectangle(whitePen, currentPos, 0, barSize - 1, 15);
                    }
                }

                currentPos += barSize;
            }

            return Icon.FromHandle(bitmap.GetHicon());
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
                speechSynthesizer?.Dispose();
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

            if (computer?.Hardware != null)
            {
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
            }

            float avgTemp = count > 0 ? sum / count : 0;
            Debug.WriteLine($"Manually calculated average: {avgTemp:F1}°C");
            return avgTemp;
        }

        public void UpdateSettings(MonitorSettings newSettings)
        {
            settings = newSettings;
            UpdateTrayIcon();
        }

        // Add the snooze functionality:
        private void ActivateSnooze()
        {
            if (settings.AlertSettings.SnoozeMinutes > 0)
            {
                snoozeUntil = DateTime.Now.AddMinutes(settings.AlertSettings.SnoozeMinutes);
                isSnoozeActive = true;
                
                Debug.WriteLine($"Snooze activated until {snoozeUntil:HH:mm:ss}");
                
                // Show notification
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(3000, 
                        "System Monitor", 
                        $"Alerts snoozed for {settings.AlertSettings.SnoozeMinutes} minutes", 
                        ToolTipIcon.Info);
                }
                
                // Clear any active blinking
                foreach (var timer in blinkTimers.Values)
                {
                    timer.Stop();
                    timer.Dispose();
                }
                blinkTimers.Clear();
                blinkStates.Clear();
                UpdateTrayIcon();
            }
        }

        private void HandleAlert(BarType type)
        {
            // Check if snooze is active
            if (isSnoozeActive)
            {
                if (DateTime.Now < snoozeUntil)
                {
                    Debug.WriteLine($"Alert for {type} suppressed due to snooze until {snoozeUntil:HH:mm:ss}");
                    return;
                }
                else
                {
                    // Snooze period ended
                    isSnoozeActive = false;
                    Debug.WriteLine("Snooze period ended");
                }
            }

            Debug.WriteLine($"HandleAlert called for {type}, Alerts enabled: {settings.AlertSettings.IsEnabled}, Sound enabled: {settings.AlertSettings.SoundEnabled}");

            // Check if alerts are enabled at all - if not, return early
            if (!settings.AlertSettings.IsEnabled)
            {
                Debug.WriteLine("Alerts are disabled - skipping alert");
                return;
            }

            // Play sound only if both alerts AND sound are enabled
            if (settings.AlertSettings.SoundEnabled)
            {
                Debug.WriteLine("Playing voice alert...");
                
                // Get current values for each type
                var currentValues = new Dictionary<BarType, float>
                {
                    { BarType.CPU, currentCpuUsage },
                    { BarType.RAM, currentRamUsage },
                    { BarType.Network, currentNetworkUsage },
                    { BarType.CPUTemp, currentCpuTemperature },
                    { BarType.CPUMaxTemp, currentCpuMaxTemperature }
                };

                // Create voice messages with current values
                var voiceMessages = new Dictionary<BarType, string>();
                
                if (currentValues.TryGetValue(type, out float currentValue))
                {
                    voiceMessages = new Dictionary<BarType, string>
                    {
                        { BarType.CPU, $"CPU usage high at {currentValue:F0} percent" },
                        { BarType.RAM, $"Memory usage high at {currentValue:F0} percent" },  
                        { BarType.Network, currentValue >= 1024 ? 
                            $"Network usage high at {currentValue/1024:F1} megabytes per second" : 
                            $"Network usage high at {currentValue:F0} kilobytes per second" },
                        { BarType.CPUTemp, $"CPU temperature high at {currentValue:F0} degrees celsius" },
                        { BarType.CPUMaxTemp, $"Max temperature critical at {currentValue:F0} degrees celsius" }
                    };
                }

                if (voiceMessages.TryGetValue(type, out string? message) && speechSynthesizer != null)
                {
                    Task.Run(() => speechSynthesizer.SpeakAsync(message));
                }
            }

            // Visual blinking - only if alerts are enabled
            if (blinkTimers.ContainsKey(type))
            {
                blinkTimers[type].Stop();
                blinkTimers[type].Dispose();
            }

            var blinkTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };

            blinkStates[type] = true;
            var startTime = DateTime.Now;

            blinkTimer.Tick += (s, e) =>
            {
                blinkStates[type] = !blinkStates[type];
                UpdateTrayIcon();

                if ((DateTime.Now - startTime).TotalSeconds >= 5)
                {
                    blinkTimer.Stop();
                    blinkTimer.Dispose();
                    blinkStates.Remove(type);
                    blinkTimers.Remove(type);
                    UpdateTrayIcon();
                }
            };

            blinkTimers[type] = blinkTimer;
            blinkTimer.Start();
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
