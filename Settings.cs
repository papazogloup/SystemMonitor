using System.Drawing;

public class MonitorSettings
{
    public bool IsHorizontalLayout { get; set; } = false;
    public List<BarSettings> Bars { get; set; }
    public AlertSettings AlertSettings { get; set; }

    public MonitorSettings()
    {
        Bars = new List<BarSettings>
        {
            new BarSettings { Type = BarType.CPU, Color = Color.FromArgb(0, 120, 255), IsVisible = true, Order = 0, Threshold = 90 },
            new BarSettings { Type = BarType.RAM, Color = Color.FromArgb(0, 255, 0), IsVisible = true, Order = 1, Threshold = 90 },
            new BarSettings { Type = BarType.Network, Color = Color.FromArgb(220, 20, 60), IsVisible = true, Order = 2, Threshold = 1000 },
            new BarSettings { Type = BarType.CPUTemp, Color = Color.FromArgb(255, 255, 0), IsVisible = true, Order = 3, Threshold = 80 },
            new BarSettings { Type = BarType.CPUMaxTemp, Color = Color.FromArgb(255, 140, 0), IsVisible = true, Order = 4, Threshold = 90 },
            new BarSettings { Type = BarType.GPUTemp, Color = Color.FromArgb(148, 0, 211), IsVisible = true, Order = 5, Threshold = 85 }
        };
        
        AlertSettings = new AlertSettings 
        { 
            IsEnabled = true,
            SnoozeMinutes = 5,
            SoundEnabled = true
        };
    }
}

public class BarSettings
{
    public BarType Type { get; set; }
    public Color Color { get; set; }
    public bool IsVisible { get; set; }
    public int Order { get; set; }
    public float Threshold { get; set; }
}

public enum BarType
{
    CPU,
    RAM,
    Network,
    CPUTemp,
    CPUMaxTemp,
    GPUTemp
}

public class AlertSettings
{
    public bool IsEnabled { get; set; }
    public int SnoozeMinutes { get; set; }
    public bool SoundEnabled { get; set; }
    public Dictionary<BarType, DateTime> SnoozeUntil { get; set; } = new();
}