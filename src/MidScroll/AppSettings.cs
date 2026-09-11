using System;
using System.IO;
using System.Text.Json;

namespace MidScroll
{
    internal class AppSettings
    {
        // --- 設定画面で編集可能な項目 ---
        public bool Enabled { get; set; } = true;
        public int LongPressThresholdMs { get; set; } = 300;
        public double WheelSpeedMultiplier { get; set; } = 1.0;
        public bool AutoStart { get; set; } = false;

        // --- ScrollEngineが内部で参照する調整値(設定画面では非公開) ---
        // TODO: git show の結果に合わせて既定値を元のフェーズ2チューニング値に修正すること
        public int PollingIntervalMs { get; set; } = 30;
        public int DeadZonePixels { get; set; } = 10;
        public int MaxSpeedRangePixels { get; set; } = 200;
        public double MinWheelStepPerTick { get; set; } = 6.0;
        public double MaxWheelStepPerTick { get; set; } = 60.0;

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
                // 保存失敗は致命的ではないため握りつぶす
            }
        }
    }
}
