using System.Diagnostics;
using Microsoft.Win32;

namespace MidScroll
{
    public static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MidScroll";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }

        public static void Enable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                key?.SetValue(ValueName, $"\"{exePath}\"");
            }
        }

        public static void Disable()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(ValueName, false);
        }
    }
}
