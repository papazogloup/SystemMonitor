using System.Drawing;

namespace SystemMonitor
{
    public enum BarType
    {
        CPU,
        RAM,
        Network,
        CPUTemp,
        CPUMaxTemp
    }

    public class MonitorSettings
    {
        public bool IsHorizontalLayout { get; set; }
        public List<BarSettings> Bars { get; set; }
        public MonitorAlertSettings AlertSettings { get; set; }

        public MonitorSettings()
        {
            IsHorizontalLayout = false;
            Bars = new List<BarSettings>
            {
                new() { 
                    Type = BarType.CPU, 
                    Color = Color.FromArgb(255, 0, 120, 255),  // Έντονο μπλε #0078FF
                    IsVisible = true, 
                    Threshold = 90 
                },
                new() { 
                    Type = BarType.RAM, 
                    Color = Color.FromArgb(255, 0, 255, 0),    // Έντονο πράσινο #00FF00
                    IsVisible = true, 
                    Threshold = 90 
                },
                new() { 
                    Type = BarType.Network, 
                    Color = Color.FromArgb(255, 220, 20, 60),  // Μπορντό #DC143C
                    IsVisible = true, 
                    Threshold = 1000 
                },
                new() { 
                    Type = BarType.CPUTemp, 
                    Color = Color.FromArgb(255, 255, 255, 0),  // Κίτρινο #FFFF00
                    IsVisible = true, 
                    Threshold = 80 
                },
                new() { 
                    Type = BarType.CPUMaxTemp, 
                    Color = Color.FromArgb(255, 255, 140, 0),  // Πορτοκαλί #FF8C00
                    IsVisible = true, 
                    Threshold = 90 
                }
            };
            AlertSettings = new MonitorAlertSettings();
        }

        // Add method to reset settings
        public void ResetToDefaults()
        {
            var defaults = new MonitorSettings();
            IsHorizontalLayout = defaults.IsHorizontalLayout;
            Bars = defaults.Bars.Select(b => b.Clone()).ToList();
            AlertSettings = defaults.AlertSettings.Clone();
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