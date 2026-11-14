using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MidScroll
{
    // 前面(アクティブ)のウィンドウのプロセスを定期的に確認し、次の2つを判定する。
    //   1. 許可アプリ(AppSettings.AllowedProcessNames)が前面にあるか(IsAllowed / CurrentName)
    //   2. 前面限定が有効なとき、カスタム無効化アプリ(CustomBlockedProcessNames)が
    //      前面にあるか(ForegroundBlockedName)
    // どちらかの状態が変わったときにAllowedStateChangedを発行する(引数はIsAllowed)。
    // 一時停止にするかどうかの判断は、TrayApplicationContext側で行う。
    //
    // タスクバー・通知領域・メニュー・MidScroll自身のウィンドウが前面になっても、
    // 前面アプリが変わったとは扱わない(トレイメニューを開いた瞬間に判定が変わるのを防ぐ)。
    // その間は、直前の「本物の前面アプリ」で判定する。
    internal sealed class ForegroundAllowGuard : IDisposable
    {
        private static class ForegroundNative
        {
            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        }

        // 前面として扱わないウィンドウのクラス名(タスクバー、通知領域のオーバーフロー、メニュー)
        private static readonly HashSet<string> IgnoredClassNames = new(StringComparer.Ordinal)
        {
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland",
            "#32768",
        };

        private readonly AppSettings _settings;
        private readonly System.Windows.Forms.Timer _timer;
        private string _lastForeground = "";   // 直前の「本物の前面アプリ」のプロセス名
        private string _lastAllowedName = "";
        private string _lastBlockedName = "";

        public bool IsAllowed { get; private set; }

        // 許可中の前面アプリ名(拡張子なし)。許可されていないときは空文字。
        public string CurrentName { get; private set; } = "";

        // 前面にあるカスタム無効化アプリ名(拡張子なし)。該当しないとき、
        // または前面限定が無効なときは空文字。
        public string ForegroundBlockedName { get; private set; } = "";

        public event EventHandler<bool>? AllowedStateChanged;

        public ForegroundAllowGuard(AppSettings settings, int intervalMs = 500)
        {
            _settings = settings;
            _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
            _timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            Poll();
            _timer.Start();
        }

        // 設定画面での変更直後など、次の定期確認を待たずに即座に再評価したいときに呼ぶ。
        public void ForceRecheck() => Poll();

        private void Poll()
        {
            if (TryGetForeground(out string foreground) &&
                !string.Equals(foreground, _lastForeground, StringComparison.OrdinalIgnoreCase))
            {
                _lastForeground = foreground;
                DebugLog.Write($"[allow] 前面アプリ={foreground}");
            }

            string allowedName = EvaluateAllowedName();
            string blockedName = EvaluateForegroundBlockedName();

            if (string.Equals(allowedName, _lastAllowedName, StringComparison.Ordinal) &&
                string.Equals(blockedName, _lastBlockedName, StringComparison.Ordinal))
            {
                return;
            }

            _lastAllowedName = allowedName;
            _lastBlockedName = blockedName;
            CurrentName = allowedName;
            IsAllowed = allowedName.Length > 0;
            ForegroundBlockedName = blockedName;

            DebugLog.Write($"[allow] 許可状態={IsAllowed} 許可アプリ={allowedName} 前面の無効化アプリ={blockedName}");
            AllowedStateChanged?.Invoke(this, IsAllowed);
        }

        // 直前の前面アプリが許可リストにあればその名前を、なければ空文字を返す。
        private string EvaluateAllowedName()
        {
            if (!_settings.AllowedAppsEnabled) return "";
            if (_lastForeground.Length == 0) return "";

            var allowed = AntiCheatGuard.BuildNameSet(_settings.AllowedProcessNames);
            return allowed.Contains(_lastForeground) ? _lastForeground : "";
        }

        // 前面限定が有効で、直前の前面アプリがカスタム無効化リストにあればその名前を返す。
        private string EvaluateForegroundBlockedName()
        {
            if (!_settings.CustomBlockEnabled || !_settings.CustomBlockForegroundOnly) return "";
            if (_lastForeground.Length == 0) return "";

            var blocked = AntiCheatGuard.BuildNameSet(_settings.CustomBlockedProcessNames);
            return blocked.Contains(_lastForeground) ? _lastForeground : "";
        }

        // 前面として扱えるウィンドウのプロセス名を取得する。
        // 前面が無い、MidScroll自身、無視対象のクラス、取得失敗のときはfalseを返す。
        private static bool TryGetForeground(out string name)
        {
            name = "";
            try
            {
                IntPtr hwnd = ForegroundNative.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                ForegroundNative.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0 || pid == (uint)Environment.ProcessId) return false;

                var className = new StringBuilder(256);
                ForegroundNative.GetClassName(hwnd, className, className.Capacity);
                if (IgnoredClassNames.Contains(className.ToString())) return false;

                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
                return true;
            }
            catch
            {
                // 終了直後のプロセス等で取得できない場合は、前面として扱わない
                return false;
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
