using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static MidScroll.NativeMethods;

namespace MidScroll
{
    // PageUp/PageDownキーを、マウスホイール回転(1ノッチ=120)に置き換える。
    //
    // - キーボードフック(WH_KEYBOARD_LL)は、機能が有効(AppSettings.PageKeyScrollEnabled)で、
    //   かつ一時停止中(手動無効・アンチチート検知等)でない間だけ設置し、それ以外は解除する。
    //   Refresh()が設定とブロック状態から設置/解除を判断する。
    // - フックのコールバックは素早く返す必要があるため、中ではSendInputを呼ばず、
    //   押下をキューに積むだけにする。ホイール送信は専用のバックグラウンドスレッドが行う。
    //   キューが一杯のときは新しい押下を捨てる(キーリピートの積み上がり防止)。
    // - Ctrl/Shift/Alt/Winキーを押しているときと、他のツールが送った注入イベントは素通りさせる。
    // - ホイールはマウスカーソルの下のウィンドウに送られる(キーボードフォーカスのウィンドウではない)。
    // - 1回あたりのノッチ数(PageKeyScrollNotches)は送信のたびに読み直す。
    internal sealed class PageKeyScroller : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private const uint VK_PRIOR = 0x21; // Page Up
        private const uint VK_NEXT = 0x22;  // Page Down

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;   // Alt
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private const uint LLKHF_INJECTED = 0x10;

        private const int WheelDelta = 120;
        private const int MinNotches = 1;
        private const int MaxNotches = 50;
        private const int QueueCapacity = 16;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private static class KeyboardNative
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll")]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern short GetAsyncKeyState(int vKey);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr GetModuleHandle(string? lpModuleName);
        }

        private readonly AppSettings _settings;
        private readonly LowLevelKeyboardProc _proc; // GC対策でフィールドに保持
        private readonly HashSet<uint> _swallowedKeys = new(); // keydownを握りつぶしたキー(対応するkeyupも握りつぶす)
        private readonly BlockingCollection<int> _queue = new(QueueCapacity); // 送信待ちの方向(1=上、-1=下)

        private Thread? _worker;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _blocked;

        public PageKeyScroller(AppSettings settings)
        {
            _settings = settings;
            _proc = HookCallback;
        }

        // 一時停止状態(手動無効・アンチチート検知等)が変わったときに呼ぶ。
        public void SetBlocked(bool blocked)
        {
            if (_blocked == blocked) return;
            _blocked = blocked;
            Refresh();
        }

        // 設定とブロック状態から、フックを設置すべきかどうかを判断して反映する。
        public void Refresh()
        {
            bool shouldHook = _settings.PageKeyScrollEnabled && !_blocked;

            if (shouldHook && _hookId == IntPtr.Zero)
            {
                Install();
            }
            else if (!shouldHook && _hookId != IntPtr.Zero)
            {
                Uninstall();
            }
        }

        private void Install()
        {
            try
            {
                EnsureWorker();
                _hookId = KeyboardNative.SetWindowsHookEx(
                    WH_KEYBOARD_LL, _proc, KeyboardNative.GetModuleHandle(null), 0);
                DebugLog.Write($"[pagekey] フック設置 成功={_hookId != IntPtr.Zero} lastError={Marshal.GetLastWin32Error()}");
            }
            catch
            {
                // 設置に失敗しても、この機能が使えないだけでアプリ全体には影響させない
                _hookId = IntPtr.Zero;
            }
        }

        private void Uninstall()
        {
            try
            {
                if (_hookId != IntPtr.Zero)
                {
                    KeyboardNative.UnhookWindowsHookEx(_hookId);
                    DebugLog.Write("[pagekey] フック解除");
                }
            }
            catch
            {
                // 解除失敗は無視
            }
            finally
            {
                _hookId = IntPtr.Zero;
                _swallowedKeys.Clear();
            }
        }

        private void EnsureWorker()
        {
            if (_worker != null) return;

            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "PageKeyScroller"
            };
            _worker.Start();
        }

        private void WorkerLoop()
        {
            foreach (int direction in _queue.GetConsumingEnumerable())
            {
                try
                {
                    SendWheelNotches(direction);
                }
                catch
                {
                    // 送信失敗は無視して次へ
                }
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool isPageKey = data.vkCode == VK_PRIOR || data.vkCode == VK_NEXT;

                if (isPageKey && (data.flags & LLKHF_INJECTED) == 0)
                {
                    int message = wParam.ToInt32();

                    if (DebugLog.Enabled)
                    {
                        DebugLog.Write($"[pagekey] キー vk=0x{data.vkCode:X2} msg=0x{message:X} 修飾キー押下={IsModifierPressed()}");
                    }

                    if (message == WM_KEYDOWN)
                    {
                        if (!IsModifierPressed())
                        {
                            // コールバック内ではSendInputを呼ばず、キューに積んで即座に返す。
                            // 一杯のときは捨てる(TryAddは待たずにfalseを返す)。
                            try
                            {
                                _queue.TryAdd(data.vkCode == VK_PRIOR ? 1 : -1);
                            }
                            catch (InvalidOperationException)
                            {
                                // 終了処理中(CompleteAdding後)は無視
                            }

                            _swallowedKeys.Add(data.vkCode);
                            return (IntPtr)1; // 元のキー入力は他アプリに渡さない
                        }
                    }
                    else if (message == WM_KEYUP)
                    {
                        if (_swallowedKeys.Remove(data.vkCode))
                        {
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return KeyboardNative.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool IsModifierPressed() =>
            IsDown(VK_SHIFT) || IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);

        private static bool IsDown(int vk) => (KeyboardNative.GetAsyncKeyState(vk) & 0x8000) != 0;

        // direction: 1 = 上スクロール(PageUp)、-1 = 下スクロール(PageDown)。
        // 1ノッチずつ送る(アプリによっては大きな値を1回で送ると挙動が変わるため)。
        // バックグラウンドスレッドから呼ばれる。
        private void SendWheelNotches(int direction)
        {
            int notches = Math.Clamp(_settings.PageKeyScrollNotches, MinNotches, MaxNotches);
            int delta = direction * WheelDelta;
            int failed = 0;

            for (int i = 0; i < notches; i++)
            {
                var input = new INPUT
                {
                    type = INPUT_MOUSE,
                    mi = new MOUSEINPUT
                    {
                        mouseData = unchecked((uint)delta),
                        dwFlags = MOUSEEVENTF_WHEEL
                    }
                };
                var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                if (Convert.ToInt32(sent) == 0)
                {
                    failed++;
                }
            }

            DebugLog.Write($"[pagekey] ホイール送信 方向={direction} ノッチ={notches} 失敗={failed}");
        }

        public void Dispose()
        {
            Uninstall();
            _queue.CompleteAdding();
        }
    }
}
