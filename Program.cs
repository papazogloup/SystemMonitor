// System Monitor v2.0.0 - Advanced System Monitoring Tool
// Main application entry point

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using LibreHardwareMonitor.Hardware;
using System.Media;
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
        private float currentGpuTemperature = 0;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CORE_TEMP_SHARED_DATA
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] uiLoad;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public uint[] uiTjMax;
            public uint uiCoreCnt;
            public uint uiCPUCnt;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public float[] fTemp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public float[] fVID;
            public float fCPUSpeed;
            public float fFSBSpeed;
            public float fMultiplier;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public byte[] sCPUName;
            public byte ucFahrenheit;
            public byte ucDeltaToTjMax;
            public byte ucTdpSupport;
            public byte ucPowerSupport;
            public uint uiStructureVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] uiTdp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public float[] fPower;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public float[] fMultipliers;
        }

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

        // Add this field to track tooltip changes:
        private string lastTooltipText = "";

        // Add these fields to track icon changes:
        private Icon? lastIcon = null;
        private string lastIconKey = "";

        // Add this field to manage tooltip updates
        private DateTime lastTooltipUpdate = DateTime.MinValue;

        // Add this field to track recent alerts:
        private Dictionary<BarType, DateTime> recentAlerts = new();

        public SystemTrayApp()
        {
            try 
            {
                // Initialize speech synthesizer
                speechSynthesizer = new SpeechSynthesizer();
                speechSynthesizer.Volume = 80;  // 0-100
                speechSynthesizer.Rate = 0;     // -10 to 10, 0 is normal speed

                // Auto-start Core Temp if not running (needed for temperature readings)
                EnsureCoreTempRunning();

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
                    IsGpuEnabled = true,
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
            trayMenu.Items.Add("Settings...", null, OnSettings);  // Changed from "Options..." to "Settings..."
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

            // Simple single click for snooze - with better logic
            trayIcon.MouseClick += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    Debug.WriteLine("Left click detected");
                    ActivateSnooze();
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
                currentGpuTemperature = GetGpuTemperature();
                
                // Check thresholds and trigger alerts - ONLY FOR VISIBLE BARS
                if (alertSystem != null)
                {
                    var metrics = new Dictionary<BarType, float>
                    {
                        { BarType.CPU, currentCpuUsage },
                        { BarType.RAM, currentRamUsage },
                        { BarType.Network, currentNetworkUsage },
                        { BarType.CPUTemp, currentCpuTemperature },
                        { BarType.CPUMaxTemp, currentCpuMaxTemperature },
                        { BarType.GPUTemp, currentGpuTemperature }
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
                                    case BarType.GPUTemp:
                                        message = $"GPU Temperature is above {bar.Threshold}°C: {value:F1}°C";
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
                    // Create tooltip text with length limit
                    string tooltipText;

                    if (isSnoozeActive && DateTime.Now < snoozeUntil)
                    {
                        var remaining = snoozeUntil - DateTime.Now;
                        var lines = new List<string>();
                        
                        // Add ALL visible bars info FIRST (remove the .Take(3) limitation)
                        foreach (var bar in settings.Bars.Where(b => b.IsVisible))
                        {
                            switch (bar.Type)
                            {
                                case BarType.CPU:
                                    lines.Add($"CPU: {Math.Round(currentCpuUsage):F0}%");
                                    break;
                                case BarType.RAM:
                                    lines.Add($"RAM: {Math.Round(currentRamUsage):F0}%");
                                    break;
                                case BarType.Network:
                                    lines.Add($"NET: {(currentNetworkUsage >= 1024 ? $"{Math.Round(currentNetworkUsage / 1024):F0}MB/s" : $"{Math.Round(currentNetworkUsage):F0}KB/s")}");
                                    break;
                                case BarType.CPUTemp:
                                    lines.Add($"TMP: {Math.Round(currentCpuTemperature):F0}°");
                                    break;
                                case BarType.CPUMaxTemp:
                                    lines.Add($"MAX: {Math.Round(currentCpuMaxTemperature):F0}°");
                                    break;
                                case BarType.GPUTemp:
                                    lines.Add($"GPU: {Math.Round(currentGpuTemperature):F0}°");
                                    break;
                            }
                        }
                        
                        // Then add snooze info
                        lines.Add($"⏰ Snoozed: {remaining.Minutes:D2}:{remaining.Seconds:D2}");
                        lines.Add("🔇 Click to cancel snooze");
                        
                        tooltipText = string.Join("\n", lines);
                    }
                    else
                    {
                        var lines = new List<string>();
                        
                        // Add visible bars info FIRST
                        foreach (var bar in settings.Bars.Where(b => b.IsVisible))
                        {
                            switch (bar.Type)
                            {
                                case BarType.CPU:
                                    lines.Add($"CPU: {Math.Round(currentCpuUsage):F0}%");
                                    break;
                                case BarType.RAM:
                                    lines.Add($"RAM: {Math.Round(currentRamUsage):F0}%");
                                    break;
                                case BarType.Network:
                                    lines.Add($"NET: {(currentNetworkUsage >= 1024 ? $"{Math.Round(currentNetworkUsage / 1024):F0}MB/s" : $"{Math.Round(currentNetworkUsage):F0}KB/s")}");
                                    break;
                                case BarType.CPUTemp:
                                    lines.Add($"TMP: {Math.Round(currentCpuTemperature):F0}°");
                                    break;
                                case BarType.CPUMaxTemp:
                                    lines.Add($"MAX: {Math.Round(currentCpuMaxTemperature):F0}°");
                                    break;
                                case BarType.GPUTemp:
                                    lines.Add($"GPU: {Math.Round(currentGpuTemperature):F0}°");
                                    break;
                            }
                        }
                        
                        // Check if there are active alerts OR recent alerts - BUT ONLY IF ALERTS ARE ENABLED
                        bool hasActiveAlerts = settings.AlertSettings.IsEnabled && (blinkStates.Any(bs => bs.Value) || HasRecentAlerts());
                        
                        // THEN add alert messages AFTER the values - ONLY IF ALERTS ARE ENABLED
                        if (hasActiveAlerts)
                        {
                            if (blinkStates.Any(bs => bs.Value))
                            {
                                lines.Add("🚨 ALERT ACTIVE!");
                            }
                            else
                            {
                                lines.Add("⚠️ RECENT ALERT!");
                            }
                            lines.Add("👆 Click to snooze");
                        }
                        
                        tooltipText = string.Join("\n", lines);
                    }

                    // Apply length limit
                    if (tooltipText.Length > 120)
                    {
                        tooltipText = tooltipText.Substring(0, 117) + "...";
                    }

                    // Only update tooltip if it actually changed
                    if (tooltipText != lastTooltipText)
                    {
                        try
                        {
                            trayIcon.Text = tooltipText;
                            lastTooltipText = tooltipText;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            trayIcon.Text = "System Monitor";
                            lastTooltipText = "System Monitor";
                        }
                    }
                    
                    UpdateTrayIcon();
                }
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error in UpdateSystemInfo: {ex.Message}");
            }
        }

        // Replace the UpdateTrayIcon method:
        private void UpdateTrayIcon()
        {
            if (trayIcon == null) return;

            try
            {
                // Create a unique key for the current icon state
                var visibleBars = settings.Bars.Where(b => b.IsVisible).ToList();
                
                // Reduce the frequency of icon updates during blinking by grouping blink states
                var blinkKey = blinkStates.Any(bs => bs.Value) ? "blinking" : "normal";
                
                var iconKey = $"{string.Join(",", visibleBars.Select(b => $"{b.Type}:{Math.Round(GetBarValue(b.Type) / 5) * 5:F0}"))}|{blinkKey}";

                // Only update icon if something actually changed
                if (iconKey != lastIconKey)
                {
                    using var newIcon = CreateTrayIcon(currentCpuUsage, currentRamUsage, currentNetworkUsage);
                    var oldIcon = trayIcon.Icon;
                    trayIcon.Icon = (Icon)newIcon.Clone();
                    oldIcon?.Dispose();
                    
                    lastIconKey = iconKey;
                    lastIcon?.Dispose();
                    lastIcon = (Icon)newIcon.Clone();
                    
                    Debug.WriteLine($"Icon updated: {iconKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating tray icon: {ex.Message}");
            }
        }

        // Add helper method to get bar values:
        private float GetBarValue(BarType type)
        {
            return type switch
            {
                BarType.CPU => currentCpuUsage,
                BarType.RAM => currentRamUsage,
                BarType.Network => currentNetworkUsage,
                BarType.CPUTemp => currentCpuTemperature,
                BarType.CPUMaxTemp => currentCpuMaxTemperature,
                BarType.GPUTemp => currentGpuTemperature,
                _ => 0
            };
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
                
                if (trayMenu.Items.Count == 7) // (6 + 1 added above)
                {
                    trayMenu.Items.Insert(5, new ToolStripMenuItem($"GPU Temp: {currentGpuTemperature:F1}°C"));
                }
                else if (trayMenu.Items.Count > 5)
                {
                    trayMenu.Items[5].Text = $"GPU Temp: {currentGpuTemperature:F1}°C";
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
            int iconWidth = System.Windows.Forms.SystemInformation.SmallIconSize.Width;
            int iconHeight = System.Windows.Forms.SystemInformation.SmallIconSize.Height;
            
            using var bitmap = new Bitmap(iconWidth, iconHeight);
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
                    case BarType.GPUTemp:
                        values[bar.Type] = currentGpuTemperature;
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
            int barSize = iconWidth / totalBars;
            int extraPixels = iconWidth % totalBars;
            
            // Center the bars by distributing the remaining pixels
            int currentPos = extraPixels / 2;

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

                int barLength = Math.Max(1, (int)(iconHeight * scale));
                int peakLine = Math.Max(1, (int)(iconHeight * peakScale));
                int thresholdLine = Math.Max(1, (int)(iconHeight * thresholdScale));

                if (settings.IsHorizontalLayout)
                {
                    if (settings.ShowGuideLines)
                    {
                        g.DrawLine(grayPen, 
                            0, currentPos + barSize/2,
                            iconWidth, currentPos + barSize/2);
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
                            currentPos + barSize/2, iconHeight);
                    }

                    g.FillRectangle(new SolidBrush(bar.Color),
                        currentPos, iconHeight - barLength,
                        barSize, barLength);

                    if (settings.ShowPeakLines)
                    {
                        g.DrawLine(peakPen,
                            currentPos, iconHeight - peakLine,
                            currentPos + barSize, iconHeight - peakLine);
                    }

                    if (settings.ShowThresholdLines)
                    {
                        g.DrawLine(thresholdPen,
                            currentPos, iconHeight - thresholdLine,
                            currentPos + barSize, iconHeight - thresholdLine);
                    }
                }

                if (blinkStates.TryGetValue(bar.Type, out bool isBlinking) && isBlinking)
                {
                    using var whitePen = new Pen(Color.White, 1);
                    if (settings.IsHorizontalLayout)
                    {
                        g.DrawRectangle(whitePen, 0, currentPos, iconWidth - 1, barSize - 1);
                    }
                    else
                    {
                        g.DrawRectangle(whitePen, currentPos, 0, barSize - 1, iconHeight - 1);
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
                lastIcon?.Dispose();
                speechSynthesizer?.Dispose();
                computer?.Close();
                timer?.Stop();
                timer?.Dispose();
                trayIcon?.Dispose();
                cpuCounter?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Ensures Core Temp is running. If not found in running processes,
        /// attempts to locate and launch it silently (minimized to tray).
        /// Core Temp is required for temperature readings on systems where
        /// WinRing0 driver access is blocked (e.g. corporate laptops with firmware restrictions).
        /// </summary>
        private void EnsureCoreTempRunning()
        {
            try
            {
                // Already running? (process name has a space: "Core Temp")
                var coreTempProcs = Process.GetProcesses()
                    .Where(p => p.ProcessName.Replace(" ", "").Equals("CoreTemp", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (coreTempProcs.Length > 0)
                {
                    Debug.WriteLine("Core Temp is already running.");
                    return;
                }

                // Common installation paths (in priority order)
                var candidatePaths = new[]
                {
                    @"C:\Program Files\Core Temp\Core Temp.exe",
                    @"C:\Program Files (x86)\Core Temp\Core Temp.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Core Temp", "Core Temp.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Core Temp", "Core Temp.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CPUID", "CoreTemp", "CoreTemp64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CPUID", "CoreTemp", "CoreTemp.exe"),
                };

                // Also check registry for install path
                string? regPath = null;
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\CPUID\CoreTemp");
                    regPath = key?.GetValue("InstallPath") as string;
                    if (regPath != null)
                    {
                        regPath = Path.Combine(regPath, "CoreTemp64.exe");
                        if (!File.Exists(regPath))
                            regPath = Path.Combine(Path.GetDirectoryName(regPath)!, "CoreTemp.exe");
                    }
                }
                catch { }

                string? exePath = regPath != null && File.Exists(regPath)
                    ? regPath
                    : candidatePaths.FirstOrDefault(File.Exists);

                if (exePath == null)
                {
                    Debug.WriteLine("Core Temp not found. Temperature readings may be unavailable.");
                    return;
                }

                // Launch minimized (window style = minimized)
                var psi = new ProcessStartInfo(exePath)
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Debug.WriteLine($"Core Temp launched: {exePath}");

                // Give it a moment to initialize its shared memory
                System.Threading.Thread.Sleep(1500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnsureCoreTempRunning error: {ex.Message}");
            }
        }


        private float GetCpuTemperature()
        {
            try
            {
                // Try Core Temp shared memory FIRST (works even when WinRing0 driver is blocked)
                float coreTempResult = GetTemperatureFromCoreTemp();
                if (coreTempResult > 0)
                    return coreTempResult;

                // Fall back to LibreHardwareMonitor
                if (computer?.Hardware == null) return GetTemperatureViaWmi();

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
                                if (sensor.Name.Equals("Core Average", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Equals("Core (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase))
                                {
                                    coreAvg = sensor.Value;
                                }
                                else if (sensor.Name.Equals("Core Max", StringComparison.OrdinalIgnoreCase) ||
                                         sensor.Name.Equals("Core Max Temperature", StringComparison.OrdinalIgnoreCase))
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
                float manualResult = GetManualCoreAverage();
                if (manualResult <= 0)
                {
                    Debug.WriteLine("LibreHardware returned no data, trying WMI fallback");
                    return GetTemperatureViaWmi();
                }
                return manualResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetCpuTemperature: {ex.Message}");
                return GetTemperatureViaWmi();
            }
        }

        private float GetGpuTemperature()
        {
            try
            {
                if (computer?.Hardware == null) return 0;

                foreach (var hardware in computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.GpuNvidia || 
                        hardware.HardwareType == HardwareType.GpuAmd ||
                        hardware.HardwareType == HardwareType.GpuIntel)
                    {
                        hardware.Update();
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (sensor.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (sensor.Value.HasValue)
                                    {
                                        return sensor.Value.Value;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetGpuTemperature: {ex.Message}");
            }
            return 0;
        }

        /// <summary>
        /// Reads CPU temperatures from Core Temp's shared memory.
        /// Core Temp exposes real-time per-core temperatures via a named MemoryMappedFile.
        /// This works even when hardware driver access (WinRing0) is blocked by Windows 11.
        /// Core Temp must be running for this to work.
        /// </summary>
        private float GetTemperatureFromCoreTemp()
        {
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting("CoreTempMappingObject");
                using var accessor = mmf.CreateViewAccessor();

                int structSize = Marshal.SizeOf<CORE_TEMP_SHARED_DATA>();
                byte[] buffer = new byte[structSize];
                accessor.ReadArray(0, buffer, 0, Math.Min(structSize, (int)accessor.Capacity));

                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var data = Marshal.PtrToStructure<CORE_TEMP_SHARED_DATA>(handle.AddrOfPinnedObject());

                    if (data.uiCoreCnt == 0 || data.fTemp == null) return 0;

                    bool isFahrenheit = data.ucFahrenheit != 0;
                    float sum = 0;
                    float max = 0;
                    int count = 0;

                    for (int i = 0; i < data.uiCoreCnt && i < 256; i++)
                    {
                        float temp = data.fTemp[i];
                        if (isFahrenheit)
                            temp = (temp - 32f) * 5f / 9f;

                        if (temp > 0 && temp < 120)
                        {
                            sum += temp;
                            max = Math.Max(max, temp);
                            count++;
                            Debug.WriteLine($"CoreTemp Core #{i}: {temp:F1}°C");
                        }
                    }

                    if (count > 0)
                    {
                        currentCpuMaxTemperature = max;
                        float avg = sum / count;
                        Debug.WriteLine($"CoreTemp average: {avg:F1}°C (from {count} cores)");
                        return avg;
                    }
                }
                finally
                {
                    handle.Free();
                }
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("Core Temp not running — shared memory not available");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Core Temp shared memory error: {ex.Message}");
            }

            return 0;
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

        /// <summary>
        /// Fallback: reads thermal zone temperatures via WMI.
        /// Used when LibreHardwareMonitor cannot access hardware sensors
        /// (e.g. after Windows 11 updates that block WinRing0 driver).
        /// </summary>
        private float GetTemperatureViaWmi()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    @"root\WMI",
                    "SELECT * FROM MSAcpi_ThermalZoneTemperature");

                float sumTemp = 0;
                float maxTemp = 0;
                int count = 0;

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    // WMI returns temperature in tenths of Kelvin
                    double tenthsKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                    float celsius = (float)(tenthsKelvin / 10.0 - 273.15);

                    // Sanity check: valid CPU temperature range
                    if (celsius > 0 && celsius < 120)
                    {
                        sumTemp += celsius;
                        maxTemp = Math.Max(maxTemp, celsius);
                        count++;
                        Debug.WriteLine($"WMI ThermalZone: {celsius:F1}°C");
                    }
                }

                if (count > 0)
                {
                    currentCpuMaxTemperature = maxTemp;
                    float avgWmi = sumTemp / count;
                    Debug.WriteLine($"WMI average temperature: {avgWmi:F1}°C (from {count} zones)");
                    return avgWmi;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI temperature fallback failed: {ex.Message}");
            }

            return 0;
        }

        // Replace the UpdateSettings method:
        public void UpdateSettings(MonitorSettings newSettings)
        {
            // Check if alerts were disabled while snooze was active
            if (isSnoozeActive && !newSettings.AlertSettings.IsEnabled)
            {
                // Cancel snooze when alerts are disabled
                isSnoozeActive = false;
                snoozeUntil = DateTime.MinValue;
                
                Debug.WriteLine("Snooze cancelled because alerts were disabled");
                
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, 
                        "System Monitor", 
                        "Snooze cancelled - alerts disabled", 
                        ToolTipIcon.Info);
                }
            }
            
            // Stop speech if alerts or sound were disabled
            if (!newSettings.AlertSettings.IsEnabled || !newSettings.AlertSettings.SoundEnabled)
            {
                StopActiveSpeech();
                Debug.WriteLine("Speech stopped due to settings change");
            }
            
            settings = newSettings;
            UpdateTrayIcon();
        }

        // Add this method to stop any active speech:
        private void StopActiveSpeech()
        {
            try
            {
                if (speechSynthesizer != null)
                {
                    speechSynthesizer.SpeakAsyncCancelAll();
                    Debug.WriteLine("Active speech cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping speech: {ex.Message}");
            }
        }

        // Also update the ActivateSnooze method to better handle disabled alerts:
        private void ActivateSnooze()
        {
            Debug.WriteLine($"ActivateSnooze called. Current snooze state: {isSnoozeActive}");
            Debug.WriteLine($"Alerts enabled: {settings.AlertSettings.IsEnabled}");

            // Stop any active speech immediately
            StopActiveSpeech();

            // If already snoozed, cancel snooze (regardless of alert settings)
            if (isSnoozeActive)
            {
                isSnoozeActive = false;
                snoozeUntil = DateTime.MinValue;
                
                Debug.WriteLine("Snooze cancelled");
                
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, 
                        "System Monitor", 
                        "Snooze cancelled", 
                        ToolTipIcon.Info);
                }
                return;
            }

            // If alerts are disabled, don't allow NEW snooze activation
            if (!settings.AlertSettings.IsEnabled)
            {
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, 
                        "System Monitor", 
                        "Alerts are disabled", 
                        ToolTipIcon.Info);
                }
                Debug.WriteLine("Alerts are disabled - snooze not available");
                return;
            }

            // Check if there are any recent alerts (within last 15 seconds) OR currently blinking
            bool hasActiveBlinking = blinkStates.Any(bs => bs.Value);
            bool hasRecentAlerts = HasRecentAlerts();
            
            if (!hasActiveBlinking && !hasRecentAlerts)
            {
                // No active or recent alerts - show info message
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, 
                        "System Monitor", 
                        "No recent alerts to snooze", 
                        ToolTipIcon.Info);
                }
                Debug.WriteLine("No active or recent alerts to snooze");
                return;
            }

            // Activate snooze
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
            else
            {
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, 
                        "System Monitor", 
                        "Snooze time not configured", 
                        ToolTipIcon.Warning);
                }
            }
        }

        // Replace the HandleAlert method:
        private void HandleAlert(BarType type)
        {
            // Record this alert as recent
            recentAlerts[type] = DateTime.Now;
            
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
                
                // Create voice messages with current values
                var voiceMessages = new Dictionary<BarType, string>
                {
                    { BarType.CPU, $"CPU usage high at {currentCpuUsage:F0} percent" },
                    { BarType.RAM, $"Memory usage high at {currentRamUsage:F0} percent" },  
                    { BarType.Network, currentNetworkUsage >= 1024 ? 
                        $"Network usage high at {currentNetworkUsage/1024:F1} megabytes per second" : 
                        $"Network usage high at {currentNetworkUsage:F0} kilobytes per second" },
                    { BarType.CPUTemp, $"CPU temperature high at {currentCpuTemperature:F0} degrees celsius" },
                    { BarType.CPUMaxTemp, $"Max temperature critical at {currentCpuMaxTemperature:F0} degrees celsius" },
                    { BarType.GPUTemp, $"GPU temperature high at {currentGpuTemperature:F0} degrees celsius" }
                };

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
                bool previousState = blinkStates[type];
                blinkStates[type] = !blinkStates[type];
                
                // Only update icon if blink state actually changed
                if (previousState != blinkStates[type])
                {
                    UpdateTrayIcon();
                }

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

        // Helper method to check if there are recent alerts (within last 15 seconds):
        private bool HasRecentAlerts()
        {
            var cutoffTime = DateTime.Now.AddSeconds(-15);
            
            // Clean up old alerts first
            var keysToRemove = recentAlerts.Where(kvp => kvp.Value < cutoffTime).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                recentAlerts.Remove(key);
            }
            
            // Check if any alerts remain (within last 15 seconds)
            bool hasRecent = recentAlerts.Any(kvp => kvp.Value >= cutoffTime);
            
            Debug.WriteLine($"Recent alerts check: {hasRecent}, Active alerts: {string.Join(", ", recentAlerts.Where(kvp => kvp.Value >= cutoffTime).Select(kvp => $"{kvp.Key}@{kvp.Value:HH:mm:ss}"))}");
            
            return hasRecent;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Catch any unhandled exception and write to crash.log
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.WriteAllText(logPath, $"[{DateTime.Now}] CRASH:\n{e.ExceptionObject}");
            };

            Application.ThreadException += (s, e) =>
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] THREAD EXCEPTION:\n{e.Exception}");
                MessageBox.Show($"Error: {e.Exception.Message}\n\nΔες crash.log για λεπτομέρειες.", "System Monitor Error");
            };

            try
            {
                Debug.WriteLine("Application starting...");
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var app = new SystemTrayApp();
                Debug.WriteLine("Running application...");
                Application.Run(app);
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] MAIN EXCEPTION:\n{ex}");
                MessageBox.Show($"Fatal Error: {ex.Message}\n\nΔες crash.log:\n{logPath}", "System Monitor Fatal Error");
            }
        }
    }
}
