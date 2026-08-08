# System Monitor 📊

Advanced system monitoring tool providing real-time insights into your PC's health and performance, with intelligent alerts, voice notifications, and a highly customizable, professional user interface.

## 🌟 Key Features

*   **Comprehensive Hardware Tracking**: Monitor CPU usage, RAM utilization, Network activity, and both CPU/GPU temperatures in one place.
*   **Customizable Layouts**: Choose between vertical or horizontal layouts to fit your workspace perfectly.
*   **Smart Alerts & Voice Notifications**: Stay informed with intelligent alerts. Supports snooze options and voice alerts for immediate, eyes-free updates when thresholds are exceeded.
*   **Highly Configurable UI**: Customize colors, order, visibility, and alert thresholds for every single monitoring bar.
*   **Lightweight & Efficient**: Built on .NET 9.0 and Windows Forms, leveraging LibreHardwareMonitorLib for accurate and low-overhead metrics.

## 🛠️ Monitored Metrics

*   **CPU Usage** (%)
*   **RAM Usage** (%)
*   **Network Activity**
*   **CPU Temperature** (°C)
*   **CPU Max Temperature** (°C)
*   **GPU Temperature** (°C)

## ⚙️ Requirements

*   Windows OS
*   [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
*   *(Optional but Recommended)* **[Core Temp](https://www.alcpu.com/CoreTemp/)**: Used as the primary, most accurate method for CPU temperature readings (the app will gracefully fall back to LibreHardwareMonitor or WMI if it's not installed).

## 🚀 Installation (Pre-built)

1.  Download the latest release from the [Releases page](../../releases).
2.  Extract the files and move the application folder to `C:\Program Files\SystemMonitor` (recommended).
3.  Run `SystemMonitor.exe`.
4.  *Note: The application requires administrative privileges to accurately read all hardware sensors.*

## 🔨 Building from Source

If you prefer to build the application yourself:
1. Clone the repository.
2. Open `SystemMonitor.sln` in Visual Studio 2022 or use the .NET CLI:
   ```bash
   dotnet build -c Release
   ```
3. Copy the output folder `bin/Release/net9.0-windows/` into your `C:\Program Files\` directory.
4. (Optional) You can run the provided `install.ps1` script (as Administrator) to automate placement and shortcut creation.

## 🎨 Configuration

Click the settings icon (or right-click the interface) to open the configuration menu. From there, you can:
*   Toggle visibility for individual metrics.
*   Reorder the monitoring bars.
*   Set custom warning thresholds and colors.
*   Configure the intelligent alert system (Snooze times, Voice alerts, etc.).

## 📝 License

Copyright © 2026. This project is licensed under the [GNU General Public License v3.0](LICENSE).
