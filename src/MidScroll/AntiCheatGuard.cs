using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace MidScroll
{
    // 主要なアンチチートサービス、および代表的な対象ゲーム本体の実行を
    // 定期的に検知し、検知中はMidScrollの機能を自動的に一時停止する。
    // 監視対象プロセス名は今後の変化に応じて随時更新すること。
    internal sealed class AntiCheatGuard : IDisposable
    {
        // プロセス名(拡張子なし、大文字小文字区別なし)
        private static readonly HashSet<string> WatchedProcessNames = new(StringComparer.OrdinalIgnoreCase)
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
            "r5apex",                    // Apex Legends
            "tslgame",                   // PUBG: BATTLEGROUNDS
            "rainbowsix",                // Rainbow Six Siege
            "valorant-win64-shipping",
        };

        private readonly System.Windows.Forms.Timer _pollTimer;
        private bool _isBlocked;

        public event EventHandler<bool>? BlockStateChanged;

        public AntiCheatGuard(int pollIntervalMs = 3000)
        {
            _pollTimer = new System.Windows.Forms.Timer { Interval = pollIntervalMs };
            _pollTimer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            Poll(); // 起動直後にも一度確認しておく
            _pollTimer.Start();
        }

        public void Stop() => _pollTimer.Stop();

        private void Poll()
        {
            bool detected = IsAnyWatchedProcessRunning();
            if (detected == _isBlocked) return;

            _isBlocked = detected;
            BlockStateChanged?.Invoke(this, _isBlocked);
        }

        private static bool IsAnyWatchedProcessRunning()
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (WatchedProcessNames.Contains(process.ProcessName))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // 一部システムプロセスはアクセス権限等で例外が出ることがあるため無視
                    }
                }
            }
            return false;
        }

        public void Dispose()
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
        }
    }
}
