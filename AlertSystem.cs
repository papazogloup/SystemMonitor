using System.Media;

namespace SystemMonitor
{
    public class AlertSettings
    {
        public bool IsEnabled { get; set; } = true;
        public int SnoozeMinutes { get; set; } = 5;
        public bool SoundEnabled { get; set; } = true;
        public Dictionary<BarType, DateTime> SnoozeUntil { get; set; } = new();

        public AlertSettings Clone()
        {
            return new AlertSettings
            {
                IsEnabled = this.IsEnabled,
                SnoozeMinutes = this.SnoozeMinutes,
                SoundEnabled = this.SoundEnabled,
                SnoozeUntil = new Dictionary<BarType, DateTime>(this.SnoozeUntil)
            };
        }
    }

    public class AlertSystem
    {
        private readonly MonitorSettings settings;
        private Dictionary<BarType, DateTime> lastAlerts = new();
        
        // Add event for blinking
        public event Action<BarType>? OnAlert;

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
                if (DateTime.Now < snoozeTime)
                {
                    // Still in snooze period, just trigger blink without sound
                    OnAlert?.Invoke(type);
                    return;
                }
                else
                {
                    // Snooze period expired, remove it
                    settings.AlertSettings.SnoozeUntil.Remove(type);
                }
            }

            // Check for alert throttling (5 seconds between alerts)
            if (lastAlerts.TryGetValue(type, out DateTime lastAlert))
            {
                if ((DateTime.Now - lastAlert).TotalSeconds < 5) return;
            }

            // Update last alert time
            lastAlerts[type] = DateTime.Now;

            // Show notification
            using var notification = new NotifyIcon
            {
                Icon = SystemIcons.Warning,
                Visible = true
            };

            var snoozeInfo = settings.AlertSettings.SnoozeUntil.TryGetValue(type, out DateTime until)
                ? $" (Snoozed until {until:HH:mm})"
                : "";

            notification.ShowBalloonTip(5000, "System Monitor Alert",
                message + snoozeInfo, ToolTipIcon.Warning);

            // Play sound if enabled
            if (settings.AlertSettings.SoundEnabled)
            {
                SystemSounds.Exclamation.Play();
            }

            // Trigger blinking
            OnAlert?.Invoke(type);
        }

        public void Snooze(BarType type)
        {
            var snoozeTime = DateTime.Now.AddMinutes(settings.AlertSettings.SnoozeMinutes);
            settings.AlertSettings.SnoozeUntil[type] = snoozeTime;

            // Clear any existing alerts for this type
            lastAlerts.Remove(type);
        }
    }
}