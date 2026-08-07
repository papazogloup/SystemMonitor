using System.Drawing;

namespace SystemMonitor
{
    public enum BarType
    {
        CPU,
        RAM,
        Network,
        CPUTemp,
        CPUMaxTemp,
        GPUTemp
    }

    public static class BarTypeExtensions 
    {
        public static string ToDisplayString(this BarType type)
        {
            return type switch
            {
                BarType.CPUTemp => "CPU Temperature",
                BarType.CPUMaxTemp => "CPU Max Temperature",
                BarType.GPUTemp => "GPU Temperature",
                _ => type.ToString()
            };
        }
    }

    public class MonitorSettings
    {
        public bool IsHorizontalLayout { get; set; }
        public bool ShowGuideLines { get; set; } = true;      // For gray lines
        public bool ShowThresholdLines { get; set; } = true;  // For red lines
        public bool ShowPeakLines { get; set; } = true;       // For peak value lines
        public List<BarSettings> Bars { get; set; }
        public MonitorAlertSettings AlertSettings { get; set; }

        public MonitorSettings()
        {
            ResetToDefaults(); // Κάλεσε τη ResetToDefaults για να αποφύγουμε duplicate code
        }

        // Add method to reset settings
        public void ResetToDefaults()
        {
            // Reset basic settings
            IsHorizontalLayout = false;
            ShowGuideLines = true;
            ShowThresholdLines = true;
            ShowPeakLines = true;

            // Reset alert settings
            AlertSettings = new MonitorAlertSettings
            {
                IsEnabled = true,
                SnoozeMinutes = 5,
                SoundEnabled = true,
                SnoozeUntil = new Dictionary<BarType, DateTime>()
            };

            // Reset bars with default colors and order
            Bars = new List<BarSettings>
            {
                new() { 
                    Type = BarType.CPU, 
                    Color = Color.FromArgb(255, 0, 120, 255),  // Έντονο μπλε
                    IsVisible = true, 
                    Threshold = 90 
                },
                new() { 
                    Type = BarType.RAM, 
                    Color = Color.FromArgb(255, 0, 255, 0),    // Έντονο πράσινο
                    IsVisible = true, 
                    Threshold = 90 
                },
                new() { 
                    Type = BarType.Network, 
                    Color = Color.FromArgb(255, 220, 20, 60),  // Μπορντό
                    IsVisible = true, 
                    Threshold = 1000 
                },
                new() { 
                    Type = BarType.CPUTemp, 
                    Color = Color.FromArgb(255, 255, 255, 0),  // Κίτρινο
                    IsVisible = true, 
                    Threshold = 80 
                },
                new() { 
                    Type = BarType.CPUMaxTemp, 
                    Color = Color.FromArgb(255, 255, 140, 0),  // Πορτοκαλί
                    IsVisible = true, 
                    Threshold = 90 
                },
                new() {
                    Type = BarType.GPUTemp,
                    Color = Color.FromArgb(255, 148, 0, 211),  // Μωβ (DarkViolet)
                    IsVisible = true,
                    Threshold = 85
                }
            };
        }

        public MonitorSettings Clone()
        {
            var clone = new MonitorSettings
            {
                IsHorizontalLayout = this.IsHorizontalLayout,
                ShowGuideLines = this.ShowGuideLines,
                ShowThresholdLines = this.ShowThresholdLines,
                ShowPeakLines = this.ShowPeakLines
            };

            // Clone AlertSettings
            clone.AlertSettings = this.AlertSettings.Clone();
            
            // Clone Bars
            clone.Bars = this.Bars.Select(b => b.Clone()).ToList();
            
            return clone;
        }
    }

    public class BarSettings
    {
        public BarType Type { get; set; }
        public Color Color { get; set; }
        public bool IsVisible { get; set; }
        public float Threshold { get; set; }

        public BarSettings Clone()
        {
            return new BarSettings
            {
                Type = this.Type,
                Color = this.Color,
                IsVisible = this.IsVisible,
                Threshold = this.Threshold
            };
        }
    }

    public class MonitorAlertSettings
    {
        public bool IsEnabled { get; set; } = true;
        public int SnoozeMinutes { get; set; } = 5;
        public bool SoundEnabled { get; set; } = true;
        public Dictionary<BarType, DateTime> SnoozeUntil { get; set; } = new();

        public MonitorAlertSettings Clone()
        {
            return new MonitorAlertSettings
            {
                IsEnabled = this.IsEnabled,
                SnoozeMinutes = this.SnoozeMinutes,
                SoundEnabled = this.SoundEnabled,
                SnoozeUntil = new Dictionary<BarType, DateTime>(this.SnoozeUntil)
            };
        }
    }
}