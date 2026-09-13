using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MidScroll
{
    internal class AppSettings
    {
        // アンチチート検知が最初に有効になった際の既定監視対象プロセス名一覧。
        // ユーザーはAntiCheatProcessNamesを自由に編集できるが、その初期値としてここを使う。
        private static readonly List<string> DefaultAntiCheatProcessNames = new()
        {
            // Easy Anti-Cheat (Fortnite, Apex Legendsほか多数のタイトルで採用)
            "easyanticheat",
            "easyanticheat_eos",
            "eastart",

            // BattlEye (PUBG, Rainbow Six Siegeほか)
            "beservice",
            "bedaemon",
            "battleye",

            // Riot Vanguard (Valorant)。PC起動時から常駐するため、
            // インストールされている環境ではゲーム未起動時も検知され続ける点に注意。
            "vgc",
            "vgtray",
            "vgk",

            // nProtect GameGuard
            "gamemon",
            "npggsvc",

            // 代表的な対象ゲーム本体(アンチチートサービスの検知漏れに対する補助)
            "fortniteclient-win64-shipping",
            "r5apex",
            "tslgame",
            "rainbowsix",
            "valorant-win64-shipping",
        };

        private static List<ProcessRule> CreateDefaultAntiCheatRules() =>
            DefaultAntiCheatProcessNames
                .Select(name => new ProcessRule { Name = name, Enabled = true })
                .ToList();

        // --- 設定画面で編集可能な項目 ---
        public bool Enabled { get; set; } = true;
        public int LongPressThresholdMs { get; set; } = 300;
        public double WheelSpeedMultiplier { get; set; } = 1.0;
        public bool AutoStart { get; set; } = false;

        // --- アンチチート検知(独立したON/OFFと、項目ごとに個別ON/OFF可能な監視対象リスト) ---
        public bool AntiCheatDetectionEnabled { get; set; } = true;
        public List<ProcessRule> AntiCheatProcessNames { get; set; } = CreateDefaultAntiCheatRules();

        // --- ユーザー指定のカスタム無効化アプリ(独立したON/OFFと項目ごとの個別ON/OFF) ---
        public bool CustomBlockEnabled { get; set; } = true;
        public List<ProcessRule> CustomBlockedProcessNames { get; set; } = new();

        // --- ScrollEngineが内部で参照する調整値(設定画面では非公開) ---
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
                        settings.AntiCheatProcessNames ??= CreateDefaultAntiCheatRules();
                        settings.CustomBlockedProcessNames ??= new List<ProcessRule>();
                        return settings;
                    }
                }
            }
            catch
            {
                // 読み込み失敗時(旧バージョンのsettings.jsonとの型不一致含む)は既定値にフォールバック
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
