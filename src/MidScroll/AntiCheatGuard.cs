using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace MidScroll
{
    // 一時停止の原因となっているプロセス名(アンチチート検知分/カスタム無効化分)。
    internal sealed class BlockCause
    {
        public IReadOnlyList<string> AntiCheat { get; }
        public IReadOnlyList<string> Custom { get; }

        public BlockCause(IReadOnlyList<string> antiCheat, IReadOnlyList<string> custom)
        {
            AntiCheat = antiCheat;
            Custom = custom;
        }

        // 原因が変わったかどうかの判定用(同じ原因なら同じキーになる)
        public string Key => string.Join("|", AntiCheat) + "#" + string.Join("|", Custom);
    }

    // 2つの独立した検知機能を持つ:
    //   1. アンチチート検知(AppSettings.AntiCheatDetectionEnabled / AntiCheatProcessNames)
    //      主要アンチチートサービス・代表的な対象ゲーム本体の実行を検知する。
    //      監視対象リストは設定画面で編集可能(既定値はAppSettings側で定義)。
    //   2. カスタムアプリ無効化(AppSettings.CustomBlockEnabled / CustomBlockedProcessNames)
    //      ユーザーが任意に指定した実行ファイルの実行を検知する。
    // 各リストの項目(ProcessRule)はそれぞれ個別にEnabled/Disabledを持ち、
    // 機能全体のトグルとは別に項目単位でも無視できる。
    // どちらかの機能・項目で検知されればMidScrollを一時停止する。
    // BlockStateChangedは、検知状態または検知されたプロセスの内容が変わったときに
    // 発行され、引数がnullなら一時停止なし、それ以外は原因(BlockCause)を表す。
    internal sealed class AntiCheatGuard : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly System.Windows.Forms.Timer _pollTimer;
        private string _lastKey = "";

        public event EventHandler<BlockCause?>? BlockStateChanged;

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
            BlockCause? cause = DetectCause();
            string key = cause?.Key ?? "";
            if (key == _lastKey) return;

            _lastKey = key;
            BlockStateChanged?.Invoke(this, cause);
        }

        private BlockCause? DetectCause()
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
            if (!anyListActive) return null;

            var antiCheatFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var customFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (antiCheatNames != null && antiCheatNames.Contains(name))
                        {
                            antiCheatFound.Add(name);
                        }
                        else if (customNames != null && customNames.Contains(name))
                        {
                            customFound.Add(name);
                        }
                    }
                    catch
                    {
                        // 一部システムプロセスはアクセス権限等で例外が出ることがあるため無視
                    }
                }
            }

            if (antiCheatFound.Count == 0 && customFound.Count == 0) return null;

            // 表示・変化判定が安定するよう、名前順に並べる
            return new BlockCause(
                antiCheatFound.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                customFound.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
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
