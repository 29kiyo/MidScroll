using System;
using System.Windows.Forms;
using static MidScroll.NativeMethods;

namespace MidScroll
{
    internal enum ScrollCursorState
    {
        Neutral, // 中央の点+上下矢印(デッドゾーン内)
        Up,      // 上矢印のみ(上方向にスクロール中)
        Down     // 下矢印のみ(下方向にスクロール中)
    }

    // スクロールモード中、Edge等のオートスクロールと同様に
    // システム全体のカーソルを一時的に差し替える。
    // Cursor.Currentへの代入だけでは他アプリのウィンドウ上で
    // 上書きされてしまうため、SetSystemCursorでOS側のカーソルスキーム
    // 自体を一時的に置き換える方式をとる。
    internal sealed class ScrollCursorManager
    {
        private bool _overridden;
        private ScrollCursorState _currentState = ScrollCursorState.Neutral;

        public void Begin()
        {
            _overridden = true;
            _currentState = ScrollCursorState.Neutral;
            ApplyCursor(Cursors.NoMoveVert);
        }

        public void Update(ScrollCursorState state)
        {
            if (!_overridden || state == _currentState) return;
            _currentState = state;

            Cursor cursor = state switch
            {
                ScrollCursorState.Up => Cursors.PanNorth,
                ScrollCursorState.Down => Cursors.PanSouth,
                _ => Cursors.NoMoveVert
            };
            ApplyCursor(cursor);
        }

        public void End()
        {
            if (!_overridden) return;
            _overridden = false;
            // ユーザーの元のカーソルスキームに戻す
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }

        private static void ApplyCursor(Cursor cursor)
        {
            // SetSystemCursorは渡したハンドルの所有権を奪って破棄するため、
            // Cursorオブジェクトが持つハンドルをそのまま渡さず複製する。
            IntPtr copy = CopyIcon(cursor.Handle);
            if (copy != IntPtr.Zero)
            {
                SetSystemCursor(copy, OCR_NORMAL);
            }
        }
    }
}
