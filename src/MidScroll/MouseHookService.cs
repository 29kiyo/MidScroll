using System;
using System.Diagnostics;
using static MidScroll.NativeMethods;

namespace MidScroll
{
    internal sealed class MouseHookEventArgs : EventArgs
    {
        public POINT Point { get; }
        public bool Suppress { get; set; }

        public MouseHookEventArgs(POINT point)
        {
            Point = point;
        }
    }

    internal sealed class MouseHookService : IDisposable
    {
        private IntPtr _hookHandle = IntPtr.Zero;
        private readonly LowLevelMouseProc _proc;
        private bool _disposed;

        public event EventHandler<MouseHookEventArgs>? MiddleButtonDown;
        public event EventHandler<MouseHookEventArgs>? MiddleButtonUp;

        public MouseHookService()
        {
            // デリゲートをフィールドに保持し、GCで回収されないようにする
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero) return;

            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr moduleHandle = curModule != null
                ? GetModuleHandle(curModule.ModuleName)
                : IntPtr.Zero;

            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, moduleHandle, 0);

            if (_hookHandle == IntPtr.Zero)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"マウスフックの設定に失敗しました。Win32エラー: {error}");
            }
        }

        public void Stop()
        {
            if (_hookHandle == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();

                if (message == WM_MBUTTONDOWN || message == WM_MBUTTONUP)
                {
                    var hookStruct = System.Runtime.InteropServices.Marshal
                        .PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    var args = new MouseHookEventArgs(hookStruct.pt);

                    if (message == WM_MBUTTONDOWN)
                        MiddleButtonDown?.Invoke(this, args);
                    else
                        MiddleButtonUp?.Invoke(this, args);

                    if (args.Suppress)
                    {
                        // 非ゼロを返すことでOSへのメッセージ伝播を止める
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
    }
}
