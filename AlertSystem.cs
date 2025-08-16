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
        private Dictionary<BarType, DateTime> lastAlerts = new();

        public AlertSystem(MonitorSettings settings)
        {
            this.settings = settings;
        }

        public void TriggerAlert(BarType type, string message)
        {
            // Check if alerts are enabled
            if (!settings.AlertSettings.IsEnabled) return;

            // Check if we're still in snooze period
            if (settings.AlertSettings.SnoozeUntil.TryGetValue(type, out DateTime snoozeTime))
            {
                if (DateTime.Now < snoozeTime) return;
            }

            // Check if enough time has passed since last alert (prevent spam)
            if (lastAlerts.TryGetValue(type, out DateTime lastAlert))
            {
                var timeSinceLastAlert = DateTime.Now - lastAlert;
                if (timeSinceLastAlert.TotalSeconds < 5) return; // Minimum 5 seconds between alerts
            }

            // Update last alert time
            lastAlerts[type] = DateTime.Now;

            // Show notification
            using var notification = new NotifyIcon
            {
                Icon = SystemIcons.Warning,
                Visible = true
            };
            notification.ShowBalloonTip(5000, "System Monitor Alert", message, ToolTipIcon.Warning);

            // Play sound if enabled
            if (settings.AlertSettings.SoundEnabled)
            {
                Console.Beep(800, 200); // Frequency: 800Hz, Duration: 200ms
            }
        }

        public void Snooze(BarType type)
        {
            var snoozeTime = DateTime.Now.AddMinutes(settings.AlertSettings.SnoozeMinutes);
            settings.AlertSettings.SnoozeUntil[type] = snoozeTime;
        }
    }
}