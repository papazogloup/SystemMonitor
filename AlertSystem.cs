using System.Media; // Add this for SystemSounds

namespace SystemMonitor
{
    public class AlertSettings
    {
        public bool IsEnabled { get; set; } = true;
        public int SnoozeMinutes { get; set; } = 5;
        public bool SoundEnabled { get; set; } = true;
        public Dictionary<BarType, DateTime> SnoozeUntil { get; set; } = new();
    }

    public class AlertSystem
    {
        private readonly MonitorSettings settings;
        private readonly SoundPlayer? player;
        
        public AlertSystem(MonitorSettings settings)
        {
            this.settings = settings;
            if (settings.AlertSettings.SoundEnabled)
            {
                // Changed from SystemSounds.Asterisk.Location to just playing the sound directly
                player = new SoundPlayer();
            }
        }
        
        public void CheckThresholds(Dictionary<BarType, float> currentValues)
        {
            if (!settings.AlertSettings.IsEnabled) return;
            
            foreach (var bar in settings.Bars.Where(b => b.IsVisible))
            {
                if (!currentValues.TryGetValue(bar.Type, out float value)) continue;
                
                if (value > bar.Threshold)
                {
                    if (!settings.AlertSettings.SnoozeUntil.TryGetValue(bar.Type, out DateTime snoozeTime) ||
                        DateTime.Now > snoozeTime)
                    {
                        ShowAlert(bar.Type, value);
                    }
                }
            }
        }
        
        private void ShowAlert(BarType type, float value)
        {
            if (settings.AlertSettings.SoundEnabled)
            {
                player?.Play();
            }
            
            var result = MessageBox.Show(
                $"{type} value ({value:F1}) exceeds threshold!\n\nSnooze alert?",
                "Threshold Alert",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning
            );
            
            switch (result)
            {
                case DialogResult.Yes:
                    settings.AlertSettings.SnoozeUntil[type] = 
                        DateTime.Now.AddMinutes(settings.AlertSettings.SnoozeMinutes);
                    break;
                case DialogResult.No:
                    settings.AlertSettings.SnoozeUntil[type] = DateTime.MaxValue;
                    break;
            }
        }
    }
}