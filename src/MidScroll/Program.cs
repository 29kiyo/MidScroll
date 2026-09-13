using System;
using System.Threading;
using System.Windows.Forms;

namespace MidScroll
{
    internal static class Program
    {
        private const string MutexName = "MidScroll_SingleInstance_Mutex";

        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("MidScrollは既に起動しています。", "MidScroll",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();

            var settings = AppSettings.Load();

            using var hookService = new MouseHookService();
            using var scrollEngine = new ScrollEngine(hookService, settings);
            using var antiCheatGuard = new AntiCheatGuard(settings);

            using var trayContext = new TrayApplicationContext(settings, scrollEngine, antiCheatGuard);

            hookService.Start();
            antiCheatGuard.Start();

            Application.Run(trayContext);

            GC.KeepAlive(mutex);
        }
    }
}
