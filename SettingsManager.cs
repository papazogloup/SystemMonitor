using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemMonitor
{
    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemMonitor",
            "settings.json"
        );

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new ColorJsonConverter() }
        };

        public static void SaveSettings(MonitorSettings settings)
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            string jsonString = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(SettingsPath, jsonString);
        }

        public static MonitorSettings LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                string jsonString = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<MonitorSettings>(jsonString, Options);
                if (settings != null)
                {
                    // Add any missing bars from default settings (e.g. newly added GPUTemp)
                    var defaults = new MonitorSettings();
                    foreach (var defaultBar in defaults.Bars)
                    {
                        if (!settings.Bars.Any(b => b.Type == defaultBar.Type))
                        {
                            settings.Bars.Add(defaultBar.Clone());
                        }
                    }
                    return settings;
                }
            }
            return new MonitorSettings();
        }
    }

    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of object");
            }

            int a = 255, r = 0, g = 0, b = 0;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected property name");
                }

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLower())
                {
                    case "a":
                        a = reader.GetInt32();
                        break;
                    case "r":
                        r = reader.GetInt32();
                        break;
                    case "g":
                        g = reader.GetInt32();
                        break;
                    case "b":
                        b = reader.GetInt32();
                        break;
                }
            }

            return Color.FromArgb(a, r, g, b);
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("A", value.A);
            writer.WriteNumber("R", value.R);
            writer.WriteNumber("G", value.G);
            writer.WriteNumber("B", value.B);
            writer.WriteEndObject();
        }
    }
}