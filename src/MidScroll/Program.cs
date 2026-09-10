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

            Application.ApplicationExit += (s, e) =>
            {
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
