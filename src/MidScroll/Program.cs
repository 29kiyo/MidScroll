using System;
using System.Windows.Forms;

namespace MidScroll
{
    internal static class Program
    {
        private static MouseHookService? _hookService;
        private static ScrollEngine? _scrollEngine;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            Console.WriteLine("MidScroll フェーズ2 動作確認モード");
            Console.WriteLine("中クリックを長押ししてスクロールモードをテストしてください。");
            Console.WriteLine("終了するにはこのウィンドウを閉じてください。");

            var settings = AppSettings.Default;
            _hookService = new MouseHookService();
            _scrollEngine = new ScrollEngine(_hookService, settings);
            _scrollEngine.StatusChanged += (s, msg) => Console.WriteLine($"[ScrollEngine] {msg}");

            _hookService.Start();

            var antiCheatGuard = new AntiCheatGuard();
            antiCheatGuard.BlockStateChanged += (s, blocked) =>
            {
                _scrollEngine?.SetExternalBlock(blocked);
                Console.WriteLine(blocked
                    ? "[AntiCheatGuard] 対象ゲーム/アンチチートを検知。MidScrollを一時停止します。"
                    : "[AntiCheatGuard] 対象ゲームの終了を検知。MidScrollを再開します。");
            };
            antiCheatGuard.Start();

            Application.ApplicationExit += (s, e) =>
            {
                antiCheatGuard.Dispose();
                _scrollEngine?.Dispose();
                _hookService?.Dispose();
            };

            // 異常終了時でもシステムカーソルの差し替えが残らないようにする保険
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            };

            Application.Run();
        }
    }
}
