using System;
using System.Runtime.InteropServices;

namespace Glyph.Win32.Windowing
{
    public static class ForegroundWindow
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static string GetActiveWindowProcessName()
        {
            IntPtr hWnd = GetForegroundWindow();
            GetWindowThreadProcessId(hWnd, out uint processId);
            return System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
        }
    }
}