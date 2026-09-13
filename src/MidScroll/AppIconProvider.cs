using System;
using System.Drawing;
using System.Windows.Forms;

namespace MidScroll
{
    // exeに埋め込まれたアイコン(ApplicationIconで指定したicon.ico)を
    // TrayApplicationContext(トレイアイコン)とSettingsForm(タイトルバー/
    // タスクバーアイコン)の両方から共通で利用するための取得処理。
    // 取得に失敗した場合はシステム標準アイコンにフォールバックする。
    internal static class AppIconProvider
    {
        private static Icon? _cached;

        public static Icon GetIcon()
        {
            if (_cached != null) return _cached;

            try
            {
                using var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    _cached = (Icon)extracted.Clone();
                    return _cached;
                }
            }
            catch
            {
                // フォールバックへ
            }

            _cached = SystemIcons.Application;
            return _cached;
        }
    }
}
