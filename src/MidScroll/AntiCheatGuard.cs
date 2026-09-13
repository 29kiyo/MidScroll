using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace MidScroll
{
    // 2つの独立した検知機能を持つ:
    //   1. アンチチート検知(AppSettings.AntiCheatDetectionEnabled / AntiCheatProcessNames)
    //      主要アンチチートサービス・代表的な対象ゲーム本体の実行を検知する。
    //      監視対象リストは設定画面で編集可能(既定値はAppSettings側で定義)。
    //   2. カスタムアプリ無効化(AppSettings.CustomBlockEnabled / CustomBlockedProcessNames)
    //      ユーザーが任意に指定した実行ファイルの実行を検知する。
    // 各リストの項目(ProcessRule)はそれぞれ個別にEnabled/Disabledを持ち、
    // 機能全体のトグルとは別に項目単位でも無視できる。
    // どちらかの機能・項目で検知されればMidScrollを一時停止する。
    internal sealed class AntiCheatGuard : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly System.Windows.Forms.Timer _pollTimer;
        private bool _isBlocked;

        public event EventHandler<bool>? BlockStateChanged;

        public AntiCheatGuard(AppSettings settings, int pollIntervalMs = 3000)
        {
            _settings = settings;
            _pollTimer = new System.Windows.Forms.Timer { Interval = pollIntervalMs };
            _pollTimer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            Poll(); // 起動直後にも一度確認しておく
            _pollTimer.Start();
        }

        public void Stop() => _pollTimer.Stop();

        // 設定画面での変更直後など、次の定期ポーリング(既定3秒間隔)を待たずに
        // 即座に検知状態を再評価したい場合に呼ぶ。
        public void ForceRecheck() => Poll();

        private void Poll()
        {
            bool detected = IsAnyWatchedProcessRunning();
            if (detected == _isBlocked) return;

            _isBlocked = detected;
            BlockStateChanged?.Invoke(this, _isBlocked);
        }

        private bool IsAnyWatchedProcessRunning()
        {
            // 各リスト・トグルは毎回_settingsから読み直す
            // (設定画面での変更をアプリ再起動なしに反映するため)。
            HashSet<string>? antiCheatNames = _settings.AntiCheatDetectionEnabled
                ? BuildNameSet(_settings.AntiCheatProcessNames)
                : null;
            HashSet<string>? customNames = _settings.CustomBlockEnabled
                ? BuildNameSet(_settings.CustomBlockedProcessNames)
                : null;

            bool anyListActive = (antiCheatNames != null && antiCheatNames.Count > 0)
                || (customNames != null && customNames.Count > 0);
            if (!anyListActive) return false;

            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        bool matchesAntiCheat = antiCheatNames != null && antiCheatNames.Contains(process.ProcessName);
                        bool matchesCustom = customNames != null && customNames.Contains(process.ProcessName);

                        if (matchesAntiCheat || matchesCustom)
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

        // 項目ごとにEnabled=falseのものは除外し、有効な項目だけを
        // Process.ProcessName相当の形式(拡張子なし)に正規化して集合化する。
        private static HashSet<string> BuildNameSet(IEnumerable<ProcessRule> rules)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in rules)
            {
                if (!rule.Enabled) continue;
                if (string.IsNullOrWhiteSpace(rule.Name)) continue;

                string name = rule.Name.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^4];
                }

                if (name.Length > 0)
                {
                    set.Add(name);
                }
            }
            return set;
        }

        public void Dispose()
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
        }
    }
}
