using System;
using System.IO;
using System.Text.Json;

namespace MidScroll
{
    public class AppSettings
    {
        public bool Enabled { get; set; } = true;
        public int LongPressThresholdMs { get; set; } = 300;
        public double WheelSpeedMultiplier { get; set; } = 1.0;
        public bool AutoStart { get; set; } = false;

        private static string SettingsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MidScroll");

        private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // 読み込み失敗時は既定値にフォールバック
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // 保存失敗は致命的ではないため握りつぶす(将来的にログ化を検討)
            }
        }
    }
}
