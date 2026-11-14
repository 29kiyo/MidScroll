using System;
using System.IO;
using System.Threading;

namespace MidScroll
{
    // 調査用のログ。AppSettings.DebugLogEnabledがtrueのときだけ、
    // %AppData%/MidScroll/debug.log に追記する(1MBを超えたら作り直す)。
    // キーボードフックのコールバック内からも呼ばれるため、ファイル書き込みは
    // スレッドプールに任せて、呼び出し側はすぐに戻る。
    internal static class DebugLog
    {
        private const long MaxBytes = 1_000_000;
        private static readonly object Sync = new();

        public static bool Enabled { get; set; }

        private static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MidScroll", "debug.log");

        public static void Write(string message)
        {
            if (!Enabled) return;

            string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
            ThreadPool.QueueUserWorkItem(_ => Append(line));
        }

        private static void Append(string line)
        {
            try
            {
                lock (Sync)
                {
                    string path = LogPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        File.Delete(path);
                    }

                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // ログの失敗でアプリの動作に影響させない
            }
        }
    }
}
